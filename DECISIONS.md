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
**Status:** accepted

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
