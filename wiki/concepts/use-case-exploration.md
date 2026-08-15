---
title: Use-case exploration — the Computational Engineering system
type: concept
updated: 2026-08-15
sources:
  - conversation 2026-08-15 (Benji's feature statement)
  - ecosystem survey (see [[ecosystem-map]])
---

# Use-case exploration

The ask: a computational engineering and design system spanning the OBC ecosystem and [[project-bingo]], letting users design and create electronic devices, tools, machines, toys, custom components, and repairs. Named pillars: **3D scanning for fit**, **electronics/PCB/BOM/sourcing**, **inventory-driven building and ideation**.

## Personas

- **Fixer** — something broke; wants a replacement part or repair collar, fast, with minimal design skill.
- **Maker** — has a drawer of dev boards and sensors; wants ideas and guided builds.
- **Inventor** — has a product idea; needs enclosure + PCB + firmware co-designed to prototype.
- **Small-batch manufacturer** — has designs; needs DFM, BOMs, sourcing, and fabrication (local or via BINGO network).
- **Educator/parent** — wants safe, cheap, buildable projects from available parts.
- **Fleet deployer** — the existing OBC persona; deploys sensing/agent hardware at sites.

## Use-case domains

### 1. Scan-to-design (reverse engineering & fit)
- Scan an object (phone photogrammetry, LiDAR, structured light, cheap turntable scanner) → mesh → cleaned, scaled, design-ready geometry.
- **Fit around**: custom enclosure/case/holder for an existing object (tool grip, camera housing, battery cradle).
- **Fit into**: design a part constrained by a cavity (replacement knob, bracket in a tight space).
- **Repair**: scan broken part → reconstruct missing geometry → print; or design a splint/collar around the break.
- **Fit with**: import component 3D models (boards, connectors, displays from the registry) and pack them inside an enclosure with clearances, bosses, standoffs, cable routing.
- Mesh → SDF is a natural fit for PicoGK: booleans against scanned geometry are exactly what voxel kernels are robust at. Offset-shell an enclosure directly from a scan.
- Tolerance/fit calibration per printer+material (ties to [[advancedstudio]] calibration data).

### 2. Electronics: circuit → PCB → BOM → sourcing
- Requirements-driven circuit design: "battery-powered ESP32-S3 with this sensor set" → reference schematic from proven blocks (registry already knows I2C addresses and connector types).
- Schematic capture and ERC; SPICE simulation for the analog corners.
- PCB layout (KiCad is the obvious scriptable open kernel — the electronics analogue of ADR-0001), DRC against the chosen fab's rules, panelization.
- BOM generation with substitutions; live sourcing via distributor APIs (Octopart/Digi-Key/Mouser/LCSC); price-break optimization; stock-aware part swaps.
- "Design from my bin": prefer parts the user already owns (see domain 4); BOM diff = shopping list.
- Assembly outputs: interactive BOM, pick-and-place files, hand-solder guides.
- Handoff: order boards, or route through BINGO Tier 3 nodes (PCB assembly is already in its capability taxonomy).

### 3. Multi-domain co-design (the differentiator)
- Board outline, mounting holes, connector positions ↔ enclosure cutouts, bosses, wall thickness — one constraint set, not two tools exporting STEP at each other.
- Thermal: enclosure venting driven by component dissipation; antenna keep-outs driven by RF parts.
- Mechanical: motor mounts, gear trains, linkages parameterized by the actual motors in inventory.
- Firmware: [[accelerapp]] already generates firmware+UI from a device spec; the same spec should drive the PCB netlist and the enclosure. **One device spec, four artifacts** (board, enclosure, firmware, docs).

### 4. Inventory-driven building & ideation
- Personal inventory: extend the [[oh-ben-claw]] registry pattern to everything a user owns — boards, modules, passives kits, motors, bearings, fasteners, filament, stock material.
- "What can I build?" — planner matches inventory against a project/capability graph (the deployment planner already does exactly this for agent hardware; generalize it).
- "Help me build X" — gap analysis → BOM for missing parts → sourced shopping list → guided build (guide-generator pattern exists).
- Inventory capture assist: photograph the drawer, vision identifies parts (ClawCam/vision competence exists in-ecosystem).
- Community project graph: BINGO asset graph entries double as buildable projects with royalty-bearing remixes.

### 5. Computational mechanical design (ODC's existing core)
- Parametric functional parts: brackets, jigs, fixtures, tools, toys, adapters (the long tail of "a part that fits my exact situation").
- Lattices/infill engineering, mass/stiffness targets (ShapeKernel + LatticeLibrary).
- Validation gates before export: manifold, wall thickness vs. nozzle, overhang/DFM checks per process.
- Everything carries provenance (ADR-0003): inputs, voxel size, versions, commit.

### 6. Fabrication & verification
- Local: slice with tuned profiles and print via [[advancedstudio]] (propose-only writes keep the human in the loop).
- Network: publish to [[project-bingo]] — DFM, quote, match to nodes, proof-of-fabrication, royalties settled atomically.
- Verification loop: scan the printed part → compare against design SDF → dimensional report → feed compensation back into profiles. (Scan-compare closes a loop nobody in the ecosystem has closed.)
- Digital twin + HIL for electronic devices (Accelerapp).

### 7. Knowledge & provenance (substrate)
- This wiki: materials data rationale, process knowledge, calibration history, datasheet digests, design rationale.
- `data/`: cited material/process constants that models actually read (grounding rule).
- Ledger: every run, every artifact, every fabrication traceable.

## Cross-cutting requirements implied

- **Units and determinism discipline** (ODC invariants) must extend to electronics (footprint dims in mm, courtyard clearances explicit) and scanning (scale calibration is a provenance field).
- **Safety/legality gating**: "possible and not illegal per locality" needs a policy layer — refuse weapons/regulated items; BINGO nodes need jurisdiction-aware acceptance (its arbitration/acceptance specs are the hook).
- **Never invent physical data**: component parameters come from datasheets/distributor APIs with citations, not LLM recall.

## Sequencing sketch (opinion, for discussion)

1. **Thin thread first** (proves the seams): registry component → enclosure generated around it → validated STL + provenance → printed via AdvancedStudio. Touches ODC core path, registry, fabrication. No scanning, no PCB yet.
2. **Scan-to-fit** second: highest value-per-effort for Fixer/Maker; mesh→SDF is native to PicoGK.
3. **Electronics** third: KiCad-backed, registry-fed; BOM/sourcing as its first deliverable (useful before layout automation matures).
4. **Inventory/ideation** in parallel (mostly exists — generalize the deployment planner).
5. **BINGO integration** when artifacts + provenance are stable enough to be marketplace goods.

See [[open-questions]] for what needs Benji's decision before any of this becomes ARCHITECTURE.md content.
