# Scale derivation: a synthetic capture with exact ground truth

ADR-0020 makes a photogrammetry mesh's scale **derived** from a reference:
`length_mm / span_file_units`. Every test of that in the suite feeds a PicoGK
sphere and a hand-written span, which exercises the arithmetic and not the
claim. This harness exercises the claim.

A render gives what neither a synthetic mesh nor a real capture can give alone:
a genuinely scale-free reconstruction problem — COLMAP is told nothing about
how big the scene is, and nothing in the images tells it — with ground truth
known **exactly** rather than measured to caliper resolution.

## What it does not test

Perfect pinhole camera. No lens distortion, no rolling shutter, no focus
breathing, no motion blur, no sensor noise, no lighting drift between frames.
**A reconstruction that works here is not evidence that a phone capture works.**
It is evidence about the scale pipeline specifically, which is the part ADR-0020
touches. Treat every number below as a floor: the best the method can do when
nothing real is wrong.

## Running it

```powershell
$run = 'C:\some\scratch\dir'
$blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
$colmap  = 'C:\Tools\colmap-4.2.0\bin'

# 1. Render. Writes images/ and ground-truth.json (odc/synthetic-capture/0.1).
& $blender -b -P render_scene.py -- --out $run --views 36 --width 1600 --height 1200

# 2. Reconstruct. CPU SIFT on purpose — see "GPU SIFT" below.
$env:PATH = "$colmap;" + $env:PATH
colmap feature_extractor --database_path "$run\database.db" --image_path "$run\images" `
    --FeatureExtraction.use_gpu 0 --ImageReader.single_camera 1
colmap exhaustive_matcher --database_path "$run\database.db" --FeatureMatching.use_gpu 0
mkdir "$run\sparse"
colmap mapper --database_path "$run\database.db" --image_path "$run\images" --output_path "$run\sparse"
colmap model_converter --input_path "$run\sparse\0" --output_path "$run\sparse-txt" --output_type TXT

