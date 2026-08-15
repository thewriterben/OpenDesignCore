# Architecture

> Written in session one. Until then this file is a set of questions, not answers.

## The end-to-end path

The smallest useful thing OpenDesignCore does, start to finish:

⟨input⟩ → ⟨transformation⟩ → ⟨output artifact⟩

Everything else is an elaboration of that path. If a proposed module doesn't serve it, it goes in ROADMAP.md instead of the codebase.

## Domain objects

| Object | Guarantees | Owned by |
|---|---|---|
| ⟨e.g. Geometry⟩ | ⟨invariant it never violates⟩ | ⟨module⟩ |

## Modules

### ⟨module⟩

- **Responsibility:** ⟨one sentence⟩
- **Depends on:** ⟨…⟩
- **Accepts:** ⟨types crossing the boundary in⟩
- **Returns:** ⟨types crossing out⟩
- **Does not:** ⟨the thing people will keep trying to make it do⟩

## Geometry representation

⟨B-rep / mesh / implicit / hybrid⟩, because ⟨…⟩.

Costs of this choice: ⟨what gets hard later⟩. Recorded as an ADR in DECISIONS.md.

## Determinism

Sources of nondeterminism and how each is pinned:

| Source | Handling |
|---|---|
| Random seeds | Explicit, threaded through config |
| Parallel reduction order | ⟨deterministic reduction / single-threaded solver core⟩ |
| Hash iteration | Ordered maps in any path affecting output |
| Floating point | ⟨fixed op order; no fast-math⟩ |

## Failure modes at scale

Three ways this design breaks at 100× the size we're imagining:

1. ⟨…⟩
2. ⟨…⟩
3. ⟨…⟩
