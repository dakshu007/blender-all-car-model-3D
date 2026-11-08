"""build_environment_kit.py - build every environment component into one library .blend.

    python build_environment_kit.py -- --out ../../../EnvironmentLibrary/Working/ENV_Kit_v001.blend \
        --report ../../Reports/ENV_Kit_build.json [--only ENV_CherryKing ENV_Pagoda] [--overwrite]
    (or: blender -b -P build_environment_kit.py -- ...)

Each component lives in its own collection ENV_<Name> with a <Name>_Root empty
(origin on the ground at the component's base). Roots are spread on a grid in the
library file; export_env_fbx.py zeroes the root before exporting.
"""

import argparse
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))

import bpy  # noqa: E402

import pipeline_common as pc  # noqa: E402
import env_architecture as A  # noqa: E402
import env_common as E  # noqa: E402
import env_roads as R  # noqa: E402
import env_vegetation as V  # noqa: E402

COMPONENTS = [
    # name, builder, kwargs, category
    ("ENV_CherryKing", V.cherry_tree, dict(height=9.5, spread=5.6, seed=2, limbs=8, petals=40, trunk_ratio=0.46), "vegetation"),
    ("ENV_CherryTree", V.cherry_tree, dict(height=6.5, spread=3.8, seed=5, limbs=6, petals=24), "vegetation"),
    ("ENV_CherryYoung", V.cherry_tree, dict(height=4.2, spread=2.3, seed=9, limbs=5, petals=10, trunk_ratio=0.5), "vegetation"),
    ("ENV_GrassClump", V.grass_clump_component, dict(), "vegetation"),
    ("ENV_GrassPatch", V.grass_patch, dict(), "vegetation"),
    ("ENV_Bamboo", V.bamboo_cluster, dict(), "vegetation"),
    ("ENV_TeaTerrace", V.tea_terrace, dict(), "vegetation"),
    ("ENV_HouseTwoStorey", A.house_two_storey, dict(), "architecture"),
    ("ENV_HouseShop", A.house_shop, dict(), "architecture"),
    ("ENV_Garage", A.garage, dict(), "architecture"),
    ("ENV_Pagoda", A.pagoda, dict(), "architecture"),
    ("ENV_LakePavilion", A.lake_pavilion, dict(), "architecture"),
    ("ENV_PaifangGate", A.paifang, dict(), "architecture"),
    ("ENV_StoneBridge", A.stone_bridge, dict(), "architecture"),
    ("ENV_LanternHanging", A.lantern_component, dict(), "props"),
    ("ENV_LanternPost", A.lantern_post, dict(), "props"),
    ("ENV_Road_AsphaltStraight", R.road_asphalt_straight, dict(), "roads"),
    ("ENV_Road_AsphaltCurve90", R.road_asphalt_curve, dict(), "roads"),
    ("ENV_Road_Hairpin", R.road_hairpin, dict(), "roads"),
    ("ENV_Road_Gravel", R.road_gravel, dict(), "roads"),
    ("ENV_Road_Dirt", R.road_dirt, dict(), "roads"),
    ("ENV_Road_LanternStreet", R.road_lantern_street, dict(), "roads"),
    ("ENV_Water_River", R.river, dict(), "water"),
    ("ENV_Water_Lake", R.lake, dict(), "water"),
]


def parse():
    p = argparse.ArgumentParser()
    p.add_argument("--out", required=True)
    p.add_argument("--report", required=True)
    p.add_argument("--only", nargs="*", default=None)
    p.add_argument("--overwrite", action="store_true")
    return p.parse_args(pc.script_args())


def main():
    a = parse()
    out = os.path.abspath(a.out)
    if os.path.exists(out) and not a.overwrite:
        out = pc.next_version(out)
    pc.reset_scene()
    sc = bpy.context.scene
    sc.frame_start, sc.frame_end = 1, E.LOOP
    sc.render.fps = E.FPS
    lib = E.new_collection("ENV_Kit")
    cursor_x, row_y, row_depth = 0.0, 0.0, 0.0
    report = []
    for name, fn, kw, cat in COMPONENTS:
        if a.only and name not in a.only:
            continue
        coll = E.new_collection(name, lib)
        root = fn(coll, name=name, **kw)
        bpy.context.view_layer.update()
        meshes = [o for o in coll.all_objects if o.type == "MESH"]
        bb = pc.world_bbox(meshes)
        w, d = bb["size"][0], bb["size"][1]
        if cursor_x + w > 120:
            cursor_x, row_y, row_depth = 0.0, row_y - row_depth - 8.0, 0.0
        root.location = (cursor_x + w / 2 - bb["center"][0], row_y - d / 2 - bb["center"][1], 0)
        cursor_x += w + 8.0
        row_depth = max(row_depth, d)
        anim = sum(1 for o in coll.all_objects if o.animation_data and o.animation_data.action) + \
            sum(1 for o in meshes if o.data.shape_keys and o.data.shape_keys.animation_data)
        tris = sum(pc.mesh_triangle_count(o.data) for o in meshes)
        report.append({"component": name, "category": cat, "objects": len(coll.all_objects), "meshes": len(meshes),
                       "triangles": tris, "animated_objects": anim,
                       "size_m": [round(x, 2) for x in bb["size"]]})
        print(f"[built] {name:28s} tris={tris:6d} objects={len(coll.all_objects):3d} animated={anim}")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=out, compress=True)
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_as_mainfile(filepath=out, compress=True)
    pc.write_json(a.report, {"tool": "build_environment_kit.py", "generated_utc": pc.now_iso(),
                             "blender": pc.blender_info(), "saved_file": out, "loop_frames": E.LOOP, "fps": E.FPS,
                             "components": report})
    print(f"Saved {out} ({len(report)} components)")


if __name__ == "__main__":
    main()
