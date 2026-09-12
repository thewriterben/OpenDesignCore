"""
Crop the dense cloud to the subject, in the reconstruction's own frame.

    blender -b -P crop_cloud.py -- --run <dir> [--margin-mm 4]

Run under Blender only because Blender ships numpy and the system Python here
does not; nothing in this script touches bpy.

WHY

The first dense mesh came back as 277 loose parts, which reads as "photogrammetry
produces fragments" and is probably an artefact of this scene: the platter is
textured to help matching, so fusion reconstructs platter debris as separate
objects. A real turntable capture masks the background for exactly this reason.

Before designing any policy about closing open meshes, the question is whether
the *subject alone* is one shell with one boundary loop. That needs the subject
isolated, which needs knowing where it is in COLMAP's arbitrary frame.

HOW

`analyse_scale.py` deliberately recovers scale without rotation — pairwise
distances, no SVD. That is enough for ADR-0020 and not enough here: to place a
crop box we need the full similarity. So this fits one properly (Umeyama) from
the camera correspondences, which are exact on both sides, and reports the
residual so the fit can be judged rather than trusted.

The crop box is defined in millimetres around the subject's known position and
then mapped *into* COLMAP coordinates. Points are never transformed, so the
output cloud stays in reconstruction units and `colmap poisson_mesher` consumes
it exactly as before. Scale semantics are unchanged, which matters: rescaling
the cloud here would silently do the job ADR-0020 says a declared reference must
do.
"""

import argparse
import json
import os
import struct
import sys

import numpy as np

PLY_TYPES = {
    "float": ("f", 4), "float32": ("f", 4),
    "double": ("d", 8), "float64": ("d", 8),
    "uchar": ("B", 1), "uint8": ("B", 1),
    "char": ("b", 1), "int8": ("b", 1),
    "ushort": ("H", 2), "uint16": ("H", 2),
    "short": ("h", 2), "int16": ("h", 2),
    "uint": ("I", 4), "uint32": ("I", 4),
    "int": ("i", 4), "int32": ("i", 4),
}


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
    with open(path, "r", encoding="utf-8") as handle:
        lines = [ln.strip() for ln in handle if ln.strip() and not ln.startswith("#")]
    for i in range(0, len(lines), 2):
        p = lines[i].split()
        if len(p) < 10:
            continue
        R = quat_to_rotation(*(float(v) for v in p[1:5]))
        t = np.array([float(v) for v in p[5:8]])
        centres[p[9]] = -R.T @ t
    return centres


def umeyama(src, dst):
    """Similarity taking src to dst: dst ~= s * R @ src + t."""
    n = len(src)
    mu_s, mu_d = src.mean(0), dst.mean(0)
    sc, dc = src - mu_s, dst - mu_d
    cov = dc.T @ sc / n
    U, D, Vt = np.linalg.svd(cov)
    S = np.eye(3)
    if np.linalg.det(U) * np.linalg.det(Vt) < 0:
        S[2, 2] = -1
    R = U @ S @ Vt
    var_s = (sc ** 2).sum() / n
    s = np.trace(np.diag(D) @ S) / var_s
    t = mu_d - s * R @ mu_s
    return s, R, t


