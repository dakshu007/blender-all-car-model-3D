"""env_architecture.py - Chinese-theme buildings and props (fronts face -Y).

Lanterns hang from their own pivot empties and swing on the 96-frame loop.
"""

import math
import os
import random

import bpy
import bmesh
from mathutils import Vector

from env_common import (PAL, EnvBuilder, empty, hex_rgba, lattice_window, mat_glow, mat_vcol, mix, ngon, railing,
                        rect, roof, sway, tube)

TEX_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "EnvironmentLibrary", "Textures"))


def C(k, a=1.0):
    return hex_rgba(PAL[k], a)


def finish(eb, coll, parent, recalc=True):
    o = eb.finish_vcol(coll, recalc=recalc)
    if parent is not None:
        o.parent = parent
    return o


def textured_quad(coll, name, center, w, h, facing, image, parent=None):
    """A single UV-mapped quad showing an image, facing '-y', 'y', '-x' or 'x'."""
    from vehicle_builder import material
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    cx, cy, cz = center
    if facing in ("-y", "y"):
        s = -1 if facing == "-y" else 1
        pts = [(cx - w / 2 * -s, cy, cz - h / 2), (cx + w / 2 * -s, cy, cz - h / 2),
               (cx + w / 2 * -s, cy, cz + h / 2), (cx - w / 2 * -s, cy, cz + h / 2)]
    else:
        s = -1 if facing == "-x" else 1
        pts = [(cx, cy - w / 2 * s, cz - h / 2), (cx, cy + w / 2 * s, cz - h / 2),
               (cx, cy + w / 2 * s, cz + h / 2), (cx, cy - w / 2 * s, cz + h / 2)]
    f = bm.faces.new([bm.verts.new(p) for p in pts])
    for lp, t in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
        lp[uv].uv = t
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    img = bpy.data.images.load(os.path.join(TEX_DIR, image), check_existing=True)
    me.materials.append(material(f"M_{os.path.splitext(image)[0]}", "#FFFFFF", rough=0.6, image=img, specular=0.3))
    o = bpy.data.objects.new(name, me)
    coll.objects.link(o)
    if parent:
        o.parent = parent
    return o


# ---------------------------------------------------------------- lanterns ---
def lantern(coll, name, hook, parent=None, scale=1.0, phase=0.0, cord=0.25, swing=7.0):
    """Hanging red lantern. Pivot at the hook point swings on the loop."""
    pv = empty(f"{name}_Pivot", coll, loc=hook, parent=parent, size=0.1)
    m, g = mat_vcol(), mat_glow()
    s = scale
    zc = -cord * s
    eb = EnvBuilder(f"{name}_Mesh")
    f = eb.cyl((0, 0, 0), (0, 0, -cord * s), 0.008 * s, m, seg=5)
    eb.paint(f, C("wood_dark"))
    f = eb.cyl((0, 0, zc), (0, 0, zc - 0.05 * s), 0.09 * s, m, seg=12)
    eb.paint(f, C("gold"))
    rings = []
    n, seg = 9, 16
    for i in range(n):
        t = i / (n - 1)
        z = zc - 0.05 * s - 0.32 * s * t
        r = s * (0.09 + 0.13 * math.sin(math.pi * t))
        rings.append([eb.bm.verts.new((r * math.cos(2 * math.pi * k / seg), r * math.sin(2 * math.pi * k / seg), z))
                      for k in range(seg)])
    body = []
    for a, b in zip(rings, rings[1:]):
        for k in range(seg):
            body.append(eb.bm.faces.new((a[k], b[k], b[(k + 1) % seg], a[(k + 1) % seg])))
    body.append(eb.bm.faces.new(rings[0]))
    body.append(eb.bm.faces.new(list(reversed(rings[-1]))))
    eb._setmat(body, g)
    red, rib = C("lantern"), C("deep_red")
    for idx, fc in enumerate(body):
        eb.paint([fc], rib if (idx % seg) % 4 == 0 else red)
    zb = zc - 0.05 * s - 0.32 * s
    f = eb.cyl((0, 0, zb), (0, 0, zb - 0.05 * s), 0.09 * s, m, seg=12)
    eb.paint(f, C("gold"))
    f = eb.cyl((0, 0, zb - 0.05 * s), (0, 0, zb - 0.25 * s), 0.025 * s, m, seg=6, r2=0.04 * s)
    eb.paint(f, C("lantern"))
    o = eb.finish_vcol(coll, recalc=True)
    o.parent = pv
    sway(pv, amp_deg=(swing, swing * 0.6, 0), phase=phase)
    return pv


