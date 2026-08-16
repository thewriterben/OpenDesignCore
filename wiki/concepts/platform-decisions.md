---
title: Platform decisions
type: concept
updated: 2026-08-15
sources: [conversation 2026-08-15 (Benji); atopile/tscircuit public docs (web, 2026-08)]
---

# Platform decisions

Decisions that span repos. Repo-local consequences get ADRs in their own repos; the new repos' DECISIONS.md are seeded from here at creation. All decided by Benji, 2026-08-15.

## PD-1 — Electronics kernel: ~~atopile + KiCad~~ → **KiCad directly**
**Superseded 2026-08-15 (same day) by OpenCircuitCore ADR-0003.** The original decision assumed atopile was an open-source CLI compiler to KiCad. Installing it proved otherwise: the CLI is **maintenance-only** (0.15.8 is the last release; it pins Python 3.14 and its `zstd` dep has no cp314 wheel, so it needs MSVC C++ Build Tools), and **0.16+ moved to a hosted browser workspace**. A SaaS dependency in the design path contradicts a platform that is local-first and offline-capable everywhere else — pinned geometry kernel, stdlib-only settlement, local-first print studio.

**Now:** KiCad is the authoring, verification, and output substrate, driven by scripts through `kicad-cli`. Verified present: **KiCad 10.0.5** — `sch erc`, `sch export` (netlist/BOM/PDF), `pcb drc`, `pcb export` (gerbers, drill, position, ODB++, IPC-D-356, **STEP**, **STL**, VRML), plus bundled Python 3.11 for `pcbnew` scripting. GPLv3 containment unchanged: external process only, never linked. tscircuit remains on the watch list.

**Bonus:** `pcb export step|stl` gives a *verified* board→enclosure co-design path into [[opendesigncore]]'s mesh import boundary — the use-case map's multi-domain co-design now has a mechanism, not a hope.

**Lesson worth keeping:** the decision was made from documentation and a web search; it survived four hours. Kernel choices get installed and run before they are recorded.

## PD-2 — Schema-first parts registry
A new canonical registry: JSON Schema + data files, with **generated** bindings for Rust, TS, Python, and C# — killing the documented three-copy drift. [[oh-ben-claw]]'s registry.json is ingested as the boards namespace, not forked; OBC eventually consumes the generated Rust binding. Electronic parts link KiCad footprints/symbols and atopile packages. **User inventory is a separate store** referencing canonical part IDs — mutable user state, never mixed into cited reference data.

## PD-3 — Two new repos
Parts registry (schema + data + codegen) and electronics engine (atopile projects, KiCad automation, BOM/sourcing, MCP surface) each get their own repo. Registry release cadence stays independent of any engine.

## PD-4 — Licences
OpenDesignCore relicensed Apache-2.0 (ADR-0005 accepted); the two new repos start Apache-2.0. Existing ecosystem repos stay MIT. ClawCam's missing LICENSE added (MIT, matching siblings).

## PD-5 — Legality gating: two-tier, BINGO owns policy data
Design-time: a shared refusal-category taxonomy (weapons, regulated items, …) enforced by every design assistant on the platform. Fabrication-time: BINGO node acceptance declares jurisdiction + refused categories; orchestration matches jobs accordingly; arbitration handles disputes. The taxonomy lives in BINGO's asset/acceptance schema; other repos reference it. Policy data is human-maintained and cited or TODO(source) — never LLM-generated legal claims.

## PD-6 — BINGO ↔ ODC provenance contract drafted now
See [[bingo-odc-provenance-contract]]. Implementation waits for the thin thread to emit real provenance.
