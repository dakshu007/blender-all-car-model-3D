"""export_env_fbx.py - export each environment component (with its animation) to FBX for Unity.

    python export_env_fbx.py -- --blend ../../../EnvironmentLibrary/Working/ENV_Kit_v001.blend \
        --out-dir ../../../EnvironmentLibrary/Export --report ../../Reports/ENV_Kit_export.json [--only ...]

Same axis contract as the cars (axis_forward='Z', axis_up='Y', no Apply Transform).
Animation: 96-frame loop baked (sway transforms, 'Wind' blend shapes, petals, lanterns, flags).
Each export is re-imported to verify objects + animation survived.
"""

import argparse
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import bpy  # noqa: E402

import pipeline_common as pc  # noqa: E402

FBX = dict(use_selection=True, object_types={"EMPTY", "MESH"}, axis_forward="Z", axis_up="Y",
           bake_space_transform=False, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
           use_mesh_modifiers=True, mesh_smooth_type="FACE", use_custom_props=True, add_leaf_bones=False,
           colors_type="SRGB", path_mode="STRIP", embed_textures=False,
           bake_anim=True, bake_anim_use_all_bones=False, bake_anim_use_nla_strips=False,
           bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
           bake_anim_simplify_factor=0.0)


def parse():
    p = argparse.ArgumentParser()
    p.add_argument("--blend", required=True)
    p.add_argument("--out-dir", required=True)
    p.add_argument("--report", required=True)
    p.add_argument("--only", nargs="*", default=None)
    return p.parse_args(pc.script_args())


def verify(path):
    """Re-import into a scratch scene; count objects and animated channels."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, axis_forward="Z", axis_up="Y")
    new = [o for o in bpy.data.objects if o not in before]
    anim_objs = sum(1 for o in new if o.animation_data and o.animation_data.action)
    shape_anim = sum(1 for o in new if o.type == "MESH" and o.data.shape_keys and o.data.shape_keys.animation_data
                     and o.data.shape_keys.animation_data.action)
    vcols = sum(1 for o in new if o.type == "MESH" and o.data.color_attributes)
    for o in new:
        bpy.data.objects.remove(o)
    return {"objects": len(new), "animated_objects": anim_objs, "animated_blend_shapes": shape_anim,
            "meshes_with_vertex_colors": vcols}


def main():
    a = parse()
    bpy.ops.wm.open_mainfile(filepath=os.path.abspath(a.blend))
    sc = bpy.context.scene
    out_dir = os.path.abspath(a.out_dir)
    os.makedirs(out_dir, exist_ok=True)
    kit = bpy.data.collections["ENV_Kit"]
    results = []
    for coll in kit.children:
        name = coll.name
        if a.only and name not in a.only:
            continue
        objs = list(coll.all_objects)
        root = next((o for o in objs if o.name == f"{name}_Root"), None)
        saved = root.location.copy()
        root.location = (0, 0, 0)
        bpy.context.view_layer.update()
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.hide_set(False)
            o.select_set(True)
        bpy.context.view_layer.objects.active = root
        path = os.path.join(out_dir, f"{name}.fbx")
        bpy.ops.export_scene.fbx(filepath=path, **FBX)
        root.location = saved
        expected = {"objects": len(objs),
                    "animated": sum(1 for o in objs if o.animation_data and o.animation_data.action),
                    "blend_shapes": sum(1 for o in objs if o.type == "MESH" and o.data.shape_keys
                                        and o.data.shape_keys.animation_data)}
        got = verify(path)
        ok = got["objects"] == expected["objects"] and got["animated_objects"] >= expected["animated"] \
            and got["animated_blend_shapes"] >= expected["blend_shapes"]
        textures = sorted({os.path.basename(bpy.path.abspath(n.image.filepath)) for o in objs if o.type == "MESH"
                           for m in o.data.materials if m and m.node_tree for n in m.node_tree.nodes
                           if n.bl_idname == "ShaderNodeTexImage" and n.image})
        for t in textures:
            src = os.path.join(os.path.dirname(os.path.abspath(a.blend)), "..", "Textures", t)
            if os.path.exists(src):
                shutil.copy2(src, os.path.join(out_dir, t))
        tris = sum(pc.mesh_triangle_count(o.data) for o in objs if o.type == "MESH")
        results.append({"component": name, "fbx": os.path.basename(path), "bytes": os.path.getsize(path),
                        "triangles": tris, "expected": expected, "reimported": got, "textures": textures,
                        "status": "PASS" if ok else "FAIL"})
        print(f"[{'PASS' if ok else 'FAIL'}] {name:28s} {os.path.getsize(path) // 1024:6d} KB  "
              f"objs {got['objects']}/{expected['objects']}  anim {got['animated_objects']}/{expected['animated']}  "
              f"shapes {got['animated_blend_shapes']}/{expected['blend_shapes']}")
    pc.write_json(a.report, {"tool": "export_env_fbx.py", "generated_utc": pc.now_iso(), "blender": pc.blender_info(),
                             "settings": {k: (sorted(v) if isinstance(v, set) else v) for k, v in FBX.items()},
                             "loop_frames": [sc.frame_start, sc.frame_end], "results": results})
    fails = [r for r in results if r["status"] != "PASS"]
    print(f"EXPORT {'PASS' if not fails else 'FAIL'}: {len(results) - len(fails)}/{len(results)}")


if __name__ == "__main__":
    main()
