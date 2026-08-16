---
title: OpenBuildCore
type: entity
updated: 2026-08-15
sources: [OpenBuildCore/README.md, OpenBuildCore/DECISIONS.md]
---
Fourth Open*Core peer (its ADR-0001, extending PD-2/PD-3): inventory + ideation — what you own, and what you could make of it. Inventory is `part_id` + `qty` referencing [[openpartscore]]; unresolvable ids are refused. Projects declare requirements as specific parts or capabilities, each with a quantity.

Generalises [[oh-ben-claw]]'s `planDeployment`, keeping its capability-token core and fixing three things that don't survive generalisation (its ADR-0002): presence-only matching (a 2-host project satisfied by 1 board), no exclusivity (one item filling several roles), and hardcoded suggestion lists (registry never searched). Here allocation is **quantity-aware and exclusive**, specific parts allocate before capabilities, and suggestions are **queried from the registry**. Greedy, not optimal — backtracking deferred with the tradeoff stated.

CLI: `what-can-i-build`, `gaps <project>`, `inventory`, all `--json`. 8 stdlib tests. Apache-2.0. Repo: https://github.com/thewriterben/OpenBuildCore (public).
