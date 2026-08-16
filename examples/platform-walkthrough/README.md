# Platform walkthrough

One run, four peers: **what can I build → its electronics → its board → an enclosure fitted to that board → staged for fabrication.**

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
| 5 | OpenDesignCore | Artifact staged for fabrication, provenance intact, handoff recorded in the ledger |

## A real run (2026-08-15)

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
run 5: PASS
  scan       sha256:0d73223c...   <- the board STL
  artifact   sha256:e8401edf...   <- the enclosure
  provenance sha256:f4a93c06...

=== 5. OpenDesignCore: stage for fabrication ===
handoff 2: staged-offline
  staged  ...\slicing-inbox\scan-cradle-0.1-e8401edf6cd1.stl
```

## What the chain actually guarantees

- **Every part fact is cited.** The BOM's `opc_id` column resolves to OpenPartsCore entries carrying their sources; nothing in the chain invents a component value.
- **Provenance composes.** The enclosure's sidecar records `scan_sha256` — the hash of the board STL that produced it. Follow that hash back and you have the board; follow the ledger and you have the run that made the enclosure, its voxel size, and its pinned tool versions.
- **Failures are loud.** Every step gates: unbuildable project, ERC violation, DRC violation, resolution floor, or unreachable studio each stop the run rather than degrade it.
- **Content addressing throughout.** Staged filenames carry the artifact hash, so a file on a bench can be traced to the run that made it.

## What it does not yet do

- The schematic does **not drive the board's netlist** — the subcircuit and the board are generated independently. Wiring them is the next electronics milestone.
- The handoff is `--offline`: it stages files. Proposing an actual print needs studio-core running and sliced G-code (see the AdvancedStudio gaps in the wiki's open questions).
- No pricing. The shopping list names parts; ordering them is live distributor work, keyed by the same ids.
