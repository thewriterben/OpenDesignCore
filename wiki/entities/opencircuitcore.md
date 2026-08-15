---
title: OpenCircuitCore
type: entity
updated: 2026-08-15
sources: [OpenCircuitCore/README.md, OpenCircuitCore/DECISIONS.md, OpenCircuitCore/ARCHITECTURE.md]
---
Electronics engine (PD-1): circuits authored as atopile code → compiled to KiCad projects → ERC/DRC → gerbers/BOM/provenance. KiCad (GPLv3) contained as external process; atopile MIT; tscircuit on watch for browser UX. BOM part refs resolve to [[openpartscore]] ids; outputs hand off to [[advancedstudio]] or [[project-bingo]]. Determinism mirrors ODC ADR-0003: pinned atopile+KiCad versions in every provenance record. First milestone: ESP32-S3 + I2C sensor reference board end to end. Apache-2.0. Repo: https://github.com/thewriterben/OpenCircuitCore (public).
