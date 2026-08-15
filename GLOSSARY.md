# Glossary

Terms, units, and sign conventions. **This is the most load-bearing file in the repo** — it's what keeps humans and agents from silently disagreeing about what a number means.

Rules:
- SI internally, always. Non-SI appears only at UI and import/export boundaries, and is converted at exactly one place.
- Every entry states its unit and, where it can be signed, its sign convention.
- If a term is used in code, it is defined here with the same spelling.

## Conventions

| | |
|---|---|
| Length | m |
| Mass | kg |
| Time | s |
| Angle | rad (degrees only at UI/export boundaries) |
| Coordinate frame | ⟨right-handed, Z up⟩ |
| Rotation | ⟨right-hand rule, positive counterclockwise about the axis⟩ |
| Stress sign | ⟨tension positive⟩ |
| Mesh winding | ⟨counterclockwise seen from outside; normals point outward⟩ |

## Terms

### ⟨term⟩
⟨Definition.⟩ Unit: ⟨…⟩. Sign: ⟨…⟩. Not to be confused with ⟨the neighbouring term people conflate it with⟩.

### tolerance
The explicit distance below which two geometric entities are treated as coincident. Unit: m. Always passed in; never defaulted at a call site. Where a tolerance is relaxed during an operation, the relaxed value appears in the returned result.
