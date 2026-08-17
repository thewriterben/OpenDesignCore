# Roadmap

The purpose of this file is to give scope questions an answer other than "sure, let's add it."

## Now

The single end-to-end path from ARCHITECTURE.md, working for real — as the **thin thread** (ADR-0007): one component from the Oh-Ben-Claw registry → an enclosure generated around its dimensions → validated mesh + provenance record → printed via AdvancedStudio's MCP surface.

- [x] Solution skeleton: net9.0 solution, PicoGK `[2.2.0]` from NuGet + ShapeKernel submodule at `ShapeKernel-v2.1.0` (ADR-0008), builds and runs clean, verify commands in CLAUDE.md (2026-08-15)
- [x] `data/`: first cited entries — ESP32-S3-WROOM-1 envelope (Espressif datasheet) + generic PLA process constraints (secondary-sourced, TODO(source) for vendor TDS); strict loader rejects uncited values, unknown fields, non-positive dims; `validate-data` CLI; 6 tests (2026-08-15)
- [x] Model run: `EnclosureRun.Execute` — cited data in, validated STL + deterministic provenance sidecar out, run appended to `ledger.db` (2026-08-15)
- [x] Enclosure model v0: open-top tray around a part envelope (floor + walls, clearance), resolution floor declared and enforced — standoffs/port cutouts are v0.2 work (2026-08-15)
- [x] Validation gate: emptiness + bounding-box-vs-expected within 2 voxels, before export; voxel-derived meshes are closed by construction (2026-08-15)
- [x] Export binary STL with canonical-JSON provenance sidecar (SHA-256, byte-compatible with BINGO's `canonical_json`); both content-addressed into `artifacts/` (2026-08-15)
- [x] Reference test: same-inputs rerun produces byte-identical artifact + sidecar hashes; cross-machine golden pinning deferred until a second machine exists (2026-08-15)
- [x] Handoff: `handoff --run <id>` stages STL + sidecar to a slicing workspace (hash-named), verifies studio-core answers, optionally proposes `print_start` via `/api/propose` (propose-only; human approves in the dashboard) and records the confirmation id; every handoff is a ledger row. Live print pending: studio-core running + slicing (AdvancedStudio has no upload/slicer — surveyed 2026-08-15, gaps filed in wiki) (2026-08-15)

## Next

Only after Now runs end to end for someone other than me.

- [x] Mesh→SDF import boundary (scan-to-fit): `run-cradle` — units declared never inferred (AUTO refused), raw scan content-addressed and hash-chained into the sidecar, foam-insert cradle model (`scan-cradle/0.1`) with floor `min(wall/2, clearance)`; v0 requires watertight meshes, stated loudly (2026-08-15)
- [x] MCP surface for ODC itself: stdio server (`opendesigncore-mcp`), 7 tools — reads and deterministic runs execute, `handoff_to_studio` proposes only and no approval tool exists (ADR-0009); resource guards refuse pathological voxel sizes, volumes, and path escapes (2026-08-15)
- [ ] Registry schema contract with Oh-Ben-Claw (consume, don't fork; drift is upstream's documented problem to fix once)

- [x] Platform walkthrough: `examples/platform-walkthrough` runs all four peers in one chain — inventory → electronics → board → enclosure → staged; every gate live (2026-08-15)
- [x] Board→enclosure co-design proven end to end: `kicad-cli pcb export stl` → ODC mesh boundary → cradle fitted to real board geometry (run 3, 2026-08-15)
- [x] Scan-compare: `compare` measures per-axis deviation between a design and a scan of the printed part, distinguishes uniform from anisotropic shrinkage, and judges significance against a **declared** scanner accuracy. Validated against synthetic prints only — a real print and scan is still outstanding (2026-08-15)

## Not yet

Good ideas that are not this quarter's problem. Adding to this list is a valid outcome of a discussion.

- BINGO integration: ODC provenance records referenced from fabrication evidence (contract drafted, see `wiki/concepts/bingo-odc-provenance-contract.md`)
- ~~Feeding a measured compensation back into slicer profiles automatically~~ → done 2026-08-16 (ADR-0011): `compensate` judges whether a comparison justifies one and proposes it to AdvancedStudio's profile store, which computes the setting. Still needs a real print and scan to validate the number rather than the plumbing.
- Cross-machine golden fixtures — rerun byte-identity is proven on one machine only

Shipped since this list was written, and now peers rather than plans:
electronics (OpenCircuitCore), parts registry (OpenPartsCore), inventory and
ideation (OpenBuildCore).

## Open questions

- **Registry contract with Oh-Ben-Claw** — OpenPartsCore ingests OBC's registry today; OBC consuming the generated Rust binding back is the unfinished half.
- **Live sourcing** — the BOM and shopping list name parts by id; pricing and stock need distributor APIs and credentials.

## Not ever

Explicit non-goals. Cheaper to write down once than to relitigate.

- Owning a scan-capture pipeline (photogrammetry/LiDAR stacks) — ODC accepts meshes at a validated import boundary
- Electronics design, component sourcing, marketplace, settlement — peer systems in the ecosystem (ADR-0007)
- Geometry algorithms that belong upstream in PicoGK/ShapeKernel
- Designs for weapons or items regulated in the user's locality — policy gating lives at the platform layer, but this repo will not carry such models
