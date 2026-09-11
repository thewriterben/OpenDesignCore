# Decisions

Append-only. Newest at the bottom. One entry per choice that would be expensive to reverse.

Don't edit a past entry to reflect a change of mind — write a new one that supersedes it, and note the supersession in both.

---

## ADR-0001 — Build on PicoGK and the LEAP 71 ShapeKernel

**Date:** 2026-08-15
**Status:** accepted

**Context.** OpenDesignCore needs a geometry foundation. Writing one is a multi-year project in itself and is not where this project's contribution lies. LEAP 71 has open-sourced PicoGK (a voxel geometry kernel over OpenVDB) and the ShapeKernel (a shape-construction layer on top of it) under Apache-2.0, and states plainly that both layers have nothing to do with engineering design — they are the foundation engineering code runs on. That is exactly the seam we want.

**Options considered.**
1. Write our own kernel — full control, wrong decade.
2. Build on an existing B-rep kernel (OpenCASCADE) — mature, but fragile booleans and a licence that constrains downstream users.
3. Build on PicoGK + ShapeKernel — robust booleans, permissive licence, an existing community, and an explicit invitation to consume the libraries as submodules.

**Decision.** Option 3. PicoGK and LEAP71_ShapeKernel are consumed as git submodules pinned to release tags, not vendored and not forked. LatticeLibrary and QuasiCrystals are added the same way if and when a model needs them. `thewriterben/leap71ODC` is a fork of `leap71/leap71`, the organisation's landing-page repo — README and images, no kernel code — so it plays no part in the build. A fork is created only if we need to carry a patch upstream, and it would be a fork of PicoGK or ShapeKernel specifically.

**Consequences.** Geometry is solved and we inherit its constraints wholesale (see ADR-0003, ADR-0004). We depend on LEAP 71's release cadence and must pin versions to keep builds reproducible. Upgrading a submodule is a deliberate, tested act, not a background drift. The PicoGK runtime is a separate native install, so the build is no longer "clone and go" — CONTRIBUTING.md must say so.

---

## ADR-0002 — C# on .NET as the implementation language

**Date:** 2026-08-15
**Status:** accepted

**Context.** ADR-0001 commits us to PicoGK. Its runtime is C++; the higher-level PicoGK API and the ShapeKernel are C#. Any consumer either speaks C# or pays an interop tax on every geometry call.

**Options considered.**
1. C# throughout — same language as the kernel API, no boundary.
2. Python or Rust core calling PicoGK over a binding layer — preferred language, but a hand-maintained FFI surface across the hottest path in the system, and the ShapeKernel's abstractions arrive gutted.
3. Two-language split, C# for geometry and something else for the engineering layer — a service boundary in the middle of a tight design loop.

**Decision.** Option 1. OpenDesignCore is a .NET solution written in C#.

**Consequences.** Bindings for other languages become a deliberate later feature at the edge of the system, not an internal seam. The contributor pool shifts toward .NET. Toolchain, CI, and lint config in CONTRIBUTING.md and ci.yml all follow from this, and the minimum .NET version is set by whatever PicoGK's current release requires — pin it and record it.

---

## ADR-0003 — Voxel/SDF geometry with one global resolution

**Date:** 2026-08-15
**Status:** accepted. Supersedes the "tolerance is a parameter, never a constant" invariant as originally written.

**Context.** PicoGK represents geometry as signed-distance voxel fields over OpenVDB. All spatial operations are governed by a single voxel size set once at initialisation, via `Library.Go`. The repository's original invariant — every geometric predicate takes an explicit tolerance, no ambient epsilons — is incompatible with that: in a voxel kernel the resolution is genuinely global and everything downstream inherits it.

**Options considered.**
1. Keep the per-call tolerance rule and thread a tolerance parameter through our own layer anyway — an invariant that lies about the kernel underneath it, which is worse than no invariant.
2. Drop the rule entirely — loses the thing it was protecting, which is that resolution must never be implicit.
3. Restate the rule at the level where it is true.

**Decision.** Option 3. The rule becomes: **voxel size is an explicit input to every model run, never a default buried in code, and it is recorded in the provenance of every artifact.** No OpenDesignCore function invents or silently changes the resolution. Where a model has a minimum resolution below which its result is meaningless, it declares that and fails loudly rather than producing a coarse answer quietly.

**Consequences.** Resolution becomes a first-class model parameter and a first-class provenance field — helped by PicoGK writing library info and voxel size into OpenVDB metadata. Results are only comparable at equal voxel size, so reference tests pin it and convergence tests vary it deliberately. Voxel booleans are numerically robust regardless of geometry complexity, which makes the determinism requirement far easier to meet than it would be on a B-rep kernel; floating-point op order and parallel reduction order still need pinning.

---

## ADR-0004 — Millimetres as the internal length unit

**Date:** 2026-08-15
**Status:** accepted. Supersedes "SI internally, always" for length.

**Context.** PicoGK is millimetre-based throughout its API surface — voxel size, distances, and coordinates are all in mm. The repository originally specified SI metres internally. Holding metres would mean converting at every kernel call, which is precisely the silent-conversion bug class the unit rule exists to prevent.

**Options considered.**
1. Metres internally, converting at the kernel boundary — thousands of conversion sites, each one a place to drop a factor of 1000.
2. Millimetres internally, matching the kernel.

**Decision.** Option 2. Length is millimetres throughout OpenDesignCore. Other quantities stay SI unless they are dimensionally coupled to length, in which case the coupled unit is declared explicitly in GLOSSARY.md rather than inferred. Conversion happens only at UI and import/export boundaries, in exactly one place per boundary.

**Consequences.** No conversion layer between us and the kernel. Anything derived from length — density, stress, thermal quantities — now needs its unit stated explicitly rather than assumed from an "it's all SI" rule, which is more work per entry in GLOSSARY.md and much less ambiguity. Importers and exporters own their conversions and get tested for them.

---

## ADR-0005 — Licence

**Date:** 2026-08-15
**Status:** accepted 2026-08-15 — relicensed to Apache-2.0 the same day, before any external contribution existed.

**Context.** OpenDesignCore was seeded MIT. PicoGK and the ShapeKernel are Apache-2.0. MIT consuming Apache-2.0 is legally clean, but leaves the project retaining upstream's NOTICE obligations while its own contributions carry no express patent grant — in a domain where the code encodes manufacturing methods.

**Options considered.**
1. Stay MIT — shortest licence, most familiar, no patent grant.
2. Move to Apache-2.0 — matches upstream, express patent grant, retaliation clause, NOTICE requirements handled uniformly across the stack.

**Decision.** Option 2, Apache-2.0 — for this repository and for the two new platform repos (parts registry, electronics engine) at creation. Existing ecosystem repos (Oh-Ben-Claw, OBC-Prime, deployment-generator, ClawCam, Accelerapp, ProjectBINGO) stay MIT for now; MIT and Apache-2.0 compose cleanly in both directions.

**Consequences.** Express patent grant and retaliation clause on the code most likely to encode manufacturing methods. NOTICE obligations now handled uniformly with the upstream PicoGK/ShapeKernel stack. Done while the contributor set is exactly one person, which is the only cheap moment. Not legal advice — worth a lawyer's eye if the choice gains commercial consequences.

