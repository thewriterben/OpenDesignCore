# Changelog

Format: [Keep a Changelog](https://keepachangelog.com). Versioning: [SemVer](https://semver.org).

## [Unreleased]

### Added
- Repository seeded: docs skeleton, contribution rules, CI stub, reference-test scaffold.
- `wiki/`: LLM Wiki instantiated (ADR-0006 accepted) — schema, index, log, 8 entity pages, ecosystem map, use-case exploration, open questions.
- ADR-0007: OpenDesignCore is an engine among peers; the Computational Engineering platform is the MCP-composed ecosystem.
- ROADMAP filled: thin thread as "Now"; scan-to-fit and MCP surface as "Next"; explicit non-goals.
- Platform decisions PD-1..PD-6 recorded in `wiki/concepts/platform-decisions.md`; BINGO↔ODC provenance contract drafted (v0).

- ASCII STL import at the mesh boundary. PicoGK 2.2.0 throws `NotImplementedException` for ASCII STL and `kicad-cli pcb export stl` emits exactly that, so the OpenCircuitCore→OpenDesignCore co-design bridge crashed on the first real board. Binary/ASCII is detected by the `84 + 50n` size identity (not the `solid` prefix, which binary files also use); parsing is file I/O only — geometry stays with the kernel. Test asserts both encodings of one geometry yield the same cradle but different recorded scan hashes, since provenance tracks the bytes that arrived.
- MCP surface (`src/OpenDesignCore.Mcp`, stdio, ModelContextProtocol `[2.2.0]`): 7 tools — `list_models`, `list_parts`, `list_runs`, `get_provenance`, `run_enclosure`, `run_cradle` execute; `handoff_to_studio` proposes only. No approval tool exists and a test enforces that (ADR-0009). `McpGuard` refuses voxel sizes outside [0.05, 5] mm, requests over a 2e9 voxel budget, and paths escaping the working root (set with `ODC_ROOT`). 8 new tests incl. DI-level tool discovery; 33 total.
- Scan-to-fit: `run-cradle --stl <path> --units <u> --voxel-mm <v>` imports a scanned mesh at a strict boundary (units declared, AUTO refused; raw scan bytes content-addressed; recentred deterministically via the Matrix4x4 transform — the (scale, offset) overload in PicoGK 2.2.0 has a per-axis-component bug, noted for upstream), then carves a foam-insert-style cradle (`scan-cradle/0.1`): bounding block minus the clearance-offset scan volume up to a split height. Sidecar hash-chains cradle → scan. Resolution floor `min(wall/2, clearance)`. 5 new tests with a synthetic PicoGK-generated scan; 25 total.
- Fabrication handoff: `handoff --run <id> --stage <dir>` stages the artifact + provenance sidecar (hash-named) for slicing, verifies AdvancedStudio answers `GET /api/state`, optionally proposes `print_start` through the studio's propose-only seam and records the confirmation id; `handoffs` table added to the ledger (append-only). `--offline` records `staged-offline` explicitly — degradation is never silent. 5 tests against a loopback studio stub.
- Thin thread runs end to end: `run-enclosure --voxel-mm <v>` builds an open-top enclosure tray around a cited part envelope on headless PicoGK, validates it, exports binary STL, writes a deterministic canonical-JSON provenance sidecar (SHA-256, byte-compatible with Project BINGO's `canonical_json`, verified against Python-generated vectors), content-addresses both into `artifacts/`, and appends the run to `ledger.db` (Microsoft.Data.Sqlite, append-only, `Pooling=False`). Resolution floor enforced (wall >= 2 voxels). 15 tests including rerun byte-identity.
- `data/` reference store: parts + materials namespaces, snake_case JSON, strict loader (`DataStore`) enforcing citations, unknown-field rejection, and positive dimensions; `validate-data` CLI command; first test project (6 tests) activates `dotnet test`. First entries: `parts/esp32-s3-wroom-1` (Espressif datasheet), `materials/pla-generic` (secondary-sourced, TODO(source)).
- Thin-thread solution skeleton: net9.0, PicoGK `[2.2.0]` via NuGet, ShapeKernel submodule pinned to `ShapeKernel-v2.1.0` compiled in a non-strict wrapper project; builds, runs, and formats clean. ADR-0008. Verify commands recorded in CLAUDE.md.

### Changed
- Relicensed MIT → Apache-2.0 (ADR-0005 accepted); README and DEPENDENCIES.md updated.
- DEPENDENCIES.md kernel-stack table rewritten for NuGet-era PicoGK (ADR-0008); .gitignore covers .NET build output.
