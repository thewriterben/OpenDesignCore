# Calibrate first, then measure

Step 0 of [the calibration loop](README.md).

A measured dimension is the design, times the material's shrinkage, times the
machine's idea of a millimetre, times how much plastic the extruder actually
pushed. A caliper sees the product, not the factors. Whichever factor you
*name* when you store the result is the one that inherits all the others' error.

That is the whole reason this document exists, and it is why the order below is
not optional. **Everything upstream of shrinkage must be right before a
shrinkage number means anything.**

---

## The order

Each step depends on the one before it. Doing them out of order means redoing
them.

### 1. Machine

Standard mechanical calibration, nothing exotic:

- belts at correct and equal tension
- pulley grub screws tight on the motor shafts
- gantry square, both X-ends at the same height
- input shaping / resonance compensation run

The K2 Plus is CoreXY, which matters for one thing: **X and Y cannot be scaled
independently.** Both motors move for every move (motor A travels X+Y, motor B
travels X−Y), so matched `rotation_distance` values scale X and Y *together*,
and mismatched ones produce **skew**, not scale. If you ever need a uniform
scale correction it goes on both steppers with the same value.

*(An earlier version of this file, and of the `MachineScaleError` message, told
you to multiply the Y stepper's `rotation_distance` by 0.99174. That was wrong
for this machine and would have introduced a real geometric fault. Retracted.)*

Config lives behind Fluidd on port **4408**, or SSH in root/expert mode. Back up
`printer.cfg` before touching it — a firmware update can overwrite it.

### 2. Filament — and this is the step that was actually missing

Standard Orca/Creality Print sequence, in this order because each depends on the
last:

| | Why it must come before shrinkage |
|---|---|
| **Temperature** | Affects viscosity, and therefore flow, and therefore everything downstream. |
| **Flow rate** | **The important one.** Over-extrusion bulges every external wall, adding a roughly constant number of millimetres to every outside dimension. |
| **Pressure advance** | Corners and seams. A PA error deposits extra material exactly where a caliper is most likely to land. |

Flow rate deserves emphasis because **an uncalibrated flow ratio is the same bug
this whole ADR is about, one layer down.** Measure shrinkage with flow 3% high
and you get a number that says *"PLA shrinks −0.4%"* when it actually means
*"my flow ratio is 3% high"* — a printer setting, permanently filed under the
name of a plastic, with a provenance hash making it look sourced.

#### Calibrating flow with the instrument you already have

Creality Print and Orca both ship a flow test that prints nine blocks at −20 to
+20 and asks you to pick the best-looking one. It works, and its answer depends
on who is looking. An unrepeatable judgement sitting upstream of a recorded
number is worth avoiding when the alternative costs one small print.

The objective method uses a caliper, which is the instrument this loop already
declares an accuracy for:

1. Slice a 20–30 mm cube with **1 perimeter, 0 % infill, 0 top layers**. A
   single wall means the wall thickness *is* the extrusion width.
2. Print it at the temperature you will actually use. Flow is only calibrated
   for the temperature it was calibrated at — which is why temperature comes
   first, and why leaving a known-good profile's temperature alone is a
   legitimate way to satisfy that step.
3. Measure the wall in the **middle of each of the four faces**, away from
   corners and away from the first and last layers. Average them.
4. Read your profile's **external perimeter line width** — do not assume it
   equals the nozzle diameter.

   For the K2 Plus this was read from the vendor's own profiles rather than
   recalled: every `@Creality K2 Plus 0.4 nozzle` process in Creality Print 7.2
   sets `outer_wall_line_width = 0.42`, against a nozzle diameter of 0.4. Also
   `inner_wall_line_width = 0.45` and `initial_layer_line_width = 0.5` — the
   first layer is deliberately wider, which is the sourced reason for measuring
   away from it rather than a rule of thumb.

   If your measured wall lands near 0.45 rather than 0.42, the slicer treated
   it as an inner wall; check that the model really is one perimeter.

```
new_flow_ratio = current_flow_ratio × (line_width ÷ measured_wall_thickness)
```

