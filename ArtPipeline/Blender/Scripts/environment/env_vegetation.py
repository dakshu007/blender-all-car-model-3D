"""env_vegetation.py - cherry blossom trees, bamboo, grass, tea terraces (animated)."""

import math
import random

import bpy  # noqa: F401
import bmesh
from mathutils import Vector


from env_common import (PAL, EnvBuilder, smooth_by_angle, blob, empty, falling_petals, hex_rgba, mat_vcol, mix, sway, tube,
                        wind_shape_key)


# ------------------------------------------------------------ cherry tree ---
def _blossom_color(rng, cz, r):
    dark, mid, light, hi = (hex_rgba(PAL["pink_dark"]), hex_rgba(PAL["pink"]), hex_rgba(PAL["pink_light"]),
                            hex_rgba(PAL["blossom_hi"]))
    seed = rng.random() * 50

    def fn(face, loop):
        p = loop.vert.co
        t = max(0.0, min(1.0, (p.z - cz) / (r * 1.3) + 0.45))
        col = mix(dark, mid, min(1, t * 1.7)) if t < 0.55 else mix(mid, light, (t - 0.55) / 0.45)
        h = math.sin(p.x * 13.1 + seed) * math.sin(p.y * 11.7 + seed * 0.3) * math.sin(p.z * 9.3 + seed * 0.7)
        if t > 0.45 and h > 0.22:
            col = mix(col, hi, 0.85)
        elif h < -0.55:
            col = mix(col, dark, 0.35)
        return col
    return fn


def _bark(rng):
    a, b = hex_rgba("#7A5A4E"), hex_rgba("#5A4239")

    def fn(face, loop):
        return mix(a, b, 0.35 + 0.35 * math.sin(face.index * 1.7))
    return fn