def read_ply(path):
    with open(path, "rb") as handle:
        header, line = [], b""
        while True:
            line = handle.readline()
            header.append(line.decode("ascii").strip())
            if header[-1] == "end_header":
                break
        data = handle.read()

    fmt, count, props = None, 0, []
    in_vertex = False
    for ln in header:
        parts = ln.split()
        if not parts:
            continue
        if parts[0] == "format":
            fmt = parts[1]
        elif parts[0] == "element":
            in_vertex = parts[1] == "vertex"
            if in_vertex:
                count = int(parts[2])
        elif parts[0] == "property" and in_vertex:
            props.append((parts[1], parts[2]))

    if fmt != "binary_little_endian":
        raise SystemExit(f"only binary_little_endian is handled; got {fmt}")

    struct_fmt = "<" + "".join(PLY_TYPES[t][0] for t, _ in props)
    stride = struct.calcsize(struct_fmt)
    names = [n for _, n in props]
    return header, struct_fmt, stride, names, count, data


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run", required=True)
    parser.add_argument("--margin-mm", type=float, default=4.0)
    parser.add_argument(
        "--z-min-mm", type=float, default=None,
        help="Floor of the crop in world mm. Default -margin, which includes a "
             "slab of platter under and around the part. Set above the support "
             "plane (e.g. 1.5) to keep ONLY the part's observed surface — the "
             "first run did not, and the resulting mesh was of part-plus-platter, "
             "which made Poisson's extrapolation impossible to separate from the "
             "crop's own contents.")
    parser.add_argument("--out-name", default="cropped.ply")
    opts = parser.parse_args(args_after_double_dash())

    with open(os.path.join(opts.run, "ground-truth.json"), encoding="utf-8") as h:
        truth = json.load(h)

    true_c = {v["image"]: np.array(v["position_mm"]) for v in truth["views"]}
    recon_c = read_colmap_centres(os.path.join(opts.run, "sparse-txt", "images.txt"))
    shared = sorted(set(true_c) & set(recon_c))

    src = np.array([recon_c[k] for k in shared])   # COLMAP units
    dst = np.array([true_c[k] for k in shared])    # millimetres
    s, R, t = umeyama(src, dst)

    residual = np.linalg.norm(dst - (s * (R @ src.T).T + t), axis=1)
    print(f"[fit] views: {len(shared)}")
    print(f"[fit] scale: {s:.9f} mm per reconstruction unit")
    print(f"[fit] residual mm  mean {residual.mean():.6f}  max {residual.max():.6f}")

    dims = truth["subject"]["dimensions_mm"]
    m = opts.margin_mm
    z_min = -m if opts.z_min_mm is None else opts.z_min_mm
    lo = np.array([-dims["x"] / 2 - m, -dims["y"] / 2 - m, z_min])
    hi = np.array([dims["x"] / 2 + m, dims["y"] / 2 + m, dims["z"] + m])
    print(f"[crop] world box mm: {lo.round(2).tolist()} .. {hi.round(2).tolist()}")

    ply_path = os.path.join(opts.run, "dense", "fused.ply")
    header, fmt, stride, names, count, data = read_ply(ply_path)
    ix, iy, iz = names.index("x"), names.index("y"), names.index("z")
    print(f"[crop] input points: {count:,}")

    # World -> COLMAP, applied to the point once per vertex.
    kept = bytearray()
    n_kept = 0
    kept_world = []
    for i in range(count):
        rec = struct.unpack_from(fmt, data, i * stride)
        p_recon = np.array([rec[ix], rec[iy], rec[iz]])
        p_world = s * (R @ p_recon) + t
        if np.all(p_world >= lo) and np.all(p_world <= hi):
            kept += data[i * stride:(i + 1) * stride]
            kept_world.append(p_world)
            n_kept += 1

    print(f"[crop] kept: {n_kept:,} ({100.0 * n_kept / count:.2f}%)")
    if n_kept == 0:
        raise SystemExit("crop kept nothing — the fit or the box is wrong")

    # The observed extent. Any mesh built from this cloud that measures larger
    # is extrapolating, and this is the baseline that makes that visible —
    # comparing a mesh to the *nominal* part cannot separate Poisson's invention
    # from whatever the crop happened to contain.
    kw = np.array(kept_world)
    aabb = kw.max(0) - kw.min(0)
    centred = kw - kw.mean(0)
    _, _, Vt = np.linalg.svd(centred, full_matrices=False)
    oriented = (centred @ Vt.T).max(0) - (centred @ Vt.T).min(0)
    print(f"[cloud] observed extent, world mm")
    print(f"        axis-aligned : {[round(v, 3) for v in aabb]}")
    print(f"        own axes     : {[round(v, 3) for v in sorted(oriented, reverse=True)]}")

    out_path = os.path.join(opts.run, "dense", opts.out_name)
    with open(out_path, "wb") as handle:
        for ln in header:
            if ln.startswith("element vertex"):
                handle.write(f"element vertex {n_kept}\n".encode("ascii"))
            elif ln.startswith("element face"):
                handle.write(b"element face 0\n")
            else:
                handle.write((ln + "\n").encode("ascii"))
        handle.write(bytes(kept))

    print(f"[crop] -> {out_path}")


if __name__ == "__main__":
    main()
