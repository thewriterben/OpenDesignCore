"""
Recover the reconstruction's scale, and test whether a scale is even the right
kind of correction.

    python analyse_scale.py --run <dir>

where <dir> holds ground-truth.json (from render_scene.py) and sparse-txt/
(from `colmap model_converter --output_type TXT`).

WHAT THIS MEASURES

Structure-from-motion recovers geometry up to an unknown *similarity*: a
rotation, a translation, and one scale. ADR-0020 leans on that word. If the
recovered geometry really is the true geometry times a single scalar, then one
reference measurement fixes the whole reconstruction and `length_mm /
span_file_units` is exact. If it is not — if there is skew, drift, or a radial
distortion the camera model failed to absorb — then no single scale is correct
and a reference merely picks which part of the model to be right about.

The test needs no alignment and no SVD. For every pair of cameras, take

    ratio(i,j) = d_true(i,j) / d_recon(i,j)

Rotation and translation cancel in a distance, so under a perfect similarity
every pair gives exactly the same ratio. The median is the scale; the spread is
the departure from similarity. A tight spread means one number can correct the
reconstruction. A wide one means ADR-0020's premise does not hold for this
capture, and the honest output is a refusal rather than an averaged scale.

Camera centres are used rather than scene points because we know where the
cameras were exactly — they are the ground truth the renderer wrote down.
"""

import argparse
import json
import math
import os


def quat_to_rotation(qw, qx, qy, qz):
    """COLMAP stores world-to-camera rotation as a Hamilton quaternion."""
    n = math.sqrt(qw * qw + qx * qx + qy * qy + qz * qz)
    qw, qx, qy, qz = qw / n, qx / n, qy / n, qz / n
    return [
        [1 - 2 * (qy * qy + qz * qz), 2 * (qx * qy - qz * qw), 2 * (qx * qz + qy * qw)],
        [2 * (qx * qy + qz * qw), 1 - 2 * (qx * qx + qz * qz), 2 * (qy * qz - qx * qw)],
        [2 * (qx * qz - qy * qw), 2 * (qy * qz + qx * qw), 1 - 2 * (qx * qx + qy * qy)],
    ]


def camera_centre(qw, qx, qy, qz, tx, ty, tz):
    """C = -R^T t. The translation alone is not the camera position."""
    R = quat_to_rotation(qw, qx, qy, qz)
    t = (tx, ty, tz)
    return tuple(-sum(R[r][c] * t[r] for r in range(3)) for c in range(3))


def read_colmap_images(path):
    centres = {}
    with open(path, "r", encoding="utf-8") as handle:
        lines = [ln.strip() for ln in handle if ln.strip() and not ln.startswith("#")]
    # Two lines per image; the second is the 2D points, which we do not need.
    for i in range(0, len(lines), 2):
        parts = lines[i].split()
        if len(parts) < 10:
            continue
        qw, qx, qy, qz, tx, ty, tz = (float(v) for v in parts[1:8])
        name = parts[9]
        centres[name] = camera_centre(qw, qx, qy, qz, tx, ty, tz)
    return centres


def distance(a, b):
    return math.sqrt(sum((a[k] - b[k]) ** 2 for k in range(3)))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run", required=True)
    opts = parser.parse_args()

    with open(os.path.join(opts.run, "ground-truth.json"), encoding="utf-8") as handle:
        truth = json.load(handle)

    true_centres = {v["image"]: tuple(v["position_mm"]) for v in truth["views"]}
    recon_centres = read_colmap_images(
        os.path.join(opts.run, "sparse-txt", "images.txt"))

    shared = sorted(set(true_centres) & set(recon_centres))
    print(f"views: {len(true_centres)} rendered, {len(recon_centres)} registered, "
          f"{len(shared)} shared")
    if len(shared) < 3:
        raise SystemExit("too few registered views to say anything")

    ratios = []
    for i in range(len(shared)):
        for j in range(i + 1, len(shared)):
            a, b = shared[i], shared[j]
            d_true = distance(true_centres[a], true_centres[b])
            d_recon = distance(recon_centres[a], recon_centres[b])
            if d_recon > 0:
                ratios.append(d_true / d_recon)

    ratios.sort()
    n = len(ratios)
    median = ratios[n // 2] if n % 2 else 0.5 * (ratios[n // 2 - 1] + ratios[n // 2])
    mean = sum(ratios) / n
    spread = ratios[-1] - ratios[0]
    stdev = math.sqrt(sum((r - mean) ** 2 for r in ratios) / n)

    print(f"\npairs compared: {n}")
    print(f"scale (mm per reconstruction unit)")
    print(f"  median : {median:.9f}")
    print(f"  mean   : {mean:.9f}")
    print(f"  min    : {ratios[0]:.9f}")
    print(f"  max    : {ratios[-1]:.9f}")
    print(f"\nsimilarity check — how far from ONE scale being correct")
    print(f"  absolute spread   : {spread:.3e}")
    print(f"  spread / median   : {spread / median:.6%}")
    print(f"  stdev / median    : {stdev / median:.6%}")

    # What a reference measurement would have to reproduce. This is the
    # quantity ADR-0020 derives from length_mm / span_file_units, so a real
    # reference disagreeing with this by more than the spread above is
    # measuring something other than the scale.
    print(f"\nfor ADR-0020: a reference of known length L mm should span")
    print(f"  L / {median:.9f} = L * {1.0 / median:.9f} reconstruction units")


if __name__ == "__main__":
    main()