def cherry_tree(coll, name, height=6.0, spread=3.2, seed=1, limbs=6, petals=24, trunk_ratio=0.42, lean=0.15):
    """Stylised cherry blossom tree. Hierarchy: Root > Trunk, Limb_## pivots (sway) > limb mesh."""
    rng = random.Random(seed)
    root = empty(f"{name}_Root", coll, kind="ARROWS", size=0.6)
    root["component"] = name
    th = height * trunk_ratio
    tr = 0.09 * height ** 0.85
    # trunk with root flare and a gentle S lean
    la = rng.uniform(0, 2 * math.pi)
    ld = Vector((math.cos(la), math.sin(la), 0)) * lean * th
    pts = [(0, 0, -0.1), (0, 0, 0.05), (0, 0, 0.35)] + \
          [tuple(Vector((0, 0, th * t)) + ld * math.sin(t * math.pi * 0.9)) for t in (0.35, 0.6, 0.85, 1.0)]
    radii = [tr * 1.9, tr * 1.55, tr * 1.15, tr, tr * 0.9, tr * 0.8, tr * 0.72]
    eb = EnvBuilder(f"{name}_Trunk")
    m = mat_vcol()
    tube(eb, pts, radii, m, seg=9, col=_bark(rng))
    # root buttresses
    for k in range(5):
        a = 2 * math.pi * k / 5 + rng.uniform(-0.3, 0.3)
        d = Vector((math.cos(a), math.sin(a), 0))
        tube(eb, [(0, 0, 0.45), tuple(d * tr * 1.4 + Vector((0, 0, 0.12))), tuple(d * tr * 2.6 + Vector((0, 0, -0.05)))],
             [tr * 0.55, tr * 0.4, tr * 0.12], m, seg=6, col=_bark(rng))
    trunk = eb.finish_vcol(coll, recalc=False, wind=lambda co: max(0.0, co.z / height) * 0.3)
    trunk.parent = root
    top = Vector(pts[-1])

    limb_objs = []
    for i in range(limbs + 1):
        crown = i == limbs  # the last limb is the upright crown leader
        if crown:
            base = top
            az = la
            reach = spread * 0.2
            rise = (height - th) * 0.55
        else:
            az = 2 * math.pi * i / limbs + rng.uniform(-0.35, 0.35)
            base = Vector((0, 0, th * rng.uniform(0.62, 0.98))) + ld * 0.8
            reach = spread * rng.uniform(0.8, 1.1)
            rise = (height - th) * rng.uniform(0.25, 0.6)
        d = Vector((math.cos(az), math.sin(az), 0))
        side = Vector((-d.y, d.x, 0))
        pv = empty(f"{name}_Limb_{i:02d}", coll, loc=tuple(base), parent=root, size=0.2)
        lb = EnvBuilder(f"{name}_LimbMesh_{i:02d}")
        # gnarled limb in pivot-local space
        k1 = rng.uniform(-0.25, 0.25)
        lp = [Vector((0, 0, 0)), d * reach * 0.3 + Vector((0, 0, rise * 0.45)) + side * k1,
              d * reach * 0.65 + Vector((0, 0, rise * 0.8)) - side * k1 * 0.6, d * reach + Vector((0, 0, rise))]
        lr = [tr * 0.62, tr * 0.45, tr * 0.3, tr * 0.12]
        tube(lb, [tuple(p) for p in lp], lr, m, seg=7, col=_bark(rng))
        tips = [lp[-1], lp[2]]
        for t in range(rng.randint(2, 3)):  # twigs
            s = lp[1].lerp(lp[2], rng.uniform(0.2, 1.0))
            ta = az + rng.uniform(-1.1, 1.1)
            td = Vector((math.cos(ta), math.sin(ta), 0))
            tip = s + td * reach * rng.uniform(0.3, 0.5) + Vector((0, 0, rise * rng.uniform(0.2, 0.5)))
            tube(lb, [tuple(s), tuple(s.lerp(tip, 0.5) + Vector((0, 0, 0.15))), tuple(tip)],
                 [tr * 0.22, tr * 0.14, tr * 0.05], m, seg=5, col=_bark(rng))
            tips.append(tip)
        # blossom: many soft flattened clumps along the outer limb + drooping skirt + specks
        cr = 0.12 * height ** 0.9 * (0.95 if crown else 1.0)
        anchors = list(tips) + [lp[1].lerp(lp[2], 0.6), lp[2].lerp(lp[3], 0.5)]
        hi = hex_rgba(PAL["blossom_hi"])
        lite = hex_rgba(PAL["pink_light"])
        for tp in anchors:
            for c in range(rng.randint(4, 6)):
                off = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.25, 0.55))) * cr * 1.3
                cc = tp + off
                rr = cr * rng.uniform(0.7, 1.2)
                blob(lb, tuple(cc), rr, (1.25, 1.25, 0.66), m, rng, _blossom_color(rng, cc.z, rr), subdiv=2,
                     noise=0.2)
                for sp in range(3):  # little blossom specks catching the light
                    a = rng.uniform(0, 2 * math.pi)
                    q = cc + Vector((math.cos(a) * rr * 0.8, math.sin(a) * rr * 0.8, rr * 0.45))
                    f = lb.sphere(tuple(q), rr * 0.16, m, seg=6)
                    lb.paint(f, hi if rng.random() < 0.6 else lite)
            if not crown and rng.random() < 0.7:  # drooping cluster under the limb
                cc = tp + Vector((0, 0, -cr * 1.0))
                rr = cr * 0.8
                blob(lb, tuple(cc), rr, (1.0, 1.0, 0.85), m, rng, _blossom_color(rng, cc.z, rr), subdiv=2,
                     noise=0.2)
        lo = lb.finish_vcol(coll, recalc=True, wind=lambda co: min(1.0, 0.25 + co.length / (spread + 1e-3)))
        lo.parent = pv
        smooth_by_angle(lo, 50)
        sway(pv, amp_deg=(1.6 if not crown else 1.0, 1.6, 0.6), phase=rng.uniform(0, 6.28))
        limb_objs.append((pv, lo))
    if petals:
        falling_petals(coll, root, (0, 0), spread * 1.1, height, petals, rng, name)
    return root


