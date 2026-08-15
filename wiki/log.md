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
