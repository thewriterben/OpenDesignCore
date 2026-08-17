# Closing the loop with a caliper

Design → print → measure → compensation, without a 3D scanner. About an hour,
most of it printing.

The part is already staged at
`3DP/AdvancedStudio/studio-core/staging/calibration-block-0.1-1ccd5e63dfbe.stl`
(20 × 30 × 15 mm, artifact `1ccd5e63dfbe`). Regenerate it any time with step 1.

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
the material you want to calibrate. Compensation is per material and per spool.

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

Three readings, each across the flat faces, caliper square to the surface.

**Avoid the first layer.** Elephant's foot is a first-layer squish artefact,
not shrinkage, and measuring across it gives a Z compensation that makes every
subsequent part wrong in the middle. Take the Z height across the body, and X
and Y a few millimetres up from the bed.

Sanity check before believing them: if two of your three readings are close to
each other and far from nominal, you probably measured the same pair of faces
twice. That is exactly what the unequal axes are for.

## 4. Compare

```
dotnet run --project src/OpenDesignCore -c Release -- \
  compare --design artifacts/1c/1ccd5e63...stl --units mm --voxel-mm 0.3 \
          --measured 19.93x29.89x15.02 --instrument-accuracy-mm 0.02
```

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
      --propose-to-profile petg --studio http://localhost:8770
```

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