---

## ADR-0006 — Persistence: three stores, not one

**Date:** 2026-08-15
**Status:** accepted 2026-08-15. `wiki/` created the same day with its own schema file (`wiki/CLAUDE.md`); `data/`, `ledger.db`, and `artifacts/` follow when the first code that reads or writes them lands.

**Context.** "Add a database" was the starting request, with the LLM Wiki pattern (Karpathy, April 2026) as the default candidate because it is already in use on adjacent projects. The pattern has three layers: immutable raw sources, an LLM-written and LLM-maintained markdown wiki, and a schema file telling the agent how to maintain it. It is a knowledge-synthesis pattern, not a datastore, and its own design is explicit that the wiki layer is derivative and the raw sources are the grounding authority.

OpenDesignCore has four kinds of state, and they have incompatible requirements:

| State | Requirement |
|---|---|
| Model definitions, material data, process constraints | Reviewable, diffable, cited, versioned with the code that reads it |
| Run and provenance records | Machine-written, append-only, queryable, never hand-edited |
| Generated artifacts (VDB fields, meshes, exports) | Large binaries, content-addressed, cheap to garbage-collect |
| Engineering knowledge, rationale, ingested literature | Synthesised, cross-referenced, expected to be rewritten as understanding improves |

**Options considered.**
1. One store for all four — whichever product is chosen is wrong for three of them.
2. LLM Wiki as the system of record — its whole value is that an LLM rewrites pages as understanding changes, which is disqualifying for a provenance ledger.
3. Separate stores matched to requirement, with an explicit rule about which may ground which.

**Decision.** Option 3.

- **Definitions and reference data** — git-tracked text (TOML or JSON), schema-validated on load. Every material property and process constraint carries a citation. Changes arrive through code review, which is the point: a density that changes should be visible in a diff, not in a row update nobody saw.
- **Run and provenance ledger** — SQLite. Embedded, single file, no server, first-class in .NET via `Microsoft.Data.Sqlite`, trivially backed up. Append-only: one row per model run recording inputs, voxel size, pinned submodule versions, commit, output hashes, and pass/fail against requirements. Written by code, never by an agent, never edited by hand.
- **Artifacts** — content-addressed files on disk, referenced by hash from the ledger. Not blobs in the database.
- **Engineering knowledge** — the LLM Wiki pattern, in its own directory with its own schema file, fed by papers, standards, vendor data, and our own run results.

**The grounding rule.** A wiki page is never the source of a number that enters a model run. Numbers come from the git-tracked reference data with a citation; the wiki may link to that data and explain it, but nothing reads a value out of a wiki page. Likewise the wiki may read the ledger and never writes to it.

**Consequences.** Four stores to keep coherent instead of one, and a boundary that has to be enforced rather than assumed — worth encoding as a load-time check that rejects reference data lacking a citation. In exchange, each store does what it is good at, the reproducibility guarantee in ADR-0003 survives contact with an agent that rewrites files, and the wiki gets to be genuinely useful without becoming load-bearing for correctness.

The failure mode this is avoiding is well documented in the LLM Wiki thread itself: derived pages accumulating in the same index as sources with equal standing, until the wiki quietly cites itself. Several practitioners reported hitting it independently. Keeping the layers separated by kind, rather than by discipline, is what prevents it here.

---

## ADR-0007 — OpenDesignCore is an engine among peers, not the platform umbrella

**Date:** 2026-08-15
**Status:** accepted; the *peer enumeration* is superseded in part by ADR-0014 (the decision itself stands)

**Context.** The stated vision is a Computational Engineering system spanning the OBC ecosystem and Project BINGO: 3D scanning for fit, circuit/PCB design with BOM generation and component sourcing, inventory-driven building and ideation, and fabrication both local and networked. That is a multi-domain platform. This repository's ARCHITECTURE.md deliberately scopes it to one path: requirements → model evaluation on PicoGK → validated artifact + provenance. The two are in tension, and an ecosystem survey (2026-08-15, `wiki/concepts/ecosystem-map.md`) showed the surrounding repos have already converged on a composition pattern: MCP as the seam, Oh-Ben-Claw's registry as component ground truth, propose-only writes for anything that moves hardware, and evidence/provenance chains per repo.

**Options considered.**
1. Expand OpenDesignCore's mission to absorb scanning, electronics, sourcing, inventory, and ideation — one repo, one process, and the end of the scope rule that keeps this codebase reviewable.
2. Keep OpenDesignCore narrow and create a new umbrella orchestration repo.
3. OpenDesignCore remains the deterministic mechanical/geometry engine; the platform *is* the ecosystem, composed over MCP, with peers owning their domains: electronics engine (kernel choice pending its own ADR), registry and planning (Oh-Ben-Claw), local fabrication (AdvancedStudio), networked fabrication and settlement (Project BINGO), device codegen (Accelerapp).

**Decision.** Option 3. OpenDesignCore's contribution to the platform is exactly its existing path, hardened: validated, provenance-carrying geometry — plus an MCP surface so peers can invoke it. Scanning enters ODC only as mesh→SDF import (a boundary with validation and scale provenance), not as a capture pipeline. Electronics, sourcing, inventory, and marketplace features live with peers; requests for them here are routed to ROADMAP.md's "Not ever" or to the owning repo.

**Consequences.** ARCHITECTURE.md's scope statement stands. The near-term deliverable is the thin thread (ROADMAP.md "Now"): registry component → generated enclosure → validated artifact + provenance → printed via AdvancedStudio, which exercises ODC's path and two peer seams without new domains. Cross-repo contracts (registry schema, provenance references from BINGO fabrication evidence to ODC ledger records, MCP tool surface) become explicit interface work rather than internal design. The wiki carries the platform-wide picture so the narrow scope doesn't lose it.

---

## ADR-0008 — PicoGK via NuGet; ShapeKernel remains a source submodule

**Date:** 2026-08-15
**Status:** accepted. Supersedes the consumption *mechanics* of ADR-0001 (submodules + separate native runtime install); the substance of ADR-0001 — build on PicoGK/ShapeKernel, pin versions, never fork casually — stands.

**Context.** ADR-0001 was written against PicoGK's installer era. Since v1.7.7.5 LEAP 71 ships PicoGK on NuGet with the runtime bundled and has retired the installer; current package is 2.3.0, with 2.2.0 the release-noted, widely-pulled version (runtime 26.2, OpenVDB v13). The ShapeKernel is not on NuGet; its latest tag is ShapeKernel-v2.1.0 (`313d676`).

**Decision.** PicoGK is consumed as a NuGet `PackageReference` pinned exactly to `[2.2.0]`. LEAP71_ShapeKernel is a git submodule at `external/LEAP71_ShapeKernel`, pinned to tag `ShapeKernel-v2.1.0`, compiled into the OpenDesignCore assembly as sources. Target framework `net9.0` — PicoGK 2.2.0's declared target (NU1202 rejects net8.0; the ".NET 8" documentation was 1.x-era). Built with SDK 10.0.301. Upgrades of any of these remain deliberate single commits.

