---
title: Platform decisions
type: concept
updated: 2026-08-15
sources: [conversation 2026-08-15 (Benji); atopile/tscircuit public docs (web, 2026-08)]
---

# Platform decisions

Decisions that span repos. Repo-local consequences get ADRs in their own repos; the new repos' DECISIONS.md are seeded from here at creation. All decided by Benji, 2026-08-15.

## PD-1 — Electronics kernel: atopile + KiCad
Circuits are authored as code in atopile (MIT; declarative language compiling to KiCad projects with schematics, layout, BOM, fab files). KiCad is the verification and output substrate: ERC/DRC, footprint/symbol libraries, fab-accepted formats. KiCad (GPLv3) is invoked as an external tool — CLI/IPC only, never linked — so designs and platform code carry their own licences. tscircuit (MIT, React/TS) stays on watch for browser-side preview UX; its ERC/DRC was still incomplete as of 2026-08. Becomes the founding ADR of the electronics engine repo.

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
