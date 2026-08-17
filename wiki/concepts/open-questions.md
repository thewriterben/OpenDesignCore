---
title: Open questions
type: concept
updated: 2026-08-16
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
7. ~~REFUSAL-CATEGORIES.md review~~ → reviewed and merged by Benji, 2026-08-15. Remaining: per-jurisdiction mappings (all TODO(source)), and freezing the category-list hash into JOB_ACCEPTED alongside the acceptance checklist hash.
8. **ASSET-GRAPH v0.2** — formalize `design_provenance` and `policy_categories` as optional manifest fields (currently extensions).
9. ~~studio-mcp broken against MCP SDK 2.0~~ → **fixed 2026-08-15**: ported to `MCPServer` (two lines; decorator and run() surfaces unchanged), requirements pinned `mcp>=2.0,<3`, and the proxy now carries `X-Studio-Token` on writes. All 12 tools register; verified Connected against a real client. Original note follows.

~~**studio-mcp is broken against MCP SDK 2.0** (found 2026-08-15 while building OpenBuildCore's surface). It imports `mcp.server.fastmcp`, which no longer exists — `MCPServer` replaced `FastMCP` in 2.x — and its requirement `mcp>=1.2` has no upper bound, so a fresh install gets 2.0.0 and the server will not start. Confirmed by running the import on this machine. Fix is either pinning `mcp<2` or porting to `MCPServer`; the latter is a small change and the ODC/OpenBuildCore servers are worked examples.~~
11. ~~**K2 Plus build volume is unknown to the system**~~ → **closed 2026-08-16**: 350 × 350 × 350 mm, from Creality Print 7.2's own machine profile (`resources/profiles/Creality/machine/Creality K2 Plus 0.4 nozzle.json`, `printable_area` + `printable_height`), read from the installation on this computer. The placeholder held for a day and was replaced by a source rather than a recollection — the convention working as intended. **Still unsourced on that machine: throughput**, so every time question there still answers "requires slicing". A sourced envelope does not imply a sourced rate.
12. **AdvancedStudio upstream gaps** (surveyed 2026-08-15, needed for a full closed loop): (a) no file upload and no slicer — studio manages pre-sliced G-code only; an upload or headless-OrcaSlicer endpoint would close the STL→print gap; (b) approvals queue is in-memory with 300 s TTL — no persistence; (c) no jobs/history store and no metadata/provenance field on proposals — nowhere to attach an ODC artifact sha256; ~~(d) no auth on :8770~~ → **addressed 2026-08-15**: studio-core now binds 127.0.0.1 by default (the exposure came from studio.example.toml, since no studio.toml exists), plus optional shared-token auth on the four state-changing endpoints and a loud startup warning when exposed without one. 11 tests. ~~Not committed — AdvancedStudio is not a git repository.~~ → git-initialised and committed 2026-08-15.
13. ~~**No slicer is installed on this machine**~~ → **wrong, corrected 2026-08-16 by Benji**. **Creality Print 7.2 is installed** at `C:\Program Files\Creality\Creality Print 7.2`. My check grepped Program Files for `slic|cura|prusa`, which matches nothing in "Creality Print" — a filter that only finds what it already expects to find. Recorded because the failure mode is more useful than the fact.

    **What it changes.** Gap 12(a) is a **wiring** problem, not a dependency decision. Creality Print is Bambu Studio lineage (`OrcaArena` appears in its vendor profile list) and the CLI option table is present in `CrealityPrint_Slicer.dll` — `--load-settings`, `--load-filaments`, `--slice`, `--outputdir`, `--plate-to-slice`, `--custom-gcode`, `--allow-rotations`, `--skip-objects`, plus the strings `cli mode, Current CrealityPrint Version %1%` and `no action, start gui directly`. Confirmed by reading the binary, not by assuming the lineage.

    **What is not yet known.** A naive `--slice 0 --outputdir X model.stl` returned in 0 s and produced nothing: `CrealityPrint.exe` is a 151 KB launcher stub, and the CLI needs `--load-settings`. The Creality profiles use `inherits` with no resolved `machine_full/` directory (only BBL has one), and `cli_config.json` exists only under `profiles/BBL/`. So driving it headlessly is real work with genuine unknowns, not a quick win — its own planned change.
14. **The compensation loop is plumbed but the number is unvalidated** (2026-08-16). ODC ADR-0011 and AdvancedStudio ADR-0001 connect a scan-measured deviation to a slicer profile value, with the origin recorded. Every refusal path is proven and the wire is proven against a running studio, but whether the resulting percentage makes the next print *better* needs a real print and a real scan — the same blocker as validating `compare` itself. Gap 12(c) narrowed but is not closed: a *profile* value can now carry its origin; a *print* proposal still cannot.
