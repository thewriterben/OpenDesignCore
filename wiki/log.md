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

## [2026-08-15] build | Scan-to-fit landed (PR #6 branch)
run-cradle: strict mesh import boundary (declared units only — STL headers lie; raw scan content-addressed) + scan-cradle/0.1, a foam-insert cradle: bounding block minus clearance-offset scan volume to a split height. Only verified voxel ops. Demo run 2 cradled run 1's own enclosure STL — the sidecar's scan_sha256 IS run 1's artifact hash, so the ledger now hash-chains derived geometry across runs, which is exactly the provenance composition the BINGO contract wants. Upstream bug found: PicoGK 2.2.0 mshCreateTransformed(vecScale, vecOffset) scales each vertex by a different axis component; used the Matrix4x4 overload instead — worth a PicoGK issue. v0 requires watertight scans (voxelization emptiness check catches leaky meshes loudly); mesh repair stays upstream. 25/25 tests.

## [2026-08-15] build | MCP surface landed (PR #7 branch)
opendesigncore-mcp: stdio server, 7 tools. ADR-0009 draws the line the ecosystem convention implies but never stated: effects confined to ODC's own content-addressed stores execute (reads + deterministic model runs, idempotent by hash); anything reaching beyond stops at a proposal. No approval tool exists, enforced by a test that fails if one is ever added. McpGuard refuses (never clamps) pathological voxel sizes, volume budgets, and path escapes — agent-facing surfaces need resource limits the CLI doesn't. Two things learned: WithTools<T> rejects static classes (SDK instantiates for discovery); a bespoke stdio smoke harness failed twice on stream handling, so verification moved to DI-level tool discovery — same registration path Program.cs uses, and it proved the count was 7 not 6. Real client verification still wants a live MCP client (Claude Code/OBC) — noted as remaining work.

## [2026-08-15] verify+build | MCP transport confirmed; registry contract delivered
MCP: registered opendesigncore-mcp with a real client (claude mcp add/list) — "Connected". Closes the gap flagged on PR #7; DI-level testing was necessary but not sufficient, and the real client cost one command.

