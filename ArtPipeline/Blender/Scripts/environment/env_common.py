"""env_common.py - shared helpers for the Chinese spring-valley environment kit.

Vertex-coloured meshes (one shared material per object type, Unity friendly),
the upturned-eave roof generator, and seamless wind/sway animation helpers.
Animation loop: frames 1..97 (frame 97 == frame 1), 24 fps, 4 s.
"""

import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))

import bpy  # noqa: E402
import bmesh  # noqa: E402
from bpy_extras import anim_utils  # noqa: E402
from mathutils import Euler, Matrix, Vector  # noqa: E402

import vehicle_builder as vb  # noqa: E402

LOOP = 96          # frames per loop
FPS = 24

PAL = {
    "vermilion": "#C8372D", "deep_red": "#8E2A22", "roof_red": "#B5342A", "roof_under": "#5E1F1A",
    "slate": "#3E4148", "slate_light": "#565B64", "gold": "#D9A441", "cream": "#F1E6CF",
    "lattice": "#3B2A24", "wood": "#6B4A33", "wood_dark": "#4A3224", "stone": "#B9B2A6",
    "stone_dark": "#8F897F", "plaster": "#EFE6D6", "pink_dark": "#EB8AA3", "pink": "#F7B6C7",
    "pink_light": "#FCD9E2", "blossom_hi": "#FFF1E6", "bark": "#5A4038", "bark_dark": "#3E2B26",
    "grass_dark": "#4E9A2F", "grass": "#7CC04B", "grass_light": "#A8D86A", "bamboo": "#7FA845",
    "bamboo_dark": "#5C8233", "tea": "#3F7D35", "tea_light": "#5E9E45", "soil": "#9B7A55",
    "lantern": "#D8342A", "water": "#5FA8C8", "asphalt": "#4A4C52", "white": "#F2EFE7",
}


def hex_rgba(h, a=1.0):
    """sRGB hex -> linear RGBA (vertex colours are stored linear in Blender)."""
    c = vb.hex_lin(h)
    return (c[0], c[1], c[2], a)


def mix(c1, c2, t):
    return tuple(a + (b - a) * t for a, b in zip(c1, c2))


# --------------------------------------------------------------- materials ---
_MATS = {}


