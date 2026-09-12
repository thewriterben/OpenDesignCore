# OpenDesignCore

Open engineering and design.

A computational engineering core that generates mechanical geometry from cited inputs and refuses to
produce a number it cannot account for.

Every artifact it emits carries a provenance record — the inputs, the voxel size, the pinned kernel
versions, the commit — and the same inputs at the same resolution produce a byte-identical result.
Where a value is unknown, the tooling says so and stops, rather than defaulting.

**Who it is for:** people building physical things who need a dimension to be *defensible* — where
"it looked about right in CAD" is not a sufficient answer, and someone may later have to ask where a
number came from.

**Why it exists:** a CAD file records geometry but not what determined it. A dimension in a STEP file
has no citation, no uncertainty, and no record of the measurement or datasheet behind it, so the
reasoning lives in someone's head and expires when they forget it. This engine treats that reasoning
as the artifact.

**Status:** pre-alpha. Nothing here is stable, and the model library is deliberately small.

## Quick start

Windows x64 or macOS arm64 — PicoGK 2.2.0 ships native runtimes for those two only, so the test suite
cannot run on Linux (ADR-0008 amendment, [CONTRIBUTING.md](CONTRIBUTING.md)).

```
git clone --recurse-submodules https://github.com/thewriterben/OpenDesignCore
cd OpenDesignCore
dotnet build OpenDesignCore.sln -c Release

# An enclosure around a part whose dimensions come from a cited datasheet envelope.
dotnet run --project src/OpenDesignCore -c Release -- `
    run-enclosure --part electronic/esp32-s3-wroom-1 --voxel-mm 0.3
```

Expected output — a validated STL, a provenance sidecar, and a ledger row:

```
run 1: PASS
  artifact   sha256:7b1c8fb9dcdb7436b2d6893960e66906477ea5e34dc43e3f4c71e2786b1aa02b
  provenance sha256:...
  stl        artifacts/7b/7b1c8fb9dcdb...stl
```

**That artifact hash is not an example — it is the answer.** The same command on a different machine
produces the same hash; CI checks it on every run. If yours differs, something in the pinned stack
moved, and that is worth knowing before you trust anything else it tells you.

Parts come from a sibling [OpenPartsCore](https://github.com/thewriterben/OpenPartsCore) checkout
(`ODC_OPENPARTSCORE`, else `../OpenPartsCore`); only entries carrying a cited envelope are offered
(ADR-0016). Entries without one are not hidden — the MCP surface's `list_parts` returns them
separately with the reason each is unavailable, so absence is visible rather than silent.

The other commands: `validate-data`, `run-cradle`, `run-calibration-block`, `compare`, `compensate`,
`verify-artifact`, `render-thumbnail`, `handoff`. Run with no arguments for usage.

## What it is

- **Models that run deterministically and record why.** Enclosures fitted to a cited part envelope;
  cradles carved around an imported scan; a calibration block designed to be measured.
- **An import boundary that refuses ambiguity.** Mesh → SDF with units *declared, never inferred*,
  and — for photogrammetry, which is scale-free by construction — a scale reference that determines
  the scale rather than describing it (ADR-0019, ADR-0020).
- **A measurement loop that knows what it cannot conclude.** `compare` judges a printed part against
  its design using repeated readings, widening each axis's uncertainty to the observed spread when
  the surface is rougher than the instrument; `compensate` proposes a slicer correction only when the
  deviation survives that test.
- **Independent verification.** `verify-artifact` re-measures an artifact with a second geometry
  kernel, against tolerances the operator pins and the caller cannot choose (ADR-0017, ADR-0018).
- **Pictures that carry their own provenance.** `render-thumbnail` renders an artifact
  deterministically and records the settings that produced it — and records, in every copy, that a
  picture is not a measurement (ADR-0022). Human CLI only; it is not on the MCP surface.
- **An MCP surface that proposes and never commits.** Reads and deterministic runs execute; anything
  that would start a print is a proposal a human approves elsewhere (ADR-0009).

## What it is not

- **Not a scanner.** It accepts meshes at a validated import boundary; capture pipelines live
  elsewhere.
- **Not an electronics tool, a marketplace, or a settlement layer.** Those are peer systems (ADR-0007).
- **Not a geometry kernel.** Booleans, fields and meshing belong upstream in PicoGK and the LEAP 71
  ShapeKernel. A geometry algorithm proposed here should be a pull request there.
- **Not a home for regulated designs.** Weapons and items restricted in the user's locality are out of
  scope regardless of policy gating elsewhere in the platform.

## Conventions

Length is **millimetres**, matching the PicoGK kernel (ADR-0004); other quantities are SI unless
dimensionally coupled to length, in which case the unit is stated explicitly rather than inferred.
Conversion happens at exactly one place per boundary.

Resolution is **one global voxel size per model run** (ADR-0003), supplied explicitly by the caller,
never defaulted in code, and recorded in the provenance of every artifact. A model below its
resolution floor fails loudly rather than answering coarsely.

See [GLOSSARY.md](GLOSSARY.md) for terms, units, and sign conventions.

## Docs

| | |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Module boundaries and what crosses them |
| [GLOSSARY.md](GLOSSARY.md) | Domain terms, units, sign conventions |
| [DECISIONS.md](DECISIONS.md) | Why things are the way they are |
| [ROADMAP.md](ROADMAP.md) | Now / next / not yet / not ever |
| [DEPENDENCIES.md](DEPENDENCIES.md) | Every dependency, its licence, and why |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Build, test, PR expectations |

## License

Apache-2.0 — see [LICENSE](LICENSE). (ADR-0005)
