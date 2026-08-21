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

## 1. Make the block

```
dotnet run --project src/OpenDesignCore -c Release -- \
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

Two things to get right, because they are the difference between measuring the
printer and measuring a defect:

- **Let it cool completely.** Warm PETG is still shrinking.
- **Print with shrinkage compensation OFF**, or you are measuring the
  compensation rather than the material.

You can move the sliced job to the printer through the studio:

```
dotnet run --project src/OpenDesignCore -c Release -- \
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

```
dotnet run --project src/OpenDesignCore -c Release -- \
  compare --design artifacts/b7/b74407de...stl --units mm --voxel-mm 0.2 \
          --measured 39.86x59.79x4.05x25.10 \
          --nominal-step-z-mm 4 --instrument-accuracy-mm 0.02 \
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

```
dotnet run --project src/OpenDesignCore -c Release -- \
  compensate --comparison <report sha256> --max-axis-spread-pct 0.15
```

`--max-axis-spread-pct` is yours to declare: how much X/Y disagreement still
permits a single shrinkage factor. A process judgement, not a constant.

Expect it to refuse sometimes. All three refusals are useful:

| Verdict | Meaning |
|---|---|
| `WithinScannerNoise` | The deviation is inside your caliper's accuracy. Nothing to compensate — you would be compensating for the tool. |
| `AccuracyUndeclared` | No instrument accuracy given, so signal cannot be told from error. |
| `AxesDisagree` | X and Y differ by more than your threshold. One factor cannot express that, and their mean is wrong on both. |

**Z is never folded into the XY figure.** Orca's Shrinkage (XY) applies to X
and Y only, and Z has different causes. Not pedantry: in a worked example with
X −0.35%, Y −0.37%, Z +0.13%, averaging all three gives −0.20% against the
correct −0.36%. Roughly half the compensation you need.

## 6. Propose it to the profile

```
  ... compensate --comparison <sha> --max-axis-spread-pct 0.15 \
      --propose-to-profile pla --studio http://localhost:8770
```

**The profile must match the material you measured.** Proposing a PLA
measurement to a `petg` profile is refused before it reaches the studio — that
is not a hypothetical, it is what this walkthrough told the first person to do.

AdvancedStudio computes the OrcaSlicer shrinkage percentage from the measured
pair — this side never computes slicer settings — and holds it for approval.

The stored value carries `odc-comparison:<sha256>` as its origin, so months
later "where did this 0.4% come from" has an answer that is not "I think I
measured it once".

## 7. Prove it worked

Print the same block again with the new compensation applied and repeat step 3.
The deviation should fall toward your caliper's accuracy, at which point
`compensate` starts answering `WithinScannerNoise` — the loop telling you it is
done.

**Until step 7, none of these numbers are validated.** The refusals are tested
and the wiring is proven end to end; whether the resulting percentage makes the
next print better is the one thing only a real print can answer.