def lantern_component(coll, name="ENV_LanternHanging"):
    root = empty(f"{name}_Root", coll, kind="ARROWS", size=0.3)
    root["component"] = name
    eb = EnvBuilder(f"{name}_Bracket")
    f = eb.box((0, 0.25, 2.4), (0.12, 0.6, 0.08), mat_vcol())
    eb.paint(f, C("wood_dark"))
    finish(eb, coll, root)
    lantern(coll, f"{name}_L", (0, 0.5, 2.36), parent=root, scale=1.6, cord=0.3)
    return root


def lantern_post(coll, name="ENV_LanternPost", phase=0.0):
    root = empty(f"{name}_Root", coll, kind="ARROWS", size=0.3)
    root["component"] = name
    m = mat_vcol()
    eb = EnvBuilder(f"{name}_Post")
    f = eb.box((0, 0, 0.2), (0.5, 0.5, 0.4), m)
    eb.paint(f, C("stone"))
    f = eb.box((0, 0, 0.45), (0.36, 0.36, 0.1), m)
    eb.paint(f, C("stone_dark"))
    f = eb.cyl((0, 0, 0.5), (0, 0, 3.1), 0.075, m, seg=8)
    eb.paint(f, C("vermilion"))
    tube(eb, [(0, 0, 2.9), (0.15, 0, 3.15), (0.45, 0, 3.2), (0.62, 0, 3.12)], [0.05, 0.045, 0.04, 0.035], m, seg=6,
         col=C("deep_red"))
    f = eb.sphere((0, 0, 3.18), 0.1, m, seg=8)
    eb.paint(f, C("gold"))
    finish(eb, coll, root)
    lantern(coll, f"{name}_L", (0.6, 0, 3.08), parent=root, scale=1.4, phase=phase, cord=0.15)
    return root


# ------------------------------------------------------------------ houses ---
def house_two_storey(coll, name="ENV_HouseTwoStorey", seed=1):
    rng = random.Random(seed)
    root = empty(f"{name}_Root", coll, kind="ARROWS", size=0.6)
    root["component"] = name
    m = mat_vcol()
    eb = EnvBuilder(f"{name}_Mesh")
    P = lambda f, k: eb.paint(f, C(k))  # noqa: E731
    P(eb.box((0, 0, 0.3), (8.4, 6.4, 0.6), m), "roof_under")
    for i in range(3):
        P(eb.box((0, -3.2 - 0.25 - i * 0.3, 0.1 + 0.2 * (2 - i)), (2.2, 0.3, 0.2 * (3 - i)), m), "stone")
    P(eb.box((0, 0, 2.0), (7.2, 5.2, 2.8), m), "vermilion")
    for x, y in ((-3.6, -2.6), (3.6, -2.6), (-3.6, 2.6), (3.6, 2.6), (-1.2, -2.6), (1.2, -2.6)):
        P(eb.cyl((x, y, 0.6), (x, y, 3.45), 0.17, m, seg=10), "deep_red")
    P(eb.box((0, -2.62, 1.75), (1.5, 0.08, 2.3), m), "roof_under")
    P(eb.box((0, -2.67, 1.75), (0.04, 0.04, 2.2), m), "lattice")
    for x in (-0.18, 0.18):
        P(eb.sphere((x, -2.69, 1.8), 0.07, m, seg=8), "gold")
    for x in (-2.4, 2.4):
        lattice_window(eb, (x, -2.6, 1.95), 1.4, 1.0, "-y", m, C("lattice"), C("cream"), bars=4)
    for y in (-0.8, 0.8):
        lattice_window(eb, (3.6, y, 1.95), 1.0, 0.9, "x", m, C("lattice"), C("cream"), bars=3)
        lattice_window(eb, (-3.6, y, 1.95), 1.0, 0.9, "-x", m, C("lattice"), C("cream"), bars=3)
    railing(eb, [(-4.1, -3.05, 0.6), (-1.15, -3.05, 0.6)], 0.75, m, C("vermilion"))
    railing(eb, [(1.15, -3.05, 0.6), (4.1, -3.05, 0.6)], 0.75, m, C("vermilion"))
    roof(eb, rect(0, 0, 4.7, 3.7), rect(0, 0, 3.05, 2.15), 3.35, 0.85, 0.55, m, C("roof_red"), C("roof_under"),
         C("deep_red"), sub=10, rings=6, thickness=0.14)
    P(eb.box((0, 0, 5.05), (5.6, 4.0, 2.5), m), "vermilion")
    for x, y in ((-2.8, -2.0), (2.8, -2.0), (-2.8, 2.0), (2.8, 2.0)):
        P(eb.cyl((x, y, 3.8), (x, y, 6.25), 0.15, m, seg=10), "deep_red")
    for x in (-1.75, 0, 1.75):
        lattice_window(eb, (x, -2.0, 5.15), 1.05, 0.95, "-y", m, C("lattice"), C("cream"), bars=3)
    railing(eb, [(-2.9, -2.45, 4.15), (2.9, -2.45, 4.15)], 0.6, m, C("vermilion"), post_every=0.7)
    roof(eb, rect(0, 0, 3.85, 2.95), rect(0, 0, 1.7, 0.1), 6.2, 1.7, 0.75, m, C("roof_red"), C("roof_under"),
         C("deep_red"), sub=10, rings=7, thickness=0.14)
    finish(eb, coll, root)
    hooks = [(-4.55, -3.55, 3.75), (4.55, -3.55, 3.75), (-3.7, -2.8, 6.65), (3.7, -2.8, 6.65),
             (-1.05, -3.0, 3.25), (1.05, -3.0, 3.25)]
    for i, h in enumerate(hooks):
        lantern(coll, f"{name}_Lantern_{i}", h, parent=root, scale=1.25, phase=rng.uniform(0, 6.28))
    return root


