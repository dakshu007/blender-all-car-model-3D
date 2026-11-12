"""export_vehicle_fbx.py - export ONLY the approved vehicle collection to FBX for Unity.

Usage:
    blender -b -P export_vehicle_fbx.py -- --blend CAR-009_Working_v001.blend --car-id CAR-009 \
        --out-dir ../Export --report ../../Blender/Reports/CAR-009_export.json

Axis contract (Blender +Y forward / +Z up  ->  Unity +Z forward / +Y up):
    axis_forward='Z', axis_up='Y', bake_space_transform=False, apply_scale_options='FBX_SCALE_UNITS'.
    The exporter puts the axis conversion on <CAR>_Root only (Lcl Rotation 90,0,180); every
    child keeps its Blender-local transform. A point at Blender (x, y, z) lands at Unity (x, z, y).
    "Apply Transform" (bake_space_transform=True) is NOT used: in Blender 5.0 it corrupts the
    transforms of objects nested two or more levels deep (wheel tyres/rims under their pivot),
    verified by parsing the exported file (see Documentation/BLENDER_TO_UNITY.md).
    Verify once in Unity before batch exporting.

Also writes <CAR>_vehicle_data.json (wheel positions, radius, colliders in Unity space)
and copies textures next to the FBX (path_mode STRIP: file names only, no absolute paths).
The collision collection is never part of the visual FBX.
"""

import argparse
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

import pipeline_common as pc  # noqa: E402

FBX_SETTINGS = dict(
    use_selection=True,
    object_types={"EMPTY", "MESH"},
    axis_forward="Z",
    axis_up="Y",
    bake_space_transform=False,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    global_scale=1.0,
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    use_tspace=False,
    use_custom_props=True,
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="STRIP",  # texture referenced by file name only; copied next to the FBX below
    embed_textures=False,
    colors_type="NONE",
)


def parse():
    p = argparse.ArgumentParser()
    p.add_argument("--blend", required=True)
    p.add_argument("--car-id", required=True)
    p.add_argument("--out-dir", required=True)
    p.add_argument("--report", required=True)
    p.add_argument("--name", default=None, help="FBX file name (default <CAR>_Export.fbx)")
    return p.parse_args(pc.script_args())


