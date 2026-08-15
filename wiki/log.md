# Log

## [2026-08-15] init | Wiki instantiated per llm-wiki.md and ADR-0006 (proposed)
Seeded schema (CLAUDE.md), index, 8 entity pages, 3 concept pages from an ecosystem survey (Oh-Ben-Claw, OBC-Prime, OBC-deployment-generator, ClawCam, ProjectBINGO, Accelerapp, AdvancedStudio, OpenDesignCore). Trigger: Benji's Computational Engineering system vision (scan-to-fit, electronics/PCB/BOM/sourcing, inventory-driven building). No sources/ or deep-research/ directories found in any repo; flagged as open question #2.

## [2026-08-15] decisions | Scope, sources, first slice, ADR-0006
Benji resolved four questions: ODC = engine among peers (→ ADR-0007 written), scattered research docs are the raw-source layer, thin thread is the first build (→ ROADMAP "Now" filled), ADR-0006 accepted. DECISIONS.md, ROADMAP.md, CHANGELOG.md updated in the repo; open-questions page rewritten.

## [2026-08-15] decisions | Second round: all six open questions resolved
PD-1..PD-6 recorded in [[platform-decisions]]: atopile+KiCad electronics kernel; schema-first parts registry with codegen bindings (inventory separate); two new repos; Apache-2.0 for ODC + new repos (ADR-0005 accepted, ODC relicensed, ClawCam got its missing MIT LICENSE); two-tier legality gating with BINGO owning policy data; BINGO↔ODC provenance contract drafted as [[bingo-odc-provenance-contract]]. open-questions rewritten with the new (smaller, concrete) list.

## [2026-08-15] scaffold | OpenPartsCore + OpenCircuitCore created
Named by Benji (Open*Core family). Both scaffolded at F:\Documents\GitHub\, Apache-2.0, docs-first per house style, git-initialised with root commits. OpenPartsCore ships schema v0, boards/esp32-s3 (cited to OBC registry schema_version 1), and a stdlib validator (positive + negative tested). OpenCircuitCore ships ADRs (atopile+KiCad, GPL containment), pipeline architecture, and roadmap to an ESP32-S3+I2C reference board. Entity pages added; open question #1 closed, #2 narrowed to the codegen ADR.

## [2026-08-15] ingest | Eight sources filed; three more open questions closed
Read BINGO v3 specs directly: SHA-256 hex confirmed, EXTERNAL-ANCHOR is a transparency log (ordering) not a provenance vehicle — contract corrected to v0.1 with the sidecar-as-manifest-file design. Drafted REFUSAL-CATEGORIES.md in ProjectBINGO/v3/specs (uncommitted). OpenPartsCore ADR-0003: hand-rolled emitters, golden-fixture parity. Ingested all 8 queued sources into wiki/sources/ with conflict flags (PDFs vs LANDSCAPE-2026: blockchain necessity, 3DOS credibility, royalty enforceability, Xometry health). Key cross-links: hardware-scout VID/PID shared-ID convention affects OpenPartsCore schema; AdvancedStudio shrinkage/fit numbers must reach data/ with citations before models read them.

## [2026-08-15] build | Thin-thread skeleton green
.NET SDK 8 installed via winget (SDK 10 was already present); PicoGK's installer era is over — NuGet package since 1.7.7.5, latest 2.3.0, pinned [2.2.0] (ADR-0008 supersedes ADR-0001's consumption mechanics). Two facts learned by building: PicoGK 2.2.0 targets net9.0 (docs' ".NET 8" was 1.x-era), and ShapeKernel v2.1.0 carries self-deprecation warnings — compiled in a non-strict wrapper project so OpenDesignCore's own code stays warnings-as-errors. Solution builds 0/0, runs, dotnet format clean. ShapeKernel submodule at external/LEAP71_ShapeKernel @ 313d676. Next roadmap items: data/ first cited entries, model-run type + ledger.

## [2026-08-15] publish | OpenPartsCore + OpenCircuitCore public on GitHub
gh authenticated (device flow). ODC PR #1 opened (platform-foundations: 3 commits, ADRs 0005-0008, wiki, thin-thread skeleton). Both new repos published public under thewriterben/ and pushed. Entity pages updated with URLs.

## [2026-08-15] build | data/ layer landed (PR #2 branch)
PR #1 merged by Benji. data/ store created per ADR-0006: parts/esp32-s3-wroom-1 (18.0x25.5x3.1 mm, Espressif datasheet Table 1-1/§10.1 — DevKitC board dims are unpublished, so the thin thread designs around the WROOM-1 module) and materials/pla-generic (secondary-sourced from AdvancedStudio Research-Report, TODO(source) for vendor TDS). Strict loader: citations mandatory, unknown fields rejected, dims must be positive. validate-data CLI wired; test project live, 6/6 green; build/test/format all clean.

## [2026-08-15] build | Thin thread runs end to end (PR #3 branch)
The ROADMAP "Now" core is real: run 1 PASS — 0.2 mm voxels, enclosure-shell/0.1 around parts/esp32-s3-wroom-1, validated, binary STL content-addressed, deterministic provenance sidecar (canonical JSON byte-verified against BINGO's Python kernel via embedded test vectors), ledger row appended. Headless PicoGK confirmed working via scoped Library instances (no viewer, no global registration — raw PicoGK with explicit lib everywhere; ShapeKernel not needed for this model). Two things learned: Microsoft.Data.Sqlite pooling holds the db file after Dispose (fixed with Pooling=False); PicoGK's Library.Dispose prints to console (cosmetic). Remaining "Now": AdvancedStudio MCP handoff. Floats are banned from canonical JSON — measured quantities are strings with units in the key, mirroring BINGO's integers-and-strings discipline.

## [2026-08-15] build | Fabrication handoff landed; thin thread "Now" complete
StudioHandoff: stage (hash-named STL + sidecar into a slicing workspace) -> verify (GET /api/state, fail loudly; --offline records staged-offline) -> propose (POST /api/propose print_start, confirmation id recorded; approval stays human). handoffs table added to the ledger. Survey truth honored: studio-core has no upload, no slicer, in-memory approvals, no provenance field - gaps filed as open question #9. Run 1's enclosure staged for real at F:\Documents\3DP\slicing-inbox (staged-offline; studio was not running). 20/20 tests. Every ROADMAP "Now" item is now checked.
