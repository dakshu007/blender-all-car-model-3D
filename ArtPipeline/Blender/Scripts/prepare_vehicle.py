"""prepare_vehicle.py - prepare a COPY of a downloaded vehicle model.

Never modifies the input. Writes a new versioned .blend plus a before/after report.
Every operation is opt-in. Use --dry-run first to see the plan without saving.

Example (the CAR-009 source normalisation that was actually run):
    python prepare_vehicle.py -- \
        --input ../../Incoming/CAR-009/rally-car.fbx --car-id CAR-009 \
        --rename-map ../../VehicleLibrary/CAR-009/Documentation/source_rename_map.json \
        --source-forward=-Y --make-root --wheel-origins --apply-transforms \
        --dedupe-materials --collision-proxy \
        --out ../../VehicleLibrary/CAR-009/Source/CAR-009_Source.blend \
        --report ../Reports/CAR-009_prepare_source.json [--dry-run]

Rename map JSON: {"source object name": "CAR-009_Wheel_FL", ...}. Only objects
listed in the map are renamed - the map is the human approval step. Wheel origin
fixing only touches objects whose NEW name ends in _Wheel_FL/FR/RL/RR.
"""

import argparse
import json
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import pipeline_common as pc  # noqa: E402


def parse():
    p = argparse.ArgumentParser()
    p.add_argument("--input", required=True)
    p.add_argument("--car-id", required=True)
    p.add_argument("--out", required=True, help="Target .blend (a _vNNN suffix is bumped if it exists)")
    p.add_argument("--report", required=True)
    p.add_argument("--dry-run", action="store_true")
    p.add_argument("--rename-map")
    p.add_argument("--source-forward", default="+Y", choices=["+Y", "-Y", "+X", "-X"],
                   help="Forward axis of the source; it is rotated to +Y")
    p.add_argument("--make-root", action="store_true", help="Create <CAR>_Root empty at ground centre")
    p.add_argument("--wheel-origins", action="store_true")
    p.add_argument("--apply-transforms", action="store_true", help="Apply rotation+scale on meshes")
    p.add_argument("--recalc-normals", action="store_true")
    p.add_argument("--merge-dist", type=float, default=0.0, help=">0 merges duplicate vertices")
    p.add_argument("--dedupe-materials", action="store_true")
    p.add_argument("--collision-proxy", action="store_true")
    return p.parse_args(pc.script_args())


def snapshot():
    objs = []
    for o in bpy.data.objects:
        e = {"name": o.name, "type": o.type, "parent": o.parent.name if o.parent else None,
             "world_origin": [round(x, 4) for x in o.matrix_world.translation]}
        if o.type == "MESH":
            e["verts"] = len(o.data.vertices)
            e["tris"] = pc.mesh_triangle_count(o.data)
        objs.append(e)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    return {"objects": objs, "materials": sorted(m.name for m in bpy.data.materials),
            "bbox": pc.world_bbox(meshes)}


def material_signature(m):
    if not m.node_tree:
        return ("nonodes",)
    sig = []
    for n in sorted(m.node_tree.nodes, key=lambda n: n.name):
        vals = []
        for i in n.inputs:
            if hasattr(i, "default_value") and not i.is_linked:
                v = i.default_value
                try:
                    v = tuple(round(x, 4) for x in v)
                except TypeError:
                    v = round(v, 4) if isinstance(v, float) else v
                vals.append((i.identifier, v))
        img = n.image.name if getattr(n, "image", None) else None
        sig.append((n.bl_idname, img, tuple(vals)))
    return tuple(sig)