def main():
    a = parse()
    car = a.car_id
    log = pc.Log()
    bpy.ops.wm.open_mainfile(filepath=os.path.abspath(a.blend))
    coll = bpy.data.collections.get(car)
    if coll is None:
        raise SystemExit(f"Collection '{car}' not found - nothing exported")
    objs = [o for o in coll.all_objects if o.type in ("MESH", "EMPTY")]
    for o in objs:
        if any(abs(s - 1.0) > 1e-4 for s in o.scale) and o.type == "MESH":
            log.warn(f"{o.name} has unapplied scale {tuple(o.scale)}")
    names = [o.name for o in objs]
    if len(set(names)) != len(names):
        log.warn("duplicate object names in export set")

    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects.get(f"{car}_Root") or objs[0]

    out_dir = os.path.abspath(a.out_dir)
    os.makedirs(out_dir, exist_ok=True)
    fbx_path = os.path.join(out_dir, a.name or f"{car}_Export.fbx")
    if os.path.exists(fbx_path):
        backup = pc.next_version(fbx_path.replace(".fbx", "_backup.fbx"))
        shutil.copy2(fbx_path, backup)
        log.act(f"existing export backed up to {os.path.basename(backup)}")
    bpy.ops.export_scene.fbx(filepath=fbx_path, **FBX_SETTINGS)
    log.act(f"exported {len(objs)} objects -> {fbx_path}")

    copied = []
    for img in bpy.data.images:
        if img.type != "IMAGE" or not img.users:
            continue
        src = bpy.path.abspath(img.filepath)
        if not os.path.exists(src):
            log.warn(f"texture missing on disk: {img.filepath}")
            continue
        dst = os.path.join(out_dir, os.path.basename(src))
        if os.path.abspath(src) != dst:
            shutil.copy2(src, dst)
        copied.append(os.path.basename(src))

    # Unity-facing vehicle data
    wheels = {}
    for slot in pc.WHEEL_SLOTS:
        piv = bpy.data.objects.get(f"{car}_Wheel_{slot}")
        if piv is None:
            log.warn(f"{car}_Wheel_{slot} missing")
            continue
        tire = bpy.data.objects.get(f"{car}_Wheel_{slot}_Tire")
        dims = tire.dimensions if tire else piv.dimensions
        wheels[slot] = {
            "transform": piv.name,
            "blender_position": [round(x, 5) for x in piv.matrix_world.translation],
            "unity_local_position": pc.blender_to_unity(piv.matrix_world.translation),
            "radius": round(max(dims.y, dims.z) / 2, 4),
            "width": round(dims.x, 4),
            "steer": bool(piv.get("steer", slot.startswith("F"))),
            "drive": bool(piv.get("drive", True)),
        }
    colliders = []
    ccoll = bpy.data.collections.get(f"{car}_Collision")
    if ccoll:
        for o in ccoll.all_objects:
            if o.type != "MESH":
                continue
            bb = pc.world_bbox([o])
            colliders.append({"name": o.name, "type": "BoxCollider",
                              "unity_center": pc.blender_to_unity(bb["center"]),
                              "unity_size": pc.blender_to_unity(bb["size"])})
    vis = [o for o in objs if o.type == "MESH"]
    bb = pc.world_bbox(vis)
    tris = sum(pc.mesh_triangle_count(o.data) for o in vis)
    root = bpy.data.objects.get(f"{car}_Root")
    steering = bpy.data.objects.get(f"{car}_SteeringWheel")
    data = {
        "car_id": car,
        "display_name": root.get("display_name") if root else car,
        "vehicle_class": root.get("vehicle_class") if root else None,
        "fbx": os.path.basename(fbx_path),
        "root": f"{car}_Root",
        "axes": {"blender": "+Y forward, +Z up, +X right", "unity": "+Z forward, +Y up, +X right",
                 "mapping": "unity = (blender.x, blender.z, blender.y)"},
        "bounds_unity": {"center": pc.blender_to_unity(bb["center"]), "size": pc.blender_to_unity(bb["size"])},
        "triangles": tris,
        "wheels": wheels,
        "wheel_list": [dict(slot=k, **v) for k, v in wheels.items()],  # array form for Unity JsonUtility
        "wheelbase": round(abs(wheels["FL"]["blender_position"][1] - wheels["RL"]["blender_position"][1]), 4)
        if "FL" in wheels and "RL" in wheels else None,
        "track": round(abs(wheels["FL"]["blender_position"][0] - wheels["FR"]["blender_position"][0]), 4)
        if "FL" in wheels and "FR" in wheels else None,
        "steering_wheel": {"transform": steering.name, "steer_axis": "local +Z in Unity (Blender local +Y)"}
        if steering else None,
        "colliders": colliders,
        "textures": copied,
        "materials": sorted({m.name for o in vis for m in o.data.materials if m}),
    }
    # optional camera anchors written by build_generic.py (cars built after CAR-019)
    for key in ("driver_eye", "hood_cam"):
        if root is not None and key in root.keys():
            data[key] = pc.blender_to_unity(list(root[key]))
    data_path = os.path.join(out_dir, f"{car}_vehicle_data.json")
    pc.write_json(data_path, data)
    log.act(f"vehicle data -> {data_path}")

    pc.write_json(a.report, {
        "car_id": car, "tool": "export_vehicle_fbx.py", "generated_utc": pc.now_iso(),
        "blender": pc.blender_info(), "blend": os.path.abspath(a.blend), "fbx": fbx_path,
        "fbx_size_bytes": os.path.getsize(fbx_path), "object_count": len(objs),
        "objects": sorted(names), "triangles": tris, "textures_copied": copied,
        "settings": {k: (sorted(v) if isinstance(v, set) else v) for k, v in FBX_SETTINGS.items()},
        "actions": log.actions, "warnings": log.warnings,
    })
    print(f"FBX: {fbx_path} ({os.path.getsize(fbx_path)} bytes, {len(objs)} objects, {tris} tris, "
          f"{len(log.warnings)} warnings)")


if __name__ == "__main__":
    main()
