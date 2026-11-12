"""validate_vehicle.py - gate a vehicle before it can be marked approved.

Usage:
    blender -b -P validate_vehicle.py -- --blend CAR-009_Working_v001.blend --car-id CAR-009 \
        --fbx ../Export/CAR-009_Export.fbx --report ../../Blender/Reports/CAR-009_validation.json

Exit code 0 = PASS, 1 = FAIL. A FAIL must block the asset from being approved.
Checks the .blend (structure, pivots, materials, textures, UVs, budget) and then
re-imports the FBX into an empty scene to confirm names and wheel positions
survived the export (round trip with the same axis contract).
"""

import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import pipeline_common as pc  # noqa: E402

# ranges cover the classic rally cars and the SUVs / trucks / buggy of CAR-020..031 (roof racks, tents, wide trophy truck)
DIM_RANGE = {"length": (3.0, 6.0), "width": (1.4, 2.6), "height": (0.9, 2.6)}
TRI_BUDGET = (15000, 35000)
PIVOT_TOL = 0.005
ROUNDTRIP_TOL = 0.002


class Checks:
    def __init__(self):
        self.items = []

    def add(self, name, ok, detail="", severity="error"):
        status = "PASS" if ok else ("FAIL" if severity == "error" else "WARN")
        self.items.append({"check": name, "status": status, "detail": detail})
        print(f"[{status}] {name}: {detail}")

    @property
    def failed(self):
        return [i for i in self.items if i["status"] == "FAIL"]


def parse():
    p = argparse.ArgumentParser()
    p.add_argument("--blend", required=True)
    p.add_argument("--car-id", required=True)
    p.add_argument("--fbx", required=True)
    p.add_argument("--report", required=True)
    return p.parse_args(pc.script_args())


def wheel_geometry_center(piv):
    meshes = [c for c in piv.children_recursive if c.type == "MESH"] or ([piv] if piv.type == "MESH" else [])
    bb = pc.world_bbox(meshes)
    return Vector(bb["center"]) if bb else None


def fbx_unity_positions(path, car):
    """Parse the FBX node graph directly (no importer conventions) and return, per model,
    its world position and local-X axis in Unity space. FBX is right-handed Y-up here;
    Unity converts to left-handed by negating X."""
    import addon_utils
    from mathutils import Euler, Matrix
    addon_utils.enable("io_scene_fbx")
    from io_scene_fbx import parse_fbx
    root, _ver = parse_fbx.parse(path)
    sub = lambda e, i: [c for c in e.elems if c.id == i]  # noqa: E731
    models = {}
    for m in sub(sub(root, b"Objects")[0], b"Model"):
        name = m.props[1].split(b"\x00")[0].decode()
        t, r, s_ = (0, 0, 0), (0, 0, 0), (1, 1, 1)
        for p in sub(sub(m, b"Properties70")[0], b"P"):
            if p.props[0] == b"Lcl Translation":
                t = tuple(p.props[4:7])
            elif p.props[0] == b"Lcl Rotation":
                r = tuple(p.props[4:7])
            elif p.props[0] == b"Lcl Scaling":
                s_ = tuple(p.props[4:7])
        local = Matrix.LocRotScale(Vector(t), Euler([math.radians(x) for x in r], "XYZ"), Vector(s_))
        models[m.props[0]] = {"name": name, "local": local, "parent": None}
    for c in sub(sub(root, b"Connections")[0], b"C"):
        if c.props[0] == b"OO" and c.props[1] in models and c.props[2] in models:
            models[c.props[1]]["parent"] = c.props[2]
    out = {}
    for uid, m in models.items():
        mat = m["local"]
        p = m["parent"]
        while p is not None:
            mat = models[p]["local"] @ mat
            p = models[p]["parent"]
        pos = mat.translation
        ax = (mat.to_3x3() @ Vector((1, 0, 0))).normalized()
        out[m["name"]] = {"unity_position": [-pos.x, pos.y, pos.z], "unity_axle_axis": [-ax.x, ax.y, ax.z]}
    return out