**Consequences (ADR-0008).** "Clone and go" is back — no separate runtime install, and CI needs only the .NET SDK. Determinism inputs recorded in provenance become: PicoGK package version, ShapeKernel tag, TFM, tool version, commit. 2.3.0 exists and is not adopted yet; adopting it is its own tested commit.

**Amendment, 2026-08-21 — "only the .NET SDK" was incomplete: it also needs a supported platform.** PicoGK 2.2.0 bundles native runtimes for `win-x64` and `osx-arm64` only; there is no `linux-x64` payload in the package (`obj/project.assets.json`, `runtimeTargets`). Linux restores and builds the managed assembly fine and then fails at the first `new Library(...)`, so a Linux CI job would go green on build and red on test for a reason the log does not explain. CI therefore runs on `windows-latest`, matching the development machine; a macOS arm64 runner would serve equally. The consequence worth naming: **a contributor on Linux cannot run the test suite at all**, only `dotnet build` and `dotnet format`. That is a property of the pinned dependency, not a choice this project made, and it is the cost side of ADR-0001's "geometry is solved and we inherit its constraints wholesale". Revisit if LEAP 71 ships a Linux runtime.

---

## ADR-0009 — What the MCP surface may execute, and what it may only propose

**Date:** 2026-08-15
**Status:** accepted

**Context.** ADR-0007 makes OpenDesignCore an engine among peers, composed over MCP. That requires an inbound surface. The ecosystem's existing convention, arrived at independently by AdvancedStudio and ClawCam, is *reads execute, writes propose* — a rule written for tools that move physical hardware. OpenDesignCore's "writes" are not of that kind: a model run's only effects are a content-addressed artifact and an append-only ledger row, both reproducible from recorded inputs (ADR-0003). Applying the physical-safety rule literally would make the engine useless over MCP; ignoring it would let an agent reach a printer.

**Options considered.**
1. Reads only — safe, and leaves the engine unreachable for the composition ADR-0007 depends on.
2. Everything executes, including handoff — an agent could stage and start fabrication with no human in the loop.
3. Draw the line at the store boundary: effects confined to OpenDesignCore's own content-addressed stores execute; anything reaching beyond them stops at a proposal.

**Decision.** Option 3.

- **Execute:** `list_models`, `list_parts`, `list_runs`, `get_provenance`, `run_enclosure`, `run_cradle`. Model runs are deterministic and idempotent by content hash — rerunning one costs CPU and produces the identical artifact.
- **Propose only:** `handoff_to_studio` stages an artifact and may propose a print to AdvancedStudio, which registers it in the studio's own approval queue. **This server exposes no approval tool and must not acquire one** — a test asserts no tool name contains "approve" or "confirm". The human approves in the fabricator's interface, where the machine is.
- **Resource guards** (`McpGuard`), because a caller that can name a voxel size can exhaust the machine: voxel size clamped to [0.05 mm, 5 mm] by *refusal*, a 2×10⁹ voxel budget on implied volume, and path arguments confined to the working root. Refusals, never silent clamping — the same rule as the geometry layer.

**Consequences.** Agents can drive design end to end and stop exactly where a human must decide. The CLI remains the unguarded path for deliberate local work (finer voxels, larger volumes) — the guards are about untrusted callers, not about capability. A second inbound surface now needs keeping in step with the CLI; both call the same executors, so drift means duplicated argument parsing, not duplicated behaviour. If a future peer needs to *approve* fabrication programmatically, that is a new ADR and a different threat model, not a quiet addition here.

---

## ADR-0010 — Provenance records the artifact's own dimensions (schema 0.2)

**Date:** 2026-08-16
**Status:** accepted

**Context.** The sidecar written since ADR-0003 describes what went *into* a run — the part envelope, the scan hash, the clearance and wall — plus versions, commit, and the artifact's media type and hash. It never recorded how big the artifact itself is.

That gap surfaced while wiring OpenBuildCore's machine capability check (its ADR-0005) to consume ODC output. The obvious question a fabricator asks of a design — *does this fit the build volume* — could not be answered from the provenance record. A consumer had to fetch the STL and re-parse it, which defeats the purpose of a record that travels with the artifact, and forces every downstream peer to carry a mesh parser to answer a question about dimensions.

An artifact record that omits the artifact's dimensions is incomplete on its own terms, independent of who wanted to read it.

**Decision.** The `artifact` block gains `bbox_mm` (`x`/`y`/`z`) and `volume_cubic_mm`. Schema string bumps `odc/provenance/0.1` → `0.2`.

The two figures are measured differently and are deliberately not interchangeable:

- **Extents** come from the mesh's axis-aligned bounding box at float precision. They never pass through the voxel grid, so voxel size does not bound them. This is the same distinction the scan-compare significance bug got backwards, recorded here so the next reader does not have to rediscover it.
- **Volume** comes from the voxel field and *is* bounded by the voxel size, which the sidecar already records alongside.

Extents are axis-aligned in the artifact's own frame. Nothing here presumes an orientation; a consumer deciding whether the part fits a machine may rotate it, and that is the consumer's business.

Lengths are unit-keyed fixed-precision strings, like every other length in the record — floats stay banned from canonical JSON because their text form is not stable across languages and this record is hash-compared against Python.

**Consequences.** Every sidecar hash changes, and so does the schema string that consumers key on. The change is additive — a 0.1 reader encountering a 0.2 record finds every field it knew still present — but the version bump is not cosmetic: a consumer that *requires* `bbox_mm` must be able to distinguish a record that has it from one that does not, and refuse rather than guess. OpenBuildCore's `can-print --from-sidecar` does exactly that, and says which schema it found.

Byte-identity across reruns survives: `CalculateProperties` is deterministic on the same voxel field, verified by the existing rerun test rather than assumed. Cross-machine identity remains unproven, as it was before.

Artifacts produced under 0.1 are not rewritten. They are immutable and content-addressed; re-running the same inputs produces a 0.2 record, and the old one stays valid as a description of what it described.

---

## ADR-0011 — A measurement may propose a compensation; deciding whether it *should* is this engine's job, computing it is not

**Date:** 2026-08-16
**Status:** accepted

**Context.** `compare` closed the verification loop as far as a number: design → print → scan → measured per-axis deviation, recorded and hash-chained. The last step, turning that into a slicer setting so the next print is closer, was a human retyping a figure — if they remembered which print it came from.

Both halves of the arithmetic already existed. AdvancedStudio's `calibration/calculators.py` converts a nominal/measured pair into an OrcaSlicer shrinkage percentage and is calibrated against its process research. This repo had the measurement. Nothing connected them.

**The temptation, and why it was refused.** The obvious move is to compute the percentage here — it is three lines — and post a setting. That would give the platform two implementations of one formula, in two languages, free to drift, with no test that could ever catch the drift because neither side would know the other existed.

**Decision.** The split follows what each side actually knows.

