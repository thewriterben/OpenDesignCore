# Glossary

Terms, units, and sign conventions. **This is the most load-bearing file in the repo** — it's what keeps humans and agents from silently disagreeing about what a number means.

Rules:
- Length is millimetres, matching PicoGK (ADR-0004). Other quantities are SI unless dimensionally coupled to length, in which case the entry states its unit explicitly rather than leaving it to be inferred.
- Non-mm units appear only at UI and import/export boundaries, and are converted at exactly one place per boundary.
- Every entry states its unit and, where it can be signed, its sign convention.
- If a term is used in code, it is defined here with the same spelling.

## Conventions

| | |
|---|---|
| Length | mm |
| Mass | kg |
| Time | s |
| Angle | rad (degrees only at UI/export boundaries) |
| Density | ⟨state explicitly — kg/mm³ and kg/m³ differ by 10⁹⟩ |
| Stress | ⟨state explicitly — MPa is N/mm², which is convenient here⟩ |
| Coordinate frame | ⟨right-handed, Z up⟩ |
| Rotation | ⟨right-hand rule, positive counterclockwise about the axis⟩ |
| Stress sign | ⟨tension positive⟩ |
| Mesh winding | ⟨counterclockwise seen from outside; normals point outward⟩ |

## Terms

### voxel size
The edge length of one voxel in the field, in mm. Set once per run at library initialisation and global to everything downstream. An explicit input to every model run; never defaulted in code; recorded in provenance. Detail smaller than one voxel cannot be represented — as a rule of thumb the smallest feature you care about should be at least one voxel across.

### resolution floor
The largest voxel size at which a given model's result is still meaningful. Declared by the model. Running below it is an error, not a coarse answer.

### provenance
The record that lets a given artifact be reproduced: inputs, voxel size, pinned submodule versions, tool version, commit. An artifact without one is not a result.

### cradle slot
A vertical groove in a fixture that a board edge slides into, sized to the board thickness plus a clearance per side. Holds by friction; carries the board's weight on its blind lower end. Not to be confused with a *cavity*, which surrounds a part rather than gripping one edge of it.

### grip depth
How far a cradle slot reaches inboard of a board's edge, in mm, per side. Sets how much of the board is held and — since the two slots face each other — how wide the gap between them is. Too deep and whatever passes between the uprights, such as a lens barrel, fouls them.

### seat lift
The height in mm from a fixture's base top to the floor of its cradle slot, i.e. how far a seated board is held clear of the base. Exists so connectors and castellations along a board's lower edge do not bear on anything.

### ⟨term⟩
⟨Definition.⟩ Unit: ⟨…⟩. Sign: ⟨…⟩. Not to be confused with ⟨the neighbouring term people conflate it with⟩.
