"""render_previews.py - neutral, reproducible studio preview renders for any vehicle.

Same camera rig, lighting, ground and render settings for every car, so style
mismatches between cars are easy to spot.

Usage:
    blender -b -P render_previews.py -- --input CAR-009_Working_v001.blend --out-dir Previews \
        --prefix CAR-009 [--forward +Y] [--views all] [--samples 48] [--res 1280 800]

Views: front rear left right front34 rear34 top hero (default: all). The script
loads the input, adds its own studio (never saved back into the input file).
"""

import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import pipeline_common as pc  # noqa: E402

# Azimuth is measured around +Z, 0 = looking at the car's FRONT (camera on +forward side).
VIEWS = {
    "front":   {"az": 0,    "el": 4,  "ortho": True},
    "rear":    {"az": 180,  "el": 4,  "ortho": True},
    "left":    {"az": -90,  "el": 2,  "ortho": True},   # driver-left side (camera on -X for +Y fwd)
    "right":   {"az": 90,   "el": 2,  "ortho": True},
    "front34": {"az": 38,   "el": 14, "ortho": False},
    "rear34":  {"az": 148,  "el": 16, "ortho": False},
    "top":     {"az": 0,    "el": 89.9, "ortho": True},
    "hero":    {"az": -32,  "el": 9,  "ortho": False},
}

# Palette for the studio: warm hazy "golden hour" look.
SKY = (1.0, 0.93, 0.82)
GROUND = (0.93, 0.86, 0.78)


def parse():
    p = argparse.ArgumentParser()
    p.add_argument("--input", required=True)
    p.add_argument("--out-dir", required=True)
    p.add_argument("--prefix", required=True)
    p.add_argument("--forward", default="+Y", choices=["+Y", "-Y", "+X", "-X"])
    p.add_argument("--views", nargs="+", default=["all"])
    p.add_argument("--samples", type=int, default=48)
    p.add_argument("--res", nargs=2, type=int, default=[1280, 800])
    p.add_argument("--ignore-collections", nargs="*", default=["Collision", "CAR_Collision"],
                   help="Collections hidden from render (e.g. collision proxies)")
    return p.parse_args(pc.script_args())


def forward_yaw(fwd):
    # Rotation (deg, around Z) that maps the studio's +Y onto the model's forward axis.
    return {"+Y": 0, "-Y": 180, "+X": -90, "-X": 90}[fwd]


def make_mat(name, color, rough=0.9):
    m = bpy.data.materials.new(name)
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = rough
    return m


def build_studio(scene, bbox, samples, res):
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 6
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast" if "AgX - Medium High Contrast" in [
        i.identifier for i in scene.view_settings.bl_rna.properties["look"].enum_items] else "None"
    scene.view_settings.exposure = 0.25

    world = bpy.data.worlds.new("Studio_World")
    scene.world = world
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (*SKY, 1)
    bg.inputs["Strength"].default_value = 0.85

    span = max(bbox["size"]) * 30
    bpy.ops.mesh.primitive_plane_add(size=span, location=(0, 0, bbox["min"][2]))
    ground = bpy.context.active_object
    ground.name = "Studio_Ground"
    ground.data.materials.append(make_mat("Studio_Ground_Mat", GROUND, 1.0))

    # Key light: warm low sun from front-left, soft shadows.
    sun = bpy.data.lights.new("Studio_Key", "SUN")
    sun.energy = 4.2
    sun.color = (1.0, 0.85, 0.66)
    sun.angle = math.radians(4)
    key = bpy.data.objects.new("Studio_Key", sun)
    key.rotation_euler = (math.radians(56), 0, math.radians(-128))
    scene.collection.objects.link(key)
    # Cool fill from the opposite side, no shadows.
    fill = bpy.data.lights.new("Studio_Fill", "SUN")
    fill.energy = 0.45
    fill.color = (0.80, 0.86, 1.0)
    fill.use_shadow = False
    fo = bpy.data.objects.new("Studio_Fill", fill)
    fo.rotation_euler = (math.radians(65), 0, math.radians(40))
    scene.collection.objects.link(fo)


