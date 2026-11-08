"""build_diorama.py - a spring-valley vignette assembled from the environment kit, in the spirit
of the reference painting and the map blueprint (path -> paifang gate -> bridge -> pagoda hill).

    python build_diorama.py -- --out ../../../EnvironmentLibrary/Working/ENV_Diorama_v001.blend [--overwrite]

Cameras: Diorama_Cam (hero), Closeup_Cam (gate / blossom close-up for the motion test).
"""

import argparse
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import pipeline_common as pc  # noqa: E402
import env_architecture as A  # noqa: E402
import env_common as E  # noqa: E402
import env_roads as R  # noqa: E402
import env_vegetation as V  # noqa: E402
import render_env  # noqa: E402

RIVER_Y = 40.0
TREES = [("K", -9.5, -9, 2), ("K", 9.0, -16, 7), ("T", -7.5, 3, 5), ("T", 7.5, 1.5, 11), ("T", -9, 22, 13),
         ("T", 9.5, 24, 17), ("Y", -7.0, -24, 19), ("Y", 6.5, -30, 23), ("T", -15, -20, 29), ("T", 15, -6, 31),
         ("Y", 12, 15, 37), ("Y", -12, 10, 41), ("T", -6, 56, 43), ("T", 12, 52, 47), ("Y", -14, 64, 53),
         ("T", 24, 46, 59), ("Y", 30, 70, 61), ("T", -40, 18, 67), ("T", 40, 0, 71), ("Y", 18, 95, 73)]
LAKE = (27.0, 18.0, 13.0, 8.5)        # cx, cy, rx, ry
HILL = (14.0, 125.0, 36.0, 30.0)      # cx, cy, height, sigma


def catmull(pts, step=1.0):
    out = []
    P = [Vector(p) for p in pts]
    P = [P[0]] + P + [P[-1]]
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        n = max(2, int((p2 - p1).length / step))
        for k in range(n):
            t = k / n
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t +
                              (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t))
    out.append(P[-2])
    return out


DIRT = catmull([(0, -48), (-1.5, -32), (-2.8, -19), (0.2, -7), (2.0, 3), (0, 13)])
ASPH = catmull([(0, 13), (0, 22), (0, RIVER_Y - 11)])
ASPH2 = catmull([(0, RIVER_Y + 11), (0.5, 57), (4, 72), (10, 87)])
PATHS = DIRT + ASPH + ASPH2


def dist_to_path(x, y):
    best = 1e9
    p = Vector((x, y, 0))
    for a, b in zip(PATHS, PATHS[1:]):
        a2, b2 = Vector((a.x, a.y, 0)), Vector((b.x, b.y, 0))
        ab = b2 - a2
        L2 = ab.length_squared or 1e-9
        t = max(0.0, min(1.0, (p - a2).dot(ab) / L2))
        best = min(best, (p - (a2 + ab * t)).length)
    return best


def height(x, y, near_path):
    h = 0.35 * math.sin(x * 0.11) * math.sin(y * 0.09) + 0.2 * math.sin(x * 0.27 + y * 0.19)
    hx, hy, hh, hs = HILL
    h += hh * math.exp(-((x - hx) ** 2 + (y - hy) ** 2) / (2 * hs * hs))
    h += 20 * math.exp(-((x + 75) ** 2 + (y - 95) ** 2) / (2 * 32 ** 2))
    h += 24 * math.exp(-((x - 85) ** 2 + (y - 80) ** 2) / (2 * 26 ** 2))
    h += 14 * math.exp(-((x + 40) ** 2 + (y - 150) ** 2) / (2 * 30 ** 2))
    d = near_path
    if d < 7.0:
        flat = -0.07
        t = max(0.0, (d - 4.0) / 3.0)
        h = flat + (h - flat) * t * t
    dr = abs(y - RIVER_Y)
    if dr < 11.0:
        h = min(h, -1.15 + max(0.0, dr - 9.0) * 0.6)
    cx, cy, rx, ry = LAKE
    k = math.hypot((x - cx) / rx, (y - cy) / ry)
    if k < 1.4:
        h = min(h, -1.15 + max(0.0, k - 1.25) * 8)
    return h


