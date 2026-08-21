# Verifying a machine's axes before you trust a measurement

This is step 0 of [the calibration loop](README.md), written out in full because
the first attempt at it gave the wrong instruction.

Read the correction first. It is about your machine specifically.

---

## Correction: the advice this tool gave you is wrong for a CoreXY

`compensate` told you:

> Scale the Y axis by 0.99174 (Klipper: multiply that axis's `rotation_distance`
> by it), then print and measure again.

**Do not do that.** The K2 Plus is CoreXY, and on a CoreXY that instruction is
not merely risky — it does not describe an available operation.

On a bedslinger or a Cartesian machine, one motor drives one axis, so
`rotation_distance` on `stepper_y` scales Y and nothing else. CoreXY does not
work that way. Both motors move for every move:

```
motor A travel = X + Y
motor B travel = X − Y
```

Which means:

| What you change | What actually happens |
|---|---|
| Both `rotation_distance` values by the same factor | X **and** Y scale together, equally |
| One `rotation_distance` only | The axes stop being perpendicular — **skew**, not scale |

So on this machine X and Y *cannot* have different scale factors from
`rotation_distance`, and applying 0.99174 to one stepper would have taken a
machine with an unexplained measurement and given it a real, permanent geometric
fault. A pure X move would start producing Y motion.

The verdict logic is right about the physics it checked — no material contracts
on one in-plane axis while expanding on the other — but it named a fix without
knowing the machine's kinematics. That is a bug in the tool, not a subtlety, and
it is tracked at the end of this document.

---

## What you actually measured

From comparison `5f44cb5370ef…`:

| Axis | Design | Measured | Deviation | |
|---|---|---|---|---|
| X | 40.000 mm | 39.90 | −0.100 mm | −0.250 % |
| Y | 60.000 mm | 60.50 | **+0.500 mm** | **+0.833 %** |
| Z-span | 21.000 mm | 21.02 | +0.020 mm | +0.095 % |

First-layer offset came out at **0.026 mm**, and Z-span is inside your caliper's
0.02 mm accuracy. Your Z axis and first layer are in good shape. The entire
problem is that one number: **Y is half a millimetre long.**

Now look at the shape of the error, because it rules things out:

- **Not uniform scale.** That would show the same *percentage* on both.
  −0.250 vs +0.833 is not that.
- **Not over- or under-extrusion.** Wall bulge adds a roughly constant *number
  of millimetres* to every outside dimension. −0.100 vs +0.500 is not that
  either.
- **Not `rotation_distance`.** See above — on CoreXY that is not a thing that
  can happen.

Which leaves two candidates, and they are very far apart in likelihood.

### Candidate 1: skew — arithmetically possible, physically implausible

A skewed gantry turns a commanded rectangle into a parallelogram. To make a
60 mm side measure 60.5 you need a skew factor of about 0.129, which is **7.4°
off square**. Over the 60 mm side, that displaces the far corner by **7.8 mm**.

You would not need a caliper. The part would visibly lean. Pick it up and look
at it — if it looks square, skew of this magnitude is not your answer.

(Smaller skew is still worth ruling out, and there is a very sensitive test for
it below. But it cannot be the cause of a 0.5 mm error on its own.)

### Candidate 2: the measurement or the print artifact — much more likely

+0.500 mm on one pair of faces is the classic signature of measuring something
that is not the wall:

- **the Z seam.** The slicer parks the layer-change seam on one face. A seam
  ridge is exactly the kind of thing that reads as a few tenths on one axis and
  nothing on the other.
- **caliper not square to the face**, catching a corner. Reading long is what
  that always does — never short.
- **a blob, stringing witness, or scar** where a travel move crossed a wall.
- **the caliper riding on the corner radius** rather than sitting flat.

This is the leading hypothesis, and it is the reason the procedure below starts
with re-measuring instead of with a wrench.

---

## The procedure

Work in order. Each step is cheap and each one either stops the process or
narrows it. **Do not change any printer setting until step 3 tells you which
one.**

### Step 1 — Re-measure the part you already have (5 minutes, no printing)

Get the block back out.

**First, just look at it.** Under a light, sighting down each face:

- Does it look square, or does it lean? (Skew check by eye — see candidate 1.)
- Can you see the seam? Which face is it on?
- Any blobs, ridges, or scars on the 60 mm faces?

**Then measure properly.** Caliper jaws flat on the face, closed with the thumb
wheel only — never squeezed — and square to the surface. For each dimension take
**three readings at different heights and positions** and write all three down:

| | Reading 1 | Reading 2 | Reading 3 |
|---|---|---|---|
| X (across the 40 mm faces) | | | |
| Y (across the 60 mm faces) | | | |

Deliberately avoid the seam on one set of readings and deliberately cross it on
another. If the seam is worth 0.3 mm, this is where it shows up.

**Also measure both diagonals of the bottom face**, corner to corner:

| | Measured |
|---|---|
| Diagonal 1 | |
| Diagonal 2 | |

Nominal is √(40² + 60²) = **72.11 mm**, but the absolute value matters less than
the difference between the two. A true rectangle has equal diagonals. This is a
far more sensitive skew detector than side lengths — even 0.5° of skew shows up
as roughly **0.6 mm of diagonal difference**, which any caliper will catch.

**Stop and decide:**

- Diagonals differ by more than ~0.2 mm → you have real skew. Go to step 4.
- Y readings vary by more than 0.1 mm between positions, or the seam-crossing
  reading is the high one → your original 60.50 was an artifact. The machine may
  be fine. Go to step 2 to confirm, then straight to step 5.
- Y is consistently ~60.5 everywhere, diagonals are equal → go to step 2.

### Step 2 — The rotation test (one print, decisive)

This is the single most informative thing you can do, and it settles the
question that matters: **is the error attached to the part, or to the machine?**

Slice the *same* block twice:

- **Copy A** in its normal orientation — 40 mm along machine X, 60 mm along machine Y.
- **Copy B rotated 90° about Z** — so the 60 mm dimension now runs along machine X.

Same profile, same material, same spool, shrinkage compensation still **off**.
Print them together on the same plate if they fit, which removes "different day,
different conditions" as a variable.

Measure both. Then read the result off this table:

| What you see | What it means |
|---|---|
| The **60 mm dimension** reads long in *both* copies, whichever machine axis it lies on | The error follows **the part**. It is the model, the slicer, or the measurement — **not the machine.** Your printer is fine. |
| Whatever lies along **machine Y** reads long in both copies (so the 40 mm side is long in copy B) | The error follows **the machine**. Go to step 4. |
| Both copies now measure correctly | The first print had a one-off fault — a shifted layer, a blob. Re-run the loop and move on. |

Write down which one you got. It goes in the machine record either way.

### Step 3 — Uniform scale check (only if step 2 says "machine")

Before assuming anything exotic, check whether both axes are simply off by the
same factor. This is the one thing `rotation_distance` *can* fix on a CoreXY,
and it is fixed on **both** steppers at once, never one.

From your step-2 measurements, compute the deviation percentage of each axis. If
X and Y now agree with each other within your caliper's accuracy and both differ
from nominal by the same percentage, that is uniform scale:

```
new_rotation_distance = old_rotation_distance × (measured ÷ nominal)
```

Applied to **both** `[stepper_x]` and `[stepper_y]` in `printer.cfg`, with the
same value in each. On the K2 Plus you reach the config through Fluidd on port
**4408** of the printer's local address, or over SSH in root/expert mode.

Before editing anything: **copy `printer.cfg` somewhere safe.** A Creality
firmware update can overwrite it, and you want to know what you changed.

If the two axes still disagree with each other, this is not uniform scale.
Continue to step 4.

### Step 4 — Skew correction (only if the diagonals disagreed)

Klipper has a first-class module for this and you should use it rather than
hand-computing anything. `[skew_correction]` takes measurements from a purpose-
built calibration object and computes the correction with `CALC_MEASURED_SKEW`,
which you then apply with `SET_SKEW` and persist.

Two things to know before you start:

- **Skew correction compensates in software for a mechanical problem.** It is
  legitimate and widely used, but if the gantry is badly out of square, squaring
  it is the better fix and correction is the fallback.
- Print the skew calibration object, not our block. It is designed for the
  diagonal measurement the calculation wants.

Check the frame and belts first, because they are free to check: both X-gantry
ends at the same height, belts at equal and correct tension, no loose pulley
grub screws on the motor shafts.

