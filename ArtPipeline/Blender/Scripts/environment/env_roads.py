"""env_roads.py - road kit (asphalt / gravel / dirt / lantern-street paving) and water."""

import math
import os
import random

import bpy
import bmesh
from mathutils import Vector

from env_common import (PAL, EnvBuilder, blob, empty, hex_rgba, mat_texture, mat_vcol, mat_water, mix)

TEX_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "EnvironmentLibrary", "Textures"))
SURF = {"asphalt": ("ENV_Road_Asphalt.png", 5.0), "gravel": ("ENV_Road_Gravel.png", 5.0),
        "dirt": ("ENV_Road_Dirt.png", 5.0), "paving": ("ENV_Road_Paving.png", 3.0)}


def C(k):
    return hex_rgba(PAL[k])


def _frames(pts):
    pts = [Vector(p) for p in pts]
    out = []
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)])
        d.z = 0
        d.normalize()
        out.append((p, Vector((d.y, -d.x, 0))))  # right-hand normal
    return out


def road_strip(coll, name, pts, width, kind, parent=None, shoulders=1.0, lines=None):
    """Sweep a road surface along a centreline (list of (x, y, z)). U across, V along (metres / tile)."""
    tex, tile = SURF[kind]
    fr = _frames(pts)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rows = []
    dist = 0.0
    for i, (p, n) in enumerate(fr):
        if i:
            dist += (p - fr[i - 1][0]).length
        crown = 0.04 if kind == "asphalt" else 0.02
        row = [bm.verts.new(p + n * (width * (u - 0.5)) + Vector((0, 0, crown * (1 - abs(2 * u - 1)))))
               for u in (0.0, 0.5, 1.0)]
        rows.append((row, dist))
    for (r0, d0), (r1, d1) in zip(rows, rows[1:]):
        for j in range(2):
            f = bm.faces.new((r0[j + 1], r0[j], r1[j], r1[j + 1]))
            us = (0.5 * (j + 1), 0.5 * j, 0.5 * j, 0.5 * (j + 1))
            vs = (d0 / tile, d0 / tile, d1 / tile, d1 / tile)
            for lp, uu, vv in zip(f.loops, us, vs):
                lp[uv].uv = (uu, vv)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat_texture(f"M_Env_Road_{kind.title()}", os.path.join(TEX_DIR, tex)))
    o = bpy.data.objects.new(name, me)
    coll.objects.link(o)
    if parent:
        o.parent = parent
    lines = (kind == "asphalt") if lines is None else lines
    eb = EnvBuilder(f"{name}_Edges")
    m = mat_vcol()
    W2 = width / 2
    if shoulders > 0:
        col = hex_rgba("#A99A80") if kind == "asphalt" else hex_rgba("#8DBF55")
        for s in (-1, 1):
            for (p0, n0), (p1, n1) in zip(fr, fr[1:]):
                a0, a1 = p0 + n0 * s * W2, p1 + n1 * s * W2
                b0 = p0 + n0 * s * (W2 + shoulders) - Vector((0, 0, 0.08))
                b1 = p1 + n1 * s * (W2 + shoulders) - Vector((0, 0, 0.08))
                vs = [eb.bm.verts.new(v) for v in ((a0, a1, b1, b0) if s > 0 else (b0, b1, a1, a0))]
                f = eb.bm.faces.new(vs)
                eb._setmat([f], m)
                eb.paint([f], col)
    if lines:
        white = C("white")
        acc = 0.0
        for (p0, n0), (p1, n1) in zip(fr, fr[1:]):
            seg_len = (p1 - p0).length
            for off, wdt, dashed in ((-(W2 - 0.35), 0.15, False), ((W2 - 0.35), 0.15, False), (0.0, 0.14, True)):
                mid_d = acc + seg_len / 2
                if dashed and (mid_d % 6.0) > 3.0:
                    continue
                q = [p0 + n0 * (off - wdt / 2), p0 + n0 * (off + wdt / 2), p1 + n1 * (off + wdt / 2),
                     p1 + n1 * (off - wdt / 2)]
                crown = 0.04 * (1 - abs(off) / W2)
                vs = [eb.bm.verts.new(v + Vector((0, 0, 0.012 + crown))) for v in q]
                f = eb.bm.faces.new(list(reversed(vs)))
                eb._setmat([f], m)
                eb.paint([f], white)
            acc += seg_len
    if eb.bm.faces:
        e = eb.finish_vcol(coll, recalc=False)
        for f in e.data.polygons:
            if f.normal.z < 0:
                f.flip()
        e.parent = parent
    else:
        eb.bm.free()
    return o


def _root(coll, name):
    r = empty(f"{name}_Root", coll, kind="ARROWS", size=0.6)
    r["component"] = name
    return r


