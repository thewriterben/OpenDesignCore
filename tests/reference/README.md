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

### `CameraMountVolumeTest` — voxel volume against exact geometry, at two resolutions

**Source:** none needed; derived in the test from the model's own parameters. The mount is a base slab plus two uprights, each with a slot cut into it — five boxes, two cavities — so:

```
V = baseX·baseY·bt  +  2·(ux·uy·uz)  −  2·(sx·sy·gh)
```

Each slot lies wholly inside its upright, so it subtracts once with no intersection term.

**Inputs:** the measured `CameraMountParams.ODefault()` — board 27.90 × 68.25 × 1.25 mm, wall 2.40, slot clearance 0.20, grip 3.00 × 40.00, base 4.00 thick reaching 32.00 front / 13.00 back, seat lift 3.00. Voxel 0.30 mm, and 0.15 mm for the convergence case.

**Expected:** 8641.920 mm³, over a derived surface area of 6188.4 mm².

**Tolerance and why:** `|V_voxel − V_exact| ≤ A · voxel_size`, the same one-voxel-band argument as `EnclosureVolumeTest`. The area term matters more here than there, and it is derived rather than estimated because **the slots contribute less area than they appear to**: a slot removes its opening from the upright's inboard face and its mouth from the upright's top, and adds its far wall, its floor and its two side walls — the first two pairs cancel term for term, so each slot's net contribution is exactly its two side walls. A closed form that missed that cancellation would produce a plausible number. It was checked twice, by hand and in code, and both give 6188.4 mm².

**Measured:** 0.240 % (20.723 mm³ against a bound of 1856.520 mm³ — 1.12 % of the ceiling), Windows x64, 2026-09-25.

**Convergence:** this case does what `EnclosureVolumeTest` does not — it runs at 0.30 and 0.15 mm and asserts the finer error is smaller. Measured: 20.723 mm³ → 2.166 mm³.

**The ratio of 9.57 is not a convergence rate and must not be read as one.** The error changes *sign* between the two resolutions: the coarse field under-reports (8621.197 against 8641.920) and the fine field over-reports (8644.086). Two errors of opposite sign are not the same quantity, so their ratio measures a crossing rather than an order of accuracy. The assertion is therefore only on direction — the weakest defensible form of the rule on this page — and it stays that way until enough runs exist to say something about rate honestly. Tightening it to a rate on the strength of this one figure is exactly what rule 4 above forbids.

**If it starts failing:** on the volume case, the geometry composition or the volume measurement changed — five boxes have an exact volume and it does not drift. On the convergence case, refining made the answer worse, which means the error is no longer dominated by surface discretisation and something resolution-dependent is wrong in the composition.

**Gap against the convergence rule above:** this case pins one resolution and does not yet check that halving the voxel size moves the answer toward the reference at the expected rate. That is a real omission measured against the rules on this page, not a design choice. It is not written yet because an unrun convergence assertion fails identically for a genuine bug and for an error already down at float noise, and telling those apart needs the measured figures this directory has only just started producing.

### `RetroactiveFixtureCheckTest` — the assembly check rejects the two mounts that failed on the bench

**Source:** the bench, not arithmetic. camera-mount/0.1 would not accept the board: its slots met the JST connectors (2026-09-25). camera-mount/0.2 held the board but the USB-C port could not be plugged in (2026-09-26). The part is OpenPartsCore's `features` for `boards/lilygo-t-camera-plus-s3` at V1.0-V1.1, read from LILYGO's STEP (model sha256 `9f8e6d6c...82ab9`). The answer, "rejected, for these features", was given by the physical parts before this check existed.

**Inputs:** v0.1 rebuilt test-only from a56a7ac (`Historical/CameraMountV01.cs`); v0.2 from `CameraMountParams.ODefault()`. Voxel 0.15 mm, tolerance 0.30 mm (two voxels, the least the check accepts).

**Expected:**
- v0.1: contact at CN1 and CN2.
- v0.2: contact at USB1; USB1's access listed as unverified, because no plug length is recorded.
- Access at USB1 for a probe plug of `2 x tolerance + voxel`, the shortest depth the check can resolve. If that collides, every real plug does.
- Positive control, v0.2 with the floor dropped to 4 mm: no contact violations.

**Tolerance and why:** membership, not magnitude. The overlap volumes are reported and are voxel-dependent. The assertion is which features appear.

**The unmeasured values:** which laminate face the lens stands on, and the lens axis's position across the board. Every case runs under both faces and at 0 and ±2 mm about the board centre. Measured 2026-09-26, the violation lists are identical across all of them. The volumes spread by up to 3.7 % (USB1 contact, 7.828 to 8.115 mm³).

**If it starts failing:** the registry's features changed (the output names the revision, the model hash and the registry commit), or the check stopped seeing geometry. A check that no longer rejects these two would pass the next part that fails the same way.

**Still missing from the list at the top of this page:** sphere and torus volume, thick-walled cylinder stress, cantilever tip deflection. The first two would characterise the kernel's discretisation rather than this repo's composition, which is arguably upstream's to defend — but they are what calibrates the expectation for every case that *is* ours, so the intent recorded here stands.