def main():
    a = parse()
    log = pc.Log()
    src = os.path.abspath(a.input)
    out = os.path.abspath(a.out)
    if "Incoming" in out.split(os.sep):
        raise SystemExit("Refusing to write into an Incoming folder (Incoming is immutable).")
    if os.path.abspath(out) == src:
        raise SystemExit("Refusing to overwrite the source file.")
    pc.reset_scene()
    pc.import_model(src)
    before = snapshot()
    car = a.car_id

    # 1. Renames - only from the approved map.
    rename_map = {}
    if a.rename_map:
        with open(a.rename_map, encoding="utf-8") as fh:
            rename_map = json.load(fh)
        for old, new in rename_map.items():
            o = bpy.data.objects.get(old)
            if o is None:
                log.warn(f"rename map: object '{old}' not found - skipped")
                continue
            log.act(f"rename '{old}' -> '{new}'")
            if not a.dry_run:
                o.name = new
                if o.data and o.type == "MESH":
                    o.data.name = new

    # 2. Orientation: rotate everything about the world origin so forward becomes +Y.
    yaw = {"+Y": 0, "-Y": 180, "+X": 90, "-X": -90}[a.source_forward]
    tops = [o for o in bpy.data.objects if o.parent is None]
    if yaw:
        log.act(f"rotate top-level objects {yaw} deg about Z (source forward {a.source_forward} -> +Y)")
        if not a.dry_run:
            R = Matrix.Rotation(math.radians(yaw), 4, "Z")
            for o in tops:
                o.matrix_world = R @ o.matrix_world
            bpy.context.view_layer.update()

    # 3. Root empty at ground centre.
    root = None
    if a.make_root:
        meshes = [o for o in bpy.data.objects if o.type == "MESH"]
        bb = pc.world_bbox(meshes)
        ground = Vector((bb["center"][0], bb["center"][1], bb["min"][2]))
        log.act(f"create {car}_Root at ground centre {tuple(round(x, 4) for x in ground)}; parent top-level objects")
        if not a.dry_run:
            root = bpy.data.objects.new(f"{car}_Root", None)
            root.empty_display_type = "ARROWS"
            bpy.context.scene.collection.objects.link(root)
            root.location = ground
            bpy.context.view_layer.update()
            for o in tops:
                mw = o.matrix_world.copy()
                o.parent = root
                o.matrix_world = mw

    # 4. Apply rotation/scale on meshes (keeps parent hierarchy, armatures untouched).
    if a.apply_transforms:
        if any(o.type == "ARMATURE" for o in bpy.data.objects):
            log.warn("armature present - transform application skipped; inspect the rig manually")
        else:
            for o in [o for o in bpy.data.objects if o.type == "MESH"]:
                log.act(f"apply rotation+scale on '{o.name}'")
                if not a.dry_run:
                    loc, rot, sca = o.matrix_basis.decompose()
                    o.data.transform(Matrix.LocRotScale(None, rot, sca))
                    o.matrix_basis = Matrix.Translation(loc)
            # Empties that were rotated keep their rotation in children - bake into children.
            if not a.dry_run:
                for o in [o for o in bpy.data.objects if o.type == "EMPTY"]:
                    loc, rot, sca = o.matrix_basis.decompose()
                    if rot.angle > 1e-6 or (sca - Vector((1, 1, 1))).length > 1e-6:
                        kids = [(c, c.matrix_world.copy()) for c in o.children]
                        o.matrix_basis = Matrix.Translation(loc)
                        bpy.context.view_layer.update()
                        for c, mw in kids:
                            c.matrix_world = mw
                            if c.type == "MESH":
                                l2, r2, s2 = c.matrix_basis.decompose()
                                c.data.transform(Matrix.LocRotScale(None, r2, s2))
                                c.matrix_basis = Matrix.Translation(l2)
                        log.act(f"baked rotation of empty '{o.name}' into its children")

    # 5. Wheel origins -> geometric centre (axle centre).
    if a.wheel_origins:
        for slot in pc.WHEEL_SLOTS:
            name = f"{car}_Wheel_{slot}"
            o = bpy.data.objects.get(name) if not a.dry_run else bpy.data.objects.get(
                next((k for k, v in rename_map.items() if v == name), name))
            if o is None:
                log.warn(f"{name} not found - wheel origin not set (is it in the rename map?)")
                continue
            mw = o.matrix_world
            cos = [mw @ v.co for v in o.data.vertices]
            mn = Vector(map(min, *cos))
            mx = Vector(map(max, *cos))
            centre = (mn + mx) / 2
            off = (centre - mw.translation).length
            log.act(f"{name}: origin -> bbox centre {tuple(round(x, 4) for x in centre)} (moved {off:.4f} m)")
            if not a.dry_run and off > 1e-6:
                local = mw.inverted() @ centre
                o.data.transform(Matrix.Translation(-local))
                o.matrix_world = Matrix.Translation(centre) @ Matrix.Translation(-mw.translation) @ mw

    # 6. Normals / doubles (opt-in: unsafe on open 'polygon soup' meshes).
    if a.merge_dist > 0 or a.recalc_normals:
        import bmesh
        for o in [o for o in bpy.data.objects if o.type == "MESH"]:
            if a.dry_run:
                log.act(f"would clean '{o.name}' (merge {a.merge_dist}, recalc normals {a.recalc_normals})")
                continue
            bm = bmesh.new()
            bm.from_mesh(o.data)
            if a.merge_dist > 0:
                n0 = len(bm.verts)
                bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=a.merge_dist)
                log.act(f"'{o.name}': merged {n0 - len(bm.verts)} duplicate verts")
            if a.recalc_normals:
                bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
                log.act(f"'{o.name}': recalculated normals")
            bm.to_mesh(o.data)
            bm.free()

    # 7. Material de-duplication (Blender's .001 copies with identical settings).
    if a.dedupe_materials:
        groups = {}
        for m in bpy.data.materials:
            base = re.sub(r"\.\d{3}$", "", m.name)
            groups.setdefault(base, []).append(m)
        for base, mats in groups.items():
            keeper = next((m for m in mats if m.name == base), mats[0])
            for m in mats:
                if m is keeper:
                    continue
                if material_signature(m) != material_signature(keeper):
                    log.warn(f"material '{m.name}' differs from '{keeper.name}' - kept separate")
                    continue
                log.act(f"replace material '{m.name}' with identical '{keeper.name}'")
                if not a.dry_run:
                    m.user_remap(keeper)
                    bpy.data.materials.remove(m)

    # 8. Collision proxy (box around body, own collection, never in the visual FBX).
    if a.collision_proxy:
        body = bpy.data.objects.get(f"{car}_Body")
        target = [body] if body else [o for o in bpy.data.objects if o.type == "MESH"]
        bb = pc.world_bbox(target)
        log.act(f"add collision box {tuple(round(x, 3) for x in bb['size'])} in collection {car}_Collision")
        if not a.dry_run:
            coll = bpy.data.collections.new(f"{car}_Collision")
            bpy.context.scene.collection.children.link(coll)
            me = bpy.data.meshes.new(f"{car}_Collider_Body")
            import bmesh
            bm = bmesh.new()
            bmesh.ops.create_cube(bm, size=1.0)
            bm.to_mesh(me)
            bm.free()
            col = bpy.data.objects.new(f"{car}_Collider_Body", me)
            col.scale = bb["size"]
            col.location = bb["center"]
            col.display_type = "WIRE"
            col.hide_render = True
            coll.objects.link(col)

    after = snapshot()
    saved = None
    if not a.dry_run:
        os.makedirs(os.path.dirname(out), exist_ok=True)
        target = pc.next_version(out)
        if os.path.exists(out) and target != out:
            log.act(f"'{os.path.basename(out)}' exists - saving as new version '{os.path.basename(target)}'")
        bpy.ops.file.pack_all()
        bpy.ops.wm.save_as_mainfile(filepath=target, compress=True)
        saved = target
        log.act(f"saved {target}")

    pc.write_json(a.report, {
        "car_id": car, "tool": "prepare_vehicle.py", "generated_utc": pc.now_iso(),
        "blender": pc.blender_info(), "dry_run": a.dry_run, "source_file": src,
        "saved_file": saved, "actions": log.actions, "warnings": log.warnings,
        "before": before, "after": after,
    })
    print(f"Report: {a.report}  (dry_run={a.dry_run}, actions={len(log.actions)}, warnings={len(log.warnings)})")


if __name__ == "__main__":
    main()