def house_shop(coll, name="ENV_HouseShop", seed=2):
    rng = random.Random(seed)
    root = empty(f"{name}_Root", coll, kind="ARROWS", size=0.6)
    root["component"] = name
    m = mat_vcol()
    eb = EnvBuilder(f"{name}_Mesh")
    P = lambda f, k: eb.paint(f, C(k))  # noqa: E731
    P(eb.box((0, 0, 0.18), (8.6, 5.6, 0.36), m), "stone_dark")
    P(eb.box((0, 0.6, 1.85), (7.8, 3.8, 3.0), m), "vermilion")      # back block
    P(eb.box((-3.4, -1.75, 1.85), (1.0, 0.3, 3.0), m), "vermilion")  # front piers
    P(eb.box((3.4, -1.75, 1.85), (1.0, 0.3, 3.0), m), "vermilion")
    P(eb.box((0, -1.3, 0.75), (5.6, 0.7, 0.8), m), "wood")           # counter
    P(eb.box((0, -1.3, 1.17), (5.8, 0.8, 0.06), m), "wood_dark")
    for x in (-1.8, 0, 1.8):
        P(eb.cyl((x, -1.25, 1.2), (x, -1.25, 1.42), 0.12, m, seg=8), "plaster")
    P(eb.box((0, -1.8, 3.1), (7.8, 0.25, 0.5), m), "deep_red")       # lintel
    for x in (-3.9, 3.9, -1.3, 1.3):
        P(eb.cyl((x, -2.1, 0.36), (x, -2.1, 3.3), 0.15, m, seg=10), "deep_red")
    for y in (0.0, 1.6):
        lattice_window(eb, (3.9, y, 1.9), 1.0, 1.0, "x", m, C("lattice"), C("cream"), bars=3)
        lattice_window(eb, (-3.9, y, 1.9), 1.0, 1.0, "-x", m, C("lattice"), C("cream"), bars=3)
    roof(eb, rect(0, 0, 5.1, 3.4), rect(0, 0, 2.5, 0.08), 3.3, 1.5, 0.6, m, C("roof_red"), C("roof_under"),
         C("deep_red"), sub=10, rings=7, thickness=0.14)
    finish(eb, coll, root)
    textured_quad(coll, f"{name}_Sign", (-3.4, -1.93, 1.9), 0.55, 1.65, "-y", "ENV_Sign_Teahouse.png", root)
    for i, x in enumerate((-3.2, -1.1, 1.1, 3.2)):
        lantern(coll, f"{name}_Lantern_{i}", (x, -2.65, 3.3), parent=root, scale=1.2, phase=rng.uniform(0, 6.28))
    return root


