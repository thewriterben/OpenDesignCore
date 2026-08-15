---
title: "Source: OBC-Prime RESEARCH-2026-07 (embodied AI survey)"
type: source-summary
updated: 2026-08-15
sources: ["OBC-Prime/docs/RESEARCH-2026-07.md"]
---
Field survey checked against the OBC codebase. Thesis: bifurcation between single-manipulator VLAs and smart-home text agents leaves a vacuum at "persistent agent over heterogeneous cheap-node mesh" — where OBC sits. OBC already has dual-location deterministic enforcement (host limits.rs mirrored by on-MCU SafetyGate) — stronger than published RoboGuard/RoboSafe. Highest-priority live bug: no staleness/TTL in world.rs (temporal memory contamination; violation rates 0.3–0.5 broad retrieval, arXiv 2605.17830). Perception is an unguarded injection surface: physical text-hijack 95.5%/81.8% (CHAI); MQTT spine has no auth story. Skill rot: 202-skill library costs ~21% pass rate; shadowing explains up to 68%. Do-not-build list: VLA, simulator, datasets, low-level control, new A2A protocol. Best novel bets: provenance-aware staleness arbitration; **an MCP profile for physical actuation** (elicitation/confirm-before-actuate primitive, zero robotics users) — directly relevant to the fabrication-execution seam ([[advancedstudio]], [[project-bingo]] node agents).