- **This engine decides whether a compensation is defensible at all.** That is a property of the measurement, and only the measurement can answer it. `compensate` reads a recorded comparison and returns a verdict.
- **AdvancedStudio computes the setting**, from the nominal/measured pair this engine hands it. Slicer semantics belong with the slicer's tooling.
- **Applying it is a proposal, never an execution** (ADR-0009). A profile change reaches beyond this engine's stores.

Three refusals, each a real failure mode:

| Verdict | Why |
|---|---|
| `WithinScannerNoise` | The deviation is inside the declared scanner accuracy. A setting from it compensates for the instrument, not the print. |
| `AccuracyUndeclared` | No accuracy declared, so signal cannot be separated from instrument. Unknown, never "small enough to ignore". |
| `AxesDisagree` | X and Y differ by more than the caller's declared threshold. Orca's Shrinkage (XY) is one number; their mean is wrong on both axes. |

**Z is never folded into the XY figure.** Orca's Shrinkage (XY) applies to X and Y only, and Z deviation has different causes — layer squish, first-layer offset. Averaging three axes into one number would be silently wrong in a way nobody would notice, and a test pins that the XY pair is the mean of x and y alone.

**`--max-axis-spread-pct` is declared by the caller and has no default.** How much X/Y disagreement still permits one factor is a process judgement, not a constant. While adding this, `compare`'s advisory output was found to be making that same judgement against a hard-coded `0.5`, which is exactly the constant-instead-of-parameter the tolerance rule forbids; it now defers to `compensate` instead of deciding quietly.

**Consequences.** The loop closes: a scan can change what the slicer does next time, and the stored value carries `odc-comparison:<sha256>` so it can be traced back. A compensation in a profile stops being indistinguishable from a number somebody typed.

The system will often refuse. That is the intended behaviour — most comparisons should not become settings — and the refusal names itself rather than returning a number nobody should use.

**Validated on synthetic prints only.** Every refusal path is proven and the wire is proven end to end against a running studio, but whether the resulting percentage makes the next print better needs a real print and a real scan. The plumbing is correct; the water is unvalidated.

---

## ADR-0012 — An uncalibrated machine may be measured, but its measurement may not become a material profile

**Date:** 2026-08-21
**Status:** accepted

**Context.** ADR-0011 closed the loop as far as a proposal, and noted the water was unvalidated. It got validated. Benji printed the calibration block on a Creality K2 Plus in PLA and measured it with calipers: X 39.90 against a nominal 40, Y 60.50 against 60, Z-span 25.05 against 25.

He had deliberately not calibrated the machine first, and said so afterwards: the point was to see what the pipeline would do.

What it did was almost produce a number. X was 0.25% under, Y was 0.83% over. Both are in the range a shrinkage figure lives in, and averaged into the single XY value OrcaSlicer wants they read as "PLA shrinks about 0.29%" — plausible, close to published PLA figures, and wrong. No material contracts on one in-plane axis and expands on the other; shrinkage is a bulk property and moves X and Y the same way. The Y axis was mechanically short by 0.83%, and that fault was about to be recorded as a property of a plastic, in a profile that shapes every subsequent print in that plastic, carrying a comparison hash that made it look sourced rather than guessed.

**Two changes, and only one of them is this ADR.**

The first is diagnosis: a `MachineScaleError` verdict fires when the two in-plane axes deviate in opposite directions by more than the instrument's accuracy, because no material or flow effect does that. It names the axis and gives the correction (`scale Y by 0.99174`; on Klipper, multiply that axis's `rotation_distance`). That is a verdict like the others and needs no policy.

The second is the policy, and it is the harder question: **what should the tooling do when it cannot tell whether the machine is any good?** The K2's fault was large enough to spot. A machine 0.15% out on one axis would produce a perfectly self-consistent, entirely wrong shrinkage figure and no verdict would fire.

**Options.**

1. *Refuse to measure an uncalibrated machine.* Rejected, and not narrowly. Measuring is how you discover a machine is uncalibrated — this whole finding came from measuring one. A tool that requires calibration before it will measure cannot be used to calibrate.
2. *Warn and proceed.* Rejected. The warning appears once, in a terminal, next to a number that is about to be stored permanently. The stored value outlives the warning by years.
3. *Gate the write, not the read.* Accepted.

**Decision.** ADR-0009's line — effects confined to a peer's own content-addressed stores execute; anything reaching beyond proposes — applies one step earlier here.

- `compensate` computes and records the comparison **regardless of machine calibration state**. The record is a measurement, and measurements are always allowed. Nothing about this path changed.
- `compensate --propose-to-profile` **additionally requires** `--machines` and `--machine-id`, reads the machine's `axis_calibration` from the OpenBuildCore registry, and refuses to propose unless all three axes are verified.

Three states, deliberately not two:

| State | Meaning | Proposal |
|---|---|---|
| `Unknown` | no `axis_calibration` recorded | refused |
| `Partial` | some axes verified, others not | refused, naming the missing ones |
| `Verified` | x, y and z each carry a date, a residual and a method | permitted |

`Unknown` is not `Verified` with a zero residual, and the type refuses to let the two collapse: `WorstResidualPct` is `null`, never `0.0`. A zero residual claims a machine was measured and found perfect; unknown claims nothing at all. Conflating them is precisely the failure this ADR exists to prevent, expressed in a nullable field.

`Partial` is refused rather than waved through because a part is measured on all three axes, and the unverified axis is exactly where a fault hides. It hid there here — X and Z would have looked fine.

**Where the state lives.** OpenBuildCore owns machines, so `axis_calibration` is a field on its machine schema, its validator refuses a half-made claim (a date with no residual, a residual with no method), and this engine only reads the registry. That is the mirror of OpenBuildCore reading this engine's provenance sidecars, and neither repo gained a dependency on the other's code.

**Ordering is load-bearing and pinned by a test.** The calibration refusal fires before the studio is contacted. If the network call came first, an uncalibrated machine on a working studio would put the number in front of a human who has no way to see the axes underneath it, and dashboards get approved.

**Consequences.** Anyone using this loop must calibrate before a compensation can be stored — which is the intended outcome, stated as an error message that names what to do rather than as documentation. The measurement path is unaffected, so the diagnostic route stays open: print the block, measure it, read the verdict, fix the axis, record the residual, measure again.

The proposal now carries the machine and its worst residual in the origin string alongside the material, so a reader six months later can ask "was the printer any good when this was taken?" without taking it on trust.

**Cost, stated plainly.** Three extra CLI arguments on the propose path, and a cross-repo file read. The alternative was a machine fault permanently filed as a material property.

---

## ADR-0013 — A filament reference is an identity, never a source of values

**Date:** 2026-08-21
**Status:** accepted

**Context.** `--material pla` is a label, not an identity. Two spools from two brands are both `pla`, so a compensation measured on one is eligible for the other, and `compensate`'s own caveat has said the quiet part since ADR-0011: *"shrinkage varies by spool, geometry and cooling."* `CompareRun` says it too, in a comment: *"compensation is per material and per spool — the ADRs said so from the start, and nothing enforced it."* ADR-0012 then closed the machine half of that sentence and left the spool half open. The profile-key gate compares free text against free text; `pla` measured, `pla` targeted, two different filaments, no complaint.