### Step 5 — Record what you found

This is the step that unblocks the tooling, and it takes two minutes.

Open `OpenBuildCore/example/machines.json`, find the K2, and replace
`"axis_calibration": null` with what you actually established:

```json
"axis_calibration": {
  "x": {
    "verified_on": "2026-08-21",
    "residual_pct": -0.05,
    "how_measured": "calibration-block/0.2 at 0 and 90 deg, caliper 0.02 mm; diagonals equal within 0.05 mm"
  },
  "y": {
    "verified_on": "2026-08-21",
    "residual_pct": 0.04,
    "how_measured": "calibration-block/0.2 at 0 and 90 deg, caliper 0.02 mm; diagonals equal within 0.05 mm"
  },
  "z": {
    "verified_on": "2026-08-21",
    "residual_pct": 0.10,
    "how_measured": "calibration-block/0.2 stepped Z-span, caliper 0.02 mm"
  }
}
```

Rules the validator enforces, so you may as well know them going in:

- All three of `verified_on`, `residual_pct` and `how_measured` are required on
  any axis you list. A date with no residual, or a residual with no method, is
  refused — that is the appearance of a calibration rather than one.
- `rotation_distance` is optional; include it if you changed one, because
  future-you will want to know.
- You need **all three axes** present before `compensate --propose-to-profile`
  will write anything. Two out of three is refused, and it names the missing one.

The residual is what is left over *after* whatever correction you applied — so
if step 2 concluded "the machine is fine, it was the seam", your residuals are
just your step-2 deviations, and that is a completely valid record. "Verified
and found good" is the outcome we are hoping for.

Then check it:

```
cd OpenBuildCore
python scripts/validate.py
```

You want `all valid`.

### Step 6 — Re-run the loop and get your actual PLA number

Print the block again, cleanly:

- shrinkage compensation **off** in the profile
- let it cool completely before measuring — warm plastic is still moving
- note the spool

```
cd OpenDesignCore

dotnet run --project src/OpenDesignCore -c Release -- ^
  compare --design artifacts/b7/b74407dedcd54f8737b70dbe6d185d19c8d50278cefa8ed6f76e47a886c72b0b.stl ^
          --units mm --voxel-mm 0.2 ^
          --measured <X>x<Y>x<Zlow>x<Zhigh> ^
          --nominal-step-z-mm 4 --instrument-accuracy-mm 0.02 ^
          --material pla
```

Take the comparison hash it prints, then:

```
dotnet run --project src/OpenDesignCore -c Release -- ^
  compensate --comparison <hash> --max-axis-spread-pct 0.15 ^
             --propose-to-profile pla ^
             --machines ../OpenBuildCore/example/machines.json --machine-id <the K2's machine_id> ^
             --studio http://localhost:8770
```

If the machine record is complete and the axes now agree, you get a proposal
waiting for approval in the studio, and it carries the material, the machine and
that machine's worst residual in its origin string.

If it still refuses, the refusal names what is wrong. Read it rather than
working around it.

---

## What is likely to happen

Based on the numbers you have: **the most probable outcome is that your printer
turns out to be fine**, step 2 shows the error following the part, and the
0.5 mm was a seam or a caliper angle. Z-span at +0.095 % and a first-layer
offset of 0.026 mm are the readings of a well-behaved machine, and they are hard
to reconcile with a gantry 7° out of square.

That is still a successful run of this loop. It refused to write a number, the
number was not trustworthy, and finding out why cost one print.

---

## Open bug in this tool

`MachineScaleError` names a fix — "multiply that axis's `rotation_distance`" —
without knowing the machine's kinematics. That advice is correct on a Cartesian
machine and actively harmful on a CoreXY, which is most modern fast printers
including this one.

The fix has two parts:

1. OpenBuildCore's machine schema needs a `kinematics` field
   (`cartesian` | `corexy` | `corexz` | `delta` | …), since the machine registry
   is where machine facts belong.
2. The verdict must either read it and tailor the advice, or — when kinematics
   is unknown — describe the *observation* and stop, rather than prescribing a
   remedy it cannot justify.

Until that lands, treat any `rotation_distance` advice from this tool as
Cartesian-only and check it against this document.
