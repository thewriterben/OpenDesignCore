---
title: OpenBuildCore
type: entity
updated: 2026-08-16
sources: [OpenBuildCore/README.md, OpenBuildCore/DECISIONS.md, OpenBuildCore/ROADMAP.md]
---
Fourth Open*Core peer (its ADR-0001, extending PD-2/PD-3): inventory + ideation + machines — what you own, and what you could make of it. Inventory is `part_id` + `qty` referencing [[openpartscore]]; unresolvable ids are refused. Projects declare requirements as specific parts or capabilities, each with a quantity.

Generalises [[oh-ben-claw]]'s `planDeployment`, keeping its capability-token core and fixing three things that don't survive generalisation (its ADR-0002): presence-only matching (a 2-host project satisfied by 1 board), no exclusivity (one item filling several roles), and hardcoded suggestion lists (registry never searched). Here allocation is **quantity-aware and exclusive**, specific parts allocate before capabilities, and suggestions are **queried from the registry**. Greedy, not optimal — backtracking deferred with the tradeoff stated.

Shopping lists state their basis explicitly (ADR-0004): sequential by default (parts reused, quantity = worst single shortfall), `--simultaneous` sums. A validator catches the two failure modes invisible in the advisor — a `part_id` no registry entry provides, and a `capability` no part provides — because both present as ordinary gaps.

**Machines** (ADR-0005) answer the physical half: fit over all six axis-aligned orientations with the working one named, material support, feature size against `min_feature_mm` or the nozzle diameter, and time. Machines are owned state like inventory (`machines.json` git-ignored), with field names copied from [[project-bingo]]'s node machine record so one hands to a node without translation.

**Print time is never modelled.** An estimate exists only when the record carries a `measured_throughput` its owner measured with `how_measured` saying how; everything else answers "requires slicing", and even a measured estimate is labelled pre-slicing triage a slicer supersedes. Same discipline as ODC's `--scan-accuracy-mm`: absence is unknown, not a gap to fill with a model. The shipped K2 Plus record has a `1×1×1` `TODO(source)` envelope because the build volume isn't in the cited material, so every fit check on it fails loudly — pinned by a test, plus a second proving the check flips on a real envelope.

**Made parts** (ADR-0006) are a third requirement kind beside `part_id` and `capability`, carrying a size and material and judged against machines. Kept apart because a missing part and an unmakeable part are fixed differently — buy the radio; you cannot buy your way out of a 260 mm part on a 220 mm bed — so they never reach the shopping list, and a result carries two booleans (`buildable`, `makeable`) rather than one label that hides which half failed. No machines declared means `makeable: null`.

`can-print --from-sidecar` reads [[opendesigncore]]'s `artifact.bbox_mm` and `volume_cubic_mm` (its ADR-0010), so a verdict is about real geometry and names the artifact hash it judged. **The peers meet at the provenance record, not at an API**: this repo imports nothing from ODC. It is also the first consumer of a provenance field anywhere in the platform, and consuming it is what exposed that the field was missing.

CLI: `advisor.py what-can-i-build | gaps <project> | inventory | shopping-list`, `machines.py list | can-print`, `validate.py`; `--json` throughout. MCP surface `obc_mcp`: 8 tools, all reads, none editing inventory or machines. 70 stdlib tests. Apache-2.0. Repo: https://github.com/thewriterben/OpenBuildCore (public).
