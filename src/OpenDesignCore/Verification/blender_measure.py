# Measure an STL with Blender's mesh kernel and print one JSON line.
#
# Embedded in OpenDesignCore and run as
#     blender --background --factory-startup --python <this> -- <stl>
# by BlenderCrossCheck (ADR-0017). It measures; it never renders, never saves,
# never writes anything but stdout. Everything it reports is in millimetres:
# ODC's STLs are written in mm (Mesh.EStlUnit.MM), and Blender's STL importer
# reads unit-less STL coordinates as scene units, so 1 unit here is 1 mm.
#
# Blender 5.2's `stl_import(global_scale=...)` was observed to accept and not
# apply the argument (2026-09-07). No scale is requested; the extent is read.
import bmesh
import bpy
import json
import sys

stl = sys.argv[sys.argv.index("--") + 1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.stl_import(filepath=stl)
objs = list(bpy.context.selected_objects) or list(bpy.context.scene.objects)
if len(objs) != 1:
    print("__ODC_ERROR__" + json.dumps("expected one object from the STL, got %d" % len(objs)))
    sys.exit(0)
me = objs[0].data

bm = bmesh.new()
bm.from_mesh(me)
non_manifold_edges = sum(1 for e in bm.edges if not e.is_manifold)
boundary_edges = sum(1 for e in bm.edges if e.is_boundary)
volume = bm.calc_volume(signed=True)
bm.free()

xs = [v.co.x for v in me.vertices]
ys = [v.co.y for v in me.vertices]
zs = [v.co.z for v in me.vertices]
extent = [max(a) - min(a) if a else 0.0 for a in (xs, ys, zs)]

print("__ODC_RESULT__" + json.dumps({
    "blender": bpy.app.version_string,
    "vertices": len(me.vertices),
    "faces": len(me.polygons),
    "bbox_mm": {"x": extent[0], "y": extent[1], "z": extent[2]},
    "volume_cubic_mm": volume,
    "non_manifold_edges": non_manifold_edges,
    "boundary_edges": boundary_edges,
}))
