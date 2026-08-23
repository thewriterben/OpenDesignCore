---
title: Ecosystem map
type: concept
updated: 2026-08-15
sources:
  - Oh-Ben-Claw/README.md, docs/ECOSYSTEM-INTEGRATION.md
  - OBC-Prime/PLAN.md (§2 four-repo assessment)
  - ProjectBINGO/VISION.md, v3/specs/
  - Accelerapp/README.md, HARDWARE_GENERATION.md
  - 3DP/AdvancedStudio/docs/Research-Report.md
---

# Ecosystem map

Eight repos, one shape: **brains, bodies, fabrication, and settlement**, converging on MCP as the universal seam. A ninth, [[clawbot]], appeared 2026-08-22 and holds the **mechanism** domain — a peer domain since ADR-0014 (2026-08-23), on the argument that a serial chain's reachable set is not a box and its payload is not a scalar.

| Repo | Role | Stack | The reusable asset |
|---|---|---|---|
| [[oh-ben-claw]] | Embodied agent core ("brain") | Rust, ~33 crates | Hardware **registry** (44+ boards, connector taxonomy, capabilities), **deployment planner** (inventory + desires → topology + gap analysis), firmware generator |
| [[obc-prime]] | Public, evidence-first substrate | Rust + WASM + TS | **Parity discipline**: three planner implementations, byte-identical output; Reference Bodies (runnable deployment templates) |
| [[obc-deployment-generator]] | UX front door | Expo/React Native | 3-step wizard: Inventory → Desires → Plan; the closest existing analogue to a design front end |
| [[clawcam]] | Perception proof | ESP-IDF + Python | Vision pipeline, MCP tool catalog with read-free/write-gated approval model |
| [[accelerapp]] | Device codegen | Python | YAML spec → firmware/SDK/UI; **EnclosureGenerator** (environment/material/IP-rated, print-ready); regional **cost analyzer**; digital twins |
| [[advancedstudio]] | Fabrication execution | Python FastAPI + MCP | K2 Plus control, calibration, material profiles, RAG KB; **reads-execute / writes-propose** MCP safety pattern |
| [[project-bingo]] | Distributed manufacturing protocol | Python stdlib (v3) | Asset graph with **royalties settled at fabrication**, node agents (Klipper/Bambu/OctoPrint/LinuxCNC), capability tiers 0–3 (Tier 3 = PCB assembly), proof-of-fabrication |
| [[opendesigncore]] | Deterministic engineering core | C#/.NET on PicoGK | Requirements → validated geometry + provenance; the only repo with a rigorous determinism/provenance contract |
| [[clawbot]] | Mechanism model | Python stdlib + Rust binding | Links, joints, actuators and what they can reach; **derived answers that carry their assumptions in the value** — and a Rust binding that turns the platform's refusals into compile errors. Mechanism peer domain, ADR-0014. |

## The convergence

Every repo independently arrived at the same patterns:

1. **MCP as the seam.** AdvancedStudio, ClawCam, and Oh-Ben-Claw all expose/consume MCP with the same approval model. Any new capability (electronics, scanning) should arrive as an MCP surface, not a monolith.
2. **Registry as ground truth.** Oh-Ben-Claw's `registry.json` is the component database three repos consume. Extending it (passives, ICs, mechanical hardware, filament, user inventory) is cheaper than building a new one. **Known problem:** ECOSYSTEM-INTEGRATION.md documents registry drift across Rust/TS/Python copies.
3. **Inventory + desires → plan → artifact** is already the ecosystem's native workflow (deployment planner, wizard). The computational-engineering system generalizes this from "agent deployments" to "anything fabricable."
4. **Provenance/evidence chains everywhere**: ODC's ledger, BINGO's signed hash-chained fabrication evidence, OBC-Prime's parity fixtures. These should compose: a BINGO fabrication proof should be able to reference an ODC provenance record.

## Gaps (nothing in the ecosystem does these today)

- 3D scanning / mesh capture → design-ready geometry (photogrammetry, reconstruction, scan-to-SDF)
- Schematic capture / circuit design / PCB layout (BINGO Tier 3 *assembles* PCBs; nothing *designs* them)
- BOM generation and live component sourcing (Accelerapp's cost analyzer is closest, but no distributor integration)
- Multi-domain co-design: board outline ↔ enclosure ↔ thermal ↔ mounting as one constrained problem

See [[use-case-exploration]] for the full use-case space and [[open-questions]] for unresolved scope decisions.
