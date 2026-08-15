---
title: Oh-Ben-Claw
type: entity
updated: 2026-08-15
sources: [Oh-Ben-Claw/README.md, Oh-Ben-Claw/docs/ECOSYSTEM-INTEGRATION.md, Oh-Ben-Claw/Knowledge Base/]
---
Embodied AI agent core, 100% Rust (~33 obc-* crates). Perceive→remember→react→act over an MQTT spine to ESP32/SBC nodes; Track-0 deterministic safety gate mirrored on MCU. Assets for the design platform: **hardware registry** (`registry/registry.json` from `src/peripherals/registry.rs` — VID/PID, capabilities, connector taxonomy: grove/qwiic/stemma_qt/…, I2C addresses, compatible_boards), **deployment planner** (inventory+desires → topology + gap analysis + missing-hardware suggestions), firmware generator (compilable ESP32-S3 projects), guide-generator (wizard → PDF build guides). Research: `Knowledge Base/` hardware-scout reports, `AI-Agents-*.md` deep-research fan-outs. Known issue: registry drift across Rust/TS/Python consumers. See [[ecosystem-map]].
