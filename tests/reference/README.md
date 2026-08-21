# Reference cases

Cases with answers we didn't compute ourselves. This is the only thing standing between the project and confidently wrong numbers.

Two kinds:

1. **Analytic** — a closed-form solution exists. Sphere and torus volume at known voxel sizes, thick-walled cylinder stress, cantilever tip deflection.
2. **Published** — a benchmark from literature or a standards body, cited exactly.

## Each case records

- The source, cited precisely enough to find it again
- Inputs, with units, and the voxel size
- Expected result, with units
- Tolerance, and *why* that tolerance — discretisation error at that voxel size, floating point, or a stated bound from the source
- What it would mean if this one started failing

## Rules

- Results are only comparable at equal voxel size. Pin it in the test.
- Convergence matters as much as the value: halving the voxel size should move the answer toward the reference at the expected rate. A case that passes at one resolution and diverges as you refine is a failing case.
- A tolerance is never loosened to make a test pass. If the result moved, either the change is wrong or the tolerance was wrong for a reason you can now articulate — write which in the commit.
- Report the measured deviation even when the case passes. A bound never compared against the actual number stays loose forever, and tightening it later should be an informed commit rather than a guess.

## How these run

The files here compile into `OpenDesignCore.Tests` via an explicit `Compile Include` in that project, so `dotnet test OpenDesignCore.sln -c Release` runs them with everything else. Separate directory, one command — the point is the distinction in kind, not a second test runner.

## Cases

### `EnclosureVolumeTest` — voxel volume against exact geometry

**Source:** none needed; the expectation is derived in the test from the model's own parameters. The enclosure tray is a rectangular solid minus a rectangular cavity, so its volume is closed-form:

```
V = (Ex + 2c + 2w)(Ey + 2c + 2w)(w + Ez + c) − (Ex + 2c)(Ey + 2c)(Ez + c)
```

**Inputs:** envelope 18.0 × 25.5 × 3.1 mm, clearance 0.30 mm, wall 2.40 mm, voxel 0.10 mm.

**Expected:** 2543.184 mm³. The difference from the voxel field's reported volume is discretisation error and nothing else — there is no modelling approximation in a box.

**Tolerance and why:** `|V_voxel − V_exact| ≤ A · voxel_size`, one voxel of material spread over the analytic surface area `A`. A signed-distance field places the interface inside a band around the true surface, and being wrong by a full voxel everywhere is the worst that band permits. It scales with two declared inputs, so it is not an epsilon. This is a **ceiling**, not a claim about how accurate PicoGK is.

**Measured:** 0.162 % (4.130 mm³ against a bound of 237.996 mm³), on Windows x64, 2026-08-21. Far below the ceiling, as expected. Tighten the bound once several CI runs have reported the figure — never to whatever one run produced.

**If it starts failing:** the geometry composition or the volume measurement changed, not the discretisation. A box's exact volume does not drift.

**Gap against the convergence rule above:** this case pins one resolution and does not yet check that halving the voxel size moves the answer toward the reference at the expected rate. That is a real omission measured against the rules on this page, not a design choice. It is not written yet because an unrun convergence assertion fails identically for a genuine bug and for an error already down at float noise, and telling those apart needs the measured figures this directory has only just started producing.

**Still missing from the list at the top of this page:** sphere and torus volume, thick-walled cylinder stress, cantilever tip deflection. The first two would characterise the kernel's discretisation rather than this repo's composition, which is arguably upstream's to defend — but they are what calibrates the expectation for every case that *is* ours, so the intent recorded here stands.
