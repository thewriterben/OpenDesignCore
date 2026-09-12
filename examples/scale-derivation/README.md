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

## Not done yet

Dense reconstruction and meshing. A true end-to-end ADR-0020 round trip is:
export a mesh, measure a span in it, pass `--scale-ref-span`, and confirm the
subject returns as 30 × 20 × 15 mm. **The camera-derived scale above is the
ground truth that round trip has to reproduce.** Note also that ODC's mesh
import requires a watertight mesh (v0), and Poisson output is not automatically
watertight — expect that to be the next thing that bites.
