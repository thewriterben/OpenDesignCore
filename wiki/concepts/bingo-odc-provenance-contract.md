---
title: BINGO ↔ ODC provenance contract (v0.1)
type: concept
updated: 2026-08-15
sources: [ProjectBINGO/v3/specs/ASSET-GRAPH.md (v0.1 draft), ProjectBINGO/v3/specs/EXTERNAL-ANCHOR.md, OpenDesignCore/ARCHITECTURE.md persistence section]
---

# BINGO ↔ ODC provenance contract — v0.1

**Purpose.** A part fabricated on the BINGO network is traceable, by hash, to the exact OpenDesignCore model run that produced its geometry.

## Confirmed against BINGO v3 specs (2026-08-15)

- **Hash algorithm: SHA-256 hex** — ASSET-GRAPH v0.1: "all hashes are SHA-256 hex"; asset ID = SHA-256 of the canonical manifest; every `content.files[]` entry carries `sha256`. **Consequence: ODC's artifact store uses SHA-256** so hashes are directly comparable.
- **EXTERNAL-ANCHOR is not the vehicle** (v0 guess corrected). It is an RFC 6962-style transparency log proving *ordering* (rollback/equivocation defence), orthogonal to content identity. Design provenance rides the asset manifest; anchoring the manifest later strengthens *when*, not *what*.
- BINGO's own "provenance record" is per fabricated **unit** (asset → order → job → node → PoF chain). Design provenance sits upstream, at asset registration.

## The contract

An asset whose geometry came from an ODC model run includes the provenance sidecar as a manifest file entry, plus one extension field:

```json
"content": {
  "files": [
    { "name": "bracket.stl",             "sha256": "<geometry>",  "media_type": "model/stl" },
    { "name": "bracket.provenance.json", "sha256": "<sidecar>",   "media_type": "application/vnd.odc.provenance+json" }
  ]
},
"design_provenance": {
  "system": "opendesigncore",
  "run_id": "<ledger.db run row id>",
  "artifact_sha256": "<geometry — must equal the files[] entry>"
}
```

**Verification rule.** Valid iff `design_provenance.artifact_sha256` equals both the manifest's geometry file hash and the artifact hash in ODC ledger row `run_id`. The sidecar (inputs, voxel size, pinned versions, commit) travels *inside* the asset — auditable by anyone holding it, and ODC's determinism (ADR-0003) makes the claim re-checkable by re-running the recorded inputs. Because the asset ID commits to the manifest, it commits to the design provenance too — remixes inherit auditability through `derives_from` with no extra chaining.

**Dependency direction.** BINGO references ODC records; ODC knows nothing of BINGO. OpenCircuitCore board designs use the identical pattern (its own sidecar media type).

## Remaining before v1

- Sidecar schema (owned by the ODC thin thread) + registering the media type string.
- `design_provenance` as a formal optional field in ASSET-GRAPH v0.2 (currently an extension).
- Canonical-manifest JSON rules already exist in BINGO (`canonical_json`); ODC sidecar serialization should reuse the same canonicalization for hash stability.