# ---------------------------------------------------------------- bamboo ---
def bamboo_cluster(coll, name="ENV_Bamboo", stalks=16, radius=1.3, seed=7, height=(6.5, 9.5)):
    rng = random.Random(seed)
    root = empty(f"{name}_Root", coll, kind="ARROWS", size=0.6)
    root["component"] = name
    m = mat_vcol()
    g, gd, gl = hex_rgba(PAL["bamboo"]), hex_rgba(PAL["bamboo_dark"]), hex_rgba(PAL["grass_light"])
    leaf_a, leaf_b = hex_rgba("#6FA83C"), hex_rgba("#9CCB55")
    for i in range(stalks):
        a = rng.uniform(0, 2 * math.pi)
        r = radius * math.sqrt(rng.random())
        base = (r * math.cos(a), r * math.sin(a), 0)
        h = rng.uniform(*height)
        rad = rng.uniform(0.065, 0.095)
        pv = empty(f"{name}_Stalk_{i:02d}", coll, loc=base, parent=root, size=0.15)
        eb = EnvBuilder(f"{name}_StalkMesh_{i:02d}")
        lean = Vector((rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12), 1)).normalized()
        z = 0.0
        while z < h:
            seg = rng.uniform(0.35, 0.55)
            p0, p1 = lean * z, lean * min(h, z + seg)
            f = eb.cyl(tuple(p0), tuple(p1), rad * (1 - 0.35 * z / h), m, seg=8)
            eb.paint(f, mix(g, gd, rng.uniform(0, 0.4)))
            f = eb.cyl(tuple(p1 - lean * 0.02), tuple(p1 + lean * 0.02), rad * (1 - 0.35 * z / h) * 1.18, m, seg=8)
            eb.paint(f, gd)
            z += seg
        for k in range(rng.randint(9, 13)):  # leaf sprays on the upper stalk
            zz = h * rng.uniform(0.45, 0.98)
            c = lean * zz
            for j in range(rng.randint(5, 8)):
                la = rng.uniform(0, 2 * math.pi)
                d = Vector((math.cos(la), math.sin(la), rng.uniform(-0.5, 0.1))).normalized()
                L = rng.uniform(0.5, 0.85)
                w = Vector((-d.y, d.x, 0)).normalized() * 0.075
                p0, p2 = c, c + d * L + Vector((0, 0, -0.08))
                p1 = c + d * L * 0.45
                vs = [eb.bm.verts.new(v) for v in (p0, p1 + w, p2, p1 - w)]
                f1 = eb.bm.faces.new(vs)
                f2 = eb.bm.faces.new([eb.bm.verts.new(v.co.copy()) for v in reversed(vs)])
                eb._setmat([f1, f2], m)
                eb.paint([f1, f2], mix(leaf_a, leaf_b, rng.random()))
        o = eb.finish_vcol(coll, recalc=False, wind=lambda co, h=h: min(1.0, max(0.0, co.z) / h) ** 1.5)
        o.parent = pv
        sway(pv, amp_deg=(1.4, 1.4, 0.0), phase=rng.uniform(0, 6.28))
    # ground litter / shoots
    eb = EnvBuilder(f"{name}_Base")
    for k in range(8):
        a = rng.uniform(0, 2 * math.pi)
        r = radius * rng.uniform(0.2, 1.2)
        f = eb.cyl((r * math.cos(a), r * math.sin(a), 0), (r * math.cos(a), r * math.sin(a), rng.uniform(0.2, 0.5)),
                   0.04, m, seg=6, r2=0.005)
        eb.paint(f, gd)
    base = eb.finish_vcol(coll, recalc=True, wind=lambda co: 0.0)
    base.parent = root
    return root


