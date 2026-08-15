# Roadmap

The purpose of this file is to give scope questions an answer other than "sure, let's add it."

## Now

The single end-to-end path from ARCHITECTURE.md, working for real — as the **thin thread** (ADR-0007): one component from the Oh-Ben-Claw registry → an enclosure generated around its dimensions → validated mesh + provenance record → printed via AdvancedStudio's MCP surface.

- [x] Solution skeleton: net9.0 solution, PicoGK `[2.2.0]` from NuGet + ShapeKernel submodule at `ShapeKernel-v2.1.0` (ADR-0008), builds and runs clean, verify commands in CLAUDE.md (2026-08-15)
- [x] `data/`: first cited entries — ESP32-S3-WROOM-1 envelope (Espressif datasheet) + generic PLA process constraints (secondary-sourced, TODO(source) for vendor TDS); strict loader rejects uncited values, unknown fields, non-positive dims; `validate-data` CLI; 6 tests (2026-08-15)
- [ ] Model run type: inputs + voxel size in, artifact + provenance out; ledger.db append
- [ ] Enclosure model: parametric shell around a board envelope (walls, standoffs, port cutouts), resolution floor declared
- [ ] Validation gate: manifold/watertight, min wall vs. nozzle, before any export
- [ ] Export STL/3MF with provenance sidecar; content-addressed into `artifacts/`
- [ ] Reference test: golden fixture, byte-identical at pinned voxel size
- [ ] Handoff: submit the artifact to AdvancedStudio via MCP (propose-only) and record the print against the run

## Next

Only after Now runs end to end for someone other than me.

- [ ] Mesh→SDF import boundary (scan-to-fit): validation, scale as a provenance field — capture pipelines stay out of scope (ADR-0007)
- [ ] MCP surface for ODC itself, so peers (planner, BINGO orchestration) can invoke model runs
- [ ] Registry schema contract with Oh-Ben-Claw (consume, don't fork; drift is upstream's documented problem to fix once)

## Not yet

Good ideas that are not this quarter's problem. Adding to this list is a valid outcome of a discussion.

- Electronics engine (schematic/PCB/BOM/sourcing) — peer system, kernel choice needs its own ADR (KiCad presumptive); see `wiki/concepts/use-case-exploration.md` §2
- Inventory-driven ideation — generalization of Oh-Ben-Claw's deployment planner, lives with the planner
- BINGO integration: ODC provenance records referenced from fabrication evidence
- Scan-compare verification loop (print → scan → dimensional report → profile compensation)

## Open questions

- **Electronics kernel ADR** — KiCad vs. code-as-schematic (skidl/atopile-style); owned by the electronics peer, tracked here because ODC co-design contracts depend on it.
- **Licence** — ADR-0005 still proposed.

## Not ever

Explicit non-goals. Cheaper to write down once than to relitigate.

- Owning a scan-capture pipeline (photogrammetry/LiDAR stacks) — ODC accepts meshes at a validated import boundary
- Electronics design, component sourcing, marketplace, settlement — peer systems in the ecosystem (ADR-0007)
- Geometry algorithms that belong upstream in PicoGK/ShapeKernel
- Designs for weapons or items regulated in the user's locality — policy gating lives at the platform layer, but this repo will not carry such models