Registry (OpenPartsCore PR #1): ingested OBC's registry — and found the schema truth nobody had written down. registry.json is a list of USB *identity rows*, not boards: 69 rows, 66 board models, 44 vid/pid pairs, neither a key. esp32-s3 appears 3x (native USB / CP2102 / CH343); 303a:1001 covers 17 different boards. So canonical entries are board models with usb_ids lists (ADR-0004), and board identification is a MATCH, not a lookup — worth telling OBC, whose documented "select on name" rule is itself ambiguous for esp32-s3 and arduino-uno. Accessories → electronic namespace (ADR-0005). First binding emitted (TS, 100 entries, tsc --strict clean) with a --check staleness gate. 100/100 valid, re-ingest idempotent.

## [2026-08-15] decision | PD-1 superseded: KiCad directly, atopile dropped
Went to build OpenCircuitCore's reference board and the toolchain refused to exist. atopile resolves to 0.12.6, which prints "no longer supported — CLI replaced by the app at app.atopile.io (0.16+)"; 0.15.8 is the last CLI release, maintenance-only, pins Python 3.14, and its zstd dep has no cp314 wheel (two install attempts died there — stopped per two-strikes and put it to Benji rather than thrashing). 0.16+ is a hosted browser workspace. Benji chose KiCad-direct. KiCad 10.0.5 was already installed (under %LOCALAPPDATA%\Programs, not Program Files — cost three winget attempts to discover). kicad-cli covers erc/drc/netlist/BOM/gerbers/ODB++/IPC-D-356 and, notably, STEP + STL export — which turns the board↔enclosure co-design from an aspiration into a verified path through ODC's mesh boundary. OpenCircuitCore ADR-0001 had written its own exit clause ("the exit cost is authoring, not data"); it was cheap because nothing had been authored. Real lesson: this kernel choice was made from docs and a search, and survived four hours. Install and run before recording.

## [2026-08-15] build | Reference board + co-design bridge proven across repos
OpenCircuitCore PR #2: first board via pcbnew (30x40mm, 4x M3), DRC 0 violations, STEP+STL exported. ODC PR #8: running the bridge immediately broke it — kicad-cli emits ASCII STL (no binary option) and PicoGK 2.2.0 throws NotImplementedException on ASCII; worse, my import boundary let it escape unhandled rather than failing specifically, which is exactly what that boundary exists to prevent. Fixed by parsing ASCII at the boundary (file I/O, not geometry; upstream says "not implemented at this time", so it's contributable). Detection uses the 84+50n size identity — the 'solid' prefix is a trap, binary headers use it too. ODC run 3 then produced an enclosure fitted to real board outline+holes. Test asserts both encodings yield the same cradle but different scan hashes: provenance tracks bytes that arrived, not an idea of them. Lesson repeated from PD-1: ADR-0003 called this path "verified" when only the commands' existence had been checked. Run the path.

Operational gotcha: a registered MCP server is a *running process* and holds OpenDesignCore.dll open — builds fail MSB3027 until it's stopped.

## [2026-08-15] build | Registry-bound components + BOM; DRC earned its keep
OpenCircuitCore PR #2 extended: footprints carry an opc_id field, so BOM lines resolve to cited OpenPartsCore entries (U1→boards/esp32-s3, U2→electronic/bme280) and an unresolved id writes no BOM at all. This is the OpenPartsCore↔OpenCircuitCore integration working.

Placing components took DRC from 0 → 27 violations, all real: the WROOM-1's 0.2mm thermal vias vs KiCad's 0.3mm default minimum, and mounting holes sitting inside the module's antenna keepout. Fixed by engineering, not by suppressing rules — min through-drill set to 0.2mm as a deliberate fab-capability statement, and the keepout *measured* from the placed footprint (x −7..41, y −7.75..13.25, i.e. full board width) before moving the upper hole pair below it; board grew to 34×46. Third fix attempt, but each was driven by new measurement rather than a guess. Now 0 errors. ODC run 4 fits an enclosure to the populated board.

Still no schematic → no nets. .kicad_sch embeds full lib_symbols graphics per part, which is real work; that's the next milestone and it's what makes ERC meaningful.

## [2026-08-15] build | Schematic generation: circuits as code recovered
The milestone that makes ERC mean something. scripts/sexp.py (95-line stdlib S-expression reader/writer) + make_reference_schematic.py: the circuit is declared as parts + (part,pin)->net and ALL geometry is derived — each connection is a global label placed exactly on the pin's connection point, so nets form by name and there is no wire routing to get subtly wrong. Symbol definitions lifted verbatim from stock .kicad_sym into lib_symbols (the format requires them embedded).

This substantially recovers what ADR-0003 conceded when atopile was dropped: "schematics become KiCad files rather than reviewable text." The netlist description IS the reviewable text; the .kicad_sch is a build artifact. Worth noting in a future ADR amendment — the concession was larger than it needed to be.

BME280 I2C subcircuit (CSB high, SDO low = 0x76, 4.7k pull-ups, 100n decoupling, 2x PWR_FLAG): kicad-cli sch erc --severity-error = 0 violations, first run. Netlist verified real: +3V3 joins C1.1, R1.1, R2.1, U2 CSB/VDDIO/VDD with correct pintypes. Schematic BOM carries opc_id through.

Key format facts learned: schematic y grows down while symbol-library y grows up (position = symX+pinX, symY-pinY); a global label on a pin's connection point connects it without any wire; power_in pins need PWR_FLAG or ERC calls them undriven. Gaps: passives have no opc_id (OpenPartsCore lacks generic 0603 parts), the subcircuit doesn't drive the board yet, rotation-0 placement only.

## [2026-08-15] build | Generic passives close the BOM gap; ADR-0003 amended
OpenPartsCore PR #2: electronic/r-0603 and electronic/c-0603. The citation question was the interesting part — a "generic 4.7k resistor" has no datasheet to cite. Resolution: the registry entry fixes the PACKAGE (body 1.6x0.8mm, cited to IPC-SM-782A, which KiCad's own footprint descr names by page); resistance/dielectric/tolerance/voltage are explicitly properties of the BOM line and a chosen MPN, marked TODO(source). Inventing plausible ratings for a generic part is precisely what the citation rule exists to stop. 102 entries, binding regenerated.

Every OpenCircuitCore schematic BOM line now resolves; ERC still 0.

OpenCircuitCore ADR-0004 amends ADR-0003's consequences (not its decision): the authoring concession was overstated. The netlist description is the source, .kicad_sch is a build artifact, so diffable/reviewable/agent-writable circuit source survived dropping atopile. What is genuinely still worse is now named rather than glossed: no type system over connections, no package manager for reusable modules, no layout aesthetics, and a hazard that GUI edits put artifact ahead of source.

## [2026-08-15] build | OpenBuildCore created — the last named pillar
Benji chose a fourth peer repo over folding inventory into OpenPartsCore or OBC's deployment-generator. This closes the third pillar of the original brief ("add the components you own, get help building what you want or ideas on what to build").

Read OBC's planDeployment closely first. Its capability-token core is good and reused; three properties don't survive generalisation and are fixed here: (1) presence-only matching — every check is .length>0 or [0], so a two-host project is "satisfied" by one board; (2) no exclusivity — one item silently fills several roles; (3) suggestions are hardcoded string arrays, the registry is never searched, so advice staleness grows with the registry. OpenBuildCore allocates by quantity, exclusively, specific-parts-before-capabilities (otherwise a capability requirement eats the only unit of a named part and reports a false gap), and derives suggestions by querying OpenPartsCore.

Demo on a 6-part example drawer: env-monitor and camera-trap buildable, two-node-mesh short 2 LoRa radios with 5 registry-found candidates. Note the exclusivity working — two-node-mesh consumed both ESP32s for its two hosts, so sensor_read fell through to the BME280.

Honest limit recorded in its ADR-0002: greedy allocation is not optimal and could report a false gap in a pathological case; backtracking deferred rather than hidden.

## [2026-08-15] build | Shopping list closes the ideation loop (OpenBuildCore PR #1)
what can I build → what am I missing → what do I buy. Gaps aggregate across projects into one list sorted by how many projects each item unlocks.

The decision worth recording (its ADR-0004): a shopping list needs a quantity per line and there are two defensible answers — sequential builds reuse parts so quantity is the MAX shortfall; simultaneous builds SUM. On the seed catalogue that's 2 vs 3 LoRa radios. Picking one silently is how a list under-orders, and under-ordering is found at the bench after parts arrive. So sequential is the default, and the basis is printed in human output and carried as a `basis` field in --json — a consumer never infers it. Added a fourth project (lora-relay) specifically so the difference is visible in shipped data, not only in a unit test. 12 tests.

Same principle as the rest of the platform: an assumption that changes a number gets stated, not defaulted quietly.
