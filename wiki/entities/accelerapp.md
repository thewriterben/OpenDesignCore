---
title: Accelerapp
type: entity
updated: 2026-08-15
sources: [Accelerapp/README.md, Accelerapp/HARDWARE_GENERATION.md, Accelerapp/DIGITAL_TWIN_FEATURES.md]
---
Python platform: YAML device spec → firmware + SDK + UI via multi-agent codegen (local Ollama or cloud LLMs). Targets Arduino/ESP32(+CAM/S3)/STM32/Nordic/RPi. Assets: **EnclosureGenerator** (8 environments, 6 materials PLA→Nylon, IP20–IP67, print-ready with settings), environmental validation (−40…85 °C, UV, lifetime), **economics analyzer** (regional pricing, volume discounts, budget-targeted design adjustment), component registry.py + registry.json, digital twins, HIL, air-gap/post-quantum security posture. No PCB design or distributor-integrated BOM sourcing. OBC-Prime's plan: harvest hardware/registry.py + firmware templates. See [[use-case-exploration]] §3 — its device spec is the seed of "one spec, four artifacts."
