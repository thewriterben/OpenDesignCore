---
title: Open questions
type: concept
updated: 2026-09-11
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
6. ~~**Thin-thread build/test commands** — CLAUDE.md "Verify with" block still has placeholders~~ → **closed 2026-09-11 as stale; it had been false for 27 days.** CLAUDE.md carries the real commands (`dotnet build/test/format` against `OpenDesignCore.sln`, `--exclude external`), and has since the solution skeleton landed on 2026-08-15 — the same day this question was written, hours later. Nothing was wrong with the repo; the *question* rotted, and every reader since has been told to check a placeholder that is not there. Found by a lint pass, which is what the lint operation is for. Cheap to fix, worth noting: an open-questions list is a claim about the present, and a stale entry in it is the same failure class as a stale README claim — it is just less visible because nobody diffs a question against reality.
7. ~~REFUSAL-CATEGORIES.md review~~ → reviewed and merged by Benji, 2026-08-15. Remaining: per-jurisdiction mappings (all TODO(source)), and freezing the category-list hash into JOB_ACCEPTED alongside the acceptance checklist hash.
8. **ASSET-GRAPH v0.2** — formalize `design_provenance` and `policy_categories` as optional manifest fields (currently extensions).
9. ~~studio-mcp broken against MCP SDK 2.0~~ → **fixed 2026-08-15**: ported to `MCPServer` (two lines; decorator and run() surfaces unchanged), requirements pinned `mcp>=2.0,<3`, and the proxy now carries `X-Studio-Token` on writes. All 12 tools register; verified Connected against a real client. Original note follows.