def terrain(coll):
    eb = E.EnvBuilder("Diorama_Terrain")
    m = E.mat_vcol("M_Env_Terrain", rough=0.95, sheen=0.1)
    x0, x1, y0, y1, st = -120.0, 120.0, -55.0, 185.0, 1.6
    nx, ny = int((x1 - x0) / st), int((y1 - y0) / st)
    g1, g2, g3 = E.hex_rgba("#93C866"), E.hex_rgba("#77B24E"), E.hex_rgba("#B7DA80")
    pk, pk2 = E.hex_rgba("#F3C2CF"), E.hex_rgba("#FBE3EA")
    spots = [(x, y, 7.5 if k == "K" else (5.0 if k == "T" else 3.2)) for k, x, y, _s in TREES]
    haze, rock = E.hex_rgba("#A9C7CF"), E.hex_rgba("#93998A")
    grid = []
    for j in range(ny + 1):
        row = []
        y = y0 + j * st
        for i in range(nx + 1):
            x = x0 + i * st
            dp = dist_to_path(x, y) if -60 < y < 100 and abs(x) < 40 else 99.0
            row.append(eb.bm.verts.new((x, y, height(x, y, dp))))
        grid.append(row)
    faces = []
    for j in range(ny):
        for i in range(nx):
            faces.append(eb.bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i])))
    eb._setmat(faces, m)

    def col(face, lp):
        p = lp.vert.co
        n = 0.5 + 0.5 * math.sin(p.x * 0.21 + math.sin(p.y * 0.13) * 2.0) * math.sin(p.y * 0.17)
        c = E.mix(g2, g1, n)
        if math.sin(p.x * 0.07 + 1.3) * math.sin(p.y * 0.05) > 0.55:
            c = E.mix(c, g3, 0.6)
        if p.z > 18:
            c = E.mix(c, rock, min(1, (p.z - 18) / 14) * 0.6)
        for sx, sy, sr in spots:  # fallen-petal carpets under the blossom trees
            d = math.hypot(p.x - sx, p.y - sy)
            if d < sr:
                speck = 0.5 + 0.5 * math.sin(p.x * 3.7 + p.y * 2.9) * math.sin(p.x * 1.3 - p.y * 4.1)
                c = E.mix(c, pk2 if speck > 0.7 else pk, (1 - d / sr) ** 0.7 * 0.85)
        t = max(0.0, min(1.0, (p.y - 45) / 130))
        return E.mix(c, haze, t * 0.75)
    eb.paint(faces, col)
    o = eb.finish_vcol(coll, recalc=False, wind=lambda co: 0.0)
    o.data.materials.clear()
    o.data.materials.append(m)
    E.smooth_by_angle(o, 60)
    return o


