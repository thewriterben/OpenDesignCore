# Sensor-node enclosure (`sensor-node-enclosure/0.1`)

A two-part screwed enclosure for the OpenCircuitCore `boards/sensor-node`. It is generated from the board's OpenPartsCore features (`boards/opencircuitcore-sensor-node`), not from numbers typed into the model.

```
dotnet run --project src/OpenDesignCore -c Release -- run-sensor-node-enclosure \
  --voxel-mm 0.1 --tolerance-mm 0.2 --accept-unverified-access J1,J2,U1-ANT
```

## Run of 2026-11-06

| Part | Ledger run | Size (mm) | STL sha256 | Sidecar sha256 |
|---|---|---|---|---|
| Base, floor down | 99 | 49.00 x 56.20 x 10.00 | `f21d5a03…fafe73d` | `5bd73ed2…f0b24373` |
| Lid, top down | 100 | 49.00 x 56.20 x 6.40 | `2e7f0d45…f048c60fc1` | `70ed923b…2c0894bb` |

Runs 90/91 and 96/97 produced the same STLs; 99/100 supersede them. Each sidecar records its sibling's STL hash and the full assembly-check block.

Runs 99/100 were made from commit `e992a5c` with no uncommitted changes in the working tree, and their sidecars record that commit. Runs 96/97 recorded a HEAD from before the generator was committed. The STL hashes are identical across all three pairs, so the slices below were made from runs 96/97 and still match runs 99/100.

The run used 0.1 mm voxels, which is the resolution floor. Anything coarser is refused.

## Slices (OBCSlicer `obcslice slice`, OrcaSlicer 2.4.2 portable)

Profiles:
- Machine: `Creality K2 Plus 0.4 nozzle`
- Process: `0.20mm Standard @Creality K2 Plus 0.4 nozzle`
- Filament: `Creality Generic PLA @K2-all`

Both slices have supports off. Both were linked to their design sidecars, and the STL hash was verified before slicing.

| Part | Design run | Layers | Estimate | Filament | Slice record sha256 | G-code sha256 |
|---|---|---|---|---|---|---|
| Base | 96 | 50 | 16 min 10 s | 3.16 m | `f206ba77…a88c20` | `849eb3cf…5ce082e9e` |
| Lid | 97 | 32 | 15 min 44 s | 2.51 m | `1bec4bc0…a494a639b2` | `f85ef9a2…68bd7fb7` |

The output is in `OBCSlicer/.tools/sensor-node-enclosure-v0.1/{base,lid}/`, which is gitignored, as `plate_1.gcode` plus `<stl-sha>.slice.json`.

Degradations recorded in the slice reports:
- The profiles are unflattened (they use `inherits`).
- No slicer commit is recorded.

Nothing has been printed. A print goes through the studio-3dp `propose_action` queue only.

## Hardware

- 4 x M3x16 socket head screws (ISO 4762).
- 4 x M3 hex nuts (ISO 4032).

Assembly:
1. Drop the nuts into the hex pockets from the underside.
2. Seat the board on the bosses.
3. Put the lid on and screw down from the lid top.

The screws clamp the board between the base bosses and the lid posts. The walls stop 0.2 mm short of each other, so they never carry the clamp load.

## What fits were checked, and how

| Check | How | Result |
|---|---|---|
| The board goes in with both halves on, and no plastic sits in any described part (PCB, U1, U2, R1–R3, C1–C4, J1, J2, U1-ANT) | ADR-0023 assembly check on base + lid in the assembled pose, tolerance 0.2 mm, before export | Passed. No contact violations |
| Header access (J1 SENSOR, J2 PROG) | Not provable by the assembly check: the registry has no socket-housing depth, and the pins stand above the lid top. The run accepts the unknown by id. A separate test checks that a housing 0.3 mm wider than each header on every side, from the lid top down to 0.1 mm above the board, meets no lid plastic | Passed (`ASocketHousingReachesBothHeaders`) |
| Antenna zone (y > -13.25) is thin plastic only | A 2 x 2 mm column through the lid and the base at (22, -5) measures 1.2 mm. The same column at (22, -44) measures 2.0 mm (lid) and 3.6 mm (floor). The y = 0 end wall is 1.2 mm | Passed (`TheAntennaZoneIsThinPlasticAndTheRestIsFullWall`) |
| No metal and no boss or post in the antenna zone | Parameter check: nut corner (r 3.40), screw head (r 2.75) and boss (r 3.25) around each hole. Holes H1/H2 at y = -17 reach y = -13.60. A hole moved into the zone is refused | Passed (`AHoleInTheAntennaZoneIsRefused`) |
| Screw length | M3x16 with the head on the lid top: the tip lands at -8.0. That is past the nut's far face (-7.8) and 0.6 mm above the underside (-8.6) | Checked in `ValidateParams` |
| Nut pocket | Hex, 5.9 mm across flats (5.5 + 0.4), 3.2 deep from the underside; the nut seats against the boss above it. The prism's voxel volume matches the analytic volume within 3% | Passed (`TheHexPrismIsSolidAndTheRightSize`) |
| Underside clearance | Header pin tails reach z = -1.405. The floor top is at -5.0 | Checked in `ValidateParams` |
| Lid clearance | The tallest part under the lid is the module can, at 4.695. The ceiling is at 6.0 | Checked in `ValidateParams` |
| Reproducibility | Two runs into separate stores give byte-identical STLs and sidecars | Passed |

## BME280 ventilation

A 1.2 mm baffle hangs from the lid at y = -34.77, between the module and the sensor row. It stops 0.6 mm above the board.

The sensor chamber is vented in two ways:
- Three 1.2 x 5 mm slots in the lid top over U2.
- Four vertical slots in each side wall, so air crosses the chamber.

The module chamber has five slots of its own, all outside the antenna zone.

## Not checked, and why it matters

- **Antenna detuning by the 1.2 mm of PLA.** Not modelled. Check RSSI with the lid on and off. The access depth for U1-ANT is TODO(source): Espressif's housing guidance for the ESP32-S3-WROOM-1.
- **Heat conducted through the GND pour from the ESP32 to the BME280.** The baffle stops convection only. Compare the BME280 reading against a reference with the enclosure closed.
- **Edge clearance (0.5 mm) and the nut pocket clearance (0.4 mm).** Both are design choices, not measured K2 Plus fits. If the nut will not drop in, or the board binds, those are the numbers to change.
- **Seam.** It has no lip or seal.
- **Registry entry.** The features file `OpenPartsCore/data/boards/opencircuitcore-sensor-node.json` was written for this run. The sidecar pins it by `registry_file_sha256`. Those bytes were first committed in OpenPartsCore 5e199c1 (branch `features-lilygo-camera`); a checkout at 5e199c1 reproduces the pinned hash. Later commits there (bcb045a and its merge parent 887f4de) change the ADR number in its description and move its three free-text citations to `repo:` citations of OpenCircuitCore 9546499, with the prose in `note`; no box, direction or model hash changed, so the runs were not repeated.
