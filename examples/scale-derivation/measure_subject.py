"""
Measure a reconstructed mesh in WORLD millimetres, against ground truth.

    blender -b -P measure_subject.py -- --run <dir> --ply <mesh.ply>

Every extent reported before this script was axis-aligned in COLMAP's frame,
which is rotated by an unknown amount relative to the scene. A 30 x 20 x 15 mm
box under arbitrary rotation has an axis-aligned bounding box of up to 39 mm on
a side, so a reading of "43 x 38 x 37" in that frame is consistent with a
correctly sized part and proves nothing either way. Comparing it to 30 x 20 x 15
would be comparing two different quantities.

This applies the similarity recovered from the camera correspondences, putting
the mesh in the same frame and units as the ground truth, and reports:

  * the mesh's extent in world mm, both axis-aligned and via its own principal
    axes (the latter is rotation-free and is the honest "how big is this part"),
  * the extent of the point cloud it was built from, so that surface Poisson
    invented beyond the observed data is visible as a difference rather than
    assumed absent.

The second is the measurement the trim question turns on. `trim 0` closes a
surface by extrapolating past the data; if that extrapolation is small the
closed mesh is usable, and if it is large the closure is inventing the part.
"""

import argparse
import json
import os
import sys

import bmesh
import bpy
import numpy as np


def args_after_double_dash():
    argv = sys.argv
    return argv[argv.index("--") + 1:] if "--" in argv else []


def quat_to_rotation(qw, qx, qy, qz):
    n = np.sqrt(qw * qw + qx * qx + qy * qy + qz * qz)
    qw, qx, qy, qz = qw / n, qx / n, qy / n, qz / n
    return np.array([
        [1 - 2 * (qy * qy + qz * qz), 2 * (qx * qy - qz * qw), 2 * (qx * qz + qy * qw)],
        [2 * (qx * qy + qz * qw), 1 - 2 * (qx * qx + qz * qz), 2 * (qy * qz - qx * qw)],
        [2 * (qx * qz - qy * qw), 2 * (qy * qz + qx * qw), 1 - 2 * (qx * qx + qy * qy)],
    ])


def read_colmap_centres(path):
    centres = {}
    with open(path, encoding="utf-8") as handle:
        lines = [ln.strip() for ln in handle if ln.strip() and not ln.startswith("#")]
    for i in range(0, len(lines), 2):
        p = lines[i].split()
        if len(p) >= 10:
            R = quat_to_rotation(*(float(v) for v in p[1:5]))
            t = np.array([float(v) for v in p[5:8]])
            centres[p[9]] = -R.T @ t
    return centres


def umeyama(src, dst):
    n = len(src)
    mu_s, mu_d = src.mean(0), dst.mean(0)
    sc, dc = src - mu_s, dst - mu_d
    U, D, Vt = np.linalg.svd(dc.T @ sc / n)
    S = np.eye(3)
    if np.linalg.det(U) * np.linalg.det(Vt) < 0:
        S[2, 2] = -1
    R = U @ S @ Vt
    s = np.trace(np.diag(D) @ S) / ((sc ** 2).sum() / n)
    return s, R, mu_d - s * R @ mu_s


def extents(points):
    """Axis-aligned, and along the cloud's own principal axes."""
    aabb = points.max(0) - points.min(0)
    centred = points - points.mean(0)
    # Principal axes: the rotation that makes the part's own sides axis-aligned.
    _, _, Vt = np.linalg.svd(centred, full_matrices=False)
    oriented = centred @ Vt.T
    return aabb, oriented.max(0) - oriented.min(0)


def largest_part_vertices():
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")

    best, best_n = None, -1
    for ob in bpy.context.scene.objects:
        if ob.type == "MESH" and len(ob.data.vertices) > best_n:
            best, best_n = ob, len(ob.data.vertices)

    bm = bmesh.new()
    bm.from_mesh(best.data)
    boundary = sum(1 for e in bm.edges if len(e.link_faces) == 1)
    nonmanifold = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    bm.free()

    pts = np.array([tuple(v.co) for v in best.data.vertices])
    return pts, {
        "name": best.name,
        "vertices": best_n,
        "boundary_edges": boundary,
        "nonmanifold_edges": nonmanifold,
        "watertight": boundary == 0 and nonmanifold == 0,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run", required=True)
    parser.add_argument("--ply", required=True)
    opts = parser.parse_args(args_after_double_dash())

    with open(os.path.join(opts.run, "ground-truth.json"), encoding="utf-8") as h:
        truth = json.load(h)
    true_c = {v["image"]: np.array(v["position_mm"]) for v in truth["views"]}
    recon_c = read_colmap_centres(os.path.join(opts.run, "sparse-txt", "images.txt"))
    shared = sorted(set(true_c) & set(recon_c))
    s, R, t = umeyama(np.array([recon_c[k] for k in shared]),
                      np.array([true_c[k] for k in shared]))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.ply_import(filepath=opts.ply)
    obj = bpy.context.selected_objects[0]
    bpy.context.view_layer.objects.active = obj

    pts, report = largest_part_vertices()
    world = (s * (R @ pts.T).T) + t
    aabb, oriented = extents(world)

    d = truth["subject"]["dimensions_mm"]
    nominal = np.array(sorted([d["x"], d["y"], d["z"]], reverse=True))
    measured = np.array(sorted(oriented, reverse=True))

    print(json.dumps({
        "mesh": os.path.basename(opts.ply),
        "largest_part": report,
        "world_mm": {
            "axis_aligned": [round(v, 3) for v in aabb],
            "own_axes_sorted": [round(v, 3) for v in measured],
        },
        "nominal_sorted_mm": [round(v, 3) for v in nominal],
        "error_mm": [round(v, 3) for v in (measured - nominal)],
        "error_pct": [round(v, 3) for v in 100.0 * (measured - nominal) / nominal],
        "scale_used_mm_per_unit": round(float(s), 9),
    }, indent=2), flush=True)


if __name__ == "__main__":
    main()
