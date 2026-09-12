"""Render one ODC artifact to a PNG, deterministically (ADR-0022).

Run by ThumbnailRender inside headless Blender:

    blender --background --factory-startup --python blender_thumbnail.py -- <stl> <out.png>

Nothing here is chosen for looks, because a thumbnail that depended on taste
could not be reproduced. Every setting is either fixed or derived arithmetically
from the mesh's bounding box:

  * Workbench, not Cycles. Cycles path-traces with a sampler; Workbench
    rasterises. There is no seed to pin because there is nothing stochastic.
  * Anti-aliasing OFF, for the same reason: it is the one Workbench setting that
    samples.
  * The camera is ORTHOGRAPHIC, pointed down a fixed unit vector, placed on the
    bounding-box centre, and scaled to the box's diagonal with a fixed margin.
    A perspective camera would make the projection depend on distance, and
    "frame it nicely" is not a function anyone can re-run.
  * View transform Standard, look None: no tone curve between render and file.

Measured 2026-09-11 on run 50: two processes, byte-identical IDAT chunks and
identical decompressed pixels. The only bytes that differed were two tEXt
chunks, `Date` and `RenderTime` -- a clock and a stopwatch, which the caller
strips. See ADR-0022.

1 Blender unit == 1 mm. ADR-0004's rule applies at this boundary too, and
Blender 5.2's stl_import ignores global_scale (measured; the same trap
BLENDER-INTEGRATION.md records), so no scaling is asked of it.
"""

import json
import sys
import traceback

RESULT_PREFIX = "__ODC_RESULT__"
ERROR_PREFIX = "__ODC_ERROR__"

# Fixed viewing direction. A constant of this tool, not a preference: change it
# and every thumbnail ever rendered stops matching, which is why it lives here
# and is recorded in the record rather than being a flag.
VIEW_DIRECTION = (1.0, -1.0, 0.7)
MARGIN = 1.05
RESOLUTION = 512


def main() -> None:
    import bpy
    import mathutils

    argv = sys.argv[sys.argv.index("--") + 1:]
    stl_path, out_path = argv[0], argv[1]

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.stl_import(filepath=stl_path)

    selected = [o for o in bpy.context.selected_objects if o.type == "MESH"]
    if not selected:
        raise RuntimeError("the STL imported no mesh object")
    obj = selected[0]

    corners = [obj.matrix_world @ mathutils.Vector(c) for c in obj.bound_box]
    lo = mathutils.Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    hi = mathutils.Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    centre = (lo + hi) / 2.0
    extent = hi - lo
    if min(extent) <= 0.0:
        raise RuntimeError(f"degenerate bounding box {extent.x} x {extent.y} x {extent.z}")

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = RESOLUTION
    scene.render.resolution_y = RESOLUTION
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.compression = 15
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.display.render_aa = "OFF"

    direction = mathutils.Vector(VIEW_DIRECTION).normalized()
    ortho_scale = extent.length * MARGIN

    cam_data = bpy.data.cameras.new("odc_cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = ortho_scale
    cam = bpy.data.objects.new("odc_cam", cam_data)
    scene.collection.objects.link(cam)
    # Distance only has to clear the geometry: an orthographic projection does
    # not change with it, so it is a multiple of the extent and not a tuned number.
    cam.location = centre + direction * (max(extent) * 4.0)
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = (-direction).to_track_quat("-Z", "Y")
    scene.camera = cam

    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.studio_light = "Default"
    shading.color_type = "SINGLE"
    shading.single_color = (0.8, 0.8, 0.8)
    shading.show_cavity = False
    shading.show_shadows = False

    scene.render.filepath = out_path
    bpy.ops.render.render(write_still=True)

    print(RESULT_PREFIX + json.dumps({
        "blender": bpy.app.version_string,
        "engine": "BLENDER_WORKBENCH",
        "resolution_px": RESOLUTION,
        "anti_aliasing": "OFF",
        "view_transform": "Standard",
        "projection": "ORTHO",
        "view_direction": list(VIEW_DIRECTION),
        "margin": MARGIN,
        "ortho_scale": ortho_scale,
        "extent_mm": {"x": extent.x, "y": extent.y, "z": extent.z},
        "vertices": len(obj.data.vertices),
        "faces": len(obj.data.polygons),
    }))


try:
    main()
except Exception as exc:  # noqa: BLE001 - the message is the caller's error
    print(ERROR_PREFIX + json.dumps(f"{exc}\n{traceback.format_exc()}"))