# ----------------------------------------------------------------- grass ---
def _blade(eb, base, h, w, bend, az, col0, col1, m, segs=4):
    d = Vector((math.cos(az), math.sin(az), 0))
    side = Vector((-d.y, d.x, 0))
    left, right = [], []
    for i in range(segs + 1):
        t = i / segs
        c = Vector(base) + Vector((0, 0, h * t)) + d * bend * t * t
        ww = w * (1 - t) ** 0.8 if i < segs else 0.0
        left.append(eb.bm.verts.new(c - side * ww))
        right.append(eb.bm.verts.new(c + side * ww) if i < segs else left[-1])
    faces = []
    for i in range(segs):
        if i < segs - 1:
            faces.append(eb.bm.faces.new((left[i], right[i], right[i + 1], left[i + 1])))
        else:
            faces.append(eb.bm.faces.new((left[i], right[i], left[i + 1])))
    back = []
    for f in faces:  # double-sided
        back.append(eb.bm.faces.new([eb.bm.verts.new(v.co.copy()) for v in reversed(f.verts)]))
    eb._setmat(faces + back, m)

    def col(face, loop):
        t = max(0.0, min(1.0, (loop.vert.co.z - base[2]) / h))
        return mix(col0, col1, t)
    eb.paint(faces + back, col)
    return faces + back


def grass_clump(eb, center, rng, scale=1.0, blades=16, m=None):
    m = m or mat_vcol()
    shades = [("#5FAE3A", "#8ACB4F"), ("#76BE45", "#A6DA62"), ("#4E9F33", "#7CC04B"), ("#8CCB52", "#B9E27A")]
    for i in range(blades):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0, 0.12) * scale
        base = (center[0] + r * math.cos(a), center[1] + r * math.sin(a), center[2])
        h = rng.uniform(0.3, 0.62) * scale
        c0, c1 = shades[rng.randrange(len(shades))]
        _blade(eb, base, h, rng.uniform(0.045, 0.075) * scale, rng.uniform(0.04, 0.22) * scale * (1 + h),
               a + rng.uniform(-0.5, 0.5), hex_rgba(c0), hex_rgba(c1), m)


def flower(eb, center, rng, color, m):
    h = rng.uniform(0.18, 0.4)
    st = eb.cyl(center, (center[0], center[1], center[2] + h), 0.006, m, seg=4)
    eb.paint(st, hex_rgba(PAL["grass_dark"]))
    c = Vector((center[0], center[1], center[2] + h))
    for k in range(5):
        a = 2 * math.pi * k / 5
        p = c + Vector((math.cos(a), math.sin(a), 0)) * 0.025
        f = eb.sphere(tuple(p), 0.02, m, seg=5)
        eb.paint(f, hex_rgba(color))
    f = eb.sphere(tuple(c + Vector((0, 0, 0.006))), 0.015, m, seg=5)
    eb.paint(f, hex_rgba("#F2C230"))


def grass_clump_component(coll, name="ENV_GrassClump", seed=3, phase=0.0):
    rng = random.Random(seed)
    root = empty(f"{name}_Root", coll, kind="ARROWS", size=0.3)
    root["component"] = name
    eb = EnvBuilder(f"{name}_Mesh")
    grass_clump(eb, (0, 0, 0), rng, scale=1.0, blades=22)
    o = eb.finish_vcol(coll, recalc=False, wind=lambda co: min(1.0, max(0.0, co.z) / 0.75) ** 1.5)
    o.parent = root
    wind_shape_key(o, strength=0.12, height=0.75, phase=phase)
    return root