def place(root, loc, rot_deg=0.0, scale=1.0):
    root.location = loc
    root.rotation_euler = (0, 0, math.radians(rot_deg))
    root.scale = (scale, scale, scale)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--out", required=True)
    p.add_argument("--overwrite", action="store_true")
    a = p.parse_args(pc.script_args())
    out = os.path.abspath(a.out)
    if os.path.exists(out) and not a.overwrite:
        out = pc.next_version(out)
    pc.reset_scene()
    sc = bpy.context.scene
    sc.frame_start, sc.frame_end, sc.render.fps = 1, E.LOOP, E.FPS
    rng = random.Random(42)
    D = E.new_collection("Diorama")
    terrain(D)
    hz = lambda x, y: height(x, y, dist_to_path(x, y))  # noqa: E731

    # roads
    rc = E.new_collection("Diorama_Roads", D)
    R.road_strip(rc, "Diorama_DirtPath", [(v.x, v.y, 0.0) for v in DIRT], 4.4, "gravel", shoulders=0.9)
    asp = [(v.x, v.y, 0.25 * min(1.0, max(0.0, (v.y - (RIVER_Y - 14)) / 3.0))) for v in ASPH]
    R.road_strip(rc, "Diorama_Road_A", asp, 7.0, "asphalt")
    asp2 = [(v.x, v.y, 0.25 * max(0.0, 1 - (v.y - (RIVER_Y + 11)) / 3.0)) for v in ASPH2]
    R.road_strip(rc, "Diorama_Road_B", asp2, 7.0, "asphalt")

    def add(fn, name, loc, rot=0.0, scale=1.0, **kw):
        c = E.new_collection(name, D)
        r = fn(c, name=name, **kw)
        z = loc[2] if len(loc) > 2 else hz(loc[0], loc[1])
        place(r, (loc[0], loc[1], z), rot, scale)
        return r

    add(R.river, "D_River", (0, RIVER_Y, -0.02), rot=90, length=240.0)
    add(A.stone_bridge, "D_Bridge", (0, RIVER_Y, 0.0), length=22.0)
    add(R.lake, "D_Lake", (LAKE[0], LAKE[1], 0.0), rx=LAKE[2], ry=LAKE[3])
    add(A.lake_pavilion, "D_Pavilion", (LAKE[0] - 1.0, LAKE[1] + 1.5, -0.3), rot=200)
    add(A.paifang, "D_Gate", (0, 13, 0.0))
    for i, (x, y) in enumerate(((-3.6, 9), (5.4, 9), (-3.0, 4), (6.4, 4))):
        add(A.lantern_post, f"D_LanternPost_{i}", (x, y, 0.0), rot=0 if x < 0 else 180, phase=rng.uniform(0, 6))
    add(A.house_two_storey, "D_House", (-17.5, 19, 0.0), rot=-90)
    add(A.house_shop, "D_Shop", (16.0, 5.5, 0.0), rot=90)
    add(A.garage, "D_Garage", (-19.0, -4.0, 0.0), rot=-90)
    hx, hy, hh, _hs = HILL
    add(A.pagoda, "D_Pagoda", (hx, hy, hz(hx, hy) - 0.3), rot=10, scale=1.25)
    add(V.tea_terrace, "D_Tea", (46, 62), rot=-25)
    for i, (x, y) in enumerate(((-24, 33), (-30, 46), (-20, 52), (-34, 30))):
        add(V.bamboo_cluster, f"D_Bamboo_{i}", (x, y), seed=20 + i)

    # cherry blossoms framing the path
    for i, (kind, x, y, seed) in enumerate(TREES):
        if kind == "K":
            add(V.cherry_tree, f"D_CherryKing_{i}", (x, y), rot=rng.uniform(0, 360), height=9.5, spread=5.6,
                seed=seed, limbs=8, petals=40, trunk_ratio=0.46)
        elif kind == "T":
            add(V.cherry_tree, f"D_Cherry_{i}", (x, y), rot=rng.uniform(0, 360), height=6.5, spread=3.8, seed=seed,
                limbs=6, petals=18 if y < 30 else 0)
        else:
            add(V.cherry_tree, f"D_CherryYoung_{i}", (x, y), rot=rng.uniform(0, 360), height=4.2, spread=2.3,
                seed=seed, limbs=5, petals=8 if y < 30 else 0, trunk_ratio=0.5)

    # grass meadows (phase-shifted so the wind rolls across)
    n = 0
    tries = 0
    while n < 34 and tries < 400:
        tries += 1
        x, y = rng.uniform(-16, 16), rng.uniform(-40, 30)
        if dist_to_path(x, y) < 3.6 or abs(y - 13) < 2.5 or (x < -12 and -10 < y < 25) or (x > 11 and 0 < y < 12):
            continue
        add(V.grass_patch, f"D_Grass_{n:02d}", (x, y), rot=rng.uniform(0, 360), seed=100 + n,
            phase=(y + 40) / 70 * 6.28, clumps=60)
        n += 1
    for k in range(10):  # clumps right in front of the camera
        x, y = rng.uniform(-6, 6), rng.uniform(-44, -36)
        if dist_to_path(x, y) < 2.8:
            continue
        add(V.grass_clump_component, f"D_Clump_{k}", (x, y), rot=rng.uniform(0, 360), seed=200 + k,
            phase=(y + 40) / 70 * 6.28, scale=1.6)

    render_env.world_and_lights(sc)
    cam_d = bpy.data.cameras.new("Diorama_Cam")
    cam = bpy.data.objects.new("Diorama_Cam", cam_d)
    sc.collection.objects.link(cam)
    cam.location = (1.8, -40.0, 2.5)
    cam_d.lens = 24
    cam_d.clip_end = 600
    tgt = Vector((2.5, 30.0, 6.5))
    cam.rotation_euler = (tgt - cam.location).to_track_quat("-Z", "Y").to_euler()
    cd2 = bpy.data.cameras.new("Closeup_Cam")
    cam2 = bpy.data.objects.new("Closeup_Cam", cd2)
    sc.collection.objects.link(cam2)
    cam2.location = (2.6, -9.0, 1.8)
    cd2.lens = 30
    cd2.clip_end = 400
    tgt2 = Vector((0.0, 12.0, 4.6))
    cam2.rotation_euler = (tgt2 - cam2.location).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=out, compress=True)
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_as_mainfile(filepath=out, compress=True)
    tris = sum(pc.mesh_triangle_count(o.data) for o in bpy.data.objects if o.type == "MESH")
    print(f"Saved {out}  objects={len(bpy.data.objects)} tris={tris}")


if __name__ == "__main__":
    main()
