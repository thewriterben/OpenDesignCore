"""
Inspect a reconstructed mesh and export the subject alone.

    blender -b -P inspect_mesh.py -- --ply <fused-or-meshed.ply> --out <dir>

Two questions, both of which decide whether an ADR-0020 round trip is even
possible with this input:

1. **Is it watertight?** ODC's mesh import is voxelisation, and v0 says plainly
   that it needs a closed mesh. Photogrammetry of an object standing on a plane
   cannot see underneath it, so the underside is missing by construction, and
   Poisson's closure of that gap is a guess rather than a measurement. Counting
   non-manifold and boundary edges says which we have.

2. **How big is the subject, in reconstruction units?** The mesh holds the
   subject *and* the platter, so its overall bounding box is the scene, not the
   part. Separating loose parts and taking the one that is not the ground plane
   gives the span that `--scale-ref-span` wants.

Nothing here is a measurement of ODC's correctness. It is a measurement of
whether COLMAP's output can pass ODC's front door at all.
"""

import argparse
import json
import os
import sys

import bmesh
import bpy


def args_after_double_dash():
    argv = sys.argv
    return argv[argv.index("--") + 1:] if "--" in argv else []


def mesh_report(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)

    boundary = sum(1 for e in bm.edges if len(e.link_faces) == 1)
    nonmanifold = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    wire = sum(1 for e in bm.edges if len(e.link_faces) == 0)

    report = {
        "vertices": len(bm.verts),
        "edges": len(bm.edges),
        "faces": len(bm.faces),
        "boundary_edges": boundary,
        "nonmanifold_edges": nonmanifold,
        "wire_edges": wire,
        # Watertight in the sense voxelisation needs: every edge shared by
        # exactly two faces. A single boundary edge means an open surface.
        "watertight": boundary == 0 and nonmanifold == 0 and wire == 0,
    }
    bm.free()
    return report


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--ply", required=True)
    parser.add_argument("--out", required=True)
    opts = parser.parse_args(args_after_double_dash())

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.ply_import(filepath=opts.ply)

    obj = bpy.context.selected_objects[0]
    bpy.context.view_layer.objects.active = obj

    whole = mesh_report(obj)
    whole["bbox_units"] = [round(v, 6) for v in obj.dimensions]

    # Loose parts. The platter is the piece with the largest footprint; the
    # subject is the largest of the rest. Both are reported so a reader can see
    # the choice rather than trust it.
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")

    parts = []
    for part in bpy.context.scene.objects:
        if part.type != "MESH" or len(part.data.vertices) == 0:
            continue
        d = part.dimensions
        parts.append({
            "name": part.name,
            "vertices": len(part.data.vertices),
            "faces": len(part.data.polygons),
            "dimensions_units": [round(v, 6) for v in d],
            "footprint": round(d.x * d.y, 6),
        })

    parts.sort(key=lambda p: p["vertices"], reverse=True)

    result = {
        "schema": "odc/mesh-inspection/0.1",
        "source_ply": os.path.basename(opts.ply),
        "whole_mesh": whole,
        "loose_parts": parts[:10],
        "loose_part_count": len(parts),
        "blender": bpy.app.version_string,
    }

    os.makedirs(opts.out, exist_ok=True)
    path = os.path.join(opts.out, "mesh-inspection.json")
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(result, handle, indent=2, sort_keys=True)

    print(json.dumps(result, indent=2, sort_keys=True), flush=True)
    print(f"[inspect] -> {path}", flush=True)


if __name__ == "__main__":
    main()
