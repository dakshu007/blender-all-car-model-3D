"""inspect_vehicle.py - inspect a vehicle model and report, WITHOUT modifying it.

Usage (Blender app):
    blender -b -P inspect_vehicle.py -- --input path/to/model.fbx --car-id CAR-009 --out Reports/CAR-009_inspection.json
Usage (bpy module):
    python inspect_vehicle.py -- --input ... --car-id ... --out ...

The source file is only read. Nothing is saved back to disk except the JSON report.
Wheel detection is a SUGGESTION only - never rename or move objects from it.
"""

import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402  (must precede bmesh/mathutils in module mode)
import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402
from mathutils.kdtree import KDTree  # noqa: E402

import pipeline_common as pc  # noqa: E402

WHEEL_NAME_HINTS = ("wheel", "tire", "tyre", "rim", "whl")


def parse():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--input", required=True)
    p.add_argument("--car-id", required=True)
    p.add_argument("--out", required=True, help="Output JSON report path")
    p.add_argument("--merge-dist", type=float, default=1e-5, help="Distance for duplicate-vertex check")
    return p.parse_args(pc.script_args())


def mesh_health(obj, merge_dist):
    """Topology checks on a temporary bmesh copy (the object is untouched)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    non_manifold = sum(1 for e in bm.edges if not e.is_manifold)
    boundary = sum(1 for e in bm.edges if e.is_boundary)
    wire = sum(1 for e in bm.edges if e.is_wire)
    loose_verts = sum(1 for v in bm.verts if not v.link_edges)
    zero_faces = sum(1 for f in bm.faces if f.calc_area() < 1e-10)
    # Edges whose two faces disagree on winding -> flipped normals.
    inconsistent = 0
    for e in bm.edges:
        if len(e.link_faces) == 2:
            f1, f2 = e.link_faces
            v1, v2 = e.verts

            def fwd(f):
                loops = list(f.loops)
                for i, lp in enumerate(loops):
                    if lp.vert == v1 and loops[(i + 1) % len(loops)].vert == v2:
                        return True
                return False

            if fwd(f1) == fwd(f2):
                inconsistent += 1
    kd = KDTree(len(bm.verts))
    for i, v in enumerate(bm.verts):
        kd.insert(v.co, i)
    kd.balance()
    dup = 0
    for i, v in enumerate(bm.verts):
        for (_co, idx, _d) in kd.find_range(v.co, merge_dist):
            if idx > i:
                dup += 1
                break
    islands = loose_islands(bm, obj.matrix_world)
    bm.free()
    return {
        "non_manifold_edges": non_manifold,
        "boundary_edges": boundary,
        "wire_edges": wire,
        "loose_verts": loose_verts,
        "zero_area_faces": zero_faces,
        "inconsistent_winding_edges": inconsistent,
        "duplicate_verts_within_merge_dist": dup,
        "loose_parts": len(islands),
    }, islands


def loose_islands(bm, mw):
    """Connected components (by shared edges) with world-space bounds."""
    seen = set()
    islands = []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack = [v]
        seen.add(v.index)
        members = []
        while stack:
            cur = stack.pop()
            members.append(cur)
            for e in cur.link_edges:
                o = e.other_vert(cur)
                if o.index not in seen:
                    seen.add(o.index)
                    stack.append(o)
        cos = [mw @ m.co for m in members]
        mn = Vector(map(min, *cos)) if len(cos) > 1 else cos[0].copy()
        mx = Vector(map(max, *cos)) if len(cos) > 1 else cos[0].copy()
        faces = set()
        for m in members:
            for f in m.link_faces:
                faces.add(f.index)
        islands.append({"verts": len(members), "faces": len(faces), "min": mn, "max": mx})
    return islands


def wheel_like(size):
    """Two near-equal large extents + one smaller extent = disc/cylinder."""
    s = sorted(size)
    if s[2] < 0.25 or s[2] > 1.2:
        return False
    return abs(s[2] - s[1]) / s[2] < 0.12 and s[0] / s[2] < 0.75


def suggest_wheels(objects, islands_by_obj, vehicle_bbox):
    suggestions = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        reasons = []
        if any(h in obj.name.lower() for h in WHEEL_NAME_HINTS):
            reasons.append("name contains wheel-like keyword")
        if wheel_like(obj.dimensions):
            reasons.append("object bounds are disc/cylinder shaped")
        if reasons:
            suggestions.append({"object": obj.name, "kind": "object", "reasons": reasons,
                                "center": list(obj.matrix_world.translation)})
    # Joined geometry: look for wheel-shaped loose parts inside single meshes.
    zmin = vehicle_bbox["min"][2] if vehicle_bbox else 0.0
    for name, islands in islands_by_obj.items():
        # Merge islands whose bounds overlap heavily (tire + rim + hub are often separate islands)
        clusters = []
        for isl in islands:
            size = isl["max"] - isl["min"]
            if max(size) < 0.05:
                continue
            c = (isl["max"] + isl["min"]) / 2
            for cl in clusters:
                if (cl["c"] - c).length < 0.15:
                    cl["min"] = Vector(map(min, cl["min"], isl["min"]))
                    cl["max"] = Vector(map(max, cl["max"], isl["max"]))
                    cl["n"] += 1
                    cl["faces"] += isl["faces"]
                    break
            else:
                clusters.append({"c": c, "min": isl["min"].copy(), "max": isl["max"].copy(), "n": 1, "faces": isl["faces"]})
        for cl in clusters:
            size = cl["max"] - cl["min"]
            center = (cl["max"] + cl["min"]) / 2
            if wheel_like(size) and cl["min"].z < zmin + 0.1:
                suggestions.append({
                    "object": name, "kind": "loose_part_cluster",
                    "reasons": ["wheel-shaped loose geometry touching the ground plane"],
                    "center": [round(x, 4) for x in center],
                    "size": [round(x, 4) for x in size],
                    "islands": cl["n"], "faces": cl["faces"],
                })
    return suggestions


def classify_slots(suggestions, fwd_axis):
    """Attach a tentative FL/FR/RL/RR guess to each suggestion (still a guess)."""
    if len(suggestions) != 4:
        return
    ax = {"+Y": (1, 1), "-Y": (1, -1), "+X": (0, 1), "-X": (0, -1)}[fwd_axis]
    lat = 0 if ax[0] == 1 else 1
    for s in suggestions:
        c = s["center"]
        front = (c[ax[0]] * ax[1]) > 0
        # With +Y forward and +Z up, driver-left is -X.
        left_sign = -1 if fwd_axis in ("+Y", "-X") else 1
        if fwd_axis in ("+X",):
            left_sign = 1
        left = (c[lat] * left_sign) > 0
        s["tentative_slot"] = ("F" if front else "R") + ("L" if left else "R")


def guess_forward(objects, bbox):
    """Forward axis guess: longest horizontal axis, sign from where the windscreen/cabin sits is
    unreliable, so report only the axis and leave the sign for a human."""
    if not bbox:
        return None
    sx, sy = bbox["size"][0], bbox["size"][1]
    return "Y" if sy > sx else "X"


def material_report():
    mats = []
    for m in bpy.data.materials:
        entry = {"name": m.name, "users": m.users, "use_nodes": m.use_nodes, "images": [], "shader_nodes": []}
        if m.use_nodes and m.node_tree:
            for n in m.node_tree.nodes:
                entry["shader_nodes"].append(n.bl_idname)
                if n.bl_idname == "ShaderNodeTexImage" and n.image:
                    entry["images"].append(n.image.name)
        entry["shader_nodes"] = sorted(set(entry["shader_nodes"]))
        mats.append(entry)
    images = []
    for img in bpy.data.images:
        if img.type not in ("IMAGE",):
            continue
        path = bpy.path.abspath(img.filepath) if img.filepath else ""
        packed = img.packed_file is not None
        exists = packed or (bool(path) and os.path.exists(path))
        has_pixels = img.has_data or (img.size[0] > 0)
        images.append({
            "name": img.name, "filepath": img.filepath, "packed": packed,
            "file_found": exists, "size": list(img.size), "colorspace": img.colorspace_settings.name,
            "missing": not (exists or has_pixels),
        })
    return mats, images


def main():
    args = parse()
    src = os.path.abspath(args.input)
    if not os.path.exists(src):
        raise SystemExit(f"Input not found: {src}")
    mtime_before = os.path.getmtime(src)
    pc.reset_scene()
    imported = pc.import_model(src)
    objects = list(bpy.data.objects)
    meshes = [o for o in objects if o.type == "MESH"]
    bbox = pc.world_bbox(meshes)

    obj_reports = []
    islands_by_obj = {}
    totals = {"verts": 0, "edges": 0, "faces": 0, "tris": 0}
    for o in objects:
        r = {
            "name": o.name, "type": o.type,
            "parent": o.parent.name if o.parent else None,
            "children": [c.name for c in o.children],
            "location": [round(x, 5) for x in o.location],
            "rotation_euler_deg": [round(math.degrees(x), 3) for x in o.rotation_euler],
            "scale": [round(x, 5) for x in o.scale],
            "world_origin": [round(x, 5) for x in o.matrix_world.translation],
            "dimensions": [round(x, 5) for x in o.dimensions],
            "hidden_viewport": o.hide_viewport, "hidden_render": o.hide_render,
            "hidden_in_view_layer": o.hide_get(),
            "modifiers": [m.type for m in o.modifiers],
            "zero_scale": any(abs(s) < 1e-8 for s in o.scale),
            "unapplied_transform": any(abs(s - 1) > 1e-4 for s in o.scale) or any(abs(r_) > 1e-4 for r_ in o.rotation_euler),
        }
        if o.type == "MESH":
            me = o.data
            tris = pc.mesh_triangle_count(me)
            r.update({
                "verts": len(me.vertices), "edges": len(me.edges), "faces": len(me.polygons), "tris": tris,
                "uv_maps": [uv.name for uv in me.uv_layers],
                "color_attributes": [c.name for c in me.color_attributes],
                "material_slots": [s.material.name if s.material else None for s in o.material_slots],
                "empty_material_slots": sum(1 for s in o.material_slots if s.material is None),
            })
            wb = pc.world_bbox([o])
            r["world_bbox"] = wb
            # Origin relative to its own geometry: is the pivot at the mesh centre?
            r["origin_offset_from_bbox_center"] = [round(a - b, 5) for a, b in zip(o.matrix_world.translation, wb["center"])]
            health, islands = mesh_health(o, args.merge_dist)
            r["health"] = health
            islands_by_obj[o.name] = islands
            for k in ("verts", "edges", "faces", "tris"):
                totals[k] += r[k]
        obj_reports.append(r)

    wheel_sugg = suggest_wheels(objects, islands_by_obj, bbox)
    loose_wheel = [s for s in wheel_sugg if s["kind"] == "loose_part_cluster"]
    obj_wheel = [s for s in wheel_sugg if s["kind"] == "object"]
    mats, images = material_report()

    names = [o.name for o in objects]
    report = {
        "car_id": args.car_id,
        "generated_utc": pc.now_iso(),
        "tool": "inspect_vehicle.py",
        "blender": pc.blender_info(),
        "source_file": src,
        "source_size_bytes": os.path.getsize(src),
        "source_unmodified": os.path.getmtime(src) == mtime_before,
        "scene_unit_scale": bpy.context.scene.unit_settings.scale_length,
        "object_count": len(objects),
        "object_types": {t: sum(1 for o in objects if o.type == t) for t in sorted({o.type for o in objects})},
        "duplicate_object_names": sorted({n for n in names if names.count(n) > 1}),
        "totals": totals,
        "vehicle_bbox": bbox,
        "longest_horizontal_axis": guess_forward(objects, bbox),
        "objects": obj_reports,
        "materials": mats,
        "images": images,
        "missing_images": [i["name"] for i in images if i["missing"]],
        "wheel_candidates": {
            "NOTE": "Suggestions only. Verify visually before renaming, separating or moving anything.",
            "separate_wheel_objects_found": len(obj_wheel),
            "appears_to_have_four_separate_wheel_objects": len(obj_wheel) == 4,
            "wheel_shaped_loose_parts_in_joined_meshes": len(loose_wheel),
            "suggestions": wheel_sugg,
        },
    }
    pc.write_json(args.out, report)
    print(f"Inspection report written: {args.out}")
    print(f"Objects={len(objects)} meshes={len(meshes)} tris={totals['tris']} "
          f"wheel objects={len(obj_wheel)} wheel-shaped loose parts={len(loose_wheel)}")


if __name__ == "__main__":
    main()
