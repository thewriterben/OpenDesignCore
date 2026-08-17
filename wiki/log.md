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

## [2026-08-15] verify | Platform walkthrough — ADR-0007's composition claim, tested
examples/platform-walkthrough runs all four peers in one chain by shelling out to each CLI (interfaces, not internals — a break here is a break a user hits). Ran clean first try: env-monitor buildable → ERC 0 → DRC 0 → run 5 fitted an enclosure to the real board → handoff 2 staged it. This is the definition-of-done's end-to-end example, and the first evidence that the four-peer split actually composes rather than merely being drawn on a diagram.

What the chain guarantees, now demonstrable: every part fact cited (BOM opc_id → registry entries with sources); provenance composes across repos (enclosure sidecar records scan_sha256 = the board STL's hash, so geometry lineage crosses a repo boundary by content hash); every seam fails loudly rather than degrading.

Also corrected a ROADMAP that had gone stale within the day — electronics, registry and inventory were still under "Not yet" after shipping as peers, and two settled ADRs were still listed as open questions. A roadmap that lies is worse than none.

## [2026-08-15] build | Schematic drives the board (OpenCircuitCore PR #3)
boards/sensor-breakout closes the gap reference-esp32s3 named in its own README: there, schematic and board were generated independently with nothing stopping them disagreeing. Now parts come from the shared PARTS description and pads are bound to nets *parsed from the schematic's exported netlist* (NETINFO_ITEM + pad.SetNet — both verified present in pcbnew 10.0.5 before designing around them).

Result: 0 DRC violations, 10 unconnected items. That combination is the CORRECT outcome, not a failure — real nets, unrouted ratsnest, the state a router or human takes over from. Verified by reloading the saved board rather than trusting the write: U2 pads read GND/+3V3/SDA/SCL exactly as the schematic specifies (CSB high = I2C, SDO low = 0x76).

Not routed, and deliberately so: KiCad ships no autorouter and writing one isn't this project's business.

Second stale-roadmap correction of the session, this time in OpenCircuitCore — and I kept "provenance record emitted per build" explicitly unchecked, because ODC does that and OpenCircuitCore still doesn't. Marking it done would have been the easy lie.

## [2026-08-15] build | OpenCircuitCore emits provenance (PR #3, second commit)
Closed the invariant I'd deliberately left unchecked one commit earlier: ODC records provenance for every artifact, OpenCircuitCore didn't. emit_provenance.py now records design sources + hashes, the upstream netlist (a board whose nets came from another directory's schematic must say so, else its provenance omits what determined its connectivity), outputs, ERC/DRC results, KiCad version, commit. Canonicalisation byte-identical to BINGO's kernel and ODC's C# port, so hashes are comparable across all three; floats refused for the same reason.

Two bugs found by running it rather than trusting it: KiCad writes "Found 0 DRC violations" in the report file but "Found 0 violations" on the console, and ERC reports use an entirely different shape ("** ERC messages: 0  Errors 0  Warnings 0"). Assuming symmetry between two outputs of the same tool was the error. Worse, v1's response was to record -1 and continue — printing FAIL while exiting 0. It now refuses to emit a record claiming a check happened whose result it couldn't read. A provenance record that says "passed" because it couldn't parse the answer is worse than no record at all.

Pattern worth keeping: leaving an item explicitly unchecked in a roadmap made the gap visible enough to close on the next pass. Ticking it would have hidden it permanently.

## [2026-08-15] build | Cited fab profile + gerbers (OpenCircuitCore, third commit on the branch)
Last unchecked "Now" item. fab-profiles/jlcpcb-2layer-1oz.json carries JLCPCB's published capability — fetched from the vendor page, cited with section names and retrieval date. apply_fab_profile.py refuses to apply a profile lacking a citation, because manufacturing tolerances are exactly the physical data this project promised never to invent. sensor-breakout: 0 violations against real limits. Gerbers + drill exported (26 files), all hashed into provenance.

The judgement call worth recording: four capabilities are listed as not_encoded rather than approximated. The via annular minimum is the sharp one — the vendor states a PTH annular ring (>=0.20mm) but no via-specific figure, and I could have derived one from "via diameter should be 0.1mm larger than hole size". It would have looked authoritative and been my invention. Marked TODO(source) instead. The others need a .kicad_dru custom rule file (KiCad has no board-wide setting), now roadmapped rather than silently dropped.

DRC now asks the question that matters: not "is this self-consistent" but "will this house build it". Board is still unrouted — gerbers are structurally valid and manufacturable-as-drawn, but it does nothing until routed.

## [2026-08-15] decision | REFUSAL-CATEGORIES merged
Benji reviewed and merged the legality-gating taxonomy into ProjectBINGO. PD-5 now has a real spec in the repo that owns it. Open remainder: per-jurisdiction mappings (TODO(source) throughout — nobody should generate legal claims), and freezing the category-list hash into JOB_ACCEPTED next to the acceptance-checklist hash so a dispute resolves against the list version in force at order time.

## [2026-08-15] build | Rust binding (OpenPartsCore PR #3) — item 1 of Benji's list
Crate openpartscore: whole registry as const data, zero dependencies (a consumer shouldn't take serde to read static reference data). capabilities and usb_ids hoisted and typed because they're what consumers dispatch on; everything else stays as attributes_json so a niche field never forces a binding schema change.

The design point worth keeping: candidates_for_usb returns an Iterator, not an Option. ADR-0004's many-to-many mapping is thereby enforced by the type signature rather than by a doc comment nobody reads — a signature returning one answer would quietly relearn the exact bug the ingest was written to fix. 7 cargo tests pin it (0x303a:0x1001 → >5 candidates; esp32-s3 → ≥3 identities; every entry cited).

Honest scope: this makes consumption possible, not actual. OBC switching is upstream's call, now a separate unchecked roadmap item alongside reporting the duplicate-name ambiguity.

## [2026-08-15] build | OpenBuildCore MCP surface (PR #2) — item 2, first half
Five read tools (inventory, list_projects, what_can_i_build, gaps, shopping_list), all executing per ADR-0009 since nothing here writes to a store or reaches a fabricator. Deliberately NO inventory-editing tool: inventory is the user's record of physical objects, and an agent quietly changing it would poison every downstream answer with the error only surfacing at the bench.

Verified connected against a real client (claude mcp add/list), applying the lesson from ODC PR #7 where DI-level testing passed while transport was unproven.

Two findings. (1) Naming the package `mcp` shadows the SDK on import — walked into it, caught it, renamed to obc_mcp. (2) **The MCP Python SDK is at 2.0.0 and `mcp.server.fastmcp` is gone**, replaced by `MCPServer`. AdvancedStudio's studio-mcp imports the old path and pins `mcp>=1.2` with no upper bound, so it does not start on this machine — confirmed by running the import. Filed as open question #9; Benji's repo, Benji's call, and the two servers here are worked examples of the port.

Same pattern as atopile and the STEP/STL bridge: the ecosystem's written claims aged faster than its code.

## [2026-08-15] build | OpenCircuitCore MCP surface (PR #5) — item 2 complete
All four peers now have MCP surfaces; all three Python/dotnet servers verified Connected from a neutral working directory.

OpenCircuitCore ADR-0005 is the interesting part: ADR-0009's store-boundary test does NOT transfer here, and saying why matters more than applying it by rote. ODC's writes are content-addressed artifacts — rerunning yields the identical file, so executing is safe. OpenCircuitCore's writes are design source files a human also edits. Concrete failure: you route a board for an evening, an agent calls regenerate for an unrelated reason, routing gone, no undo, git only helps if committed. Proposing doesn't rescue it either — ODC can propose to AdvancedStudio because the studio owns an approval queue; this repo has nothing to propose TO, and inventing an approval mechanism to justify an unrequested tool is the wrong order. So regeneration/fab-export/profile-application are simply absent. Revisit hook recorded: a git-cleanliness check could distinguish "regenerate untouched file" from "regenerate over human work".

Registration bug worth remembering: `python -m pkg.server` resolves against the CLIENT's working directory. Registered that way, openbuildcore connected from its own repo and showed "Failed to connect" from anywhere else. Both re-registered with absolute script paths. A cwd-dependent MCP registration is a latent break.

## [2026-08-15] build | Custom DRC rules + ADR-0006 liveness requirement (OpenCircuitCore PR #6) — item 3
The rule content is minor; the discipline is the point. Custom .kicad_dru rules FAIL QUIETLY: a valid constraint whose condition never matches, or whose constraint KiCad doesn't evaluate for that object type, parses fine and reports nothing. DRC then says "0 violations", which reads as coverage and is its opposite — an unchecked design that looks checked, with the prompt to check manually now removed.

ADR-0006: a rule ships only once proven to FIRE. Set threshold absurdly, confirm DRC reports a violation naming the rule, restore, record the evidence beside the rule.

It caught a false positive on first use. npth_min_hole (JLCPCB 0.50mm min non-plated hole) fired correctly against both mounting holes and no SMD pads. The NPTH annular-ring rule did not: annular_width with the same condition at min 9.0mm, against holes with a 0.00mm annulus (pad size 2.2mm == drill 2.2mm), reported nothing — KiCad 10.0.5 evidently doesn't evaluate annular_width for NPTH pads. That rule LOOKED obviously right; without the check it would have shipped as fake coverage.

Also: .kicad_dru is now a provenance design source. A record asserting "DRC passed" without the rule file omits what passing was measured against.

This is the same failure family as the emit_provenance regex (-1 recorded as a pass) and studio-mcp's stale import: things that appear to work because nothing forced them to prove they did.

## [2026-08-15] build | OpenBuildCore schemas + validator + catalogue (PR #3) — item 4
JSON Schemas for project/inventory docs, and a validator whose real job is REFERENTIAL integrity, not shape. Two failure modes are invisible in the advisor because they present as ordinary gaps: a part_id no registry entry provides (reads as a permanent shortfall, sends someone shopping for a nonexistent name), and a capability no part provides (unbuildable by construction, empty suggestion list). Neither errors today; both are caught at validation. Nine of 21 tests are negative cases proving the validator fires — ADR-0006's lesson applied to a second repo.

Catalogue 4→8. Pulled the registry's actual capability vocabulary first rather than inventing tokens. Two projects deliberately shaped to test the matcher instead of flattering it: soil-moisture-nodes needs analog_read×3 and battery×3 (owning one board satisfies none of it — the presence-only failure), bird-feeder-cam separates nn_accel from camera_capture (host inference flattens a battery in a day).

Example inventory now builds 3 of 8 — a realistic ratio, and it makes the leverage sort visible for the first time: "1x capability:wifi unlocks bird-feeder-cam, desk-air-quality, soil-moisture-nodes". That property existed at four projects but had nothing to demonstrate it.

## [2026-08-15] build | Scan-compare (PR #10) — item 5, list complete
design → print → scan → measured deviation, recorded and hash-chained. Per-axis deviation plus volume; axis SPREAD is what makes it actionable (near zero = one scale factor valid; wide = compensate per axis, not by an average that's wrong everywhere).

The demo caught a bug in my own reasoning, which is the part worth keeping. v1 judged significance against VOXEL SIZE. Run on real board geometry it recovered an exact −0.35% on all three axes with zero spread — then declared it unresolvable. Wrong because bounding extents come from mesh vertices at float precision and never pass through the voxel grid; voxel size bounds the VOLUME figures only. I'd applied a caveat to the wrong measurement.

The real floor is the scanner's accuracy, which the code cannot know — so --scan-accuracy-mm follows the units rule: declared, never inferred. Undeclared = significance UNKNOWN, not false, and the run does not count as passed. Caveats (calibration bounds absolute scale; extents are orientation-sensitive and a rotated scan is undetectably meaningless; bulk measurement not surface deviation) travel inside the record, with a test asserting they're present.

41/41. Validated against synthetic prints only — a real print and scan needs Benji's printer.

Pattern, fourth instance today: the thing that exposed the error was running it on real data, not reasoning about it. Voxel-size-as-floor looked obviously right.

## [2026-08-15] security | AdvancedStudio write surface hardened
The most consequential item left: :8770 bound to 0.0.0.0 with /api/action able to start, pause and cancel prints, drive heaters and run macros, gated only by confirm:true in the body. Anything on the LAN could move the printer.

Two independent protections, because either alone fails differently: loopback bind by default (a service on 127.0.0.1 is not reachable from the LAN at all — strongest and least intrusive), and an optional shared token on the four state-changing endpoints for when exposure is deliberate. Reads stay open; they cannot move the printer and the dashboard polls them constantly. Constant-time comparison on the token.

Key finding: the 0.0.0.0 came from studio.example.toml, not the dataclass default — there is no studio.toml on this machine, so the example file IS the effective config. Changing only the dataclass default would have fixed nothing. Verified effective bind is now 127.0.0.1.

11 tests prove the gate blocks rather than merely exists: wrong token rejected, every write path gated, every read path not, env overrides file, and the shipped default is loopback.

NOT COMMITTED: AdvancedStudio is not a git repository. Changes are on disk and tested; whether to git init is Benji's structural call.

Still open there: studio-mcp's SDK 2.0 break, no upload/slicer, in-memory approvals, no provenance field on proposals.

## [2026-08-15] build | AdvancedStudio under version control; studio-mcp ported to SDK 2.x
git init: 69 files, .gitignore excluding .venv, kb_store (3 MB embeddings.npy, rebuildable), studio.toml (holds printer IP, API keys, and now auth_token), __pycache__. Single honest initial commit — reconstructing a "before" state to make today's security work diffable would have been fabricating history that never existed, so the commit message names what changed today instead.

studio-mcp: recommended PORT over pinning mcp<2, and did it. Pinning is the correct emergency fix but freezes the server on a superseded SDK that won't track the spec, and would leave three MCP servers in this ecosystem split across two APIs. The port is genuinely two lines — MCPServer replaces FastMCP with identical decorator and run() surfaces. All 12 tools register.

Three things fixed while there, each a real defect rather than tidying:
- requirements said `mcp>=1.2` unbounded, which is what turned an SDK major release into a server that silently stopped starting. Now `>=2.0,<3`.
- The proxy now sends X-Studio-Token on writes and explains the 401 — an integration point I created that morning by adding the auth gate, and would have broken propose_action.
- server.py now runs by absolute path as well as -m, because MCP clients register a command and -m resolves against the CLIENT's working directory (the same latent break found with openbuildcore).

All four platform MCP servers now report Connected from a neutral directory: opendesigncore, openbuildcore, opencircuitcore, studio-3dp.

## [2026-08-16] build | OpenBuildCore machines: capability model, time never modelled (PR #4)
Benji asked for printers and their capabilities so jobs can be matched to what the machine can actually do. The physical half of "what can I build" — inventory matching says nothing about whether a 260 mm bracket fits a 220 mm bed.

Machines are owned state, same shape of thing as inventory: machines.json git-ignored, example/ is the template, ADR-0001's reasoning applies unchanged. Field names copied from BINGO's NODE-AGENT machine record (machine_id, driver, make/model, process, envelope_mm, materials, tier) so a machine described here hands to a node without a translation layer — read the spec first rather than inventing a vocabulary that would have to be reconciled later.

The one decision worth the ADR was time. A volumetric estimate is easy and is what most tools do; it is also a number with no provenance, wrong by factors on anything but a solid block, and it will be read as a measurement. Asked Benji and he chose measured-throughput-only. So: an estimate exists only when the record carries a rate its owner measured AND says how, machines without one answer "requires slicing", and even a measured one is labelled pre-slicing triage with the caveat inside the returned result rather than in a README. Same discipline as --scan-accuracy-mm yesterday: absence is UNKNOWN, not a gap to fill with a model.

The K2 Plus record makes the discipline visible in an uncomfortable way. Its build volume is not stated anywhere in the cited Research-Report material, and I would not recall it from memory, so envelope_mm is a 1x1x1 placeholder marked TODO(source) and every fit check on the machine Benji actually owns fails loudly. That is the intended behaviour and two tests pin it — one that the placeholder blocks, one that the check flips when a real envelope is supplied, so it is a live check rather than a coincidence.

Fit tries all six axis-aligned orientations and names the one that works, because a part failing flat commonly fits stood on end. Arbitrary orientations are declared out of scope: they will produce false negatives, that is the safe direction, and the message says which check failed so a human can overrule it.

Validator extended. The check that earns its place rejects measured_throughput with no how_measured — an unsourced rate is indistinguishable from a recalled one and would silently become a print time the user trusts. Every check made to fail on purpose and then to pass before shipping.

47 tests, up from 21. obc_mcp gains list_machines and can_print, both reads; no tool edits machines, for the same reason none edits inventory.

Incidental but real: em-dashes were mangling to ? under cp1252 on the Windows console, and validate.py had one inside its OWN failure message — a validator that garbles the text explaining what went wrong. Scripts are ASCII-only in printed strings now. Fifth instance this week of running the thing finding what reading it did not.

## [2026-08-16] build | Closing the machines<->design loop, in three steps
Benji picked "close the loop" over the AdvancedStudio slicer work. Planned it as three steps and each one found something the previous one had hidden.

**Step 1 (ODC PR #12, ADR-0010).** Went to read the sidecar for a bounding box and there wasn't one. The record described what went IN — part envelope, scan hash, clearance, wall — and never how big the thing that came OUT was. So the obvious question a fabricator asks of a design could not be answered from the record that travels with the artifact, and every downstream peer would have needed a mesh parser to answer a question about dimensions. That is a defect in the record on its own terms, not just a missing integration hook, and it went unnoticed for exactly as long as nobody consumed the field.

artifact.bbox_mm + volume_cubic_mm, schema 0.1 -> 0.2. Extents and volume kept deliberately apart in the type: extents come from the mesh at float precision and are NOT bounded by voxel size, volume comes from the voxel field and IS. Same distinction the scan-compare significance bug got backwards, now written into the thing that carries it. Real risk was determinism — a computed field could have broken rerun byte-identity — and CalculateProperties turned out stable, checked by the existing test rather than assumed. Verified on a real run: 18.00 x 25.50 x 3.10 envelope produced 23.40 x 30.90 x 5.80, exactly envelope + 2(clearance + wall).

**Step 2 (OpenBuildCore PR #5).** can-print --from-sidecar. The peers meet at a FILE: OBC imports nothing from ODC and reads a record that already had to exist. The tempting fallback was inputs.part_envelope_mm, present in every 0.1 record — wrong by twice the clearance plus twice the wall, and entirely plausible-looking. Refused by name instead, and the gate tests for the FIELD not a version whitelist so a future 0.9 keeps working. --size with --from-sidecar refused rather than one silently winning: two answers to the same question, and preferring one hides a disagreement.

Walkthrough gained a machine-check step. First time a peer CONSUMES a provenance field rather than producing one — which is the whole point, since until something depends on a field "we record provenance" is an untested claim.

**Step 3 (OpenBuildCore PR #6, ADR-0006).** Projects declare parts to be MADE. The cheap option was a part_id under mechanical/ letting the existing gap machinery handle it, and it is wrong in a specific way: a missing part and an unmakeable part are fixed differently. Buy the LoRa radio; you cannot buy your way out of a 260 mm stake on a 220 mm bed. Collapsing them would put an unmakeable part on a shopping list where it sits unbought forever looking like an ordering oversight.

So: third requirement kind, reported under `fabricate` never `gaps`, absent from shopping by construction, no part in the exclusive allocation. Two booleans rather than one because a single label cannot say which half failed. No machines declared = makeable null, unknown not false — third application of that rule now (units, scan accuracy, machines).

Validator boundary worth recording: made parts are checked for SHAPE only, never against owned machines. Projects are shareable, machines are personal, and validating against machines would make a project's validity depend on who is reading it.

Seed data exercises fit-failure (260 mm stake vs 250 mm gantry), material-failure (ASA housing on a PLA/PETG machine) and success (desk case), with a test asserting both outcomes appear in shipped data so the negative path cannot quietly stop being exercised. Same discipline as the K2 placeholder.

Pattern for the day: the loop being closed is what exposed the missing field. Steps 2 and 3 were straightforward once step 1 existed; step 1 was invisible until something tried to read it.

## [2026-08-16] build | The compensation loop, and a provenance field that would have vanished silently
Benji chose connecting the compensation loop over the AdvancedStudio slicer work. Checked first rather than assuming: no slicer is installed on this machine at all (no Orca, no Cura, no PrusaSlicer — only LycheeSlicer, which is resin), so the upload-and-slice option would have needed a new external dependency in the fabrication path. Worth surfacing rather than quietly installing something.

Two halves of one computation had existed for weeks and never met. AdvancedStudio's `calibration/calculators.py` turns a nominal/measured pair into an Orca shrinkage percentage, calibrated against its own research; ODC's `compare` produces exactly that pair from a design and a scan. The gap between them was a person retyping a figure.

**The decision worth recording is the split.** Computing the percentage in ODC is three lines. It would also have given the platform two implementations of one formula, in two languages, free to drift, with no test able to catch the drift because neither side would know the other existed. So: ODC decides whether a compensation is DEFENSIBLE (a property only the measurement can answer), the studio decides what the number MEANS (slicer semantics belong with the slicer's tooling). Neither reimplements the other.

Three refusals, each a real failure mode rather than defensive padding: deviation inside the declared scanner accuracy (compensating for the instrument), accuracy undeclared (no basis to separate signal from instrument), axes disagreeing beyond a declared threshold (their mean is wrong on both axes). Z is never folded into the XY figure — Orca's Shrinkage (XY) applies to X and Y only and Z has different causes; a test pins that even when Z shrank five times as much.

**Found while adding a declared tolerance: an undeclared one.** `compare`'s advisory output had been making the same axes-disagree judgement against a hard-coded `0.5`. A constant deciding a tolerance, in the repo whose rules forbid exactly that, sitting in plain sight since the compare PR. It now defers instead of deciding quietly.

**The bug that matters, in AdvancedStudio.** `ProfileStore.upsert` hard-coded `source = "user"` on every update to an existing profile. Writing an origin would have been silently discarded and the result would still have looked correct — a compensation from a calibrated scan and one guessed off a forum post would have stayed indistinguishable, which is the exact condition the change exists to end. Re-seeding is guarded by the file already existing, not by that field, so nothing depended on the overwrite. Caught by a test, and I reverted the fix once to confirm the test fails without it.

Proven end to end against the running studio, not simulated: PETG 0.500% → 0.400%, `source` recorded as `odc-comparison:<hash>`, then restored — and `git diff` against the initial commit confirms Benji's profile data is byte-identical, so the probe left nothing behind.

AdvancedStudio now has a DECISIONS.md (ADR-0001) and pytest in requirements; its venv had fastapi, websockets and numpy but no pytest, so the suite was unrunnable there. A suite nobody can run is a suite nobody runs.

Deliberate non-decision, recorded because it is the obvious-looking shortcut: `MaterialProfile.max_volumetric_speed` is NOT wired to OpenBuildCore's `measured_throughput`. It looks like the same quantity and is not — a slicer ceiling versus an achieved rate — and conflating them would put a plausible unmeasured number into the print-time path, which is the failure OpenBuildCore ADR-0005 exists to prevent.

56 ODC tests, 22 studio tests. Validated on synthetic prints only: the plumbing is proven, the percentage is not, and that needs a real print and a real scan. ADR-0011 and studio ADR-0001 both say so rather than leaving it implied.
