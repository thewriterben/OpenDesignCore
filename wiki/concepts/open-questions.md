---
title: Open questions
type: concept
updated: 2026-08-15
sources: []
---

# Open questions

## Resolved 2026-08-15 (Benji) — first round

- **ODC scope** → engine among peers (ADR-0007).
- **Sources layer** → scattered research docs in the working folders are the raw sources; ingestion queue below.
- **First slice** → thin thread (ROADMAP "Now").
- **ADR-0006** → accepted.

## Resolved 2026-08-15 (Benji) — second round (see [[platform-decisions]])

- **Electronics kernel** → atopile + KiCad (PD-1).
- **Registry** → new schema-first parts registry with codegen'd bindings; inventory separate (PD-2).
- **Repo homes** → two new repos: parts registry, electronics engine (PD-3).
- **Licence** → ADR-0005 accepted: ODC relicensed Apache-2.0; new repos Apache-2.0; ecosystem stays MIT; ClawCam LICENSE added (PD-4).
- **Legality gating** → two-tier; BINGO owns the policy schema (PD-5).
- **Provenance contract** → drafted as [[bingo-odc-provenance-contract]] (PD-6).

## Open

1. ~~Names for the two new repos~~ → **OpenPartsCore** and **OpenCircuitCore**, scaffolded and git-initialised 2026-08-15 (Benji).
2. ~~Registry codegen tool~~ → hand-rolled stdlib emitters with golden-fixture parity (OpenPartsCore ADR-0003, 2026-08-15).
3. ~~BINGO hash algorithm~~ → SHA-256 hex confirmed from ASSET-GRAPH v0.1; contract updated to v0.1 ([[bingo-odc-provenance-contract]]); EXTERNAL-ANCHOR confirmed orthogonal (ordering, not identity).
4. ~~Refusal-category taxonomy v0~~ → drafted as ProjectBINGO/v3/specs/REFUSAL-CATEGORIES.md (DRAFT, uncommitted, awaiting Benji's review). Open remainder: per-jurisdiction mappings (all TODO(source)), category-list hash into JOB_ACCEPTED.
5. ~~Ingestion queue~~ → all 8 queued sources ingested to wiki/sources/ (2026-08-15). Standing conflict recorded: pre-2026 PDFs' Web3-as-core framing superseded by LANDSCAPE-2026.
6. **Thin-thread build/test commands** — CLAUDE.md "Verify with" block still has placeholders; filled when the solution skeleton lands. Plan proposed 2026-08-15, awaiting go-ahead.
7. **REFUSAL-CATEGORIES.md review** — Benji to review/commit the draft spec in ProjectBINGO.
8. **ASSET-GRAPH v0.2** — formalize `design_provenance` and `policy_categories` as optional manifest fields (currently extensions).
9. **AdvancedStudio upstream gaps** (surveyed 2026-08-15, needed for a full closed loop): (a) no file upload and no slicer — studio manages pre-sliced G-code only; an upload or headless-OrcaSlicer endpoint would close the STL→print gap; (b) approvals queue is in-memory with 300 s TTL — no persistence; (c) no jobs/history store and no metadata/provenance field on proposals — nowhere to attach an ODC artifact sha256; (d) no auth on :8770 while /api/action executes directly with confirm:true — worth hardening before anything reaches beyond localhost.