def road_asphalt_straight(coll, name="ENV_Road_AsphaltStraight", length=20.0):
    r = _root(coll, name)
    road_strip(coll, f"{name}_Surface", [(0, -length / 2 + length * i / 20, 0) for i in range(21)], 7.0, "asphalt", r)
    return r


def road_asphalt_curve(coll, name="ENV_Road_AsphaltCurve90", radius=25.0, degrees=90):
    r = _root(coll, name)
    n = 36
    pts = [(radius + radius * math.cos(math.pi - math.radians(degrees) * i / n),
            radius * math.sin(math.pi - math.radians(degrees) * i / n), 0) for i in range(n + 1)]
    road_strip(coll, f"{name}_Surface", pts, 7.0, "asphalt", r)
    return r


def road_hairpin(coll, name="ENV_Road_Hairpin", radius=9.0):
    r = _root(coll, name)
    n = 40
    pts = [(0, -10 + 10 * i / 8, 0) for i in range(8)]
    pts += [(radius - radius * math.cos(math.pi * i / n), radius * math.sin(math.pi * i / n), 0.0) for i in range(n + 1)]
    pts += [(2 * radius, -10 * i / 8, 0) for i in range(1, 9)]
    road_strip(coll, f"{name}_Surface", pts, 7.0, "asphalt", r)
    return r


def road_gravel(coll, name="ENV_Road_Gravel", length=20.0):
    r = _root(coll, name)
    pts = [(0.6 * math.sin(i / 20 * math.pi * 2), -length / 2 + length * i / 20, 0) for i in range(21)]
    road_strip(coll, f"{name}_Surface", pts, 5.0, "gravel", r, shoulders=0.8)
    return r


def road_dirt(coll, name="ENV_Road_Dirt", length=20.0):
    r = _root(coll, name)
    pts = [(1.0 * math.sin(i / 20 * math.pi * 2), -length / 2 + length * i / 20, 0) for i in range(21)]
    road_strip(coll, f"{name}_Surface", pts, 4.2, "dirt", r, shoulders=0.8)
    return r


def road_lantern_street(coll, name="ENV_Road_LanternStreet", length=20.0):
    from env_architecture import lantern_post  # noqa: WPS433
    r = _root(coll, name)
    pts = [(0, -length / 2 + length * i / 20, 0) for i in range(21)]
    road_strip(coll, f"{name}_Surface", pts, 6.0, "paving", r, shoulders=0.0)
    eb = EnvBuilder(f"{name}_Curbs")
    m = mat_vcol()
    for s in (-1, 1):
        eb.paint(eb.box((s * 3.15, 0, 0.08), (0.3, length, 0.16), m), C("stone_dark"))
    e = eb.finish_vcol(coll)
    e.parent = r
    rng = random.Random(9)
    for i, y in enumerate([-7.5, -2.5, 2.5, 7.5]):
        for s in (-1, 1):
            lp = lantern_post(coll, f"{name}_Post_{i}{'L' if s < 0 else 'R'}", phase=rng.uniform(0, 6.28))
            lp.parent = r
            lp.location = (s * 3.6, y, 0)
            lp.rotation_euler = (0, 0, math.pi if s > 0 else 0)
    return r


# ------------------------------------------------------------------- water ---
def river(coll, name="ENV_Water_River", length=24.0, width=10.0):
    r = _root(coll, name)
    eb = EnvBuilder(f"{name}_Banks")
    m = mat_vcol()
    n = 24
    sand, grass, bed = hex_rgba("#D8C8A0"), hex_rgba("#8DBF55"), hex_rgba("#3E6A72")
    for s in (-1, 1):
        for i in range(n):
            y0, y1 = -length / 2 + length * i / n, -length / 2 + length * (i + 1) / n
            x0 = 1.2 * math.sin(y0 / length * math.pi * 2)
            x1 = 1.2 * math.sin(y1 / length * math.pi * 2)
            rows = [(width / 2 - 0.5, -0.9), (width / 2, -0.25), (width / 2 + 1.5, 0.05), (width / 2 + 3.5, 0.0)]
            cols = [bed, sand, grass]
            for (wa, za), (wb, zb), col in zip(rows, rows[1:], cols):
                q = [(x0 + s * wa, y0, za), (x1 + s * wa, y1, za), (x1 + s * wb, y1, zb), (x0 + s * wb, y0, zb)]
                vs = [eb.bm.verts.new(p) for p in (q if s > 0 else list(reversed(q)))]
                f = eb.bm.faces.new(vs)
                eb._setmat([f], m)
                eb.paint([f], col)
        # river bed
    for i in range(n):
        y0, y1 = -length / 2 + length * i / n, -length / 2 + length * (i + 1) / n
        x0 = 1.2 * math.sin(y0 / length * math.pi * 2)
        x1 = 1.2 * math.sin(y1 / length * math.pi * 2)
        vs = [eb.bm.verts.new(p) for p in ((x0 - width / 2 + 0.5, y0, -0.9), (x0 + width / 2 - 0.5, y0, -0.9),
                                            (x1 + width / 2 - 0.5, y1, -0.9), (x1 - width / 2 + 0.5, y1, -0.9))]
        f = eb.bm.faces.new(vs)
        eb._setmat([f], m)
        eb.paint([f], bed)
    banks = eb.finish_vcol(coll, recalc=False)
    banks.parent = r
    wb = EnvBuilder(f"{name}_Surface")
    wm = mat_water()
    for i in range(n):
        y0, y1 = -length / 2 + length * i / n, -length / 2 + length * (i + 1) / n
        x0 = 1.2 * math.sin(y0 / length * math.pi * 2)
        x1 = 1.2 * math.sin(y1 / length * math.pi * 2)
        vs = [wb.bm.verts.new(p) for p in ((x0 - width / 2 - 0.2, y0, -0.28), (x0 + width / 2 + 0.2, y0, -0.28),
                                            (x1 + width / 2 + 0.2, y1, -0.28), (x1 - width / 2 - 0.2, y1, -0.28))]
        f = wb.bm.faces.new(vs)
        wb._setmat([f], wm)
        wb.paint([f], C("water"))
    w = wb.finish_vcol(coll, recalc=False)
    w.data.materials.clear()
    w.data.materials.append(wm)
    w.parent = r
    return r


