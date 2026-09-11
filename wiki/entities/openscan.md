---
title: OpenScan
type: entity
updated: 2026-09-11
sources:
  - research/raw/openscan-home-2026-09-11.md
  - research/raw/openscan-benchy-2026-09-11.md
  - research/raw/openscan3-beta-whats-new-2026-09-11.md
  - https://github.com/OpenScan-org (git-hosted; permanent by identifier, not archived)
  - OpenScan3ODC/docs/ARCHITECTURE.md, docs/TASKS.md (local checkout)
---
Open-source photogrammetry turntable scanners (Mini, Classic, Macro Add-on) with GPL-3.0 firmware.
Facts and vendor claims are in [[openscan-2026-09]]; this page is the assessment.

**What it is to [[opendesigncore]]: a candidate for the one capture gap this repo deliberately
refuses to fill.** ROADMAP "Not ever" says *owning a scan-capture pipeline (photogrammetry/LiDAR
stacks) — ODC accepts meshes at a validated import boundary*, and [[ecosystem-map]] lists scanning as
a gap nothing in the platform covers. The boundary is already built: `run_cradle` imports a mesh with
units **declared, never inferred** (`AUTO` refused) and hash-chains the raw scan into the sidecar;
`compare` measures per-axis deviation against a **declared** scanner accuracy, with observed spread
overriding that declaration when spread is larger (ADR-0015). So this is not a "should we build
scanning" question. It is: does OpenScan produce output that satisfies a boundary that already
refuses guesses?

**Three conditions, in descending order of how much they matter.**

**1. Scale, not resolution, is the real problem.** Photogrammetry is scale-free by construction:
structure-from-motion recovers shape up to an unknown similarity transform. A turntable rig gives
excellent *relative* geometry and no *absolute* size unless a known reference enters the scene — a
scale bar, a calibrated target, or a measured feature. `run_cradle` refusing `AUTO` is necessary and
not sufficient: a *declared* unit on a photogrammetric mesh is only as good as the scale reference
used during capture, and nothing currently records which reference was used or whether one existed.
**If a scan-derived mesh is ever to feed `compare` or `compensate`, the scale reference belongs in
the provenance record.** This is an ADR before it is code — see [[open-questions]] item 17.

**2. There is no vendor accuracy figure to declare — settled, not pending.** The headline sub-0.02 mm
numbers are asterisked to the OpenScan Benchy page, which was read on 2026-09-11 and turns out to be
a *qualitative visual* comparison: a shared model scanned on various devices, results on Sketchfab,
prose about visible layer lines. No ground truth, no deviation measurement, no accuracy figure
anywhere on it — and its stated purpose is so users "do not have to fall for some marketing claims
about accuracy and resolution". The figures are therefore unsourced rather than weakly sourced, and
the citation points at a page that argues against them. **No number from OpenScan may be passed to
`compare --declared-accuracy`.** Only a local measurement against a known artifact produces one.
ADR-0015's spread override defends a declaration that is optimistic in practice; it cannot
manufacture a declaration that was never grounded.

This is a closed topic, not an open one — the reading established a refusal, which counts as answered.

**3. Scan volume bounds the loop.** The stated volumes cover printed enclosures, small mechanism parts
and the `compare` calibration loop; nothing larger. The Macro Add-on moves toward smaller and more
detailed, which is arguably the more useful direction for verifying printed part dimensions.

**Where it would join, if it does.** [[ecosystem-map]] already states the rule — *any new capability
(electronics, scanning) should arrive as an MCP surface, not a monolith* — and OpenScan3's versioned
REST API is directly wrappable as a separate `openscan-mcp` process beside `opendesigncore-mcp`,
under the same propose/refuse discipline. Because that is a separate process over HTTP, OpenScan's
GPL-3.0 raises no linking question against this repo's Apache-2.0 (PD-4); it would need a
`DEPENDENCIES.md` line only if a client were ever vendored. A capture task could equally live in
OpenScan3's own `tasks/community/` extension point without forking, and their session/take/profile
metadata maps onto run/inputs/parameters directly. **None of this is built or planned** — it is the
shape the seam would take, recorded so the next person does not re-derive it.

**Do not reach for scan-to-CAD reconstruction.** The commercial state of the art targets editable
B-rep feature trees; this engine needs a validated mesh. See [[ai-cad-mcp-landscape-2026-09]].
