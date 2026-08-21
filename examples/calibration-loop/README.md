# Closing the loop with a caliper

Design → print → measure → compensation, without a 3D scanner. About an hour,
most of it printing.

The part is already staged at
`3DP/AdvancedStudio/studio-core/staging/calibration-block-0.2-b74407dedcd5.stl`
— 40 × 60 mm, with a shelf at 4 mm and a tall face at 25 mm. Regenerate it any
time with step 1.

**It has a step, and that is the whole trick.** The first version was a plain
box whose instructions said to measure Z "away from the first layer". That is
impossible: a bed-printed part's height *begins* at the first layer, so every
external height reading contains it.

The two errors are different in kind, which is what makes them separable:

| | |
|---|---|
| First-layer squish | a **constant** — the same fraction of a millimetre at any height |
| Shrinkage | **proportional** — a percentage of the dimension |

Measure two heights on one part and the constant cancels out of their
difference. What is left over is the squish itself, which is worth having:
it is the elephant's-foot / first-layer figure, a different slicer setting that
no shrinkage percentage will fix.

---

## 0. Calibrate first — machine, then filament, then measure

A measured dimension is the design, times the material's shrinkage, times the
machine's idea of a millimetre, times how much plastic the extruder actually
pushed. A caliper sees the product, not the factors. Whichever factor you *name*
when you store the result is the one that inherits all the others' error.

So the order is: **square the machine, calibrate the filament (temperature, then
flow, then pressure advance), then print and measure.** Flow rate especially —
over-extrusion bulges every external wall, and measuring shrinkage on top of an
uncalibrated flow ratio produces a number that says "PLA shrinks 0.4%" and means
"my flow is 3% high". Same failure this whole step exists to prevent, one layer
down.

Full sequence in [CALIBRATE-FIRST.md](CALIBRATE-FIRST.md).

This is not a caution written in advance. It happened on the first real run of
this walkthrough, on purpose:

```
x = 39.9   (nominal 40)   -0.25 %
y = 60.5   (nominal 60)   +0.83 %
```

One axis short, the other long. Averaged into the single XY number OrcaSlicer
wants, that reads as *"PLA shrinks about 0.29 %"* — a believable figure, near
the published ones, and completely wrong. **No material contracts on one
in-plane axis and expands on the other.** Shrinkage is a bulk property; it
moves X and Y the same way. Whatever produced that Y reading, it was not the
material — and the loop was one command away from filing it under the name of a
plastic, permanently, with a provenance hash making it look rigorous.

(What it *was* is still open, and that is the point. The print predated any flow
calibration, on a machine that had never been checked, so it had too many
unknowns in it to diagnose. Half a millimetre on one pair of faces is as easily
a Z seam or a caliper held at an angle as it is anything mechanical. The answer
is not forensics on a bad print — it is calibrating the things upstream and
printing a clean one.)

**If you do need a mechanical correction, the kinematics decides what is even
available.** On a Cartesian machine one motor drives one axis, so a single axis
can be scaled alone. On a CoreXY both motors move for every move, X and Y cannot
have different scale factors, and changing one stepper's `rotation_distance`
produces skew rather than scale.

Once the machine is checked, record the result in your OpenBuildCore machine
registry:

```json
"axis_calibration": {
  "x": {"verified_on": "2026-08-21", "residual_pct": 0.04,
        "how_measured": "calibration-block/0.2, caliper 0.02 mm"},
  "y": {"verified_on": "2026-08-21", "residual_pct": 0.02, "how_measured": "..."},
  "z": {"verified_on": "2026-08-21", "residual_pct": 0.03, "how_measured": "..."}
}
```

**You can run every step below on an uncalibrated machine**, and you should —
that is how you find out it needs calibrating, and step 5 will tell you which
axis. What you cannot do is store the result: step 6 refuses to write a
compensation into a material profile until the machine underneath it is known
good, on all three axes. Absent is not "probably fine", and two axes out of
three is not enough, because the untested one is where the fault hides. It hid
in Y; X and Z looked healthy.

## 1. Make the block

```powershell
dotnet run --project src/OpenDesignCore -c Release -- `
  run-calibration-block --instrument-accuracy-mm 0.02
```

`--instrument-accuracy-mm` is your caliper's stated accuracy — 0.02 mm on a
typical digital one, 0.05 mm on a cheap one, and whatever the label says on
yours. It takes no default because it decides, later, whether a deviation you
measure is real or is the tool. It does **not** set the voxel size (see step 4).

**The axes are deliberately unequal.** A calibration *cube* is the usual choice
and it is the wrong one: at 20×20×20 a measurement taken across the wrong pair
of faces reads exactly like a correct one, so a transposed reading silently
becomes a compensation on an axis you never measured. At 20×30×15 the number
tells you which face you measured.

## 2. Print it

Slice it in Creality Print with the profile you actually use, and print it in
the material you want to calibrate.

**Write down which material, and which spool.** Compensation is a property of
one material — PLA and PETG shrink differently — and every command from here
takes `--material`. It is required and has no default, because the first time
this walkthrough was followed the examples all said `petg` while never saying
what to print, and the part came out in PLA. Nothing would have caught it.

Three things to get right, because they are the difference between measuring the
material and measuring something else:

- **Calibrate flow rate for this filament first** (step 0). Over-extrusion
  bulges every external wall, and a shrinkage figure measured on top of it is
  partly a flow setting wearing the material's name.
- **Print with shrinkage compensation OFF**, or you are measuring the
  compensation rather than the material.
- **Let it cool completely.** Warm plastic is still shrinking.

You can move the sliced job to the printer through the studio:

```powershell
dotnet run --project src/OpenDesignCore -c Release -- `
  handoff --run <id> --stage <staging dir> --upload <name>.gcode
```