~~**studio-mcp is broken against MCP SDK 2.0** (found 2026-08-15 while building OpenBuildCore's surface). It imports `mcp.server.fastmcp`, which no longer exists — `MCPServer` replaced `FastMCP` in 2.x — and its requirement `mcp>=1.2` has no upper bound, so a fresh install gets 2.0.0 and the server will not start. Confirmed by running the import on this machine. Fix is either pinning `mcp<2` or porting to `MCPServer`; the latter is a small change and the ODC/OpenBuildCore servers are worked examples.~~
11. ~~**K2 Plus build volume is unknown to the system**~~ → **closed 2026-08-16**: 350 × 350 × 350 mm, from Creality Print 7.2's own machine profile (`resources/profiles/Creality/machine/Creality K2 Plus 0.4 nozzle.json`, `printable_area` + `printable_height`), read from the installation on this computer. The placeholder held for a day and was replaced by a source rather than a recollection — the convention working as intended. **Still unsourced on that machine: throughput**, so every time question there still answers "requires slicing". A sourced envelope does not imply a sourced rate.
12. **AdvancedStudio upstream gaps** (surveyed 2026-08-15): ~~(a) no file upload and no slicer~~ → **upload added 2026-08-16** (studio ADR-0002): guarded `gcode_upload` through `/api/propose`, reading from one bounded staging directory, with `GET /api/staging` listing what is sliced and waiting. **Slicing is deliberately not automated** — see 13 — because the studio does not need to: a human slices, which is where ADR-0009 wants a person anyway. ~~(b) approvals queue is in-memory with 300 s TTL — no persistence; (c) no jobs/history store and no metadata/provenance field on proposals — nowhere to attach an ODC artifact sha256~~ → **both closed 2026-08-16** (studio ADR-0003): an append-only SQLite ledger records every guarded decision, **including rejections**, with `design_artifact_sha256` as a real column. `GET /api/jobs` and `/api/jobs/by-design/<sha256>` answer "this part came off wrong — what was run, when, and did anyone approve it". The pending *queue* stays in memory on purpose: persisting an unanswered question would let an approval be granted for something proposed before a restart, in a state nobody can inspect. Losing pending proposals on restart is the safe failure. ~~(d) no auth on :8770~~ → **addressed 2026-08-15**: studio-core now binds 127.0.0.1 by default (the exposure came from studio.example.toml, since no studio.toml exists), plus optional shared-token auth on the four state-changing endpoints and a loud startup warning when exposed without one. 11 tests. ~~Not committed — AdvancedStudio is not a git repository.~~ → git-initialised and committed 2026-08-15.
13. ~~**No slicer is installed on this machine**~~ → **wrong, corrected 2026-08-16 by Benji**. **Creality Print 7.2 is installed** at `C:\Program Files\Creality\Creality Print 7.2`. My check grepped Program Files for `slic|cura|prusa`, which matches nothing in "Creality Print" — a filter that only finds what it already expects to find. Recorded because the failure mode is more useful than the fact.

    **What it changes.** Gap 12(a) is a **wiring** problem, not a dependency decision. Creality Print is Bambu Studio lineage (`OrcaArena` appears in its vendor profile list) and the CLI option table is present in `CrealityPrint_Slicer.dll` — `--load-settings`, `--load-filaments`, `--slice`, `--outputdir`, `--plate-to-slice`, `--custom-gcode`, `--allow-rotations`, `--skip-objects`, plus the strings `cli mode, Current CrealityPrint Version %1%` and `no action, start gui directly`. Confirmed by reading the binary, not by assuming the lineage.

    **Resolved 2026-08-16: the CLI crashes.** Given valid `--load-settings` / `--load-filaments` / `--slice` arguments (Benji's own user presets and the system machine/process/filament profiles all exist and resolve), `CrealityPrint.exe` exits `-1073741819` = `0xC0000005`, an access violation, producing nothing. With a *bad* path it produces a proper `Slic3r::CLI::run ... can not find setting file` error, so argument parsing works and the crash is later. Two attempts, then stopped.

    **So headless slicing is not on the table**, and it turned out not to matter: the studio does not need to slice. A human slices in the GUI, which is where ADR-0009 wants a person anyway, and the actual missing piece was upload — added as studio ADR-0002. Reopen this only if someone wants unattended batch slicing, and expect to debug a vendor crash.
14. **The compensation loop is plumbed but the number is unvalidated** (2026-08-16). ODC ADR-0011 and AdvancedStudio ADR-0001 connect a scan-measured deviation to a slicer profile value, with the origin recorded. Every refusal path is proven and the wire is proven against a running studio, but whether the resulting percentage makes the next print *better* needs a real print and a real scan — the same blocker as validating `compare` itself. ~~This is now the only thing between the platform and a closed loop~~ → **partially validated 2026-08-24 on a real print** (comparison `82d9050e8676`): with flow calibrated and the correct CFS lane, X and Y measured dead on nominal and the loop correctly refused to compensate — the refusal side works on hardware, not just in tests. Still open: a *non-zero* compensation improving the next print, which needs a part with a real in-plane deviation; and Z, which is unmeasurable until an optimised profile can hold a flat top face (see 15).

15. ~~**`compare` treats declared instrument accuracy as the whole uncertainty**~~ → **closed 2026-08-24, same day it was opened (ADR-0015).** `--measured` now takes comma-separated repeated readings per dimension; each axis's uncertainty is `max(declared accuracy, observed spread)`, the spread and raw readings are recorded in `odc/comparison/0.3`, and `compensate` re-reads them from the stored record so the widened uncertainty survives into the verdict. Run against the real Z case (readings spread 0.09 mm, mean deviation 0.045 mm), the tool refuses the figure by itself. What remains physical rather than tooling: the Z axis still has no number until an optimised profile can hold a flat top face — the tool now says so instead of a human having to; OpenBuildCore's record stays `axis_calibration: partial` until the reprint.

16. ~~**This wiki has no raw layer for web sources**~~ → **closed 2026-09-11 (Benji): archive outside
    `wiki/`.** Mutable, load-bearing pages are saved to `research/raw/` as dated retrievals and cited by
    repo-relative path like every other raw source, which keeps the schema's "raw is immutable and lives
    outside the wiki" rule literally true rather than rewriting it. Sources whose identifiers are
    permanent by design — arXiv, DOIs, git commits and tags — are cited directly and **not** archived,
    because a second copy adds drift without adding a guarantee. Five OpenScan pages archived; the schema
    gained a "Web sources" section; both source pages re-cited against the archives.

    **Two things the doing of it exposed.** (a) An archive is a *retrieval rendering*, not the page — so
    each file says what it is, and where the fetch truncated, it says so **at the point of truncation**.
    Two of the five are partial and are marked as such; a partial archive that reads as complete would be
    worse than none. (b) [[ai-cad-mcp-landscape-2026-09]] could not be archived at all, because most of
    its references were never fetched — they came from web-search summaries. That page now states its own
    standing as orientation-grade: enough to justify a direction, not a value. **Evidence quality is not
    uniform across an ingest, and a page that does not say where it sits invites a reader to assume the
    best one.** Same discipline as ClawBot's `Knowledge/` schema states for its own pages.

17. ~~**A photogrammetric mesh needs its scale reference recorded, not just its units**~~ → **closed
    2026-09-11: ADR-0019 accepted and implemented.** Import declares `EScanOrigin`; photogrammetry
    requires a scale reference and refuses without one, `cad-export` refuses *having* one, the rule sits
    in `ScanProvenance.Validate` so CLI and MCP cannot drift, and both sidecars carry `scan_origin` /
    `scan_scale_reference` present-and-null. 185 tests, nine new, six of them refusals.

    **What implementing it added to the decision.** The scope was wrong when the question was written:
    it named `run_cradle`, but `CompareRun` imports meshes too, and *that* is the dangerous path —
    `compare → compensate` turns a deviation into a slicer profile setting, so a scale error there
    becomes a provenance-stamped compensation applied to every future print in that material. A cradle
    that does not fit announces itself; a silently wrong shrinkage figure does not. The refusal had to
    live on both. Original text follows.

~~17. **A photogrammetric mesh needs its scale reference recorded, not just its units** (opened 2026-09-11).
    `run_cradle` refusing `AUTO` is necessary and not sufficient: structure-from-motion recovers shape up
    to an unknown similarity transform, so a photogrammetry scan carries *no absolute size* unless a known
    reference was in the scene. A caller can today declare `mm` on a mesh whose scale came from nowhere,
    and the sidecar will faithfully record a declaration that means less than it looks like — absence
    disguised as a value, which is the one failure mode this repo is built against. It does real damage in
    exactly one place: `compare` → `compensate` would turn a scale error into a slicer profile change.

    **Shape decided 2026-09-11 (Benji): declare the provenance class.** Import takes a declared mesh
    origin — `cad-export` | `metrology-scan` | `photogrammetry` — and requires a scale reference (bar,
    calibrated target, or measured feature plus its measurement) *only* when the class is
    `photogrammetry`; absence is UNKNOWN and refuses. A blanket requirement on every import was
    rejected because the same path serves `kicad-cli pcb export stl`, where units are authoritative,
    and ceremony that means nothing in context is ceremony people fill with noise — the failure
    ClawBot's ADR-0026 names for a `how_determined` that states nothing. Gating only downstream in
    `compare` was rejected because a scale error still produces a cradle that does not fit: it moves the
    failure later and makes it more expensive.

    **What reading the code changed.** `ScanImport.OImport` already takes an `fPostScale`, and its own
    doc comment already says "Scale and units are provenance fields" — so the mechanism exists and is
    simply *unrecorded*. `fPostScale = 1.0` on a photogrammetric mesh is a silent identity scaling,
    indistinguishable in the sidecar from a deliberate one. The gap is narrower than first written: not
    a missing capability, a missing declaration.~~ See [[openscan]].

18. ~~**No scanner accuracy may be declared until a benchmark methodology is read**~~ → **closed
    2026-09-11, the same day it was opened, by reading it.** The OpenScan Benchy page is not a metrology
    benchmark. It is a *qualitative visual* comparison: one shared model scanned on various devices,
    results posted to Sketchfab, prose about visible layer lines and print artifacts. **No ground-truth
    geometry, no deviation measurement, and no accuracy figure appear on it at all** — and its own stated
    purpose is so that users "do not have to fall for some marketing claims about accuracy and
    resolution". The published sub-0.02 mm numbers are therefore **unsourced, not weakly sourced**, and
    their citation points at a page that argues against the inference they invite. Two further details
    worth keeping: the Mini entry ran through OpenScan Cloud, and the Classic entry used a 21 MP Daheng
    industrial camera in Agisoft Metashape rather than the shipping IMX519 — so the comparison does not
    represent a stock Classic either.

    **The answer is a refusal, and that counts as answered:** no OpenScan figure may be passed to
    `compare --declared-accuracy`, ever. A declared accuracy for this device class can only come from a
    local measurement against a known artifact. Recorded in [[openscan]] and [[openscan-2026-09]].

20. **Is a two-reference scale cross-check worth building?** (opened and largely answered
    2026-09-11.) Benji proposed two-colour 3D-printed measuring templates, and the sharpest
    version of the idea was a *second* orthogonal reference — not for redundancy, since
    photogrammetry scale is isotropic, but to test whether the reconstruction is a similarity
    at all. `examples/scale-derivation` now measures that: **σ/median 0.0075 %** over 630
    camera pairs on a synthetic capture with exact ground truth. A caliper reading a 50 mm
    printed target at ±0.02 mm is ±0.04 %, so the check would be five times noisier than the
    defect and would fire on its own measurement error. **Provisionally: no.**

    Two things keep it open rather than closed. The 0.0075 % is a *synthetic floor* — perfect
    pinhole camera, no distortion — so a real capture's number is unknown and could plausibly
    be large enough to matter. And the printed template carries a separate problem worth
    recording: **a reference printed on the machine whose dimensional error you are measuring
    is circular.** If the K2 shrinks PLA 0.3 %, a nominally 10 mm marking is 9.97 mm, and
    scaling a scan by it would hide exactly the shrinkage `compare` exists to find. The
    resolution is that a printed template is a *carrier*, never a *source*: it gets
    caliper-measured after printing, and the measurement — with a date, since PLA drifts — is
    what enters `--scale-ref`. Nominal never enters anything. Same discipline as
    `calibration-block/0.2`, which is also not trusted to be the size it was asked to be.

    Closes when a real capture gives a real disagreement figure.

19. **Two PicoGK 2.2.0 bugs are unreported upstream** (opened 2026-09-11). `mshFromStlFile` ignores its
    scale argument, and the `(vecScale, vecOffset)` transform overload applies a different scale
    component per vertex. Both are worked around at the call site rather than patched, per ADR-0001, and
    both workarounds are **version-bound** — a release that fixes the first would make our transform
    double-apply. Neither has been sent to LEAP 71, so the fix we are waiting on is one nobody knows is
    wanted. Reporting is a public act on Benji's account, so it is his to make; [[picogk]] carries the
    reproduction and the measured numbers ready to paste. Marked `TODO(report)` there.
    Generalises beyond this vendor: the asterisk pattern — a precise-looking figure citing a page with no
    measurement in it — is worth checking for on every instrument spec this platform reads.
