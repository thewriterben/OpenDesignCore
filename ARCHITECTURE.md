# Architecture

> Partly written. Sections marked ⟨…⟩ come out of the first architecture session; the geometry and determinism sections are settled by ADR-0001 through ADR-0004.

## Where OpenDesignCore sits

```
  OpenDesignCore        requirements, constraints, validation, provenance, fab handoff
  ──────────────────────────────────────────────────────────────────────────
  LEAP71 ShapeKernel    shape construction, lattices
  PicoGK (C# API)       voxel fields, booleans, scalar/vector fields, viewer
  PicoGK Runtime (C++)  OpenVDB
```

The two lower layers are geometry and nothing else. OpenDesignCore's contribution is the layer above: capturing what a part has to do, encoding the manufacturing constraints it has to satisfy, checking that the generated result actually satisfies them, and carrying provenance through to fabrication. If a proposed feature belongs in ShapeKernel, it should be a pull request to ShapeKernel.

## The end-to-end path

The smallest useful thing OpenDesignCore does, start to finish:

⟨requirements input⟩ → ⟨model evaluation on PicoGK⟩ → ⟨validated artifact + provenance record⟩

Everything else is an elaboration of that path. If a proposed module doesn't serve it, it goes in ROADMAP.md instead of the codebase.

## Domain objects

| Object | Guarantees | Owned by |
|---|---|---|
| ⟨Requirement⟩ | ⟨is checkable — carries the test that decides whether a result meets it⟩ | ⟨module⟩ |
| ⟨Model run⟩ | ⟨fully described by its inputs, voxel size, and pinned versions⟩ | ⟨module⟩ |
| ⟨Artifact⟩ | ⟨never exists without the provenance record that produced it⟩ | ⟨module⟩ |

## Modules

### ⟨module⟩

- **Responsibility:** ⟨one sentence⟩
- **Depends on:** ⟨…⟩
- **Accepts:** ⟨types crossing the boundary in⟩
- **Returns:** ⟨types crossing out⟩
- **Does not:** ⟨the thing people will keep trying to make it do⟩

## Geometry representation

Signed-distance voxel fields over OpenVDB, via PicoGK. See ADR-0001 and ADR-0003.

Resolution is a single global voxel size, in millimetres, set once at initialisation through `Library.Go`. It is an explicit input to every model run and part of every artifact's provenance. Nothing in this codebase defaults it or changes it mid-run.

What this buys: booleans that are numerically robust regardless of geometry complexity, and lattice and field operations as first-class primitives.

What it costs: no exact analytic surfaces, results comparable only at equal voxel size, and memory and time that scale hard as resolution drops. Detail below one voxel cannot be expressed — a model whose result is meaningless below some resolution must declare that and fail loudly.

## Determinism

Same inputs, same voxel size, same pinned versions → byte-identical outputs.

| Source | Handling |
|---|---|
| Voxel size | Explicit model input; recorded in provenance and in OpenVDB metadata |
| Submodule versions | Pinned to release tags; upgrades are deliberate and tested |
| Random seeds | Explicit, threaded through config |
| Parallel reduction order | ⟨deterministic reduction / single-threaded in the model layer⟩ |
| Hash iteration | Ordered collections in any path affecting output |
| Floating point | ⟨fixed op order; no fast-math⟩ |
| Geometric robustness | Handled by the kernel — voxel booleans are stable regardless of complexity |

## Persistence

Four kinds of state, four stores, matched to requirement rather than to product. See ADR-0006.

```
  data/          git-tracked TOML/JSON — materials, process constraints, model definitions
                 schema-validated on load; every value carries a citation
  ledger.db      SQLite — append-only run and provenance records, written by code only
  artifacts/     content-addressed files — VDB fields, meshes, exports; hash-referenced
  wiki/          LLM Wiki pattern — engineering knowledge, rationale, ingested literature
```

**The grounding rule.** No number that enters a model run comes from `wiki/`. Values come from `data/` with a citation; the wiki may link to and explain that data but is never read for a value. The wiki reads the ledger; it never writes to it.

## Failure modes at scale

Three ways this design breaks at 100× the size we're imagining:

1. ⟨…⟩
2. ⟨…⟩
3. ⟨…⟩