def place_camera(scene, cam_obj, bbox, view, yaw_deg):
    center = Vector(bbox["center"])
    size = Vector(bbox["size"])
    radius = size.length / 2
    az = math.radians(view["az"] + yaw_deg)
    el = math.radians(view["el"])
    # Camera direction: azimuth 0 => camera on +Y looking toward -Y (sees the car front for +Y fwd)
    d = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))
    cam = cam_obj.data
    aspect = scene.render.resolution_x / scene.render.resolution_y
    target = center.copy()
    if view["ortho"]:
        cam.type = "ORTHO"
        # Fit the projected extents.
        right = d.cross(Vector((0, 0, 1)))
        if right.length < 1e-3:
            right = Vector((1, 0, 0)).cross(d) if abs(d.z) > 0.99 else right
        right.normalize()
        up = right.cross(d).normalized()
        corners = [Vector((x, y, z)) for x in (bbox["min"][0], bbox["max"][0])
                   for y in (bbox["min"][1], bbox["max"][1]) for z in (bbox["min"][2], bbox["max"][2])]
        w = max(abs((c - center).dot(right)) for c in corners) * 2
        h = max(abs((c - center).dot(up)) for c in corners) * 2
        cam.ortho_scale = max(w, h * aspect) * 1.18
        dist = radius * 6
        if view["el"] > 80:
            # top view: car lies horizontally with its nose pointing right
            cam_obj.location = center + Vector((0, 0, dist))
            cam_obj.rotation_euler = (0, 0, math.radians(yaw_deg + 90))
            length = size.y if yaw_deg in (0, 180) else size.x
            width = size.x if yaw_deg in (0, 180) else size.y
            cam.ortho_scale = max(length, width * aspect) * 1.12
            cam.clip_end = dist * 3
            return
    else:
        cam.type = "PERSP"
        cam.lens = 55
        fov = 2 * math.atan(cam.sensor_width / (2 * cam.lens))
        dist = radius / math.sin(fov / 2) * 0.82
        target.z = bbox["min"][2] + size.z * 0.42
    cam.clip_end = dist * 4
    cam_obj.location = target + d * dist
    look = (target - cam_obj.location).normalized()
    cam_obj.rotation_euler = look.to_track_quat("-Z", "Y").to_euler()


def main():
    args = parse()
    src = os.path.abspath(args.input)
    pc.reset_scene()
    pc.import_model(src)
    scene = bpy.context.scene
    for coll in bpy.data.collections:
        if coll.name in args.ignore_collections:
            coll.hide_render = True
    # Never render lights/cameras that came with the asset.
    for o in list(scene.objects):
        if o.type in ("LIGHT", "CAMERA"):
            o.hide_render = True
    meshes = [o for o in scene.objects if o.type == "MESH" and not o.hide_render
              and not any(c.hide_render for c in o.users_collection)]
    bbox = pc.world_bbox(meshes)
    build_studio(scene, bbox, args.samples, args.res)
    cam_data = bpy.data.cameras.new("Studio_Cam")
    cam_obj = bpy.data.objects.new("Studio_Cam", cam_data)
    scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj
    views = list(VIEWS) if "all" in args.views else args.views
    os.makedirs(args.out_dir, exist_ok=True)
    yaw = forward_yaw(args.forward)
    outputs = []
    for v in views:
        place_camera(scene, cam_obj, bbox, VIEWS[v], yaw)
        out = os.path.join(os.path.abspath(args.out_dir), f"{args.prefix}_{v}.png")
        scene.render.filepath = out
        bpy.ops.render.render(write_still=True)
        outputs.append(out)
        print(f"[render] {out}")
    print(f"Rendered {len(outputs)} views (Blender {bpy.app.version_string}, Cycles CPU)")


if __name__ == "__main__":
    main()