Repeat until measured and target agree within about 0.02 mm — one caliper
division, and the same accuracy figure the rest of the loop is declared against.
Two passes is usually enough.

Record the resulting ratio somewhere you will find it again. It belongs with the
material, not the machine: it is a property of this filament on this printer.

The machine gate in `compensate` catches the machine version of that mistake and
is currently blind to this one. Noted at the bottom.

### 3. Print the block clean

**Layer height is not a free choice, and this is the trap most likely to cost
you a print.** `compare` measures against the exported STL's bounding box. It
knows nothing about the slicer quantising the part to whole layers. If the
shelf at 4 mm or the tall face at 25 mm does not land exactly on a layer
boundary, the part is short or tall *by construction*, and that error is
indistinguishable from shrinkage in the reading.

Computed against the Creality Print K2 Plus profiles, all of which use a 0.2 mm
initial layer:

| Layer height | Shelf (4 mm) | Tall face (25 mm) |
|---|---|---|
| 0.08 | +0.04 | exact |
| 0.12 | +0.04 | +0.04 |
| 0.16 | +0.04 | exact |
| **0.20** | **exact** | **exact** |
| 0.24 | +0.04 | −0.08 |
| 0.28 | +0.12 | +0.12 |

**Use 0.20 mm.** It is the only one where both features are exact. At 0.24 the
span error alone is −0.12 mm over a 21 mm span — −0.57 %, the same size as real
PLA shrinkage and pointing the same way.

Then:

- shrinkage / XY compensation **OFF** — otherwise you are measuring the
  compensation, not the material
- **elephant-foot compensation 0** — it reshapes geometry, and nothing should
  be silently reshaping the thing you are about to measure
- **scale 100 %, no auto-orient, no auto-rotate.** The transposition check
  catches swapped *readings*; it cannot know the part was turned on the plate
- otherwise **use the profile you actually print production parts with** —
  walls, infill, speeds. A shrinkage figure derived from 5 walls and 40 % infill
  describes prints made that way, and applying it to 2-wall 10 % parts is a
  different number
- brim off if adhesion allows; it leaves a lip on the face you measure diagonals
  across
- the material and spool you actually want to characterise
- let it cool completely; warm plastic is still moving

**Measuring around the seam without fighting it:** take three readings per
dimension, **away from the seam**, and give `compare` all three
(comma-separated: `40.00,40.02,40.01x…`). A seam, a blob or a caliper held at
an angle can only ever make an external dimension read *larger* — never
smaller — which is why this used to say "keep the smallest": when the tool
took one number, the minimum was the least contaminated pick. That was also
the cheap explanation for the +0.5 mm Y outlier on the first run through this
loop. Now the tool takes the readings themselves, a blob shows up as spread,
and a spread wider than the caliper's accuracy makes the axis refuse rather
than resolve. If one reading is obviously seam-contaminated, the answer is to
re-measure off the seam — not to launder the set through a statistic, and not
to feed the tool a pre-minimised number that hides what the surface did.

### The spread between your three readings is the real uncertainty

`--instrument-accuracy-mm` asks for your caliper's accuracy, and this loop
treats that as the uncertainty on every dimension. **That is only true when the
surface is flatter than the instrument.**

A printed face is not a datum plane. If three readings across one face spread
further apart than your caliper's stated accuracy, the surface is what you are
measuring, not the machine — and the honest uncertainty is the spread, not the
0.02 mm on the caliper's box. As of ADR-0015 the tool derives this itself:
give it the readings and each axis's uncertainty becomes
`max(declared accuracy, observed spread)`, recorded in the comparison and
honoured by `compensate`. It can only see what it is given — one reading per
dimension still means the declared figure stands in alone.

Which surface, specifically: **the last layer printed.** On the calibration
block that is the tall top face, and it is where pillowing and top-surface
blobs live. Real numbers from one run:

| Surface | Spread across 3 readings |
|---|---|
| Vertical walls (X) | 0.02 mm |
| Vertical walls (Y) | 0.03 mm |
| Shelf top — finished at layer 20, printed over afterwards | 0.01 mm |
| **Tall top — the final layer** | **0.08 mm** |

The walls and the shelf are at caliper accuracy. The final layer is four times
worse, and the Z scale is read from it.

**What that does to Z.** The span is 0.10 mm from nominal and the surface noise
is 0.08 mm, so the answer depends on where the jaw lands:

```
25.12 − 4.21 = 20.91   →  −0.43 %
25.20 − 4.20 = 21.00   →   0.00 %
```

The available answers run from "half a percent short" to "perfect". **There is
no honest Z figure on a part like this**, and the right move is to record
nothing for that axis rather than pick.

So: **the loop's resolution is capped by the print quality it is measuring.**
That makes an optimised profile a prerequisite, not a refinement. A stock vendor
profile is tuned for appearance and speed and will not hold a flat top face; if
you need engineering tolerances you were never going to use it anyway, and you
cannot measure your way to them through it. X and Y survive a rough top face
because they are read off vertical walls. Z does not.

If your Z readings spread more than your caliper's accuracy, stop, fix the top
surface — flow, cooling, top-layer count, ironing — and reprint. Measuring
harder will not help.

### 4. Measure

Caliper jaws flat, closed with the thumb wheel only, square to the face.

| Reading | Where |
|---|---|
| X | across the 40 mm faces, a few mm up from the bed |
| Y | across the 60 mm faces, a few mm up from the bed |
| Z low | bed to top of the **shelf** |
| Z high | bed to top of the **tall face** |

Two habits worth keeping, both cheap:

- **Take each reading twice, in different places on the face.** If they disagree
  by more than your caliper's accuracy, something is on the surface — a seam, a
  blob — and you are measuring it rather than the wall.
- **Once, measure both diagonals of the bottom face.** Nominal √(40²+60²) =
  72.11 mm. Equal diagonals means square; the *difference* is a far more
  sensitive skew detector than side lengths, catching roughly 0.5° as 0.6 mm.
  Thirty seconds, and it is the one fault a clean reprint will not reveal on its
  own.

### 5. Run the loop

Use the wrapper, which chains the two commands so the comparison hash is never
copied by hand — that copy is where a measurement gets attached to the wrong
print:

```powershell
cd F:\Documents\GitHub\OpenDesignCore
.\examples\calibration-loop\measure.ps1 -X 39.98 -Y 59.97 -ZLow 4.01 -ZHigh 25.02 -Material pla
```

Add `-ProposeToProfile pla` once step 6 is done and the machine record exists.
`-InstrumentAccuracyMm` defaults to 0.02 and `-MaxAxisSpreadPct` to 0.15;
override either if your caliper or your process says otherwise.

Or run the two commands yourself:

```powershell
cd F:\Documents\GitHub\OpenDesignCore

dotnet run --project src/OpenDesignCore -c Release -- `
  compare --design artifacts/b7/b74407dedcd54f8737b70dbe6d185d19c8d50278cefa8ed6f76e47a886c72b0b.stl `
          --units mm --voxel-mm 0.2 `
          --measured <X>x<Y>x<Zlow>x<Zhigh> `
          --nominal-step-z-mm 4 --instrument-accuracy-mm 0.02 `
          --material pla
```

Then, with the hash it prints:

```powershell
dotnet run --project src/OpenDesignCore -c Release -- `
  compensate --comparison <hash> --max-axis-spread-pct 0.15 `
             --propose-to-profile pla `
             --machines ../OpenBuildCore/example/machines.json --machine-id k2-plus `
             --studio http://localhost:8770
