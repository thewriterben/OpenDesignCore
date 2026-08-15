# Contributing

## Build and test

```
⟨build command⟩
⟨test command⟩
⟨lint command⟩
```

All three must be clean before a PR.

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

`tests/reference/` holds cases with known answers — analytic solutions or published results. Any solver or numerical algorithm needs at least one. See the README in that directory.
