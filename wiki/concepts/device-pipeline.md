---
title: Device pipeline (Phase 1 + Phase 2)
type: concept
updated: 2026-11-04
sources:
  - OpenDesignCore/DECISIONS.md (ADR-0009 execute-vs-propose, ADR-0013 FilamentRef)
  - OpenCircuitCore/DECISIONS.md (ADR-0003 KiCad direct, ADR-0005 MCP inspects only, ADR-0006 prove rules fire)
  - OBCSlicer/ROADMAP.md, docs/specs/MM-3MF.md, SLICE-RECORD.md
  - OBC-Prime/firmware/obc-esp32-s3/src/board.rs
  - Research pass 2026-11-04 (KiCad 10.0.5 IPC API, FreeRouting 2.5.0, ESP-IDF 6.0, esp-hal 1.0, OrcaSlicer 2.4.2, PicoGK 2.3.0, OFD dataset 2026.11.04)
---

# Device pipeline

The platform's end state is one spec in, one *proposed* device out: a routed
PCB with gerbers, an enclosure fitted to that PCB, a print project the slicer
can run, and firmware whose pin map was derived from the same netlist. Every
hop emits a canonical-JSON record with SHA-256 links to its inputs, so the
chain can be re-verified without re-running it. No hop invents a number.

```
 spec ──► OpenCircuitCore ──► netlist (.net) ──► placed PCB ──► routed PCB ──► gerbers, STEP
            │  PARTS (code)       │                                 │
            │                     ├──► make_firmware.py ──► pins.rs / pins.h + firmware.manifest.json
            │                     │        (I2C GPIOs, sensor address, UART header; refuses strapping/USB pins)
            │                     └──► OpenPartsCore ids ──► BOM (cited parts only)
            │
            └── STEP ──► OpenDesignCore run-enclosure ──► mesh + provenance sidecar
                                                              │
                                        OBCSlicer: obc-material (OFD) ──► obc3mf ──► obcslice ──► propose
                                                                                                   │
                                                                    AdvancedStudio: human approves print
 firmware binary ──► espflash (human runs the printed command; never executed by a tool)
```

## Phase 1 — things that get printed (OpenDesignCore + OBCSlicer)

| Hop | Tool | Record | State 2026-11-04 |
|---|---|---|---|
| Geometry | ODC `run-enclosure`, PicoGK 2.2.0 pinned (2.3.0 on NuGet, not adopted) | `odc/provenance/0.1` | shipped |
| Material facts | `obcslice material pin` from the **Open Filament Database** | `obc-material/0.1` | **new today** |
| Project | `obc3mf compile` (3MF + per-part overrides; now checks nozzle temperature against the pinned material) | `obc-project/0.2` | shipped, extended today |
| Toolpaths | `obcslice slice` over OrcaSlicer CLI (2.4.2 current) | `obc-slice/0.1` | shipped |
| Graded fields | `fieldengine` (OpenVDB, `obc-field/0.2`) | — | shipped; temperature grading waits on a spool |
| Print | AdvancedStudio propose → human approve | jobs ledger | shipped |

Material rule (ADR-0013 still holds): OFD carries temperatures, density,
diameter tolerance, volumetric speed, fan and drying. It carries **no**
shrinkage and **no** mechanical data; a record cites OFD for the former and
says `not_in_source` for the latter. The pin is verified against OFD's
`manifest.json` sha256, so a record is falsifiable against the dataset version.

## Phase 2 — things that get soldered and flashed (OpenCircuitCore + firmware)

| Hop | Tool | Record | State |
|---|---|---|---|
| Circuit as code | `PARTS` list → `.kicad_sch` via global labels on pin points | — | shipped (sensor-subcircuit) |
| ERC, netlist | `kicad-cli sch erc`, `sch export netlist` | erc-report.txt | shipped |
| Placement + nets from netlist | pcbnew Python, `board.manifest.json` | — | shipped; **sensor-node board new today** (ESP32-S3-WROOM-1 + BME280 + headers) |
| Routing | `route_board.py`: pcbnew `ExportSpecctraDSN` → FreeRouting 2.5.0 CLI → `ImportSpecctraSES` → DRC | `routing.json` | **script new today; FreeRouting binary not installed** (user downloads) |
| Fab check | `apply_fab_profile.py` + `kicad-cli pcb drc` against a cited profile | drc-report.txt | shipped |
| Outputs | gerbers, drill, STEP, provenance | `occ/provenance/0.1` | shipped |
| Firmware pin map | `make_firmware.py` reads the **netlist**, not a hand table | `firmware.manifest.json` | **new today** |
| Deploy | `espflash flash` command emitted in the manifest, `executed: false` | — | human step by design (ADR-0009 line) |

### Why the pin map comes from the netlist
OBC-Prime's `board.rs` keeps a hand-written `i2c: Option<(sda, scl)>` per
board and has already been bitten by a board whose only exposed bus collided
with the camera. For a board *this* platform designs, the netlist is the truth:
the generator finds the sensor's SDI/SCK pins, follows their nets to the
module's `IOnn` pin functions, and refuses if either lands on a strapping pin
(GPIO0/3/45/46) or the USB pair (GPIO19/20). The strapping set is read from
OpenPartsCore's `electronic/esp32-s3-wroom-1` entry with its datasheet
citation, so the refusal is cited, not remembered.

### Routing decision
KiCad 10's IPC API (`kipy`) covers the board editor only and needs a running
KiCad; `kicad-cli` has no DSN export. FreeRouting 2.5.0 (5 Oct 2026) ships a
native Windows x64 CLI with headless flags (`-de`, `-do`, `-mp`, `--gui.enabled=false`)
and reads/writes Specctra, which pcbnew's SWIG API exports and imports. That
is the whole seam: two files at a process boundary, GPL stays outside, and the
router can be swapped. Not writing a router (ROADMAP "Not ever").

## Open questions carried forward
- Enclosure co-design for sensor-node: STEP export is one `kicad-cli pcb export step`; the ODC run needs the connector access features (OpenPartsCore ADR-0007) for the two headers before a usable enclosure exists.
- Distributor sourcing (Octopart free tier ~100 parts; Mouser/LCSC unresearched).
- ESP-IDF 6.0 / esp-hal 1.0 migration for OBC-Prime firmware is a separate decision; `pins.rs` is plain constants and works with either.
