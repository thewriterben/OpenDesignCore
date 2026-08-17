---
title: AdvancedStudio (3DP)
type: entity
updated: 2026-08-16
sources: [3DP/AdvancedStudio/docs/Research-Report.md, 3DP/AdvancedStudio/DECISIONS.md, 3DP/AdvancedStudio/studio-core/README.md, 3DP/AdvancedStudio/studio-mcp/README.md]
---
Local-first 3D-printing studio for a Creality K2 Plus: FastAPI core (telemetry, print control, CFS/RFID material tracking, calibration computation, material profile store, G-code mgmt, camera analysis, docs RAG) + stdio **MCP server** (12 tools) designed to register with OBC/Claude. Hard safety contract: **reads execute, writes are propose-only** (human approves in dashboard). Thesis from Research-Report: K2 is a two-API problem (Moonraker :7125 + Creality WS :9999); tolerance compensation lives in the slicer. This is the platform's local-fabrication executor and the source of per-printer/material calibration data for scan-to-fit tolerances.

Under version control since 2026-08-15 (single honest initial commit — reconstructing a "before" state would have fabricated history). Hardened the same day: loopback bind by default plus optional shared-token auth on the four state-changing endpoints. `studio-mcp` ported to MCP SDK 2.x.

**Its ADR-0001 (2026-08-16)** accepts measured compensations from [[opendesigncore]]: `/api/propose` gains `profile_update`, guarded like `print_start` because a wrong compensation is invisible on the printer and shows up weeks later in parts that do not fit. An update must carry an `origin` (`odc-comparison:<sha256>`) or it is refused, and may only write the three dimensional-compensation fields. **`ProfileStore.upsert` used to force `source="user"` on every update**, which would have silently discarded that origin while looking correct — the bug the whole change exists to prevent, caught by a test.

The division with ODC: this side owns the shrinkage arithmetic (already calibrated against its research), ODC decides whether one factor is defensible at all. Neither reimplements the other.

**Standing gaps:** no file upload and no slicer — it manages pre-sliced G-code only, and no slicer is installed on the machine at all (checked 2026-08-16), so closing the STL→print gap needs a new external dependency. Approvals are in-memory with a 300 s TTL. No jobs/history store, and no provenance field on print proposals — an ODC artifact hash still cannot be attached to a print, though a *profile* value can now carry its origin.