# 3. Measure.
python analyse_scale.py --run $run
```

**Run the mapper with `Start-Process`, not `Start-Job`,** if you drive this from
an automated session. A PowerShell job dies with its parent, and a mapper killed
mid-run leaves a frozen log and an empty `sparse/` — which reads as a mapper
failure and is a session failure. Cost an hour to notice.

## Measured, 2026-09-11

Blender 5.2.1 LTS, COLMAP 4.2.0, 36 views at 1600×1200, subject 30 × 20 × 15 mm.

| | |
|---|---|
| Features per frame | ~5,200–5,700 (CPU SIFT) |
| Exhaustive matching | 53 s |
| Registered | **36 / 36** |
| Points | 40,286 |
| Mean reprojection error | **0.31 px** |
| Scale | **30.9015 mm per reconstruction unit** |
| Similarity: σ / median | **0.0075 %** |
| Similarity: worst pair / median | **0.084 %** (630 pairs) |

### What the similarity number is for

Structure-from-motion recovers geometry up to a similarity — rotation,
translation, one scale. ADR-0020 depends on that word being true: if it is, one
reference fixes the whole reconstruction and the derivation is exact; if it is
not, no single scale is correct and a reference merely picks which part of the
model to be right about.

`analyse_scale.py` tests it without alignment or SVD. For every camera pair,
`d_true / d_recon` — rotation and translation cancel inside a distance, so under
a perfect similarity every pair returns the same ratio. The median is the scale.
The spread is the departure.

**At 0.0075 % the premise holds**, in the clean case.

### The consequence for two-reference cross-checking

A second, orthogonal scale reference was proposed to detect exactly this
departure. These numbers say it cannot, here: a caliper reading a 50 mm printed
target at ±0.02 mm is ±0.04 %, which is **five times noisier than the defect it
would be looking for**. Two references would disagree reliably — because of the
calipers.

That is a statement about the synthetic floor, not about a real capture. Its
value is diagnostic: if two references on a real capture disagree by ~1 %, that
is now attributable to optics and technique rather than to SfM being inherently
non-similar. Without this baseline the two are indistinguishable.

## GPU SIFT

`--FeatureExtraction.use_gpu 0` is deliberate. On this machine (RTX 5070,
Blackwell) COLMAP's `sift_test.exe` crashes at `ExtractSiftFeaturesGPU.Nominal`
with `0xC0000409`, while the same binary's `gpu_mat_test` passes 4/4 and the
non-GPU SIFT cases pass 17/17. COLMAP's GPU SIFT path goes through SiftGPU,
which wants an OpenGL context; these runs were headless, so **whether that crash
is a real defect or an artifact of no display has not been established.** CPU
extraction costs seconds at this image count and sidesteps the question.

Dense PatchMatch — the step that genuinely needs CUDA — is on the code path
`gpu_mat_test` exercises, and COLMAP fixed empty PatchMatch results on `sm_100+`
in 4.0.3, so dense reconstruction is expected to work. Not yet run.

## Dense reconstruction, and the watertight problem

Run 2026-09-11: `image_undistorter` → `patch_match_stereo` (CUDA, ~15 min for 72
depth maps on an RTX 5070) → `stereo_fusion` (13.9 MB) → `poisson_mesher`
(40.8 MB). **Dense PatchMatch executes CUDA kernels on Blackwell**, which is the
step COLMAP fixed in 4.0.3 and the one `gpu_mat_test` only covered indirectly.

`inspect_mesh.py` then asks the two questions that decide whether the mesh can
enter ODC at all:

| | |
|---|---|
| Vertices / faces | 851,194 / 1,661,273 |
| Boundary edges | **40,161** |
| Non-manifold edges | 0 |
| Loose parts | **277** |
| Watertight | **false** |

ODC's mesh import is voxelisation and its v0 requires a closed mesh. **This mesh
would be refused**, which was the predicted outcome and is now measured.

### Why this is not merely inconvenient

`--PoissonMeshing.trim` defaults to `10`: it discards low-confidence regions,
which is what produces those 40,161 boundary edges. Setting `trim 0` yields a
*closed* surface — and the closure is **extrapolated into space no camera ever
observed.** Photogrammetry of an object standing on a platter cannot see its
underside; that surface does not exist in any image.

So for this input class the watertight requirement and the evidence are in
tension:

- **`trim 10`** — honest mesh, open where nothing was seen, **refused** by ODC.
- **`trim 0`** — closed mesh that **passes** ODC's validity gate while carrying
  invented geometry, with a provenance record that looks clean.

The second is the failure mode this repository exists to prevent, arriving
through the gate meant to prevent it. A watertight check cannot distinguish
observed surface from plausible surface, because both are closed.

### Measured: how much `trim 0` invents

The first attempt at this compared a mesh to the *nominal* part and was
worthless, because the crop box included a slab of platter — so the mesh was of
part-plus-platter and Poisson's extrapolation could not be separated from the
crop's own contents. It reported 81–93 % oversize and meant nothing.

The clean version crops above the support plane (`--z-min-mm 1.5`) so only the
part's observed surface is kept, and compares each mesh to **the cloud it was
built from** — same frame, same units, no nominal involved.

Observed point cloud: **31.32 × 21.58 × 14.14 mm** (107,433 points).

| | largest part | boundary edges | own-axes mm | vs cloud |
|---|---|---|---|---|
| `trim 0` | 363,634 v | **14** (≈closed) | 30.68 × **27.69** × **21.38** | **+28 %, +51 %** |
| `trim 5` | 362,607 v | **662** (open) | 30.68 × 21.38 × 15.69 | +11 % on Z only |

`trim 0` is essentially watertight — 14 boundary edges in a 363k-vertex mesh —
and **28–51 % larger on two axes than the data it was built from.** It would
pass ODC's validity gate and produce a cradle for a part half again too big,
under clean provenance.

`trim 5` tracks the data to 2–3 % on X and Y and is open, so ODC refuses it. Its
only real extrapolation is +11 % on Z, at the unobserved bottom — exactly where
Poisson smooths past the boundary before trimming. X holds at 30.68 mm under
both settings; the well-observed axis is stable.

**So "pick a trim value" is not the answer.** The two settings fail in opposite
directions and neither is usable.

### What the numbers suggest, and what still needs deciding

The only genuinely unobserved region is the bottom: `trim 5` gets the sides and
top right. Capping *that* mesh against a **declared support plane** would close
it without inventing a third of the part, and the cap would be evidence — the
object demonstrably rested on something flat — rather than extrapolation. It
also fits the existing grammar: declared never inferred, recorded in provenance,
refuses when absent.

**Still undecided, and deliberately so.** The mechanism is not obvious. Capping a
boundary loop is geometry, and ODC's non-goals say geometry algorithms belong
upstream in PicoGK/ShapeKernel. There may be an SDF-native route that avoids
meshing the closure at all — building a level set from the point cloud, or a
boolean against a half-space — which would sidestep the question rather than
answer it. That wants a plan before an ADR.

## Still not done

The end-to-end ADR-0020 round trip — measure a span in the mesh, pass
`--scale-ref-span`, confirm 30 × 20 × 15 mm comes back — is blocked on the
above, and on one thing `analyse_scale.py` deliberately does not compute.

Its pairwise-distance method recovers **scale without rotation**, which is what
makes it need no SVD. But the reconstruction's axes are COLMAP's, not the
scene's, so an axis-aligned bounding box in that frame is not comparable to
`30 × 20 × 15`. Measuring subject *dimensions* needs the full similarity,
rotation included. The largest loose part measures 0.979 × 0.620 × 1.379 units,
and 0.979 × 30.9015 = **30.3 mm** against a true 30 mm — suggestive, and not a
result, because two of those three axes are not the axes they appear to be.
