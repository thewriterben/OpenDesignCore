# Working in this repo

OpenDesignCore. Read first, in this order: `ARCHITECTURE.md`, `GLOSSARY.md`, `DECISIONS.md`, then `ROADMAP.md` for scope.

## Verify with

```
⟨build⟩
⟨test⟩
⟨lint⟩
```

Run these after every step. Don't move forward while anything is red.

## Non-negotiables

- SI internally; explicit units at API boundaries; no silent conversion.
- Deterministic output: same inputs and version produce byte-identical results.
- Tolerance is always a parameter. No ambient epsilons.
- Validate geometry and mesh at boundaries before solving or exporting; fail loudly and specifically.
- Artifacts carry provenance: inputs, parameters, version, commit.
- Degradation is never silent — fallbacks and relaxed tolerances appear in the returned result, not just a log.
- Never invent material properties, constants, or standards references. Cite or mark `TODO(source)`.

## How to work

- Open the actual files. Never infer an API from its name.
- Anything touching more than one file gets a short plan first.
- Small steps, verified individually.
- Don't add code nothing calls. Documented-but-unreachable feature: wire it or delete it, and say which.
- Same error twice: stop and report rather than trying a third variation.
- Expensive-to-reverse choice: write the ADR in the same change.
