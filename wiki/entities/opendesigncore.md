---
title: OpenDesignCore
type: entity
updated: 2026-08-15
sources: [OpenDesignCore/ARCHITECTURE.md, OpenDesignCore/DECISIONS.md]
---
This repo. Deterministic computational-engineering core: requirements → model evaluation on PicoGK/ShapeKernel (voxel/SDF over OpenVDB, C#/.NET) → validated artifact + provenance. Invariants: mm everywhere (ADR-0004), explicit global voxel size in provenance (ADR-0003), byte-identical reproducibility, cited physical data only, no silent degradation. Persistence per ADR-0006 (proposed): data/ (cited reference values), ledger.db (append-only runs), artifacts/ (content-addressed), wiki/ (this). Pre-alpha; end-to-end path not yet implemented. Scope question — engine vs. umbrella — is [[open-questions]] #1.
