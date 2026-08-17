# Platform walkthrough

One run, four peers: **what can I build → its electronics → its board → an enclosure fitted to that board → can anything make it → staged for fabrication.**

This is the honest test of ADR-0007's claim that the platform is a composition of independent engines rather than one program. The script shells out to each peer's own CLI on purpose — it depends on their interfaces, not their code, so a break here is a break a user would actually hit.

## Run it

```powershell
.\run.ps1                       # defaults to F:\Documents\GitHub
.\run.ps1 -Root <your-git-dir>
```

Prerequisites: the four repos checked out as siblings, KiCad 10.x, .NET SDK, and `dotnet build -c Release` already run in OpenDesignCore.

## What happens

| Step | Peer | What it proves |
|---|---|---|
| 1 | OpenBuildCore | `env-monitor` is buildable from the example inventory; the shopping list names what is still missing for the others |
| 2 | OpenCircuitCore | Schematic generated from a netlist description, **ERC clean**, BOM resolving to cited OpenPartsCore ids |
| 3 | OpenCircuitCore | Board regenerated, **DRC clean**, geometry exported as STL |
| 4 | OpenDesignCore | Enclosure fitted to that **actual board geometry** — outline, components, mounting holes — not to a nominal envelope |
| 5 | OpenBuildCore | Which of the user's machines can make it, judged from the **enclosure's own provenance record** — not from a size typed by hand |
| 6 | OpenDesignCore | Artifact staged for fabrication, provenance intact, handoff recorded in the ledger |

Step 5 is where the loop closes, and it closes at a **file**: OpenBuildCore reads `artifact.bbox_mm` and `volume_cubic_mm` out of the sidecar (ADR-0010) and imports nothing from this repo. Two engines agreeing on a record rather than on an API is the shape ADR-0007 argued for, and it is the only step here where a peer consumes another peer's *output* rather than its CLI.

## A real run (2026-08-16)

```
=== 2. OpenCircuitCore: schematic, ERC, BOM ===
  6 parts, 16 pin connections, nets: +3V3, GND, SCL, SDA
Found 0 violations
"C1","100n","Capacitor_SMD:C_0603_1608Metric","electronic/c-0603"
"R1,R2","4.7k","Resistor_SMD:R_0603_1608Metric","electronic/r-0603"
"U2","BME280","Package_LGA:Bosch_LGA-8_...","electronic/bme280"

=== 3. OpenCircuitCore: board DRC and STL export ===
  outline 34.0 x 46.0 mm, 4 M3 mounting holes
  2 component(s): U1=boards/esp32-s3, U2=electronic/bme280
Found 0 violations / Found 0 unconnected items

=== 4. OpenDesignCore: enclosure fitted to the real board ===
run 10: PASS
  scan       sha256:5f11ca17...   <- the board STL
  artifact   sha256:e8401edf...   <- the enclosure
  provenance sha256:2587140b...

=== 5. OpenBuildCore: can any of my machines make this? ===
Part from scan-cradle/0.1 artifact sha256:e8401edf6cd1 (odc/provenance/0.2)
  39.6 x 51.6 x 7.03 mm, 9773.28 mm3, voxel 0.30 mm

[CANNOT] Creality K2 Plus  (k2-plus)
    NO   does not fit: 39.6 x 51.6 x 7.03 mm vs envelope 1 x 1 x 1 mm, in any
         axis-aligned orientation
    time unknown: no measured throughput - print time requires slicing

[CAN PRINT] Example Bench FDM  (example-bench-fdm)
    ok   fits as modelled (39.6 x 51.6 x 7.03 mm)
    time ~0.7 h - pre-slicing triage only; a slicer supersedes this entirely

=== 6. OpenDesignCore: stage for fabrication ===
handoff 3: staged-offline
  staged  ...\slicing-inbox\scan-cradle-0.1-e8401edf6cd1.stl
```

Two things in step 5 are worth reading carefully.

**39.6 × 51.6 mm is the enclosure, and 34 × 46 mm was the board.** The difference is twice the clearance plus twice the wall. Nobody typed either number: the board came from KiCad, the enclosure from the cradle run, and the figure judged here from the sidecar. Reaching for the board size instead would have been wrong by 5.6 mm on each axis and looked entirely reasonable.

**The K2 Plus refusing is the discipline working, not a broken step.** Its `envelope_mm` is a `1×1×1` placeholder marked `TODO(source)` because the build volume is not in any cited source, so it fails loudly rather than passing on a recalled number. The walkthrough does not treat that as a failure, and it will keep saying so until someone measures the bed.

## What the chain actually guarantees

- **Every part fact is cited.** The BOM's `opc_id` column resolves to OpenPartsCore entries carrying their sources; nothing in the chain invents a component value.
- **Provenance composes.** The enclosure's sidecar records `scan_sha256` — the hash of the board STL that produced it. Follow that hash back and you have the board; follow the ledger and you have the run that made the enclosure, its voxel size, and its pinned tool versions.
- **Failures are loud.** Every step gates: unbuildable project, ERC violation, DRC violation, resolution floor, or unreachable studio each stop the run rather than degrade it.
- **Content addressing throughout.** Staged filenames carry the artifact hash, so a file on a bench can be traced to the run that made it.
- **Provenance is read, not just written.** Step 5 is a consumer of the record the earlier steps produced. Until a peer actually depends on a field, "we record provenance" is a claim nobody has tested.

## What it does not yet do

- The schematic does **not drive this board's netlist** — `reference-esp32s3` and its subcircuit are generated independently. OpenCircuitCore's `sensor-breakout` does derive its nets from the exported netlist; moving this walkthrough onto that board is the next electronics step.
- **Nothing here prints.** Step 5 answers whether a machine *could*; it does not slice, and the machine that could is fictional. A real answer for the real printer needs the K2 Plus build volume measured.
- The handoff is `--offline`: it stages files. Proposing an actual print needs studio-core running and sliced G-code (see the AdvancedStudio gaps in the wiki's open questions).
- No pricing. The shopping list names parts; ordering them is live distributor work, keyed by the same ids.