The Open Filament Database (Open Filament Collective, facilitated by SimplyPrint; MIT; static REST API; dated dataset releases) catalogues brands → materials → product lines → colour variants → spool sizes → stores, and its UUIDs are what the OpenPrintTag NFC spec consumes — which is also what AdvancedStudio's CFS/RFID material tracking reads. There is a ready-made, permissively licensed identifier for exactly the thing this pipeline could not name.

**The trap this ADR mainly exists to close.** The catalogue is easy to mistake for a source of engineering values — the awesome-3d-printing entry that surfaced it describes it as carrying "print settings". It does not. It has no shrinkage figures, no dimensional tolerances, no mechanical properties. Adopting it *as a data source* would be the invented-material-property failure the project rules forbid, wearing a citation. So the rule is stated before the mechanism:

> **No number reached through a filament reference may enter a model run.** Values come from `data/` with a citation to a vendor TDS or to a measurement. A reference identifies which spool; it never says anything about how that spool behaves.

In particular, this does **not** discharge the `TODO(source)` on `data/materials/pla-generic.json`. Only a vendor TDS or a measurement does that, and it remains open.

**Options considered.**

1. *Require a reference on every measurement.* Rejected. Most real filament is uncatalogued, and refusing to record a measurement for want of a catalogue entry blocks work without making any measurement truer.
2. *Store the vendor and colour as free text.* Rejected. It is the same problem one layer down — free text does not join to anything, and two spellings of one spool are two spools.
3. *Resolve references against the catalogue API at run time.* Rejected outright. That puts a network call inside a deterministic run and makes a recorded result depend on a remote service's availability and current contents. A reference is an opaque recorded string; ODC never dereferences it.
4. *Record an optional, pinned, shape-validated reference.* Accepted.

**Decision.** A `FilamentRef` is `catalog:dataset_version:path[#uuid]`, canonical text form, validated for shape and refused loudly when malformed:

- `catalog` must be `open-filament-database` — the only one understood. An unknown catalogue is refused rather than stored uninterpreted, because a reference nothing can resolve looks like provenance and is not.
- `dataset_version` is **required**, and `latest` / `main` / `HEAD` are refused by name. The catalogue renames and retires entries; a reference without the release it was read from is a lookup that used to work.
- `path` must be a full variant path (`brands/{b}/materials/{M}/filaments/{f}/variants/{v}`). A brand- or material-level path is refused: shrinkage varies between colours of one product line, so the variant is the unit that matters.
- `uuid` is optional and must parse as a UUID if given.

It is optional everywhere it appears — on `data/` material entries, and on `compare --filament-ref`. Where `--material` is **required** and its absence refuses the compensation, an absent reference does not. The asymmetry is deliberate and pinned by a test: without a material the compensation cannot be filed at all, whereas without a spool it is merely a compensation for "some PLA" — which is what every compensation was before this ADR.

**Schema bumps.** `odc/comparison/0.1` → `0.2` and `odc/compensation/0.1` → `0.2`, each gaining `inputs.filament_ref`, recorded as `"undeclared"` when absent so a reader can tell "no spool named" from "field missing". Records written under `0.1` keep loading and read as undeclared; a schema bump that silently invalidated history would be worse than the gap it closed.

**`odc/provenance/0.2` is deliberately not bumped.** Design provenance carries no material and gains no field here — a design is not printed in anything at design time; the material is a property of the print. Adding a filament reference to `EnclosureRun`, `CradleRun` or `CalibrationBlockRun` would be a field nothing sets and nothing reads. It also means the peers consuming provenance sidecars — OpenBuildCore's capability check, the BINGO contract — see no change at all.

**Consequences.** A compensation can now name the spool it came from, and the studio proposal's origin string carries it, so a profile keyed `pla` that was measured on one specific spool says so to whoever reads it next. The profile-key gate is unchanged and still matches on material — tightening it to demand a matching reference would refuse every uncatalogued spool, which is most of them. That gate stays a material check; the reference is evidence for a human, not another automated refusal. Revisit if the catalogue's coverage ever makes the stricter gate reasonable.

---

## ADR-0014 — A mechanism engine is a peer domain, and ADR-0007's peer list is a snapshot rather than a closed set

**Date:** 2026-08-23
**Status:** accepted
**Supersedes:** ADR-0007, in part — its peer *enumeration* only. ADR-0007's decision (this repo is an engine among peers, not the umbrella) is untouched and is the reason this ADR is scoped as narrowly as it is.

**Context.** ADR-0007 enumerated the platform's peers on 2026-08-15 as *electronics engine (kernel choice pending its own ADR), registry and planning (Oh-Ben-Claw), local fabrication (AdvancedStudio), networked fabrication and settlement (Project BINGO), device codegen (Accelerapp)*. That list was accurate the day it was written and has not been true for some time: the electronics engine became OpenCircuitCore, "registry and planning" split into OpenPartsCore and OpenBuildCore, and a mechanism repo appeared on 2026-08-22.