def lake(coll, name="ENV_Water_Lake", rx=15.0, ry=10.0, seed=12):
    rng = random.Random(seed)
    r = _root(coll, name)
    m = mat_vcol()
    eb = EnvBuilder(f"{name}_Shore")
    n = 48
    sand, grass, bed = hex_rgba("#D8C8A0"), hex_rgba("#8DBF55"), hex_rgba("#3E6A72")

    def ring(k, z):
        out = []
        for i in range(n):
            a = 2 * math.pi * i / n
            wob = 1 + 0.06 * math.sin(3 * a) + 0.04 * math.sin(5 * a + 1)
            out.append(eb.bm.verts.new((rx * k * wob * math.cos(a), ry * k * wob * math.sin(a), z)))
        return out
    rings = [ring(0.0001, -1.0), ring(0.85, -0.9), ring(1.0, -0.25), ring(1.1, 0.05), ring(1.25, 0.0)]
    cols = [bed, bed, sand, grass]
    for (ra, rb), col in zip(zip(rings, rings[1:]), cols):
        for i in range(n):
            f = eb.bm.faces.new((ra[i], ra[(i + 1) % n], rb[(i + 1) % n], rb[i]))
            eb._setmat([f], m)
            eb.paint([f], col)
    for i in range(16):  # shore rocks
        a = rng.uniform(0, 2 * math.pi)
        k = rng.uniform(1.0, 1.12)
        c = (rx * k * math.cos(a), ry * k * math.sin(a), 0.0)
        rr = rng.uniform(0.3, 0.8)
        g1, g2 = hex_rgba("#A39E95"), hex_rgba("#7E7A73")
        blob(eb, c, rr, (1.3, 1.0, 0.7), m, rng, lambda f, lp: mix(g2, g1, max(0, min(1, lp.vert.co.z / rr + 0.5))),
             subdiv=1, noise=0.25)
    for i in range(14):  # lotus pads + flowers
        a = rng.uniform(0, 2 * math.pi)
        k = rng.uniform(0.35, 0.8)
        x, y = rx * k * math.cos(a), ry * k * math.sin(a)
        pr = rng.uniform(0.35, 0.55)
        f = eb.cyl((x, y, -0.27), (x, y, -0.24), pr, m, seg=12)
        eb.paint(f, hex_rgba("#5E9E45"))
        if rng.random() < 0.45:
            for p in range(6):
                pa = 2 * math.pi * p / 6
                fc = eb.cyl((x, y, -0.22), (x + 0.12 * math.cos(pa), y + 0.12 * math.sin(pa), -0.05), 0.05, m, seg=5,
                            r2=0.01)
                eb.paint(fc, hex_rgba("#F4A7B9"))
    shore = eb.finish_vcol(coll, recalc=True)
    shore.parent = r
    wb = EnvBuilder(f"{name}_Surface")
    wm = mat_water()
    cverts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        wob = 1 + 0.06 * math.sin(3 * a) + 0.04 * math.sin(5 * a + 1)
        cverts.append(wb.bm.verts.new((rx * 1.03 * wob * math.cos(a), ry * 1.03 * wob * math.sin(a), -0.28)))
    f = wb.bm.faces.new(cverts)
    wb._setmat([f], wm)
    wb.paint([f], C("water"))
    w = wb.finish_vcol(coll, recalc=False)
    w.data.materials.clear()
    w.data.materials.append(wm)
    w.parent = r
    return r
