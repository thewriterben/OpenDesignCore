---
title: PicoGK
type: entity
updated: 2026-09-11
sources:
  - external/PicoGK (submodule source, pinned [2.2.0])
  - src/OpenDesignCore/Import/ScanImport.cs (the two workarounds, with their measurement dates)
  - tests/OpenDesignCore.Tests/ScanScaleTests.cs (the regression that pins one of them)
  - DECISIONS.md ADR-0001, ADR-0003, ADR-0004, ADR-0008, ADR-0020
---
LEAP 71's voxel/SDF geometry kernel, a thin layer over OpenVDB, Apache-2.0. This repo is built on it
(ADR-0001) and inherits its constraints deliberately: one global voxel size (ADR-0003), millimetres
throughout (ADR-0004). Consumed as a pinned dependency — `[2.2.0]` from NuGet, ShapeKernel as a
submodule at `ShapeKernel-v2.1.0` (ADR-0008). Upgrading is its own commit.

**This page exists for one reason: two things in 2.2.0 do not do what their signatures say, we work
around both, and both workarounds are version-bound.** An upgrade that does not re-test them will
either reintroduce a silent bug or double-apply a correction.

## Known 2.2.0 behaviours we work around

**1. `mshFromStlFile` accepts a scale argument and does not apply it.** Measured 2026-09-11: the same
sphere imported at scale `1.0` and `2.0` came out the same size (ratio 1) through the binary path,
while our own ASCII parser scaled correctly (ratio 2). This was live from 2026-08-15 to 2026-09-11 —
every binary STL imported with a non-1.0 scale was imported unscaled while the sidecar recorded the
requested scale as applied. **Workaround:** pass `1.0f` to the kernel and apply the scale ourselves,
once, after load, for both paths (ADR-0020). **Upgrade hazard:** a version that *does* honour the
argument would double-apply. `ScanScaleTests` pins the property (scaled import is bigger; both paths
agree) but cannot tell you *where* the scaling happened — check the call site, not just the green
suite.

**2. The `(vecScale, vecOffset)` overload of `mshCreateTransformed` applies a different scale
component per vertex.** Recorded in `ScanImport` as an upstream bug at the time the import boundary
was written. **Workaround:** use the `Matrix4x4` overload throughout.

Neither is patched in the submodule — geometry belongs upstream (ADR-0001), so these are
call-site workarounds, not a fork. **Neither has been reported to LEAP 71 yet.** `TODO(report)`.

## What this means for an upgrade

The pin is not bureaucratic. Before moving off `[2.2.0]`:

1. Re-run the probe behind `ScanScaleTests` against the new version and find out whether the scale
   argument is honoured. If it is, remove our transform in the same commit — not after.
2. Re-check the transform overload before simplifying any `Matrix4x4` call back to the convenience
   form.
3. Expect artifact hashes to move. Determinism is defined against *pinned* versions
   (ADR-0003), so a kernel change legitimately changes byte-identical output — including the
   cross-machine golden `7b1c8fb9dcdb…`, which is a claim about one pinned stack and not about
   PicoGK in general.

## The lesson that generalises past this kernel

Both bugs are **silent**: a wrong number, not an exception. The scale one survived 27 days inside a
boundary whose own doc comment says it exists to prevent silently mis-scaled scans, because every
test used scale `1.0` — the one value where correct and broken are indistinguishable.

A dependency's signature is a claim, and this repo's whole discipline is that a claim is not evidence
until something measures it. **That applies to the kernel too, not only to data.** Where we rely on a
kernel argument having an effect, a test should compare the effect against its absence.