def garage(coll, name="ENV_Garage", seed=3):
    rng = random.Random(seed)
    root = empty(f"{name}_Root", coll, kind="ARROWS", size=0.6)
    root["component"] = name
    m = mat_vcol()
    eb = EnvBuilder(f"{name}_Mesh")
    P = lambda f, k: eb.paint(f, C(k))  # noqa: E731
    P(eb.box((0, 0, 0.15), (12.0, 9.0, 0.3), m), "stone_dark")
    P(eb.box((0, 0, 2.45), (11.0, 8.0, 4.3), m), "vermilion")
    P(eb.box((0, -4.02, 1.95), (5.6, 0.08, 3.3), m), "slate")        # roller door
    for i in range(10):
        P(eb.box((0, -4.07, 0.5 + i * 0.32), (5.5, 0.03, 0.04), m), "slate_light")
    P(eb.box((0, -4.03, 3.75), (6.2, 0.1, 0.3), m), "deep_red")
    for x in (-5.5, 5.5, -3.0, 3.0):
        P(eb.cyl((x, -4.15, 0.3), (x, -4.15, 4.6), 0.2, m, seg=10), "deep_red")
    for x in (-4.3, 4.3):
        lattice_window(eb, (x, -4.0, 2.4), 1.2, 1.2, "-y", m, C("lattice"), C("cream"), bars=3)
    for k in range(3):  # tyre stacks
        for j in range(4):
            P(eb.cyl((6.6, -2.5 + k * 1.0, 0.3 + j * 0.24), (6.6, -2.5 + k * 1.0, 0.52 + j * 0.24), 0.33, m, seg=14),
              "slate")
    roof(eb, rect(0, 0, 6.6, 5.2), rect(0, 0, 3.6, 0.1), 4.55, 1.9, 0.75, m, C("slate"), C("roof_under"),
         C("deep_red"), sub=12, rings=7, thickness=0.16)
    finish(eb, coll, root)
    textured_quad(coll, f"{name}_Banner", (0, -4.25, 4.35), 5.6, 1.1, "-y", "ENV_Banner_Start.png", root)
    for i, x in enumerate((-7.0, 7.0)):  # flag poles with swaying flags
        fb = EnvBuilder(f"{name}_FlagPole_{i}")
        f = fb.cyl((x, -5.0, 0.0), (x, -5.0, 6.5), 0.06, m, seg=8)
        fb.paint(f, C("slate_light"))
        finish(fb, coll, root)
        pv = empty(f"{name}_Flag_{i}", coll, loc=(x, -5.0, 6.3), parent=root, size=0.1)
        fl = EnvBuilder(f"{name}_FlagMesh_{i}")
        vs = [fl.bm.verts.new(p) for p in ((0, 0, 0), (0, 0, -1.0), (1.6, 0, -0.85), (1.6, 0, -0.1))]
        f1 = fl.bm.faces.new(vs)
        f2 = fl.bm.faces.new([fl.bm.verts.new(v.co.copy()) for v in reversed(vs)])
        fl._setmat([f1, f2], m)
        fl.paint([f1, f2], C("lantern"))
        o = fl.finish_vcol(coll, recalc=False)
        o.parent = pv
        o.rotation_euler = (0, 0, math.radians(180 if x < 0 else 0))
        sway(pv, amp_deg=(3, 0, 12), phase=rng.uniform(0, 6.28))
    for i, x in enumerate((-3.0, 3.0)):
        lantern(coll, f"{name}_Lantern_{i}", (x, -4.6, 4.5), parent=root, scale=1.3, phase=rng.uniform(0, 6.28))
    return root


