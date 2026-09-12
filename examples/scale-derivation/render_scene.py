"""
Render a synthetic turntable capture with exact ground truth.

Run headless:

    blender -b -P render_scene.py -- --out <dir> [--views 36]

Why synthetic. ADR-0020 makes a photogrammetry mesh's scale *derived* from a
reference: `length_mm / span_file_units`. Every test of that today feeds a
PicoGK sphere and a hand-written span, which exercises the arithmetic and not
the claim. A real capture would exercise the claim but bring caliper-limited
ground truth (0.02 mm at best) and no way to know the true camera geometry.

A render gives both: a genuinely scale-free reconstruction problem — COLMAP
does not know how big the scene is, and nothing in the images tells it — with
ground truth known exactly rather than measured.

What it is not. This does not test lens distortion, focus, motion blur, sensor
noise, lighting variation, or any real-world failure. A reconstruction that
works here is not evidence that a phone capture works. It is evidence about the
scale pipeline specifically, which is the part ADR-0020 touches.

Units: one Blender unit is treated as one millimetre throughout. Blender's own
unit system is left alone deliberately — introducing it would add a conversion
this script does not need and ADR-0004 says conversions live at boundaries, not
scattered.
"""

import argparse
import json
import math
import sys

import bpy
from mathutils import Vector

# Ground truth, mm. Deliberately unequal for the reason calibration-block/0.2
# states: equal dimensions make a transposed reading invisible.
SUBJECT_X_MM = 30.0
SUBJECT_Y_MM = 20.0
SUBJECT_Z_MM = 15.0

CAMERA_RADIUS_MM = 130.0
CAMERA_ELEVATION_DEG = 28.0


def args_after_double_dash():
    argv = sys.argv
    return argv[argv.index("--") + 1:] if "--" in argv else []


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def noisy_material(name, scale, seed_offset):
    """
    A procedurally textured surface. Structure-from-motion needs *visual*
    detail: a smooth untextured object gives the matcher nothing to match, and
    the reconstruction fails for reasons that have nothing to do with scale.
    Roughness is high on purpose — specular highlights move with the camera and
    are not features of the surface.
    """
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()

    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    noise = nt.nodes.new("ShaderNodeTexNoise")
    coord = nt.nodes.new("ShaderNodeTexCoord")

    # Hard contrast. SIFT keys on image gradients, not on whether a human can
    # see a pattern: the first textured render was visibly noisy and still
    # nearly gradient-free, because Noise's own output is low-contrast pastel.
    # A steep ramp turns it into something a detector can actually key on.
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    ramp.color_ramp.elements[0].position = 0.42
    ramp.color_ramp.elements[0].color = (0.02, 0.02, 0.02, 1.0)
    ramp.color_ramp.elements[1].position = 0.58
    ramp.color_ramp.elements[1].color = (0.95, 0.95, 0.95, 1.0)

    noise.inputs["Scale"].default_value = scale
    noise.inputs["Detail"].default_value = 8.0
    if "W" in noise.inputs:
        noise.inputs["W"].default_value = float(seed_offset)

    bsdf.inputs["Roughness"].default_value = 0.9
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.1

    # `Generated`, not `Object`. Noise Scale multiplies the incoming
    # coordinate, and Object coordinates here are in millimetres — tens of
    # units across the subject — so a scale tuned for a unit cube produces
    # frequencies far below a pixel and every surface renders as flat grey.
    # Generated coordinates are normalised to the bounding box, so the same
    # scale means the same visible detail whatever size the part is.
    #
    # This was not theoretical: the first render came back smooth and would
    # have given COLMAP nothing to match, failing in a way that looks like a
    # scale problem and is a texture problem.
    nt.links.new(coord.outputs["Generated"], noise.inputs["Vector"])
    nt.links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def build_scene():
    clear_scene()
    scene = bpy.context.scene

    # Subject, centred on the origin with its base on z = 0 like a part on a
    # platter. Dimensions are set explicitly rather than scaled, so the ground
    # truth below is a statement about the mesh and not about a scale factor.
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, SUBJECT_Z_MM / 2.0))
    subject = bpy.context.active_object
    subject.name = "subject"
    subject.scale = (SUBJECT_X_MM, SUBJECT_Y_MM, SUBJECT_Z_MM)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    subject.data.materials.append(noisy_material("subject_mat", 22.0, 1))

    # A textured platter. It co-rotates with the subject in a real turntable
    # capture; here the camera moves instead, which is the same relative motion
    # and avoids the background-masking problem entirely.
    bpy.ops.mesh.primitive_plane_add(size=220.0, location=(0, 0, 0))
    platter = bpy.context.active_object
    platter.name = "platter"
    platter.data.materials.append(noisy_material("platter_mat", 55.0, 2))

    # Three-point-ish lighting, kept soft. Hard shadows moving across a surface
    # between frames are a matching hazard.
    # Energies are ~10x lower than the first pass: that render was blown out,
    # and a clipped highlight has no gradient left for a detector to find.
    for i, (x, y, z, energy) in enumerate([
        (200, -200, 250, 6.0e5),
        (-220, -120, 200, 3.0e5),
        (0, 240, 180, 3.0e5),
    ]):
        bpy.ops.object.light_add(type="AREA", location=(x, y, z))
        light = bpy.context.active_object
        light.name = f"key_{i}"
        light.data.energy = energy
        light.data.size = 150.0

    cam_data = bpy.data.cameras.new("cam")
    cam_data.lens = 50.0
    cam_data.sensor_width = 36.0
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    return subject, cam


