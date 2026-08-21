# Contributing

## Build and test

```
git submodule update --init                                          # after clone
dotnet build OpenDesignCore.sln -c Release
dotnet test OpenDesignCore.sln -c Release
dotnet format OpenDesignCore.sln --verify-no-changes --exclude external
```

All three must be clean before a PR. CI runs exactly these, plus a determinism
check that runs the enclosure model twice and compares content addresses.

**Platform:** Windows x64 or macOS arm64. PicoGK 2.2.0 ships native runtimes for
those two only (ADR-0008 amendment), so on Linux the build and the formatter work
and **the test suite cannot run** — anything constructing `Library` fails at
native load. Nothing this project can fix from here; it is the pinned kernel's
platform support.

## Definition of done

- Builds with no new warnings
- Tests pass, including `tests/reference/`
- Public API documented, with units on every quantity
- At least one example still runs end to end
- CHANGELOG.md entry
- ADR in DECISIONS.md if a real decision was made

## What gets rejected

- A module nothing calls. If it isn't reachable from the end-to-end path or a test, it doesn't merge.
- A hard-coded epsilon.
- A bare number where a dimensioned quantity belongs.
- A material property, physical constant, or standards clause without a citation.
- An accuracy claim without a reference case backing it.
- An abstraction with fewer than three concrete uses.

## Reference tests

`tests/reference/` holds cases with known answers — analytic solutions or published results. Any solver or numerical algorithm needs at least one. See the README in that directory for the three rules that keep them from decaying into regression tests: the expected value is derived rather than recorded, the tolerance is argued rather than tuned, and the measured error is reported even on a pass.

They compile into `OpenDesignCore.Tests`, so `dotnet test` runs everything in one command.