# ----------------------------------------------------------- temple pieces ---
def pagoda(coll, name="ENV_Pagoda", tiers=5, seed=4):
    rng = random.Random(seed)
    root = empty(f"{name}_Root", coll, kind="ARROWS", size=0.8)
    root["component"] = name
    m = mat_vcol()
    eb = EnvBuilder(f"{name}_Mesh")
    rot = math.pi / 8
    f = eb.prism(ngon(0, 0, 5.8, 8, rot), "XY", 0.0, 0.45, m)
    eb.paint(f, C("stone_dark"))
    f = eb.prism(ngon(0, 0, 5.1, 8, rot), "XY", 0.45, 0.9, m)
    eb.paint(f, C("stone"))
    for i in range(4):
        eb.paint(eb.box((0, -5.3 - i * 0.35, 0.11 * (4 - i)), (2.2, 0.35, 0.22 * (4 - i)), m), C("stone"))
    z = 0.9
    lan = []
    for t in range(tiers):
        r = 3.3 - 0.42 * t
        h = 2.7 - 0.18 * t
        f = eb.prism(ngon(0, 0, r, 8, rot), "XY", z, z + h, m)
        eb.paint(f, C("vermilion"))
        for k in range(8):  # door / window panels on each face
            a = rot + 2 * math.pi * (k + 0.5) / 8
            nrm = Vector((math.cos(a), math.sin(a), 0))
            apo = r * math.cos(math.pi / 8)
            c = nrm * (apo + 0.02)
            ww = r * 0.45
            hh = h * (0.62 if (k == 6 and t == 0) else 0.42)
            zc = z + (hh / 2 + 0.05 if (k == 6 and t == 0) else h * 0.55)
            q = Vector((0, 0, 1)).rotation_difference(nrm)
            fb = eb.box((c.x, c.y, zc), (ww, hh, 0.06), m, rot=q)
            eb.paint(fb, C("roof_under") if (k == 6 and t == 0) else C("cream"))
        for k in range(8):  # corner columns
            a = rot + 2 * math.pi * k / 8
            p = Vector((math.cos(a), math.sin(a), 0)) * (r + 0.05)
            eb.paint(eb.cyl((p.x, p.y, z), (p.x, p.y, z + h), 0.13, m, seg=8), C("deep_red"))
        top = t == tiers - 1
        r_next = 3.3 - 0.42 * (t + 1)
        apex = ngon(0, 0, 0.12 if top else r_next * 0.98, 8, rot)
        roof(eb, ngon(0, 0, r + 1.35, 8, rot), apex, z + h - 0.05, 1.7 if top else 0.75, 0.55, m, C("slate"),
             C("roof_under"), C("deep_red"), sub=6, rings=6 if not top else 8, thickness=0.14, center=(0, 0))
        if not top:
            pts = [(x, y, z + h + 0.62) for x, y in ngon(0, 0, r_next + 0.45, 8, rot)]
            railing(eb, pts, 0.55, m, C("vermilion"), post_every=0.9, closed=True, r=0.03)
        if t < 2:
            for k in range(8):
                a = rot + 2 * math.pi * k / 8
                p = Vector((math.cos(a), math.sin(a), 0)) * (r + 1.35 * 1.07)
                lan.append((p.x, p.y, z + h + 0.38))
        z += h + 0.75
    finish(eb, coll, root)
    for i, h in enumerate(lan):
        lantern(coll, f"{name}_Lantern_{i:02d}", h, parent=root, scale=1.0, phase=rng.uniform(0, 6.28), swing=9)
    return root


def lake_pavilion(coll, name="ENV_LakePavilion", seed=5):
    rng = random.Random(seed)
    root = empty(f"{name}_Root", coll, kind="ARROWS", size=0.6)
    root["component"] = name
    m = mat_vcol()
    eb = EnvBuilder(f"{name}_Mesh")
    P = lambda f, k: eb.paint(f, C(k))  # noqa: E731
    rot = math.pi / 6
    for x, y in ngon(0, 0, 2.6, 6, rot):
        P(eb.cyl((x, y, -0.8), (x, y, 0.5), 0.14, m, seg=8), "wood_dark")
    P(eb.prism(ngon(0, 0, 3.1, 6, rot), "XY", 0.45, 0.68, m), "stone")
    cols = ngon(0, 0, 2.6, 6, rot)
    for x, y in cols:
        P(eb.cyl((x, y, 0.68), (x, y, 3.25), 0.12, m, seg=8), "vermilion")
    # benches / railings except the entrance side (facing -Y)
    for i in range(6):
        a, b = cols[i], cols[(i + 1) % 6]
        mid = Vector(((a[0] + b[0]) / 2, (a[1] + b[1]) / 2))
        if mid.y < -2.0:
            continue
        railing(eb, [(a[0], a[1], 0.68), (b[0], b[1], 0.68)], 0.55, m, C("vermilion"), post_every=0.6, r=0.028)
    for i in range(6):  # carved lintel band under the roof
        a, b = Vector((*cols[i], 3.05)), Vector((*cols[(i + 1) % 6], 3.05))
        P(eb.cyl(tuple(a), tuple(b), 0.09, m, seg=6), "deep_red")
    roof(eb, ngon(0, 0, 3.95, 6, rot), ngon(0, 0, 0.12, 6, rot), 3.15, 1.75, 0.75, m, C("slate"), C("roof_under"),
         C("deep_red"), sub=8, rings=8, thickness=0.13)
    # zig-zag walkway to the shore (-Y)
    path = [(0, -3.0), (0, -5.4), (1.8, -7.2), (0, -9.4), (0, -11.5)]
    for (x0, y0), (x1, y1) in zip(path, path[1:]):
        a, b = Vector((x0, y0)), Vector((x1, y1))
        d = (b - a)
        L = d.length
        q = Vector((0, 1, 0)).rotation_difference(Vector((d.x, d.y, 0)).normalized())
        mid = (a + b) / 2
        P(eb.box((mid.x, mid.y, 0.55), (1.4, L + 0.7, 0.12), m, rot=q), "wood")
        side = Vector((-d.y, d.x)).normalized() * 0.65
        for s in (-1, 1):
            p0, p1 = a + side * s, b + side * s
            railing(eb, [(p0.x, p0.y, 0.61), (p1.x, p1.y, 0.61)], 0.5, m, C("vermilion"), post_every=1.0, r=0.025)
            for p in (p0, p1):
                P(eb.cyl((p.x, p.y, -0.8), (p.x, p.y, 0.5), 0.08, m, seg=6), "wood_dark")
    finish(eb, coll, root)
    for i, (x, y) in enumerate(ngon(0, 0, 3.95 * 1.07, 6, rot)):
        lantern(coll, f"{name}_Lantern_{i}", (x, y, 3.85), parent=root, scale=1.1, phase=rng.uniform(0, 6.28))
    return root


