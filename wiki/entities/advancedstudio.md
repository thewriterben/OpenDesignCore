---
title: AdvancedStudio (3DP)
type: entity
updated: 2026-08-15
sources: [3DP/AdvancedStudio/docs/Research-Report.md, 3DP/AdvancedStudio/studio-core/README.md, 3DP/AdvancedStudio/studio-mcp/README.md]
---
Local-first 3D-printing studio for a Creality K2 Plus: FastAPI core (telemetry, print control, CFS/RFID material tracking, calibration computation, material profile store, G-code mgmt, camera analysis, docs RAG) + stdio **MCP server** (12 tools) designed to register with OBC/Claude. Hard safety contract: **reads execute, writes are propose-only** (human approves in dashboard). Thesis from Research-Report: K2 is a two-API problem (Moonraker :7125 + Creality WS :9999); tolerance compensation lives in the slicer. This is the platform's local-fabrication executor and the source of per-printer/material calibration data for scan-to-fit tolerances.
