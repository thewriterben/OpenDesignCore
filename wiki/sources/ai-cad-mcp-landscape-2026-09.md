---
title: "Source: AI-assisted CAD, MCP tooling and scan reconstruction — landscape survey (2026-09-11)"
type: source-summary
updated: 2026-09-11
sources:
  - https://ar5iv.labs.arxiv.org/html/2505.06507 (Text-to-CadQuery)
  - https://arxiv.org/pdf/2505.19713 (CAD-Coder) ; https://arxiv.org/pdf/2505.17702 (Seek-CAD) ; https://arxiv.org/pdf/2508.01031 (CADDesigner)
  - https://arxiv.org/html/2605.18430v1 (Text2CAD-Bench) ; https://arxiv.org/pdf/2604.19773 (PR-CAD)
  - https://www.getleo.ai/blog/open-source-text-to-cad-tools-free (open-source text-to-CAD survey, incl. Zoo.dev behaviour)
  - https://snyk.io/articles/9-mcp-servers-for-computer-aided-drafting-cad-with-ai/ ; https://chatforest.com/reviews/cad-3d-modeling-mcp-servers/
  - https://github.com/DMontgomery40/mcp-3D-printer-server ; https://github.com/jenkinsm13/metashape-mcp ; https://github.com/OrcaSlicer/OrcaSlicer/issues/13763
  - https://leap71.com/computationalengineering/ ; https://leap71.com/noyron/ ; https://www.voxelmatters.com/leap-71-fires-up-first-rocket-engine-built-through-noyron-computational-model/
  - https://www.ntop.com/resources/blog/you-can-t-reach-the-promise-of-ai-accelerated-engineering-without-fixing-the-geometry-bottleneck/
  - https://github.com/Anttwo/SuGaR ; https://arxiv.org/pdf/2506.24096 (MILo) ; https://github.com/ndming/GS-2M ; https://github.com/HanzhiChang/MeshSplat ; https://arxiv.org/pdf/2409.06765 (gsplat)
  - https://develop3d.com/3d-scanning/exploring-ais-role-in-scan-to-cad/ ; https://3dwonders.com/blogs/case-studies/how-reverse-engineering-with-3d-scanning-quicksurface-works-in-2026
---
Landscape scan, 2026-09-11. Nothing here is a value.

**Where this page sits, stated plainly, because it is not where [[openscan-2026-09]] sits.** The
OpenScan pages were *fetched and read in full*, then archived to `research/raw/`. This page was not.
Its arXiv and GitHub references are cited by permanent identifier but were **not read in full** —
titles, abstracts and summaries only. Its vendor and trade-press references (Snyk, ChatForest, Leo AI,
nTop, LEAP 71, VoxelMatters, DEVELOP3D, 3D Wonders) were surfaced through **web-search result
summaries and were not fetched at all**, so they are unarchived by the rule in `wiki/CLAUDE.md` and
also unverified by this wiki.

That makes this page **orientation-grade**: enough to justify a *decision about direction* — which
line of work to ignore, what to expect from a tool category, where the field's attention is — and not
enough to justify a *value*, a benchmark number, or a claim about what a specific tool does. Anything
on this page that starts to carry weight should be re-sourced by fetching and archiving the original
first. The one claim here doing real load-bearing work — that Zoo.dev's model will not honour exact
dimensions — rests on a single secondary summary and is flagged again where it appears.

**Text-to-geometry splits in two, and only one half is usable.** Direct ML geometry generation
(Zoo.dev's open model) returns shape in seconds but *will not honour exact dimensions* — it
approximates the description. That is disqualifying on its own terms here, and it is precisely the
plausible-number failure ADR-0006's grounding rule exists to refuse. **Single-source, secondary, and
unverified:** this rests on one third-party survey, not on Zoo's own documentation or a test run. It
is the load-bearing claim on this page, so if a decision ever turns on it, fetch the primary source
and archive it first. The credible line is **code
generation**: LLM emits CadQuery/Python, a deterministic kernel executes it (Text-to-CadQuery, 170K
pairs, gains scaling with model size; CAD-Coder adds chain-of-thought and geometric-reward RL;
Seek-CAD is training-free self-refinement; CADDesigner is an agent wrapper). Text2CAD-Bench and
PR-CAD are the 2026 benchmark layer.

**The gap that matters:** every one of these is scored on *reconstruction similarity to a reference
shape*, not on dimensional correctness under a stated tolerance, and none carries units, provenance
or determinism. Better generation makes model code cheaper to author; it does not touch the
validation gate, the resolution floor or the sidecar, which are what make an artifact a result.

**MCP-for-CAD is crowded and mostly unsafe to adopt.** Blender MCP dominates by stars (~17.8k);
FreeCAD has at least four competing servers with none canonical; Onshape, Fusion, SolidWorks,
AutoCAD, KiCad, OpenSCAD all have one. Slicers too (`mcp-3D-printer-server` across Orca / Bambu /
OctoPrint / Klipper / Duet / Prusa / Creality; an open request for a *native* OrcaSlicer server).
Photogrammetry has `metashape-mcp` (106 tools, full Metashape pipeline); no open-source equivalent
for Meshroom or COLMAP surfaced. **Systemic problem:** these are GUI-scripting wrappers that expose
whatever the host app can do — no units discipline, no determinism, no provenance, no refusal path.
Several would violate this repo's non-negotiables on contact. The correct use is the one ADR-0017
already found: a second kernel for cross-checking, tolerances pinned by the operator (ADR-0018),
never primary geometry authority.

**Computational engineering, the lineage.** LEAP 71's Noyron is a Large Computational *Engineering*
Model — codified design rules, physics and manufacturing constraints executing deterministically on
PicoGK, with specialised variants (RP rocket motors, EA actuation, HX heat exchangers); a 5 kN
kerolox thruster was generated and fired with no CAD and no human in the geometry loop. **It is not a
generative-AI system**, and the shared adjective invites exactly the wrong inference. nTop argues the
same thesis commercially: AI-accelerated engineering is blocked by the *geometry bottleneck*, and
implicit/field representations are the fix because machines can read and modify them. A vendor making
this repo's architectural case for it.

**Scan reconstruction.** The Gaussian-splatting surface wave (SuGaR, MILo, GS-2M at EG2026, MeshSplat
at AAAI2026, gsplat) optimises *appearance* and novel-view synthesis; metrology-grade surface accuracy
is not what those papers measure. COLMAP remains the structure-from-motion accuracy reference.
Commercial scan-to-CAD (QUICKSURFACE 2026 on Parasolid, Creaform, EXModel one-click, and the AI
feature-recognition work DEVELOP3D surveys) all targets **editable B-rep feature trees** — which this
repo does not want and does not need. A voxel/SDF engine needs a validated mesh, not a feature tree.
That is a large simplification in ODC's favour and it should be taken.
