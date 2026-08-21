# Reference tests

Cases with a **known answer** — an analytic solution or a published result —
rather than an answer this codebase computed and then froze. A test that pins
today's output is a regression test; it tells you a number changed, never that
it was right. Both kinds are useful and only one belongs here.

`CONTRIBUTING.md` has required this directory since the repo was seeded and it
did not exist until 2026-08-21. What follows is what it holds and, more
importantly, the rules for adding to it.

## Layout

These files compile into `OpenDesignCore.Tests` (an explicit `Compile Include`
in that project), so `dotnet test OpenDesignCore.sln -c Release` runs them with
everything else. Separate directory, one command — the point is the distinction
in kind, not a second test runner.

## The rules

**The expected value is derived, never recorded.** Every case computes its
expectation from first principles in the test itself, from the same inputs the
model was given. If the expectation is a literal, it must be traceable to a
citation in the comment beside it — a paper, a standard, a datasheet — and that
citation is subject to the same link-rot check as `data/` (`.github/workflows/citations.yml`).

**The tolerance is declared and justified, never tuned.** Picking a bound
because it makes the test pass turns a reference case into a regression test
wearing a costume. A bound must come from an argument about the discretization,
and the argument goes in the comment.

**Report the measured error even when the test passes.** A bound that is never
compared against the actual number stays loose forever. Each case writes its
measured deviation to test output, so the log carries the real figure and
tightening the bound later is a deliberate, informed commit.

## Cases

### `EnclosureVolumeTest` — voxel-derived volume against exact geometry

The enclosure tray is a rectangular solid minus a rectangular cavity, so its
volume has a closed form in the model's own parameters:

```
V = (Ex + 2c + 2w)(Ey + 2c + 2w)(w + Ez + c) − (Ex + 2c)(Ey + 2c)(Ez + c)
```

The voxel field's volume comes from `Voxels.CalculateProperties`. The difference
between them is discretization error and nothing else — there is no modelling
approximation in a box.

**Bound:** one voxel of material spread over the whole surface,
`|V_voxel − V_exact| ≤ A · voxel_size`, where `A` is the analytic surface area.
This is a *ceiling* derived from the representation — a signed-distance field
places the interface inside a band around the true surface, and being wrong by
a full voxel everywhere is the worst that band permits. It is deliberately not
a claim about how accurate PicoGK actually is. The measured figure is expected
to be far below it.

**Not yet claimed:** the tight bound. Once CI has reported the measured relative
error a few times, tighten this in its own commit with the observed number in
the message. Do not tighten it to whatever the first run produced.

**Deliberately absent:** a convergence case (halve the voxel size, assert the
error falls). It is the right test for a discretized quantity and it is not here
yet, because a convergence assertion that has never been run can fail for two
opposite reasons — a real convergence bug, or an error already down at float
noise where the ratio is meaningless — and telling those apart needs the
measurement this directory does not have yet. Add it once `EnclosureVolumeTest`
has reported real numbers.

**Deliberately out of scope:** analytic cases that test the kernel rather than
this repo — a sphere's `4/3 πr³` from a voxel field measures PicoGK's
discretization, which is upstream's to defend. A case earns a place here by
covering geometry OpenDesignCore composes.