def mat_vcol(name="M_Env_VertexColor", rough=0.75, emission=0.0, sheen=0.3):
    if name in _MATS and _MATS[name].name in bpy.data.materials:
        return _MATS[name]
    m = bpy.data.materials.new(name)
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    vc = nt.nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "Col"
    vc.location = (-400, 200)
    nt.links.new(vc.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Specular IOR Level"].default_value = sheen
    if emission:
        nt.links.new(vc.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = emission
    _MATS[name] = m
    return m


def mat_glow():
    return mat_vcol("M_Env_LanternGlow", rough=0.4, emission=2.5)


def mat_water(name="M_Env_Water"):
    """Stylised water. Preview animation: the noise mapping is keyframed (Unity: water shader)."""
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    m = bpy.data.materials.new(name)
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 0.35
    nz.inputs["Detail"].default_value = 3.0
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = hex_rgba("#3F8FB8")
    ramp.color_ramp.elements[1].color = hex_rgba("#9FD6E8")
    ramp.color_ramp.elements[0].position = 0.45
    ramp.color_ramp.elements[1].position = 0.75
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.25
    nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
    nt.links.new(mp.outputs["Vector"], nz.inputs["Vector"])
    nt.links.new(nz.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(nz.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = 0.08
    bsdf.inputs["Specular IOR Level"].default_value = 0.6
    # seamless drift: noise is 4D in W, animate W over one loop (wraps visually by drift only)
    nz.noise_dimensions = "4D"
    nz.inputs["W"].default_value = 0.0
    nz.inputs["W"].keyframe_insert("default_value", frame=1)
    nz.inputs["W"].default_value = 1.6
    nz.inputs["W"].keyframe_insert("default_value", frame=LOOP + 1)
    mp.inputs["Location"].default_value = (0, 0, 0)
    mp.inputs["Location"].keyframe_insert("default_value", frame=1)
    mp.inputs["Location"].default_value = (0.0, 6.0, 0.0)
    mp.inputs["Location"].keyframe_insert("default_value", frame=LOOP + 1)
    set_linear(nt)
    return m


def mat_texture(name, image_path, rough=0.85, scale=1.0):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    img = bpy.data.images.load(os.path.abspath(image_path), check_existing=True)
    return vb.material(name, "#FFFFFF", rough=rough, image=img, specular=0.3)


# ----------------------------------------------------------------- builder ---
class EnvBuilder(vb.MeshBuilder):
    """MeshBuilder + a 'Col' vertex-colour layer. Alpha = wind weight (0 base .. 1 tips)."""

    def __init__(self, name):
        super().__init__(name)
        self.col = self.bm.loops.layers.color.new("Col")

    def paint(self, faces, color, wind=None):
        """color: RGBA tuple or callable(face, loop) -> RGBA. wind: None or callable(co) -> 0..1."""
        for f in faces:
            for lp in f.loops:
                c = color(f, lp) if callable(color) else color
                a = wind(lp.vert.co) if wind else (c[3] if len(c) > 3 else 1.0)
                lp[self.col] = (c[0], c[1], c[2], a)
        return faces

    def finish_vcol(self, collection, mats=None, recalc=True, wind=None):
        if wind is not None:  # fill wind weights for unpainted alpha
            for f in self.bm.faces:
                for lp in f.loops:
                    c = lp[self.col]
                    w = wind(lp.vert.co)
                    w = w.real if isinstance(w, complex) else w
                    lp[self.col] = (c[0], c[1], c[2], max(0.0, min(1.0, w)))
        o = self.finish(collection, recalc=recalc)
        if not o.data.materials:
            o.data.materials.append(mat_vcol())
        return o


def faces_of(verts):
    return list({f for v in verts for f in v.link_faces})


def tube(eb, pts, radii, mat, seg=7, col=None, cap_end=True, twist=0.0):
    """Swept tube along a polyline with per-point radius. Returns faces."""
    pts = [Vector(p) for p in pts]
    rings = []
    prev_n = None
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        if prev_n is None:
            up = Vector((0, 0, 1)) if abs(d.z) < 0.95 else Vector((1, 0, 0))
            n = d.cross(up).normalized()
        else:
            n = (prev_n - d * prev_n.dot(d)).normalized()
        prev_n = n
        b = d.cross(n).normalized()
        ring = []
        for k in range(seg):
            a = 2 * math.pi * k / seg + twist * i
            ring.append(eb.bm.verts.new(p + (n * math.cos(a) + b * math.sin(a)) * radii[i]))
        rings.append(ring)
    faces = []
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(seg):
            faces.append(eb.bm.faces.new((r0[k], r0[(k + 1) % seg], r1[(k + 1) % seg], r1[k])))
    faces.append(eb.bm.faces.new(list(reversed(rings[0]))))
    if cap_end:
        faces.append(eb.bm.faces.new(rings[-1]))
    eb._setmat(faces, mat)
    if col is not None:
        eb.paint(faces, col)
    return faces


def blob(eb, center, radius, scale, mat, rng, col_fn, subdiv=2, noise=0.18):
    """Lumpy icosphere 'cloud' for canopies / bushes."""
    verts = bmesh.ops.create_icosphere(eb.bm, subdivisions=subdiv, radius=radius,
                                       matrix=Matrix.Translation(Vector(center)))["verts"]
    c = Vector(center)
    seed = rng.random() * 100
    for v in verts:
        d = v.co - c
        n = d.normalized()
        k = 1 + noise * (math.sin(n.x * 5.1 + seed) * math.sin(n.y * 4.3 + seed * 0.7) * math.sin(n.z * 3.7 + seed * 1.3)
                         + 0.4 * rng.uniform(-1, 1))
        v.co = c + Vector((d.x * scale[0], d.y * scale[1], d.z * scale[2])) * k
    faces = faces_of(verts)
    eb._setmat(faces, mat)
    eb.paint(faces, col_fn)
    return faces


# --------------------------------------------------------------- roofs ---
def roof(eb, corners, apex, z_eave, height, lift, mats, col_top, col_under, col_fascia,
         sub=10, rings=7, thickness=0.12, tiles=True, center=(0, 0)):
    """Chinese sweeping roof over a convex polygon.

    corners: eave polygon (2D, CCW). apex: top polygon with the same corner count
    (a short ridge for hip roofs, a tiny polygon for pyramid roofs). Concave sweep
    (steep at the top, flat at the eave) and upturned corners."""
    n = len(corners)
    cx, cy = center
    top_rings, bot_rings = [], []
    for k in range(rings + 1):
        t = k / rings
        drop = height * (2 * t - t * t)
        ring_top, ring_bot = [], []
        for i in range(n):
            a0 = Vector(apex[i]).lerp(Vector(corners[i]), t)
            a1 = Vector(apex[(i + 1) % n]).lerp(Vector(corners[(i + 1) % n]), t)
            for s in range(sub):
                u = s / sub
                p = a0.lerp(a1, u)
                cprox = max(0.0, 1.0 - min(u, 1 - u) / 0.32) ** 2 if s else 1.0
                up = lift * cprox * t ** 2.4
                out = 1 + 0.07 * cprox * t ** 2
                q = Vector((cx + (p.x - cx) * out, cy + (p.y - cy) * out))
                z = z_eave + height - drop + up
                ring_top.append(eb.bm.verts.new((q.x, q.y, z)))
                inset = 0.985 if k == rings else 1.0
                ring_bot.append(eb.bm.verts.new((cx + (q.x - cx) * inset, cy + (q.y - cy) * inset,
                                                 z - thickness * (0.6 + 0.4 * t))))
        top_rings.append(ring_top)
        bot_rings.append(ring_bot)
    P = n * sub
    top_f, bot_f, fas_f = [], [], []
    for k in range(rings):
        for j in range(P):
            j2 = (j + 1) % P
            top_f.append(eb.bm.faces.new((top_rings[k][j], top_rings[k][j2], top_rings[k + 1][j2], top_rings[k + 1][j])))
            bot_f.append(eb.bm.faces.new((bot_rings[k + 1][j], bot_rings[k + 1][j2], bot_rings[k][j2], bot_rings[k][j])))
    for j in range(P):
        j2 = (j + 1) % P
        fas_f.append(eb.bm.faces.new((top_rings[-1][j], top_rings[-1][j2], bot_rings[-1][j2], bot_rings[-1][j])))
    top_f.append(eb.bm.faces.new(list(reversed(top_rings[0]))))
    bot_f.append(eb.bm.faces.new(bot_rings[0]))
    eb._setmat(top_f + bot_f + fas_f, mats)
    if tiles:
        light = mix(col_top, (1, 1, 1, 1), 0.12)
        for idx, f in enumerate(top_f[:-1]):
            j = idx % P
            eb.paint([f], light if j % 2 else col_top)
        eb.paint([top_f[-1]], col_top)
    else:
        eb.paint(top_f, col_top)
    eb.paint(bot_f, col_under)
    eb.paint(fas_f, col_fascia)
    # ridge + upturned ridge ends
    zr = z_eave + height
    if len(apex) == 4 and (Vector(apex[0]) - Vector(apex[1])).length > 0.05:
        a = Vector(apex[0]).lerp(Vector(apex[3]), 0.5)
        b = Vector(apex[1]).lerp(Vector(apex[2]), 0.5)
        L = (b - a).length
        mid = (a + b) / 2
        rf = eb.box((mid.x, mid.y, zr + 0.06), (L + 0.1, 0.16, 0.16), mats)
        eb.paint(rf, col_fascia)
        for e, sgn in ((a, -1), (b, 1)):
            d = (b - a).normalized() * sgn
            tip = Vector((e.x, e.y, zr + 0.06)) + Vector((d.x, d.y, 0)) * 0.25 + Vector((0, 0, 0.32))
            ff = tube(eb, [(e.x, e.y, zr + 0.06), (e.x + d.x * 0.18, e.y + d.y * 0.18, zr + 0.14), tip],
                      [0.09, 0.07, 0.03], mats, seg=6)
            eb.paint(ff, col_fascia)
    else:  # pyramid: finial
        ff = tube(eb, [(cx, cy, zr - 0.05), (cx, cy, zr + 0.25), (cx, cy, zr + 0.55), (cx, cy, zr + 0.85)],
                  [0.12, 0.09, 0.07, 0.015], mats, seg=8)
        eb.paint(ff, hex_rgba(PAL["gold"]))
        sb = eb.sphere((cx, cy, zr + 0.45), 0.13, mats, seg=10)
        eb.paint(sb, hex_rgba(PAL["gold"]))
    return top_f


def rect(cx, cy, hx, hy):
    return [(cx - hx, cy - hy), (cx + hx, cy - hy), (cx + hx, cy + hy), (cx - hx, cy + hy)]


def ngon(cx, cy, r, n, rot=0.0):
    return [(cx + r * math.cos(rot + 2 * math.pi * i / n), cy + r * math.sin(rot + 2 * math.pi * i / n))
            for i in range(n)]


def railing(eb, pts, height, mat, col, post_every=0.9, closed=False, r=0.035):
    pts = [Vector(p) for p in pts] + ([Vector(pts[0])] if closed else [])
    faces = []
    for a, b in zip(pts, pts[1:]):
        L = (b - a).length
        n = max(1, int(L / post_every))
        for i in range(n + 1):
            p = a.lerp(b, i / n)
            faces += eb.box((p.x, p.y, p.z + height / 2), (0.07, 0.07, height), mat)
        for zz in (height * 0.95, height * 0.45):
            faces += eb.cyl((a.x, a.y, a.z + zz), (b.x, b.y, b.z + zz), r, mat, seg=6)
    eb.paint(faces, col)
    return faces


def lattice_window(eb, center, w, h, normal_axis, mat, frame_col, panel_col, bars=4):
    """Cream panel + dark lattice, facing along +/-X or +/-Y (normal_axis 'x','-x','y','-y')."""
    c = Vector(center)
    sgn = -1 if normal_axis.startswith("-") else 1
    ax = normal_axis[-1]
    faces = []

    def B(off, size_u, size_v, depth, colr):
        if ax == "y":
            f = eb.box((c.x + off[0], c.y + sgn * depth / 2 + sgn * off[2], c.z + off[1]), (size_u, depth, size_v), mat)
        else:
            f = eb.box((c.x + sgn * depth / 2 + sgn * off[2], c.y + off[0], c.z + off[1]), (depth, size_u, size_v), mat)
        eb.paint(f, colr)
        return f
    faces += B((0, 0, 0), w, h, 0.03, panel_col)
    t = 0.05
    faces += B((0, h / 2, 0.01), w + t, t, 0.06, frame_col)
    faces += B((0, -h / 2, 0.01), w + t, t, 0.06, frame_col)
    faces += B((w / 2, 0, 0.01), t, h, 0.06, frame_col)
    faces += B((-w / 2, 0, 0.01), t, h, 0.06, frame_col)
    for i in range(1, bars):
        faces += B((-w / 2 + w * i / bars, 0, 0.02), 0.022, h, 0.04, frame_col)
        faces += B((0, -h / 2 + h * i / bars, 0.02), w, 0.022, 0.04, frame_col)
    return faces


# --------------------------------------------------------------- animation ---
def _fcurves(id_data):
    ad = id_data.animation_data
    cb = anim_utils.action_get_channelbag_for_slot(ad.action, ad.action_slot)
    return cb.fcurves


def make_cyclic(id_data):
    for fc in _fcurves(id_data):
        if not any(m.type == "CYCLES" for m in fc.modifiers):
            fc.modifiers.new("CYCLES")
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
            kp.handle_left_type = kp.handle_right_type = "AUTO_CLAMPED"


def sway(obj, amp_deg=(2.0, 2.0, 0.0), phase=0.0, harmonics=(1, 2), step=4):
    """Seamless wind sway on rotation (loop = LOOP frames). Integer harmonics keep it seamless."""
    base = obj.rotation_euler.copy()
    for f in range(0, LOOP + 1, step):
        t = 2 * math.pi * f / LOOP
        w = [math.sin(h * t + phase * h + i * 1.7) / (1 + 0.6 * k) for k, h in enumerate(harmonics) for i in (0,)]
        s1 = sum(w)
        s2 = sum(math.cos(h * t + phase * 1.3 * h) / (1 + 0.6 * k) for k, h in enumerate(harmonics))
        obj.rotation_euler = (base.x + math.radians(amp_deg[0]) * s1 * 0.7,
                              base.y + math.radians(amp_deg[1]) * s2 * 0.7,
                              base.z + math.radians(amp_deg[2]) * s1 * 0.5)
        obj.keyframe_insert("rotation_euler", frame=f + 1)
    obj.rotation_euler = base
    make_cyclic(obj)


def wind_shape_key(obj, direction=(1.0, 0.3, 0.0), strength=0.25, height=1.0, phase=0.0, step=4):
    """Adds a 'Wind' blend shape (tips bend along direction) and animates it on the loop."""
    me = obj.data
    if me.shape_keys is None:
        obj.shape_key_add(name="Basis", from_mix=False)
    k = obj.shape_key_add(name="Wind", from_mix=False)
    d = Vector(direction).normalized()
    for i, v in enumerate(me.vertices):
        h = max(0.0, v.co.z) / height
        w = h ** 1.7
        k.data[i].co = v.co + d * strength * w - Vector((0, 0, 0.25 * strength * w * w))
    k.slider_min = -1.0
    k.slider_max = 1.0
    for f in range(0, LOOP + 1, step):
        t = 2 * math.pi * f / LOOP
        k.value = 0.55 * math.sin(t + phase) + 0.25 * math.sin(3 * t + phase * 2.1) + 0.25
        k.keyframe_insert("value", frame=f + 1)
    make_cyclic(me.shape_keys)
    return k


def petal_mesh():
    name = "ENV_PetalMesh"
    if name in bpy.data.meshes:
        return bpy.data.meshes[name]
    eb = EnvBuilder(name)
    vs = [eb.bm.verts.new(p) for p in ((0, -0.03, 0), (0.025, 0, 0.004), (0, 0.035, 0), (-0.025, 0, 0.004))]
    f1 = eb.bm.faces.new(vs)
    f2 = eb.bm.faces.new(list(reversed([eb.bm.verts.new(v.co.copy()) for v in vs])))
    eb._setmat([f1, f2], mat_vcol())
    eb.paint([f1, f2], hex_rgba(PAL["pink_light"]))
    me = bpy.data.meshes.new(name)
    eb.bm.to_mesh(me)
    eb.bm.free()
    me.materials.append(mat_vcol())
    return me


def falling_petals(collection, parent, origin, radius, top_z, count, rng, name_prefix, scale=1.6):
    """Keyframed petals drifting from the canopy to the ground on a seamless loop."""
    me = petal_mesh()
    objs = []
    for i in range(count):
        o = bpy.data.objects.new(f"{name_prefix}_Petal_{i:02d}", me)
        collection.objects.link(o)
        o.parent = parent
        o.scale = (scale, scale, scale)
        a = rng.uniform(0, 2 * math.pi)
        r = radius * math.sqrt(rng.random())
        x0, y0 = origin[0] + r * math.cos(a), origin[1] + r * math.sin(a)
        z0 = top_z * rng.uniform(0.55, 0.95)
        drift = Vector((rng.uniform(0.6, 1.6), rng.uniform(-0.6, 0.6), 0))
        off = rng.randint(0, LOOP - 1)
        rs = (rng.randint(1, 3), rng.randint(1, 3), rng.randint(1, 2))
        fw = (LOOP - off) % LOOP          # frame where this petal restarts at the top
        frames = sorted({f for f in range(0, LOOP + 1, 6)} | {fw, max(0, fw - 1)})
        for f in frames:
            u = ((f + off) % LOOP) / LOOP
            if f == fw - 1:
                u = (LOOP - 1) / LOOP
            o.location = (x0 + drift.x * u + 0.15 * math.sin(u * 12 + i),
                          y0 + drift.y * u + 0.15 * math.cos(u * 10 + i),
                          max(0.02, z0 * (1 - u)))
            o.rotation_euler = (rs[0] * u * 2 * math.pi, rs[1] * u * 2 * math.pi, rs[2] * u * 2 * math.pi)
            o.keyframe_insert("location", frame=f + 1)
            o.keyframe_insert("rotation_euler", frame=f + 1)
        for fc in _fcurves(o):
            for kp in fc.keyframe_points:
                # snap back to the canopy at the wrap instead of flying up
                kp.interpolation = "CONSTANT" if int(round(kp.co.x)) == fw else "LINEAR"
            fc.modifiers.new("CYCLES")
        objs.append(o)
    return objs


def set_linear(nt):
    if nt.animation_data and nt.animation_data.action:
        for fc in _fcurves(nt):
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"
            if not any(m.type == "CYCLES" for m in fc.modifiers):
                fc.modifiers.new("CYCLES")


def new_collection(name, parent=None):
    c = bpy.data.collections.new(name)
    (parent or bpy.context.scene.collection).children.link(c)
    return c


def empty(name, collection, loc=(0, 0, 0), parent=None, kind="PLAIN_AXES", size=0.3):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = kind
    e.empty_display_size = size
    e.location = loc
    collection.objects.link(e)
    if parent:
        e.parent = parent
    return e
smooth_by_angle = vb.smooth_by_angle
