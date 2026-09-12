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
- [x] Handoff: `handoff --run <id>` stages STL + sidecar to a slicing workspace (hash-named), verifies studio-core answers, optionally proposes `print_start` via `/api/propose` (propose-only; human approves in the dashboard) and records the confirmation id; every handoff is a ledger row. ~~Live print pending: studio-core running + slicing (AdvancedStudio has no upload/slicer — surveyed 2026-08-15)~~ → upload landed 2026-08-16 (studio ADR-0002, `handoff --upload`), the slicing gap is filled by **OBCSlicer** up to the propose boundary (first physical two-material print 2026-08-23, human-approved, hash-chained — see `wiki/entities/obcslicer.md`) (2026-08-15)

## Next

Only after Now runs end to end for someone other than me.

- [x] Mesh→SDF import boundary (scan-to-fit): `run-cradle` — units declared never inferred (AUTO refused), raw scan content-addressed and hash-chained into the sidecar, foam-insert cradle model (`scan-cradle/0.1`) with floor `min(wall/2, clearance)`; v0 requires watertight meshes, stated loudly (2026-08-15). **Units are not scale (ADR-0019, 2026-09-11):** a mesh now declares its origin (`cad-export` | `metrology-scan` | `photogrammetry`), and photogrammetry — scale-free by construction — must name the reference that gave it an absolute size or the import refuses. `compare --scan` declares it too, because that is the path where an unscaled mesh would have become a slicer setting.
- [x] MCP surface for ODC itself: stdio server (`opendesigncore-mcp`), 7 tools — reads and deterministic runs execute, `handoff_to_studio` proposes only and no approval tool exists (ADR-0009); resource guards refuse pathological voxel sizes, volumes, and path escapes (2026-08-15)
- [x] Registry contract: parts are read from OpenPartsCore, `data/parts/` deleted, envelope citation + registry commit + file hash in the sidecar (ADR-0016, OpenPartsCore ADR-0006). First board enclosure from a registry envelope nobody typed: run 40, FireBeetle 2 ESP32-S3 (2026-09-06)

- [x] Platform walkthrough: `examples/platform-walkthrough` runs all four peers in one chain — inventory → electronics → board → enclosure → staged; every gate live (2026-08-15)
- [x] Board→enclosure co-design proven end to end: `kicad-cli pcb export stl` → ODC mesh boundary → cradle fitted to real board geometry (run 3, 2026-08-15)
- [x] Scan-compare: `compare` measures per-axis deviation between a design and a scan of the printed part, distinguishes uniform from anisotropic shrinkage, and judges significance against a **declared** scanner accuracy. ~~Validated against synthetic prints only~~ → first real print measured 2026-08-24 (comparison `82d9050e8676`): X and Y dead on nominal, the loop correctly refused to compensate (2026-08-15)
- [x] **`compare` per-axis observed spread** — the flaw the first real measurement exposed, closed the same day (ADR-0015, 2026-08-24). `--measured` takes comma-separated repeated readings per dimension; each axis's uncertainty is `max(declared accuracy, observed spread)`, recorded in `odc/comparison/0.3` and re-read by `compensate`. Run against the real Z case, the tool refuses the figure by itself.
- [x] **Independent verification of a sidecar's numbers**: `verify-artifact` (ADR-0017) — Blender's mesh kernel agrees with PicoGK on run 50 to the hundredth on extents and 0.12 % on volume; recorded as verification 52 (2026-09-07). On the MCP surface as `verify_artifact(runId)` with the tolerances pinned by the operator, never by the caller (ADR-0018, 2026-09-07)
- [x] **Z axis measured — closed 2026-08-25, and this line sat stale for 17 days.** The profile was tuned first, exactly as `CALIBRATE-FIRST.md` said it would have to be: on the 2026-08-24 print the final-layer top face spread 0.08 mm and no honest figure existed; on the reprint it spread **0.02 mm, at instrument accuracy**. Z span read 21.02 against 21.00 nominal (+0.02 mm, +0.095 %) with all three readings per surface entered under ADR-0015's comma syntax. Recorded as residual 0 with its floor stated — uncertainty on a span is `max(instrument, both faces' spreads)` = 0.04 mm, which on 21 mm is 0.19 %, so the true residual is below the instrument rather than absent. Comparison `sha256:cc639d8a24ae`; OpenBuildCore's K2 record now carries **all three axes verified**, not `partial`. First-layer offset came out separately at ~0.26 mm and was deliberately not folded in — it is a slicer setting, not a scale error.

## Not yet

Good ideas that are not this quarter's problem. Adding to this list is a valid outcome of a discussion.

- BINGO integration: ODC provenance records referenced from fabrication evidence (contract drafted, see `wiki/concepts/bingo-odc-provenance-contract.md`)
- ~~Feeding a measured compensation back into slicer profiles automatically~~ → done 2026-08-16 (ADR-0011): `compensate` judges whether a comparison justifies one and proposes it to AdvancedStudio's profile store, which computes the setting. The refusal side is now validated on a real print (2026-08-24: in-plane deviation was genuinely zero and the loop said so); a *non-zero* compensation making the next print measurably better is still unvalidated.
- ~~Cross-machine golden fixtures — rerun byte-identity is proven on one machine only~~ → **first cross-machine evidence, 2026-08-21.** The CI determinism step and a local run produced the *same* enclosure STL hash, `7b1c8fb9dcdb7436b2d6893960e66906477ea5e34dc43e3f4c71e2786b1aa02b`, on two different machines. Provenance hashes differ, correctly — the sidecar records the commit. **What this does not yet show:** both machines are Windows x64 on the same pinned stack, and it is one model at one voxel size. macOS arm64 is untested and is where a difference would most plausibly appear. A pinned golden fixture is now worth writing; it was not before, because there was nothing to compare against.

Shipped since this list was written, and now peers rather than plans:
electronics (OpenCircuitCore), parts registry (OpenPartsCore), inventory and
ideation (OpenBuildCore), mechanism modelling (ClawBot, the fifth peer domain —
ADR-0014), and slicing (OBCSlicer, field spec → multi-material G-code →
propose-only handoff).

## Open questions

- **Registry contract with Oh-Ben-Claw** — OpenPartsCore ingests OBC's registry, and this engine now reads envelopes from OpenPartsCore (ADR-0016); OBC consuming the generated Rust binding back is the unfinished half. Two boards carry an envelope; each further one costs a drawing.
- **Live sourcing** — the BOM and shopping list name parts by id; pricing and stock need distributor APIs and credentials.

## Not ever

Explicit non-goals. Cheaper to write down once than to relitigate.

- Owning a scan-capture pipeline (photogrammetry/LiDAR stacks) — ODC accepts meshes at a validated import boundary
- Electronics design, component sourcing, marketplace, settlement — peer systems in the ecosystem (ADR-0007)
- Geometry algorithms that belong upstream in PicoGK/ShapeKernel
- Designs for weapons or items regulated in the user's locality — policy gating lives at the platform layer, but this repo will not carry such models
