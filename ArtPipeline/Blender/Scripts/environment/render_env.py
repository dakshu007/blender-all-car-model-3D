"""render_env.py - spring-light preview renders for environment components.

Still (one or more views of one component collection):
    python render_env.py -- --blend ENV_Kit_v001.blend --collection ENV_CherryKing --out-dir Previews \
        --views hero side --res 1280 800 --samples 32
Animation frames (for a GIF/MP4 loop):
    ... --views hero --anim 1 97 2      (start, end, step)
Whole scene (diorama): --collection ALL --camera Diorama_Cam
"""

import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import pipeline_common as pc  # noqa: E402

VIEWS = {"hero": (-35, 14, 50), "side": (90, 6, 50), "front": (0, 8, 50), "top": (0, 60, 50),
         "low": (-25, 4, 35), "wide": (-35, 22, 35), "back": (150, 14, 50)}


def parse():
    p = argparse.ArgumentParser()
    p.add_argument("--blend", required=True)
    p.add_argument("--collection", required=True)
    p.add_argument("--out-dir", required=True)
    p.add_argument("--prefix", default=None)
    p.add_argument("--views", nargs="+", default=["hero"])
    p.add_argument("--res", nargs=2, type=int, default=[1280, 800])
    p.add_argument("--samples", type=int, default=32)
    p.add_argument("--anim", nargs=3, type=int, default=None)
    p.add_argument("--camera", default=None)
    p.add_argument("--frame", type=int, default=1)
    p.add_argument("--ground", type=float, default=60.0, help="ground plane size (0 = none)")
    p.add_argument("--zoom", type=float, default=1.0)
    return p.parse_args(pc.script_args())


def world_and_lights(scene):
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 5
    scene.view_settings.view_transform = "AgX"
    looks = [i.identifier for i in scene.view_settings.bl_rna.properties["look"].enum_items]
    scene.view_settings.look = "AgX - Base Contrast" if "AgX - Base Contrast" in looks else "None"
    scene.view_settings.exposure = 0.25
    w = bpy.data.worlds.new("Spring_Sky")
    scene.world = w
    nt = w.node_tree
    bg = nt.nodes.get("Background")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    els = ramp.color_ramp.elements
    els[0].position, els[1].position = 0.02, 0.32
    els[0].color = (0.80, 0.90, 0.98, 1)
    els[1].color = (0.22, 0.48, 0.88, 1)
    nt.links.new(tc.outputs["Generated"], sep.inputs["Vector"])
    nt.links.new(sep.outputs["Z"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 1.0
    sun = bpy.data.lights.new("Env_Sun", "SUN")
    sun.energy = 4.0
    sun.color = (1.0, 0.92, 0.80)
    sun.angle = math.radians(3)
    so = bpy.data.objects.new("Env_Sun", sun)
    so.rotation_euler = (math.radians(48), 0, math.radians(-135))
    scene.collection.objects.link(so)


def ground(scene, center, size):
    if size <= 0:
        return
    m = bpy.data.materials.new("Env_Ground")
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 0.15
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.12, 0.30, 0.04, 1)
    ramp.color_ramp.elements[1].color = (0.26, 0.46, 0.08, 1)
    nt.links.new(nz.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.95
    bpy.ops.mesh.primitive_plane_add(size=size, location=(center.x, center.y, -0.005))
    g = bpy.context.active_object
    g.name = "Env_Ground"
    g.data.materials.append(m)


def isolate(collection_name):
    if collection_name == "ALL":
        return [o for o in bpy.context.scene.objects]
    keep = bpy.data.collections.get(collection_name)
    if keep is None:
        raise SystemExit(f"collection {collection_name} not found")
    keep_objs = set(keep.all_objects)
    for o in bpy.context.scene.objects:
        if o not in keep_objs:
            o.hide_render = True
    return list(keep_objs)


def main():
    a = parse()
    bpy.ops.wm.open_mainfile(filepath=os.path.abspath(a.blend))
    scene = bpy.context.scene
    objs = isolate(a.collection)
    for o in scene.objects:
        if o.type in ("LIGHT",) and o.hide_render is False and a.collection != "ALL":
            o.hide_render = True
    scene.frame_set(a.frame)
    meshes = [o for o in objs if o.type == "MESH" and not o.hide_render and "Petal" not in o.name]
    bb = pc.world_bbox(meshes)
    c = Vector(bb["center"])
    size = Vector(bb["size"])
    if a.collection != "ALL":
        world_and_lights(scene)
        if not a.collection.startswith("ENV_Water"):  # water sits below z=0; banks are its ground
            ground(scene, Vector((c.x, c.y, 0)), a.ground)
    scene.cycles.samples = a.samples
    scene.render.resolution_x, scene.render.resolution_y = a.res
    scene.render.fps = 24
    if a.camera:
        cam = bpy.data.objects[a.camera]
    else:
        cd = bpy.data.cameras.new("Env_Cam")
        cam = bpy.data.objects.new("Env_Cam", cd)
        scene.collection.objects.link(cam)
    scene.camera = cam
    os.makedirs(a.out_dir, exist_ok=True)
    prefix = a.prefix or a.collection
    for v in a.views:
        if not a.camera:
            az, el, lens = VIEWS[v]
            cam.data.lens = lens
            fov = 2 * math.atan(cam.data.sensor_width / (2 * lens))
            r = max(size.x, size.y, size.z * 1.4) * 0.62 / a.zoom
            dist = r / math.tan(fov / 2) * 1.05
            target = Vector((c.x, c.y, bb["min"][2] + size.z * 0.42))
            azr, elr = math.radians(az), math.radians(el)
            d = Vector((math.sin(azr) * math.cos(elr), -math.cos(azr) * math.cos(elr), math.sin(elr)))
            cam.location = target + d * dist
            cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
            cam.data.clip_end = dist * 10
        if a.anim:
            s, e, st = a.anim
            d = os.path.join(a.out_dir, f"{prefix}_{v}_frames")
            os.makedirs(d, exist_ok=True)
            for i, f in enumerate(range(s, e, st)):
                scene.frame_set(f)
                scene.render.filepath = os.path.join(d, f"f{i:03d}.png")
                bpy.ops.render.render(write_still=True)
            print(f"[anim] {d} ({len(range(s, e, st))} frames)")
        else:
            scene.frame_set(a.frame)
            out = os.path.join(a.out_dir, f"{prefix}_{v}.png")
            scene.render.filepath = out
            bpy.ops.render.render(write_still=True)
            print(f"[render] {out}")


if __name__ == "__main__":
    main()