which proposes the upload for approval; propose the print separately.

## 3. Measure it

**Four readings**, caliper square to the surface every time.

| Reading | Where |
|---|---|
| X | across the 40 mm faces, a few mm up from the bed |
| Y | across the 60 mm faces, a few mm up from the bed |
| Z low | bed to the top of the **shelf** |
| Z high | bed to the top of the **tall face** |

X and Y are taken a few millimetres up because elephant's foot flares the very
bottom — and unlike Z, that *is* avoidable, because those faces are vertical.

Both Z readings deliberately include the first layer. They have to; there is
nowhere else to start. Since both contain the same squish, their difference
contains none.

Sanity check before believing them: if two readings are close to each other and
far from nominal, you probably measured the same pair of faces twice. That is
what the unequal X and Y are for.

## 4. Compare

```powershell
dotnet run --project src/OpenDesignCore -c Release -- `
  compare --design artifacts/b7/b74407de...stl --units mm --voxel-mm 0.2 `
          --measured 39.86x59.79x4.05x25.10 `
          --nominal-step-z-mm 4 --instrument-accuracy-mm 0.02 `
          --material pla
```

Four values are X, Y, Z-low, Z-high. `--nominal-step-z-mm` is required with
four, because the design STL records its overall height but not where the shelf
was put — and the shelf's nominal height is what separates the offset from the
shrinkage.

Three values still work, for a plain box, and the tool says plainly that the Z
figure then contains the first-layer offset and cannot be cleanly compensated.

It compares against the **exported** dimensions, not the ones you asked for.
That is why voxel size is not tied to caliper accuracy: whatever the grid did
is measured and recorded in the artifact's provenance, so it never reaches your
deviation figure. An earlier version of this got that wrong and demanded
0.002 mm voxels — 10¹¹ of them.

## 5. Judge whether a compensation is defensible

```powershell
dotnet run --project src/OpenDesignCore -c Release -- `
  compensate --comparison <report sha256> --max-axis-spread-pct 0.15
```

`--max-axis-spread-pct` is yours to declare: how much X/Y disagreement still
permits a single shrinkage factor. A process judgement, not a constant.

Expect it to refuse sometimes. Every refusal is useful:

| Verdict | Meaning |
|---|---|
| `WithinScannerNoise` | The deviation is inside your caliper's accuracy. Nothing to compensate — you would be compensating for the tool. |
| `AxisNotSignificant` | One axis moved and the other sat inside instrument noise. Averaging a real reading with a non-reading halves it. |
| `AccuracyUndeclared` | No instrument accuracy given, so signal cannot be told from error. |
| `AxesDisagree` | X and Y differ by more than your threshold, in the same direction. One factor cannot express that, and their mean is wrong on both. |
| `MachineScaleError` | X and Y moved in **opposite** directions. No material does that — see step 0. It names the axis and gives you the scale factor. |

**Z is never folded into the XY figure.** Orca's Shrinkage (XY) applies to X
and Y only, and Z has different causes. Not pedantry: in a worked example with
X −0.35%, Y −0.37%, Z +0.13%, averaging all three gives −0.20% against the
correct −0.36%. Roughly half the compensation you need.

## 6. Propose it to the profile

```powershell
  ... compensate --comparison <sha> --max-axis-spread-pct 0.15 `
      --propose-to-profile pla `
      --machines ../OpenBuildCore/example/machines.json --machine-id k2-plus `
      --studio http://localhost:8770
```

Two gates stand in front of the write, and both exist because the thing they
prevent already happened once.

**The profile must match the material you measured.** Proposing a PLA
measurement to a `petg` profile is refused before it reaches the studio — that
is not a hypothetical, it is what this walkthrough told the first person to do.

**The machine must be calibrated on all three axes.** `--machines` and
`--machine-id` are required *here only*; step 5 never asks for them, because
measuring an unverified machine is the point of measuring it. This step is the
one that writes something durable, so this is where the machine has to be known.
An unrecorded calibration is refused, and so is a partial one, naming the axes
still missing. The refusal tells you what to do rather than leaving you to read
this file.

Both gates fire before the studio is contacted. That ordering is deliberate and
pinned by a test: if the network call went first, a bad number would land in
front of a human on a dashboard, and dashboards get approved.

AdvancedStudio computes the OrcaSlicer shrinkage percentage from the measured
pair — this side never computes slicer settings — and holds it for approval.

The stored value carries `odc-comparison:<sha256>` as its origin, together with
the material, the machine and that machine's worst axis residual — so months
later, "where did this 0.4% come from" and "was the printer any good when it was
taken" both have answers that are not "I think I measured it once".

## 7. Prove it worked

Print the same block again with the new compensation applied and repeat step 3.
The deviation should fall toward your caliper's accuracy, at which point
`compensate` starts answering `WithinScannerNoise` — the loop telling you it is
done.

**Status of this loop, honestly.** It has been run once for real, on a K2 Plus
in PLA. That run produced no compensation — it produced a `MachineScaleError`
and a scale factor for the Y axis, which is the correct outcome and the reason
step 0 exists. So the measurement path, the verdicts and both gates are proven
against a physical part; whether a resulting shrinkage percentage makes the next
print better is still unanswered, because no run has yet earned one.