def paifang(coll, name="ENV_PaifangGate", seed=6):
    rng = random.Random(seed)
    root = empty(f"{name}_Root", coll, kind="ARROWS", size=0.8)
    root["component"] = name
    m = mat_vcol()
    eb = EnvBuilder(f"{name}_Mesh")
    P = lambda f, k: eb.paint(f, C(k))  # noqa: E731
    for x, hgt in ((-4.2, 6.2), (4.2, 6.2), (-7.0, 5.0), (7.0, 5.0)):
        P(eb.box((x, 0, 0.45), (1.0, 1.0, 0.9), m), "stone")
        P(eb.box((x, 0, 0.95), (0.8, 0.8, 0.12), m), "stone_dark")
        P(eb.box((x, 0, 1.0 + hgt / 2), (0.55, 0.55, hgt), m), "vermilion")
    P(eb.box((0, 0, 5.55), (9.4, 0.6, 0.55), m), "vermilion")
    P(eb.box((0, -0.31, 5.55), (9.0, 0.04, 0.22), m), "gold")
    P(eb.box((0, 0, 4.35), (8.6, 0.5, 0.35), m), "deep_red")
    P(eb.box((0, -0.26, 4.35), (8.2, 0.04, 0.12), m), "gold")
    P(eb.box((0, -0.05, 4.95), (2.3, 0.3, 0.95), m), "gold")       # plaque frame
    for x in (-5.6, 5.6):
        P(eb.box((x, 0, 4.55), (2.9, 0.5, 0.45), m), "vermilion")
        P(eb.box((x, -0.26, 4.55), (2.5, 0.04, 0.14), m), "gold")
        P(eb.box((x, 0, 3.75), (2.6, 0.4, 0.28), m), "deep_red")
    for x in [i * 0.9 - 4.05 for i in range(10)]:  # bracket blocks (dougong) under the main roof
        P(eb.box((x, 0, 6.0), (0.35, 0.9, 0.35), m), "gold" if int(x * 10) % 2 == 0 else "deep_red")
    roof(eb, rect(0, 0, 5.6, 1.3), rect(0, 0, 3.8, 0.06), 6.15, 1.15, 0.65, m, C("slate"), C("roof_under"),
         C("deep_red"), sub=12, rings=6, thickness=0.14)
    for x in (-5.6, 5.6):
        roof(eb, rect(x, 0, 1.75, 1.05), rect(x, 0, 0.95, 0.05), 4.85, 0.8, 0.45, m, C("slate"), C("roof_under"),
             C("deep_red"), sub=8, rings=5, thickness=0.12, center=(x, 0))
    finish(eb, coll, root)
    textured_quad(coll, f"{name}_Plaque", (0, -0.215, 4.95), 2.05, 0.78, "-y", "ENV_Plaque_BlossomVillage.png", root)
    for i, x in enumerate((-2.4, 2.4)):
        lantern(coll, f"{name}_Lantern_{i}", (x, -0.1, 4.15), parent=root, scale=1.7, phase=rng.uniform(0, 6.28))
    return root


