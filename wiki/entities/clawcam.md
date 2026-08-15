---
title: ClawCam
type: entity
updated: 2026-08-15
sources: [ClawCam/README.md, ClawCam/NEXT_PHASE_PLAN.md]
---
Smart-camera platform: ESP32-S3 camera nodes (ESP-IDF), offline-first Python/FastAPI gateway (MQTT, OTA, MegaDetector/BirdNET), brain = [[oh-ben-claw]] over MCP. 10 device profiles beyond wildlife. Relevant patterns: **MCP approval model** (35 read tools auto-approved, 11 write tools gated; scopes call/session/forever), JSON schemas for events/devices/health, detection zones/privacy masks, camtrap-dp standard. In-ecosystem vision competence usable for inventory-photo part identification.