def grass_patch(coll, name="ENV_GrassPatch", size=4.0, seed=5, phase=0.0, clumps=70, flowers=22):
    rng = random.Random(seed)
    root = empty(f"{name}_Root", coll, kind="ARROWS", size=0.4)
    root["component"] = name
    m = mat_vcol()
    eb = EnvBuilder(f"{name}_Mesh")
    for i in range(clumps):
        c = (rng.uniform(-size / 2, size / 2), rng.uniform(-size / 2, size / 2), 0)
        grass_clump(eb, c, rng, scale=rng.uniform(0.65, 1.15), blades=rng.randint(10, 16), m=m)
    for i in range(flowers):
        c = (rng.uniform(-size / 2, size / 2), rng.uniform(-size / 2, size / 2), 0)
        flower(eb, c, rng, rng.choice(["#FFFFFF", "#F7C6D3", "#FFF3B0", "#E8A0BF"]), m)
    o = eb.finish_vcol(coll, recalc=False, wind=lambda co: min(1.0, max(0.0, co.z) / 0.8) ** 1.5)
    o.parent = root
    wind_shape_key(o, strength=0.13, height=0.8, phase=phase)
    return root


# ------------------------------------------------------------ tea terraces ---
def tea_terrace(coll, name="ENV_TeaTerrace", width=16.0, steps=4, step_depth=2.6, step_h=0.7, seed=11):
    rng = random.Random(seed)
    root = empty(f"{name}_Root", coll, kind="ARROWS", size=0.6)
    root["component"] = name
    m = mat_vcol()
    eb = EnvBuilder(f"{name}_Ground")
    soil, soil_d = hex_rgba(PAL["soil"]), hex_rgba("#7E5F3F")
    gr, gr2 = hex_rgba("#8CC85A"), hex_rgba("#76B44A")
    n = 24
    for s in range(steps):
        y0, y1 = s * step_depth, (s + 1) * step_depth
        z = s * step_h
        bend = lambda x, y: 0.9 * math.sin(x / width * math.pi * 1.4 + y * 0.15)  # noqa: E731
        top, riser_bot = [], []
        for i in range(n + 1):
            x = -width / 2 + width * i / n
            top.append((x, y0 + bend(x, y0), z + step_h, x, y1 + bend(x, y1)))
        for i in range(n):
            xa, ya0, za, _, ya1 = top[i]
            xb, yb0, zb, _, yb1 = top[i + 1]
            vs = [eb.bm.verts.new(p) for p in ((xa, ya0, za), (xb, yb0, za), (xb, yb1, za), (xa, ya1, za))]
            f = eb.bm.faces.new(vs)
            eb._setmat([f], m)
            eb.paint([f], gr if (i % 2) else gr2)
            vs = [eb.bm.verts.new(p) for p in ((xa, ya0, za - step_h - 0.02), (xb, yb0, za - step_h - 0.02),
                                                (xb, yb0, za), (xa, ya0, za))]
            f = eb.bm.faces.new(vs)
            eb._setmat([f], m)
            eb.paint([f], lambda face, lp: mix(soil_d, soil, max(0, min(1, (lp.vert.co.z - (za - step_h)) / step_h))))
    ground = eb.finish_vcol(coll, recalc=True, wind=lambda co: 0.0)
    ground.parent = root
    bb = EnvBuilder(f"{name}_Bushes")
    t1, t2 = hex_rgba("#4E9A3C"), hex_rgba("#7DBA52")
    for s in range(steps):
        for row in (0.35, 0.72):
            yb = s * step_depth + step_depth * row
            z = s * step_h + step_h
            x = -width / 2 + 0.6
            while x < width / 2 - 0.5:
                y = yb + 0.9 * math.sin(x / width * math.pi * 1.4 + yb * 0.15)
                rr = rng.uniform(0.38, 0.5)

                def col(face, loop, z=z):
                    face.normal_update()
                    return t2 if face.normal.z > 0.55 and (face.index % 3 == 0) else mix(t1, t2, 0.3 * ((face.index * 7) % 5) / 4)
                blob(bb, (x, y, z + rr * 0.55), rr, (1.25, 1.0, 0.8), m, rng, col, subdiv=1, noise=0.12)
                x += rng.uniform(0.75, 0.95)
    bushes = bb.finish_vcol(coll, recalc=True, wind=lambda co: 0.4)
    bushes.parent = root
    wind_shape_key(bushes, strength=0.04, height=steps * step_h + 1.0, phase=0.7)
    return root
