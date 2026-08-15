---
title: Project BINGO
type: entity
updated: 2026-08-15
sources: [ProjectBINGO/VISION.md, ProjectBINGO/v3/specs/, ProjectBINGO/docs/LANDSCAPE-2026.md, ProjectBINGO PDFs (3, repo root)]
---
Open protocol for distributed manufacturing with **creator royalties enforced at the point of fabrication** — designer paid in the same atomic transaction as the fabricator. v3 (live): Python stdlib-only; Ed25519 signed hash-chained evidence; content-addressed asset registry. Claims first real settled fabrication 2026-08-03 (K2 Plus part). Five layers: L1 asset graph (per-unit royalty licenses, remix split trees), L2 fabrication network (Klipper/Moonraker, Bambu, OctoPrint, LinuxCNC node agents; capability tiers 0–3, Tier 3 = PCB assembly/injection/sheet metal; human labor as node type; proof-of-fabrication), L3 orchestration (intake→DFM→quote→match→QA→settle as MCP-style tools), L4 capital rails, L5 agent-first API/marketplace. v2 microservices retired. Platform role: the **outsourced-fabrication and marketplace layer**; ODC provenance records should be referenceable from BINGO fabrication evidence.