def aim_at_origin(cam, target=Vector((0.0, 0.0, SUBJECT_Z_MM / 2.0))):
    direction = target - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def configure_render(scene, width, height):
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False

    # EEVEE is enough: this tests geometry recovery, not light transport, and
    # Cycles would spend minutes per frame for detail SfM discards.
    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue

    scene.world = bpy.data.worlds.new("world")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.05, 0.05, 0.06, 1.0)
        bg.inputs[1].default_value = 1.0


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    parser.add_argument("--views", type=int, default=36)
    parser.add_argument("--width", type=int, default=1600)
    parser.add_argument("--height", type=int, default=1200)
    opts = parser.parse_args(args_after_double_dash())

    import os
    images_dir = os.path.join(opts.out, "images")
    os.makedirs(images_dir, exist_ok=True)

    subject, cam = build_scene()
    scene = bpy.context.scene
    configure_render(scene, opts.width, opts.height)

    elevation = math.radians(CAMERA_ELEVATION_DEG)
    z = CAMERA_RADIUS_MM * math.sin(elevation)
    r = CAMERA_RADIUS_MM * math.cos(elevation)

    cameras = []
    for i in range(opts.views):
        theta = 2.0 * math.pi * i / opts.views
        cam.location = Vector((r * math.cos(theta), r * math.sin(theta), z))
        aim_at_origin(cam)
        bpy.context.view_layer.update()

        name = f"view_{i:03d}.png"
        scene.render.filepath = os.path.join(images_dir, name)
        bpy.ops.render.render(write_still=True)

        cameras.append({
            "image": name,
            "position_mm": [round(v, 6) for v in cam.location],
        })
        print(f"[render] {name}", flush=True)

    # Ground truth. The subject dimensions are what the scaled mesh must come
    # back as; the camera positions are what the recovered poses are aligned
    # against to recover the scale factor. Both are exact — that is the whole
    # reason for rendering rather than photographing.
    truth = {
        "schema": "odc/synthetic-capture/0.1",
        "units": "mm",
        "subject": {
            "name": subject.name,
            "dimensions_mm": {
                "x": SUBJECT_X_MM,
                "y": SUBJECT_Y_MM,
                "z": SUBJECT_Z_MM,
            },
        },
        "camera": {
            "lens_mm": cam.data.lens,
            "sensor_width_mm": cam.data.sensor_width,
            "radius_mm": CAMERA_RADIUS_MM,
            "elevation_deg": CAMERA_ELEVATION_DEG,
            "resolution": [opts.width, opts.height],
        },
        "views": cameras,
        "blender": bpy.app.version_string,
    }

    truth_path = os.path.join(opts.out, "ground-truth.json")
    with open(truth_path, "w", encoding="utf-8") as handle:
        json.dump(truth, handle, indent=2, sort_keys=True)

    print(f"[render] {opts.views} views -> {images_dir}", flush=True)
    print(f"[render] ground truth -> {truth_path}", flush=True)


if __name__ == "__main__":
    main()