The staleness was not inert. Two repos cited that enumeration as granting them peer status when it does not name them — OpenBuildCore's README ("Fourth peer in the platform (OpenDesignCore ADR-0007)") and ClawBot's, which said the same thing with "Fifth". ClawBot [reported this against itself](https://github.com/thewriterben/OpenDesignCore/issues/15) and stopped citing ADR-0007; OpenBuildCore's was corrected separately. A citation pointing at a document that does not say the thing being cited is the failure this platform's whole discipline exists to prevent, and it had propagated twice from one stale list.

**The substantive question, separated from the bookkeeping.** Whether a *mechanism* — links, joints, actuators, and what they can reach — is a peer domain at all, or a machine kind belonging inside an existing repo. The argument that it is distinct is about data shape rather than territory:

- **Reachability is not containment.** OpenBuildCore answers "can this be made" with `envelope_mm` and axis-aligned containment. For a serial chain past two joints the reachable set is non-convex, frequently holed at the base, and can be disconnected across configuration branches. Every box either claims points the arm cannot reach or disclaims points it can, with no conservative choice available — so the containment test is not merely imprecise here, it is structurally the wrong predicate.
- **Capacity is a function of pose.** A printer's material list does not change when the gantry moves; an arm's usable payload falls with extension, because shoulder torque is force times moment arm. A scalar `payload_kg` is true at one configuration and misleading everywhere else.
- **A mechanism straddles PD-2.** A design is shareable reference data; the robot on your bench is owned state; one partway built is an OpenBuildCore project. Nothing else in the ecosystem sits on both sides of that line.
- **It is not this repo either.** ODC holds deterministic geometry with provenance. A provenance record describes an artifact, not an articulation — there is no joint, no limit, and no actuator in it. Absorbing articulation here is precisely the domain expansion ADR-0007 refused.

**Options considered.**

1. *Decline; a mechanism is a machine kind inside OpenBuildCore.* Rejected on the first bullet above. The containment predicate is wrong in kind, and a wrong predicate that returns a confident boolean is worse than a missing feature.
2. *Fold articulation into OpenDesignCore as a geometry variant.* Rejected. ADR-0007 exists to stop this repo absorbing domains, and reach and torque are not geometry.
3. *Re-enumerate the platform's full peer set here, correcting every drift at once.* **Rejected, and the reason matters more than the option.** OpenPartsCore and OpenCircuitCore assert peer status nowhere — not in their READMEs, not in their ADRs. Deciding it for them in this file would be this repo acting as the umbrella that ADR-0007 explicitly refused to be. OpenBuildCore shows the correct shape: its *own* ADR-0001 decides it is a fourth peer, "under ADR-0007's engine-among-peers shape". The shape is this repo's to publish; occupying it is each repo's own decision.
4. *Accept a mechanism peer domain, and say plainly what ADR-0007's list is.* Accepted.

**Decision.** Two parts, deliberately small.

1. **A mechanism engine is a peer domain**, on the data-shape argument above. ClawBot occupies it. It may cite this ADR for that status, which is what it has lacked since it was created.
2. **ADR-0007's peer enumeration is a snapshot of 2026-08-15 and is not a closed set.** It may not be cited as granting or withholding peer status to any repo. A repo's peer status is decided in that repo's own ADRs, under the shape ADR-0007 established.

What this does **not** decide: the status of OpenPartsCore, OpenCircuitCore, or any other repo — see option 3. Nor does it discharge ClawBot's own ADR-0001 admission that it is "justified by a data shape rather than by demand: nothing is asking for it yet". That remains true and remains recorded. This ADR says the domain is a peer domain, not that anything currently requires one.

**Consequences.** ClawBot's README may state its status with a citation that supports it, replacing "by the shape of the argument in ADR-0001, not by anyone's blessing". The wiki's `[[clawbot]]` entity page and `ecosystem-map` drop "peer status unsettled" and cite this ADR instead; `wiki/log.md` records the change, per the ingest rule. OpenBuildCore's README needs no further change — it already cites its own ADR-0001, which is the pattern this ADR endorses.

The two citation defects this ADR grew out of were both caught by reading, not by tooling. Nothing here prevents the third: no check verifies that a README's cross-repo citation says what the README claims it says, and after this ADR there is one more document worth citing incorrectly.

---

## ADR-0015 — Measurement uncertainty is max(instrument, observed spread), derived per axis from the readings themselves

**Date:** 2026-08-24
**Status:** accepted

**Context.** `compare` judges significance against `--instrument-accuracy-mm`, a single declared figure used as the uncertainty on every axis. That is only correct when the surface being measured is flatter than the instrument, and a printed face is not a datum plane. On the first real run of the calibration loop, three readings per surface showed vertical walls spreading 0.02–0.03 mm and a layer-20 shelf 0.01 mm — at or under the caliper's 0.02 mm — while the final-layer top face spread **0.08 mm**, four times the declared accuracy, on exactly the surface the Z scale is read from. With the effect at 0.10 mm and the noise at 0.08, the available Z answers ran from −0.43 % to zero depending on where the jaw landed. Two figures were entered and both withdrawn (one a transcription error, one never measured); a human had to notice the top face was not flat.

The failure is structural, not procedural: a declared accuracy that is too optimistic does not understate error bars, it **reports an unresolvable deviation as a real finding** — and CALIBRATE-FIRST.md had asked for three readings per dimension since it was written, while the tool accepted one number and threw the other two away. The information that would have refused the figure was collected and then discarded at the command line.

**Options considered.**

1. *Tell the caller to declare a larger accuracy when the surface is rough.* Rejected. That makes the uncertainty a judgement call entered as if it were an instrument property, in a field whose name says instrument — an invented number wearing a declared one's clothes.
2. *Refuse any axis read off a top face.* Rejected. The tool cannot know which face a reading came from, and the shelf top measured *better* than the vertical walls — the surface, not the orientation, is the variable.
3. *Take the readings and derive the spread per axis.* Accepted.

**Decision.** `--measured` accepts comma-separated repeated readings per dimension (`40.00,40.02,40.01x…`); a single reading per dimension remains valid. Per axis:

- the **measured value is the mean** of that axis's readings — matching the flow-calibration procedure the docs already state;
- the **uncertainty is `max(declared instrument accuracy, observed spread)`**, spread being max − min. Repeated readings can widen an axis's uncertainty; they can never narrow it below the declared accuracy, because tight repetition proves repeatability, not accuracy;
- the **z-span's spread is the sum of the two face spreads** — the span is a difference, and the extreme answers pair opposite extremes;
- significance is judged **per axis** against that axis's uncertainty, in the report's `WithinScanAccuracy` and in every verdict gate. A rough top face widens Z's uncertainty without saying anything about X and Y, which come off vertical walls — the same scoping rule that keeps Z out of the XY verdict.

**The spread is recorded, not consumed.** Comparison schema `odc/comparison/0.2` → `0.3`: each axis carries `observed_spread_mm` and `uncertainty_mm`, and the manual path records `raw_readings_mm` keyed by the label each set was taken under. `compensate` re-reads the spread from the stored record, so the widened uncertainty survives into a verdict made later — dropping it at either hop would launder an unresolvable deviation back into a finding. Records written under 0.1/0.2 still load; an absent spread reads as zero, which is what a single reading honestly was.

**A tension resolved rather than hidden.** CALIBRATE-FIRST.md advised "take three readings and keep the smallest", on the sound ground that seam and blob contamination only ever inflates an external dimension. That was the right rule for a tool that took one number. It is superseded by giving the tool all three: a contaminated reading now shows up as spread and widens the uncertainty toward refusal, which is the correct outcome — the remedy for a blob under the jaw is re-measuring off the seam, not laundering the set through a statistic. The doc keeps the physics and drops the pre-minimising.

**Consequences.** Run against the real Z case, the tool now refuses the figure by itself: mean deviation 0.045 mm under a 0.09 mm spread is not a finding, and the refusal names the surface rather than the instrument so the reader knows the fix is a flatter top face, not a better caliper. Verdict messages distinguish which side of the max() limited each axis. The single-reading path is unchanged in behaviour and honestly weaker, and the CLI says so. What this does not do: detect a surface problem from one reading (nothing can), or validate that the mean of three readings is closer to truth than any of them — the readings are the caller's; the tool's contract is only that it no longer discards them.

## ADR-0016 — Parts are read from OpenPartsCore, and the private parts store is deleted

**Date:** 2026-09-06
**Status:** accepted

**Context.** ROADMAP.md has carried "Registry schema contract with Oh-Ben-Claw (consume, don't fork)" unchecked since 2026-08-15, and ADR-0007's thin thread is *"one component from the registry → an enclosure generated around its dimensions"*. What actually existed was `data/parts/esp32-s3-wroom-1.json`: one module, hand-copied from a datasheet into a private store, because the registry the thread is named after held no dimensions at all. OpenPartsCore ADR-0006 (2026-09-06) fixed that half — an optional `envelope_mm` with a citation of its own, preserved across re-ingest, with the first two entries sourced. This ADR is the other half.

A survey written the day before (`Local LLM Deployment/ODC-INTEGRATION.md`) proposed this as "data only, no engine change". Reading the files showed otherwise, and two of its premises were wrong: the walkthrough's run 10 was a *scan-cradle* fitted to OpenCircuitCore's board STL, not an enclosure around a registry envelope; and that board (`env-monitor`, generic DevKit) is not the benchtop body's board (a FireBeetle 2). The envelope path and the geometry path are different threads, and until today only the second had ever run against a real board.

**Options.**
1. *Keep `data/parts/` and add a `part_ref` to OpenPartsCore as identity only* (the ADR-0013 shape). Cheap, but the values are still hand-copied here, the ROADMAP item stays open, and every new board means a second copy of a number that already has a home.
2. *Read OpenPartsCore at run time; keep `data/parts/` for anything not yet upstream.* Two sources for one kind of fact, and the private one would be the easier place to put a number, which is the wrong incentive.
3. *Read OpenPartsCore at run time; delete `data/parts/`; refuse its reappearance.* Accepted.

**Decision.** `DataStore.LoadAll(dataDir, partsRegistryDir)` reads parts from an OpenPartsCore checkout — `ODC_OPENPARTSCORE`, else the sibling `../OpenPartsCore` — through `PartsRegistry`. Only entries with a cited `envelope_mm` become a `PartEntry`; the rest are returned as `UnofferedParts` with the reason, and `list_parts` shows both under `offered` / `not_offered`, so "the registry knows this board but not its size" is a visible state rather than an absent row. The reader is strict about the envelope object (unknown keys, non-positive or missing axes, and a missing citation of its own all fail naming the entry) and lenient about everything else in the entry, which is the registry's own CI's job. `data/parts/` is deleted, and a `data/parts/` directory found on load is an error that says why. Materials stay here: they are process constraints this engine measures, not catalogue facts.

**Provenance.** `enclosure-shell` sidecars move to `odc/provenance/0.3`. `inputs.part_registry` records the checkout's commit — read from its `.git` directly, no git on PATH required, and `"unresolved"` when there is none to read — and the entry file's SHA-256, which pins the content even when the commit cannot or the tree is dirty. `part_source_citation` becomes `part_envelope_citation`: it is the envelope's citation that is recorded, not the entry's, because for an ingested board the entry's citation is a registry that never held the number. A test pins each of these against a fixture registry, and the run against the real sibling checks the commit is a real hash.

**Consequences.** `run-enclosure` and the MCP `run_enclosure` now depend on a sibling checkout at run time, and fail loudly, naming the env var, when it is absent — a runtime dependency on a peer, which is the coupling ADR-0007 accepted by choosing composition over one program. Tests that read the real registry return early when it is not there (the repo's existing convention for peer reads; CI does not clone peers) and everything else runs against a fixture. The first real run under this ADR is ledger run 40: an enclosure around `boards/dfrobot-firebeetle2-esp32s3`, a board whose envelope nobody typed — 30.90 × 66.87 × 11.15 mm from a 25.5 × 61.47 × 8.45 mm bounding box of DFRobot's own STEP model, with the model's hash, the registry commit and the file hash all in the sidecar. Two boards is a thin registry; that is the honest count, and each further one costs a drawing, not a guess.

## ADR-0017 — A second kernel's measurement is evidence a sidecar can carry

**Date:** 2026-09-07
**Status:** accepted

**Context.** Every number in a provenance sidecar — `artifact.bbox_mm`, `volume_cubic_mm` — is measured by the kernel that produced the artifact. ADR-0003 says numerical claims need evidence and ADR-0009's consumers (OpenBuildCore's `can_print_design`) read those numbers as fact, but nothing has ever checked them against anything PicoGK did not compute. A bug in `ArtifactGeometry.OMeasure` would propagate to every machine-capability verdict downstream and be invisible, because the only thing that could catch it is the thing that has it.

Blender 5.2 is installed on the development machine (BLENDER-INTEGRATION.md, Local LLM Deployment). Its mesh kernel shares no code with PicoGK or OpenVDB. Run headless over run 50's STL it reported 30.90 × 66.87 × 11.15 mm — the sidecar's numbers to the hundredth — and 8854.27 mm³ against the sidecar's 8843.81, a +0.12 % difference of the size a voxel shell at 0.3 mm should produce. Two independent kernels agreeing is the evidence a sidecar's own numbers cannot be.

**Options.**
1. *Trust the producing kernel; keep unit tests.* The tests check the code against itself and a few analytic cases. They cannot catch a systematic measurement error the way a second kernel can.
2. *Cross-check inside PicoGK by another route (e.g. mesh-side vs voxel-side).* Same kernel, same authors, same assumptions.
3. *An optional, recorded cross-check by an external kernel, never on the critical path.* Accepted.

**Decision.** `verify-artifact --run N --volume-tol-pct P [--bbox-tol-mm T] [--blender <exe>]`. Blender runs headless over the run's STL with an embedded, hashed measure script; extents, signed volume and manifoldness are laid beside the sidecar's claims and each is judged against an explicit tolerance. The result is a content-addressed `odc/verification/0.1` record in the artifact store and a ledger row under model `blender-crosscheck/0.1`, whose `Passed` means *the sidecar describes the artifact within the stated tolerances and the mesh is watertight* — never that the part is fit for anything.

Tolerances are parameters, not constants (project rule). The bounding-box tolerance defaults to **2 × the run's voxel size**, the bound the repo's own enclosure tests already use for a voxel mesh's extents, and the record states that derivation. The volume tolerance has **no default**: two kernels count volume differently by roughly a voxel shell, and how much of that to accept is a decision the record should show someone made.

Blender absent means **skipped, exit 3, nothing written** — the same rule as a peer test with no peer checkout. A skip is not a pass and it is not silent.

**Consequences.** Blender becomes an optional external process (DEPENDENCIES.md: GPL-3.0, invoked over stdio, never linked; its licence does not reach this repo). The artifact is never modified; disagreement is a record, and what to do about it is a person's call. Only STL artifacts are covered — the cross-check reads what Blender can import. It is not on the MCP surface: this is a human-run check on a run that already exists, and an agent that could verify its own output with a tolerance it chose would be laundering, not verifying. The first record is verification 52 over run 50, all claims agreeing. Thumbnails (BLENDER-INTEGRATION.md option 2) are deliberately not part of this: a record of numbers and a record of pixels are different claims.

## ADR-0018 — The agent may ask for a second opinion; it may not set the bar

**Date:** 2026-09-07
**Status:** accepted — amends ADR-0017's "not on the MCP surface"

**Context.** ADR-0017 kept `verify-artifact` off the MCP surface with one argument: an agent that could verify its own output with a tolerance it chose would be laundering, not verifying. The argument is about *who picks the tolerance*, not about who presses the button. Meanwhile the local model now drives `list_parts → run_enclosure` end to end (ODC-INTEGRATION.md option 5, Oh-Ben-Claw `[[mcp.servers]]`), and every artifact it produces goes unverified unless a person remembers to run the CLI afterwards. A cross-check that exists only when someone remembers is a cross-check most artifacts never get.

The precondition for spawning Blender from an MCP host is the stdin fix (PR #25): a stdio server's child inherits the JSON-RPC pipe and blocks. Without that this ADR could not be honest about working.

**Options.**

1. Leave it off. ADR-0017 stands; verification stays a human act. Costs every unattended artifact its record.
2. Expose `verify_artifact(runId)` with tolerances as parameters. Exactly the laundering ADR-0017 refused: the agent retries with a looser number until `passed` is true, and the record shows a tolerance nobody with judgement chose.
3. Expose `verify_artifact(runId)` with tolerances **pinned by the operator** in the server's environment (`ODC_VERIFY_VOLUME_TOL_PCT`, required; `ODC_VERIFY_BBOX_TOL_MM`, optional, else 2 × voxel as before). The agent can request the measurement; it cannot vary the bar, and the record says so in `volume_tolerance_source` / `bbox_tolerance_source`: *declared by the operator (server environment); not choosable by the caller*.
4. Read-only: list existing verification records over MCP. Honest, useless for unattended runs.

**Decision.** Option 3. The tool lives in its own type (`OdcVerifyTools`) and is **registered only when** `ODC_BLENDER` points at a file and `ODC_VERIFY_VOLUME_TOL_PCT` parses — the server logs why when it does not. An agent is never shown a tool it cannot call, and "not configured" is an absent tool rather than a tool that refuses, so it costs nothing in every model's context. There is still no default volume tolerance anywhere: an unset operator value means no tool, not a guessed number.

The tool's signature is `(runId)` and a test asserts exactly that, so adding a tolerance parameter later is a deliberate act against a named alarm, not a drift.

**Consequences (ADR-0018).** ADR-0017's reasoning survives intact — the tolerance is chosen by a person with the machine in front of them, once, and recorded per run as theirs. What changes is that the person chooses it in advance rather than at each run. A failed check is recorded and returned with the instruction not to re-run for a different answer; the inputs are the same and so would be the record. The CLI path is unchanged and still says *declared by the caller*. Operators who do not want agents triggering Blender at all leave the variable unset. Thumbnails remain out of scope, for ADR-0017's reason.


## ADR-0019 — Units are not scale: a mesh declares its origin

**Date:** 2026-09-11
**Status:** accepted

**Context.** `ScanImport` has refused `AUTO` units since 2026-08-15, on the argument that an STL carries no reliable unit truth and a silently mis-scaled scan is the bug class the unit rules exist to prevent. That argument is right and incomplete. Declaring `mm` says *how to read the numbers in the file*. It says nothing about whether those numbers were ever tied to a physical size.

For a CAD export they always were — `kicad-cli pcb export stl` writes millimetres because the board is millimetres. For a photogrammetric reconstruction they were not: structure-from-motion recovers shape only up to an unknown similarity transform, so absolute size enters solely from a known reference in the scene. A caller could therefore pass `--units mm` on a photogrammetry mesh, get a run, and get a sidecar faithfully recording a declaration that means less than it looks like. Absence disguised as a value, arriving through the one door built to stop it.

The mechanism was already half-present and this is what made it easy to miss: `OImport` takes an `fPostScale`, and its own doc comment already says *"Scale and units are provenance fields."* Nothing recorded where that scale came from, so `fPostScale = 1.0` on an unscaled reconstruction is an identity scaling indistinguishable in the record from a deliberate one.

The damage is not evenly spread. A wrong cradle does not fit and the failure is visible. But `compare` measures a printed part against its design, and `compensate` turns that deviation into a slicer profile setting — so on that path a scale error becomes a persistent, provenance-stamped compensation applied to every future print in that material. That is the path this ADR exists for.

Prompted by the OpenScan survey (`wiki/entities/openscan.md`), which asked whether a photogrammetry rig could feed this boundary. The answer exposed that the boundary was not ready, independent of any particular scanner.

**Options.**

1. **Leave it.** Units are declared; treat scale as the caller's problem. Rejected: the record already looks authoritative, and a reader cannot tell a scaled mesh from an unscaled one.
2. **Require a scale reference on every mesh import.** Uniform, nothing to mis-declare. Rejected: a `kicad-cli` export has no scale to establish, so the field would be ceremony in the majority of calls — and a required field that means nothing in context is one people satisfy with noise. ClawBot's ADR-0026 names this failure for a `how_determined` that states nothing.
3. **Gate downstream only** — let `compare`/`compensate` refuse an unreferenced scan, leave import alone. Rejected: a 2 % scale error still produces a cradle that does not fit. It moves the failure later and makes it more expensive, and the import record stays ambiguous either way.
4. **Declare the provenance class at import**, and require a scale reference only where the class makes one necessary.

**Decision.** Option 4. Import takes a declared `EScanOrigin` — `cad-export` | `metrology-scan` | `photogrammetry` — with no default, alongside the units it already required.

- `photogrammetry` **requires** a `ScaleReference`: a positive measured length in mm plus a description of what was measured and how. Absence is UNKNOWN and refuses, naming the reason.
- `cad-export` **refuses** a scale reference. It is not a harmless extra: recording one asserts a measurement that was never made, against a mesh whose units were authoritative already.
- `metrology-scan` (structured light, laser, CT) may carry one and does not require it — the instrument established scale itself.

The rule lives in one place, `ScanProvenance.Validate`, so the CLI and the MCP surface cannot drift apart. Both sidecars gain `scan_origin` and `scan_scale_reference`, written **present-and-null** rather than omitted when there is none, so a reader can tell *"none was needed"* from *"nobody said"*.

`CompareRun` declares the two sides separately and deliberately: the design is always `cad-export` (it is this engine's own output), and the measured side carries the caller's declaration. The manual caliper path records `scan_origin: null` — a caliper reads a physical part, so there is no reconstruction to scale.

**On judging the description.** It is free text and is *not* length-checked or keyword-checked. A minimum length is a guess that manufactures confidence, and any threshold is satisfiable by filler. The real gate is the positive measured length, which cannot be produced by typing a word.

**Schema.** `odc/provenance/0.2 → 0.3` for the cradle sidecar and `odc/comparison/0.3 → 0.4`. `EnclosureRun` is untouched — it is already on `odc/provenance/0.3` for ADR-0016's reasons and imports no mesh, so the cross-machine golden `7b1c8fb9dcdb…` is unaffected. The version tracks each sidecar's own content; the `inputs` block has always been model-specific. `scan-cradle/0.1` keeps its model id: the geometry is byte-identical, only the record changed.

**Consequences.** `--scan-origin` is **required**, so every existing `run-cradle` and `compare --scan` invocation breaks until it is added. That is the intent — silence is what is being removed — and the walkthrough is updated to pass `cad-export` for the KiCad board STL, which is the honest declaration there.

What this does *not* do: it does not verify the scale reference. Nobody checks that the gauge block was really 10 mm or that it was in frame. The claim is the author's, recorded as theirs, exactly as a declared scanner accuracy is under ADR-0015. What changes is that an unscaled reconstruction can no longer pass silently, and a later reader can see which meshes had their size established and by what.

Open, deliberately: existing records written before this carry no `scan_origin`, and an absent field reads as "nobody said" — correct, since nobody did.