def stone_bridge(coll, name="ENV_StoneBridge", length=18.0, width=8.4, chord=9.0, rise=3.0, seed=8):
    """Arched stone bridge; the road runs along +Y over a river flowing along X."""
    from env_roads import road_strip  # noqa: WPS433
    rng = random.Random(seed)
    root = empty(f"{name}_Root", coll, kind="ARROWS", size=0.6)
    root["component"] = name
    m = mat_vcol()
    eb = EnvBuilder(f"{name}_Mesh")
    R = (chord * chord / 4 + rise * rise) / (2 * rise)
    z0 = rise - R
    L2 = length / 2

    def top(y):
        return 0.25 + 3.75 * max(0.0, 1 - (y / L2) ** 2) ** 0.85

    def bot(y):
        return z0 + math.sqrt(R * R - y * y) if abs(y) < chord / 2 else 0.0
    N = 56
    ys = [-L2 + length * i / N for i in range(N + 1)]
    for c in (-chord / 2, chord / 2):
        if c not in ys:
            ys.append(c)
    ys.sort()
    W2 = width / 2
    stone, sd, sl = C("stone"), C("stone_dark"), hex_rgba("#CFC8BB")
    V = {s: [(eb.bm.verts.new((s * W2, y, top(y))), eb.bm.verts.new((s * W2, y, bot(y)))) for y in ys] for s in (-1, 1)}
    faces_side, faces_top, faces_bot = [], [], []
    for i in range(len(ys) - 1):
        for s in (-1, 1):
            t0, b0 = V[s][i]
            t1, b1 = V[s][i + 1]
            f = eb.bm.faces.new((t0, t1, b1, b0) if s > 0 else (b0, b1, t1, t0))
            faces_side.append((f, ys[i]))
        faces_top.append(eb.bm.faces.new((V[-1][i][0], V[1][i][0], V[1][i + 1][0], V[-1][i + 1][0])))
        faces_bot.append(eb.bm.faces.new((V[-1][i + 1][1], V[1][i + 1][1], V[1][i][1], V[-1][i][1])))
    ends = [eb.bm.faces.new((V[-1][0][1], V[1][0][1], V[1][0][0], V[-1][0][0])),
            eb.bm.faces.new((V[-1][-1][0], V[1][-1][0], V[1][-1][1], V[-1][-1][1]))]
    allf = [f for f, _ in faces_side] + faces_top + faces_bot + ends
    eb._setmat(allf, m)
    bmesh.ops.recalc_face_normals(eb.bm, faces=allf)
    for f, y in faces_side:
        near_arch = abs(y) < chord / 2 + 0.6
        eb.paint([f], lambda face, lp, y=y: sd if (near_arch and lp.vert.co.z < bot(y) + 0.55 and abs(y) < chord / 2 + 0.3)
                 else mix(stone, sl, ((int((y + 30) * 1.3) + int(lp.vert.co.z * 2.2)) % 3) / 3))
    eb.paint(faces_top, sd)
    eb.paint(faces_bot + ends, sd)
    # parapet posts + rails
    k = 0
    for s in (-1, 1):
        x = s * (W2 - 0.25)
        n = 12
        prev = None
        for i in range(n + 1):
            y = -L2 + 0.5 + (length - 1.0) * i / n
            z = top(y)
            eb.paint(eb.box((x, y, z + 0.5), (0.32, 0.32, 1.0), m), sl)
            eb.paint(eb.box((x, y, z + 1.06), (0.4, 0.4, 0.12), m), stone)
            if prev:
                eb.paint(eb.cyl((x, prev[0], prev[1] + 0.85), (x, y, z + 0.85), 0.07, m, seg=6), stone)
                eb.paint(eb.cyl((x, prev[0], prev[1] + 0.35), (x, y, z + 0.35), 0.05, m, seg=6), sd)
            prev = (y, z)
    finish(eb, coll, root)
    # asphalt carriageway following the deck
    pts = [(0.0, y, top(y) + 0.02) for y in [-L2 + length * i / 40 for i in range(41)]]
    road_strip(coll, f"{name}_Road", pts, 7.0, "asphalt", root, shoulders=0.0)
    return root
