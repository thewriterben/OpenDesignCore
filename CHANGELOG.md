# Changelog

Format: [Keep a Changelog](https://keepachangelog.com). Versioning: [SemVer](https://semver.org).

## [Unreleased]

### Added
- Repository seeded: docs skeleton, contribution rules, CI stub, reference-test scaffold.
- `wiki/`: LLM Wiki instantiated (ADR-0006 accepted) — schema, index, log, 8 entity pages, ecosystem map, use-case exploration, open questions.
- ADR-0007: OpenDesignCore is an engine among peers; the Computational Engineering platform is the MCP-composed ecosystem.
- ROADMAP filled: thin thread as "Now"; scan-to-fit and MCP surface as "Next"; explicit non-goals.
- Platform decisions PD-1..PD-6 recorded in `wiki/concepts/platform-decisions.md`; BINGO↔ODC provenance contract drafted (v0).

- `data/` reference store: parts + materials namespaces, snake_case JSON, strict loader (`DataStore`) enforcing citations, unknown-field rejection, and positive dimensions; `validate-data` CLI command; first test project (6 tests) activates `dotnet test`. First entries: `parts/esp32-s3-wroom-1` (Espressif datasheet), `materials/pla-generic` (secondary-sourced, TODO(source)).
- Thin-thread solution skeleton: net9.0, PicoGK `[2.2.0]` via NuGet, ShapeKernel submodule pinned to `ShapeKernel-v2.1.0` compiled in a non-strict wrapper project; builds, runs, and formats clean. ADR-0008. Verify commands recorded in CLAUDE.md.

### Changed
- Relicensed MIT → Apache-2.0 (ADR-0005 accepted); README and DEPENDENCIES.md updated.
- DEPENDENCIES.md kernel-stack table rewritten for NuGet-era PicoGK (ADR-0008); .gitignore covers .NET build output.