```

### 6. Record the machine

`compensate --propose-to-profile` refuses until the machine registry says the
axes have been verified. In `OpenBuildCore/example/machines.json`, replace the
K2's `"axis_calibration": null`:

```json
"axis_calibration": {
  "x": {"verified_on": "2026-08-21", "residual_pct": 0.04,
        "how_measured": "calibration-block/0.2 after flow calib, caliper 0.02 mm; diagonals equal within 0.05 mm"},
  "y": {"verified_on": "2026-08-21", "residual_pct": 0.05, "how_measured": "..."},
  "z": {"verified_on": "2026-08-21", "residual_pct": 0.09, "how_measured": "..."}
}
```

All three of `verified_on`, `residual_pct`, `how_measured` are required on any
axis you list — a date with no residual is the appearance of a calibration
rather than one, and the validator refuses it. All three axes must be present.
`rotation_distance` is optional; include it if you changed one.

The residual is what is left *after* correction. If step 1 concluded "checked,
found good", your residuals are just your measured deviations. **"Verified and
found good" is a complete and valid record** — most of the time it is the one
you want.

```powershell
cd ..\OpenBuildCore; python scripts/validate.py    # expect: all valid
```

---

## If it still comes out weird

With flow calibrated and the machine checked, the numbers should be boring. If
they are not, one signature is worth knowing:

**X and Y deviating in opposite directions.** No material or flow effect does
that — shrinkage pulls both in, over-extrusion pushes both out. `compensate`
returns `MachineScaleError` and refuses. If the diagonals from step 4 were
equal, it is not skew either, and the next thing to check is whether the reading
itself is real: half a millimetre on one pair of faces is as easily a seam or a
caliper held at an angle as anything mechanical.

The decisive test, if you get there, is one print: slice the block twice, one
copy rotated 90° about Z. If the same *part* dimension reads long in both, the
error follows the model or the measurement. If whatever lies along machine Y
reads long in both, it follows the machine.

That test is worth doing when there is an anomaly to explain. It is not worth
doing pre-emptively, which is what an earlier draft of this document asked for.

---

## Your first run, for reference

| Axis | Design | Measured | Deviation |
|---|---|---|---|
| X | 40.000 | 39.90 | −0.100 mm / −0.250 % |
| Y | 60.000 | 60.50 | **+0.500 mm / +0.833 %** |
| Z-span | 21.000 | 21.02 | +0.020 mm / +0.095 % |

First-layer offset 0.026 mm. Z-span inside the caliper's 0.02 mm accuracy.

Z and the first layer look healthy, which is not what a badly out-of-square
machine looks like. That print was taken before flow calibration, on a machine
that had never been checked, so the honest reading is that it has too many
unknowns in it to diagnose — which is exactly what `compensate` said by refusing
to turn it into a number.

---

## Open items in the tooling

1. **`MachineScaleError` prescribed a kinematics-specific fix without knowing
   the kinematics.** Now states the observation and stops. Proper fix:
   OpenBuildCore's machine schema needs a `kinematics` field
   (`cartesian` | `corexy` | `delta` | …) and the verdict should read it.

2. **The gate checks machine calibration but not filament calibration.** Flow
   ratio is equally capable of corrupting a shrinkage figure — same failure
   mode, one layer down — and nothing currently asks whether it was calibrated.
   A `flow_calibrated` fact belongs somewhere in the material or profile record,
   and `--propose-to-profile` should want it.

3. ~~**`--instrument-accuracy-mm` is the wrong name for what it does.**~~ →
   **closed 2026-08-24 (ADR-0015).** `--measured` now takes comma-separated
   repeated readings per dimension; each axis's uncertainty is
   `max(declared instrument accuracy, observed spread)`, the spread and raw
   readings are recorded in the comparison (`odc/comparison/0.3`), and
   `compensate` reads them back out of the stored record — so the widened
   uncertainty survives into the verdict rather than being a command-line
   courtesy. Run against the Z case above, the tool now refuses the figure by
   itself. A single reading per dimension still works and falls back to the
   declared accuracy, which is what one reading honestly supports.

Sources for the calibration ordering:
[OrcaSlicer calibration guide (Obico)](https://www.obico.io/blog/orcaslicer-comprehensive-calibration-guide/) ·
[Flow ratio calibration (OrcaSlicer wiki)](https://www.orcaslicer.com/wiki/calibration/flow_ratio_calib)