def main():
    a = parse()
    car = a.car_id
    K = Checks()
    bpy.ops.wm.open_mainfile(filepath=os.path.abspath(a.blend))
    coll = bpy.data.collections.get(car)
    K.add("export collection exists", coll is not None, car)
    if coll is None:
        return finish(a, K, {})
    objs = list(coll.all_objects)
    meshes = [o for o in objs if o.type == "MESH"]

    body = bpy.data.objects.get(f"{car}_Body")
    K.add("body object exists", body is not None and body.type == "MESH", f"{car}_Body")

    root = bpy.data.objects.get(f"{car}_Root")
    root_ok = root is not None and root.matrix_world.translation.length < 1e-4 and \
        Vector(root.rotation_euler).length < 1e-6
    K.add("root at origin, no rotation", root_ok, f"{car}_Root")
    K.add("forward axis documented on root", bool(root and root.get("forward_axis")),
          str(root.get("forward_axis")) if root else "missing")

    wheel_pos = {}
    for slot in pc.WHEEL_SLOTS:
        piv = bpy.data.objects.get(f"{car}_Wheel_{slot}")
        if piv is None:
            not_ready = root is not None and root.get(f"wheel_{slot}_not_ready")
            K.add(f"wheel {slot} exists", bool(not_ready), "missing" + (" (marked not ready)" if not_ready else ""))
            continue
        K.add(f"wheel {slot} exists", True, piv.name)
        c = wheel_geometry_center(piv)
        off = (c - piv.matrix_world.translation).length if c else 1e9
        K.add(f"wheel {slot} pivot at axle centre", off < PIVOT_TOL, f"offset {off * 1000:.2f} mm")
        p = piv.matrix_world.translation
        want_x = -1 if slot.endswith("L") else 1
        want_y = 1 if slot.startswith("F") else -1
        K.add(f"wheel {slot} on correct corner (+Y fwd, -X left)",
              (p.x * want_x > 0) and (p.y * want_y > 0), f"({p.x:.3f}, {p.y:.3f}, {p.z:.3f})")
        wheel_pos[slot] = list(p)

    bb = pc.world_bbox(meshes)
    size = bb["size"]
    dims = {"width": size[0], "length": size[1], "height": size[2]}
    for k, (lo, hi) in DIM_RANGE.items():
        K.add(f"{k} plausible", lo <= dims[k] <= hi, f"{dims[k]:.3f} m (range {lo}-{hi})")
    K.add("sits on ground plane", abs(bb["min"][2]) < 0.01, f"min z {bb['min'][2]:.4f}")

    zero = [o.name for o in objs if any(abs(s) < 1e-8 for s in o.scale)]
    K.add("no zero-scale objects", not zero, ", ".join(zero) or "none")
    empty_meshes = [o.name for o in meshes if len(o.data.polygons) == 0]
    K.add("no empty mesh objects", not empty_meshes, ", ".join(empty_meshes) or "none")
    empty_slots = [o.name for o in meshes if any(s.material is None for s in o.material_slots)]
    no_mats = [o.name for o in meshes if not o.material_slots]
    K.add("no missing material references", not empty_slots and not no_mats,
          ", ".join(empty_slots + no_mats) or "none")
    no_uv = [o.name for o in meshes if not o.data.uv_layers]
    K.add("every mesh has a UV map", not no_uv, ", ".join(no_uv) or "none")
    missing_tex = []
    for img in bpy.data.images:
        if img.type == "IMAGE" and img.users:
            path = bpy.path.abspath(img.filepath)
            if not (img.packed_file or os.path.exists(path)):
                missing_tex.append(img.filepath)
    K.add("no missing texture images", not missing_tex, ", ".join(missing_tex) or "none")
    abs_paths = [img.filepath for img in bpy.data.images
                 if img.users and img.filepath and not img.filepath.startswith("//") and not img.packed_file]
    K.add("texture paths are relative", not abs_paths, ", ".join(abs_paths) or "ok", severity="warn")
    names = [o.name for o in objs]
    K.add("no duplicate / auto-suffixed names", len(set(names)) == len(names) and
          not any(n[-4:-3] == "." and n[-3:].isdigit() for n in names), "ok")
    hidden = [o.name for o in objs if o.hide_render or o.hide_get()]
    K.add("no hidden objects in export set", not hidden, ", ".join(hidden) or "none", severity="warn")

    tris = sum(pc.mesh_triangle_count(o.data) for o in meshes)
    K.add("triangle count recorded / within budget", TRI_BUDGET[0] <= tris <= TRI_BUDGET[1],
          f"{tris} tris (target {TRI_BUDGET[0]}-{TRI_BUDGET[1]})", severity="warn")
    coll_col = bpy.data.collections.get(f"{car}_Collision")
    K.add("collision proxies present (separate collection)",
          bool(coll_col and any(o.type == "MESH" for o in coll_col.all_objects)),
          f"{car}_Collision", severity="warn")

    # FBX round trip
    fbx = os.path.abspath(a.fbx)
    exists = os.path.exists(fbx)
    K.add("export file exists", exists, f"{fbx} ({os.path.getsize(fbx) if exists else 0} bytes)")
    rt = {}
    if exists:
        pc.reset_scene()
        bpy.ops.import_scene.fbx(filepath=fbx, axis_forward="Z", axis_up="Y")
        imported = {o.name for o in bpy.data.objects}
        missing = sorted(set(names) - imported)
        extra = sorted(imported - set(names))
        K.add("FBX contains every exported object", not missing, ", ".join(missing) or "all present")
        K.add("FBX contains nothing extra", not extra, ", ".join(extra) or "none", severity="warn")
        for slot, want in wheel_pos.items():
            o = bpy.data.objects.get(f"{car}_Wheel_{slot}")
            if o is None:
                continue
            got = o.matrix_world.translation
            d = (got - Vector(want)).length
            K.add(f"FBX round trip wheel {slot} position", d < ROUNDTRIP_TOL, f"delta {d * 1000:.2f} mm")
            rt[slot] = {"blend": [round(x, 4) for x in want], "fbx_reimport": [round(x, 4) for x in got],
                        "unity_expected": pc.blender_to_unity(want)}
        rtt = sum(pc.mesh_triangle_count(o.data) for o in bpy.data.objects if o.type == "MESH")
        K.add("FBX triangle count matches", rtt == tris, f"{rtt} vs {tris}")
        raw = fbx_unity_positions(fbx, car)
        for slot, want in wheel_pos.items():
            node = raw.get(f"{car}_Wheel_{slot}")
            if node is None:
                K.add(f"raw FBX wheel {slot} -> Unity position", False, "node not found")
                continue
            exp = Vector(pc.blender_to_unity(want))
            d = (Vector(node["unity_position"]) - exp).length
            K.add(f"raw FBX wheel {slot} -> Unity position", d < ROUNDTRIP_TOL,
                  f"unity {tuple(round(x, 3) for x in node['unity_position'])} (expected "
                  f"{tuple(round(x, 3) for x in exp)})")
            ax = Vector(node["unity_axle_axis"])
            K.add(f"raw FBX wheel {slot} spin axis is lateral", abs(abs(ax.x) - 1) < 1e-3,
                  f"local X -> unity {tuple(round(x, 3) for x in ax)}")
            rt.setdefault(slot, {})["unity_from_raw_fbx"] = [round(x, 4) for x in node["unity_position"]]
    return finish(a, K, {"dimensions_m": dims, "triangles": tris, "wheels": wheel_pos, "fbx_round_trip": rt})


def finish(a, K, extra):
    status = "FAIL" if K.failed else "PASS"
    pc.write_json(a.report, {"car_id": a.car_id, "tool": "validate_vehicle.py", "generated_utc": pc.now_iso(),
                             "blender": pc.blender_info(), "status": status, "blend": os.path.abspath(a.blend),
                             "fbx": os.path.abspath(a.fbx), "checks": K.items, **extra})
    print(f"VALIDATION {status}: {len(K.failed)} failed, "
          f"{sum(1 for i in K.items if i['status'] == 'WARN')} warnings, {len(K.items)} checks")
    sys.exit(1 if K.failed else 0)


if __name__ == "__main__":
    main()
