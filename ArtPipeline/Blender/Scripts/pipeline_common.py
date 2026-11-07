"""Shared helpers for the rally-car Blender pipeline.

Works both inside the Blender application (``blender -b -P script.py -- ...``)
and with the standalone ``bpy`` module (``python script.py -- ...``).
"""

import datetime
import json
import os
import sys

import bpy

WHEEL_SLOTS = ("FL", "FR", "RL", "RR")


def script_args(argv=None):
    """Return the arguments meant for the script (everything after ``--``)."""
    argv = sys.argv if argv is None else argv
    if "--" in argv:
        return argv[argv.index("--") + 1:]
    return argv[1:]


def now_iso():
    return datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat()


def blender_info():
    return {
        "version": bpy.app.version_string,
        "build_hash": bpy.app.build_hash.decode() if isinstance(bpy.app.build_hash, bytes) else str(bpy.app.build_hash),
        "background": bpy.app.background,
    }


def reset_scene():
    """Start from a completely empty scene (no default cube/camera/light)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0


def import_model(path):
    """Import a model file into the current scene. Returns the new objects."""
    ext = os.path.splitext(path)[1].lower()
    before = set(bpy.data.objects)
    if ext == ".blend":
        bpy.ops.wm.open_mainfile(filepath=path)
        return list(bpy.data.objects)
    if ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    elif ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=path)
    else:
        raise ValueError(f"Unsupported model format: {ext}")
    return [o for o in bpy.data.objects if o not in before]


def write_json(path, data):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=2, sort_keys=False, default=str)
        fh.write("\n")


def mesh_triangle_count(mesh):
    return sum(len(p.vertices) - 2 for p in mesh.polygons)


def world_bbox(objects):
    """Axis-aligned world bounding box of the given mesh objects."""
    from mathutils import Vector

    mins = Vector((1e9, 1e9, 1e9))
    maxs = Vector((-1e9, -1e9, -1e9))
    found = False
    for obj in objects:
        if obj.type != "MESH":
            continue
        for corner in obj.bound_box:
            w = obj.matrix_world @ Vector(corner)
            mins = Vector(map(min, mins, w))
            maxs = Vector(map(max, maxs, w))
            found = True
    if not found:
        return None
    return {"min": list(mins), "max": list(maxs), "size": list(maxs - mins), "center": list((mins + maxs) / 2)}


class Log:
    """Tiny action/warning logger that is also written into reports."""

    def __init__(self):
        self.actions = []
        self.warnings = []

    def act(self, msg):
        print(f"[action] {msg}")
        self.actions.append(msg)

    def warn(self, msg):
        print(f"[warning] {msg}")
        self.warnings.append(msg)


def next_version(path):
    """Return path, or the next free _vNNN variant so nothing is ever overwritten."""
    import re
    if not os.path.exists(path):
        return path
    base, ext = os.path.splitext(path)
    m = re.search(r"_v(\d{3})$", base)
    stem = base[:m.start()] if m else base
    n = int(m.group(1)) + 1 if m else 2
    while os.path.exists(f"{stem}_v{n:03d}{ext}"):
        n += 1
    return f"{stem}_v{n:03d}{ext}"


def blender_to_unity(v):
    """Blender (+X right, +Y forward, +Z up) -> Unity (+X right, +Y up, +Z forward),
    valid for FBX exported by export_vehicle_fbx.py (axis_forward='Z', axis_up='Y')."""
    return [round(v[0], 5), round(v[2], 5), round(v[1], 5)]
