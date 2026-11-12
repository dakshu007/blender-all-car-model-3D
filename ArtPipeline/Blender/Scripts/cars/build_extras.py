"""build_extras.py - bolt-on accessories for the off-road / SUV / modern cars (CAR-020..031).

Called by build_generic.py when a spec has an "acc" block. Every part is optional and
built from the same low-poly primitives as the rest of the car (boxes, 8-sided tubes), so
the cars stay clean and in the style of the original collection:

    roof_rack, roof_rails, roof_tent, light_bar, light_pod, snorkel, bull_bar, winch,
    spare (tailgate / bed / flat), bed (pickup liner + rails), bed_cage, jerrycans,
    steps (rock sliders), skid, arms (visible long-travel suspension), rear_engine (buggy),
    neon (EV light strips), diffuser, hood_scoop

Lamps on light bars / pods use M_Light_Aux but live in <CAR>_Accessories, not <CAR>_Lights,
so the game's lamp measurements (CarAnatomy) only see the real head / aux lamps.
"""

import math

import bmesh
from mathutils import Euler, Matrix, Vector

import vehicle_builder as vb
from vehicle_builder import MeshBuilder


def _lamp_row(mb, M, cx, y, z, w, n, r, face_y_sign=1, mat="aux"):
    """n round lamps facing +Y (or -Y) spread over width w, with chrome bezels."""
    for i in range(n):
        x = cx - w / 2 + w * (i + 0.5) / n
        mb.cyl((x, y, z), (x, y + face_y_sign * 0.012, z), r * 1.1, M["chrome"], seg=14)
        mb.cyl((x, y + face_y_sign * 0.006, z), (x, y + face_y_sign * 0.016, z), r * 0.9, M[mat], seg=14)


def roof_z(B):
    return B.S.gh["z1"] + 0.004


def build(B):
    """Returns the list of exterior accessory objects (already in the Exterior collection)."""
    S, M, d = B.S, B.M, B.d
    acc = d.get("acc") or {}
    if not acc:
        return []
    g = S.gh
    C = B.C
    mb = MeshBuilder(f"{C}_Accessories")
    objs = []
    z1 = roof_z(B)
    rack_top = None

    # ---------------------------------------------------------------- roof rack / rails
    rk = acc.get("roof_rack")
    if rk:
        mat = M[rk.get("mat", "trim")]
        h = rk.get("h", 0.07)
        ya, yb = rk.get("y0", g["yF1"] - 0.02), rk.get("y1", g["yR1"] + 0.04)
        x = g["w1"] - rk.get("inset", 0.02)
        z = z1 + h
        r = 0.016
        mb.tube_path([(-x, ya, z), (x, ya, z), (x, yb, z), (-x, yb, z), (-x, ya, z)], r, mat)
        n = rk.get("bars", 4)
        for i in range(1, n + 1):
            y = ya + (yb - ya) * i / (n + 1)
            mb.cyl((-x, y, z), (x, y, z), r * 0.8, mat, seg=6)
        for y in (ya + 0.05, (ya + yb) / 2, yb - 0.05):
            for sx in (-1, 1):
                mb.box((sx * (x - 0.01), y, z1 + h / 2), (0.04, 0.06, h), M["trim"])
        if rk.get("basket", True):
            zt = z + rk.get("rail", 0.07)
            mb.tube_path([(-x, ya, zt), (x, ya, zt), (x, yb, zt), (-x, yb, zt), (-x, ya, zt)], r * 0.8, mat)
            for t in range(5):
                y = ya + (yb - ya) * t / 4
                for sx in (-1, 1):
                    mb.cyl((sx * x, y, z), (sx * x, y, zt), r * 0.6, mat, seg=6)
        rack_top = z + r
        B.rack = {"x": x, "y0": ya, "y1": yb, "z": rack_top}
    rr = acc.get("roof_rails")
    if rr:
        x = g["w1"] - 0.07
        ya, yb = g["yF1"] - 0.08, g["yR1"] + 0.10
        z = z1 + 0.05
        for sx in (-1, 1):
            mb.tube_path([(sx * x, ya, z1), (sx * x, ya + 0.04, z), (sx * x, yb - 0.04, z), (sx * x, yb, z1)], 0.017,
                         M[rr.get("mat", "chrome")])

    # ---------------------------------------------------------------- roof tent
    tn = acc.get("roof_tent")
    if tn and rack_top is not None:
        rk = B.rack
        ya, yb = tn.get("y0", rk["y0"] - 0.02), tn.get("y1", (rk["y0"] + rk["y1"]) / 2 - 0.15)
        hgt = tn.get("h", 0.26)
        wx = rk["x"] - 0.04
        zc = rack_top + 0.012 + hgt / 2
        mb.box((0, (ya + yb) / 2, zc), (2 * wx, abs(ya - yb), hgt), M["tent"], top_scale=(0.97, 0.97))
        mb.box((0, (ya + yb) / 2, rack_top + 0.012 + 0.025), (2 * wx + 0.01, abs(ya - yb) + 0.01, 0.05), M["trim"])
        for t in (0.25, 0.75):
            y = ya + (yb - ya) * t
            mb.box((0, y, zc), (2 * wx + 0.012, 0.035, hgt + 0.012), M["accent"])

    # ---------------------------------------------------------------- jerrycans
    for jc in acc.get("jerrycans", []):
        x, y, z = jc["pos"]
        if jc.get("on") == "rack" and rack_top is not None:
            z = rack_top + 0.17 + 0.01
        rot = Euler((0, 0, math.radians(jc.get("yaw", 0)))).to_quaternion()
        mb.box((x, y, z), (0.16, 0.44, 0.34), M["can"], rot=rot, top_scale=(1.0, 0.94))
        mb.box((x, y, z), (0.168, 0.30, 0.20), M["can"], rot=rot)   # pressed rib
        hp = rot @ Vector((0, 0.12, 0.19))
        mb.box((x + hp.x, y + hp.y, z + hp.z), (0.05, 0.14, 0.03), M["trim"], rot=rot)
        sp = rot @ Vector((0, -0.16, 0.19))
        mb.cyl((x + sp.x, y + sp.y, z + 0.17), (x + sp.x, y + sp.y, z + 0.215), 0.02, M["trim"], seg=8)

    # ---------------------------------------------------------------- light bar (roof)
    lbar = acc.get("light_bar")
    if lbar:
        w = lbar.get("w", 2 * g["w1"] - 0.1)
        n = lbar.get("lamps", 6)
        if lbar.get("on") == "cage":
            y = g["yF1"] + 0.06
            z = z1 + 0.09
        else:
            y = lbar.get("y", g["yF1"] - 0.03)
            z = (rack_top + 0.07) if rack_top is not None and lbar.get("on", "rack") == "rack" else z1 + 0.10
            for sx in (-1, 1):
                mb.box((sx * (w / 2 - 0.08), y - 0.02, (z + z1) / 2), (0.035, 0.05, z - z1), M["trim"])
        mb.box((0, y, z), (w, 0.11, 0.115), M["trim"], top_scale=(1.0, 0.85))
        _lamp_row(mb, M, 0.0, y + 0.055, z, w - 0.06, n, min(0.045, (w - 0.06) / n * 0.42))

    # ---------------------------------------------------------------- bonnet light pod (snow rally)
    pod = acc.get("light_pod")
    if pod:
        y = pod.get("y", S.y_max - 0.30)
        zb = S.deck_z_at(y) + 0.03
        n, r = pod.get("n", 4), pod.get("r", 0.068)
        w = n * r * 2.6
        z = zb + r + 0.03
        mb.box((0, y - 0.04, z), (w + 0.04, 0.09, 2 * r + 0.05), M[pod.get("mat", "trim")], top_scale=(0.98, 0.7))
        for sx in (-1, 1):
            mb.box((sx * w * 0.32, y - 0.06, (zb + z) / 2 - 0.02), (0.04, 0.06, z - zb), M["trim"])
        _lamp_row(mb, M, 0.0, y + 0.005, z, w, n, r)

    # ---------------------------------------------------------------- snorkel
    sn = acc.get("snorkel")
    if sn:
        sx = sn.get("side", 1)
        y_a = g["yF0"] + sn.get("dy", 0.16)
        zb = S.zbelt_at(y_a)
        xw = S.body_half_width_at(y_a, zb - 0.08)
        r = sn.get("r", 0.046)
        top = (sx * (g["w1"] + 0.09), g["yF1"] + 0.03, g["z1"] - 0.02)
        pts = [(sx * (xw - 0.02), y_a, zb - 0.26), (sx * (xw + 0.045), y_a, zb - 0.20),
               (sx * (xw + 0.05), y_a - 0.01, zb + 0.06), top]
        mb.tube_path(pts, r, M[sn.get("mat", "trim")], seg=10)
        mb.box((top[0], top[1] + 0.03, top[2] + 0.07), (0.13, 0.17, 0.12), M[sn.get("mat", "trim")],
               top_scale=(0.9, 0.8))
        mb.box((top[0], top[1] + 0.12, top[2] + 0.07), (0.10, 0.012, 0.085), M["trim_grey"])
        # pillar brackets
        for t in (0.35, 0.75):
            p = Vector(pts[2]).lerp(Vector(top), t)
            mb.box((p.x - sx * 0.04, p.y, p.z), (0.07, 0.025, 0.025), M["trim"])

    # ---------------------------------------------------------------- bull bar / winch / skid
    bb = acc.get("bull_bar")
    ff = B.front_face
    if bb:
        mat = M[bb.get("mat", "trim")]
        y = ff["y"] + bb.get("dy", 0.09)
        z0, z1b = ff["z0"] - 0.02, ff["z1"] + bb.get("h", 0.30)
        xo = bb.get("x", 0.42)
        r = 0.03
        mb.tube_path([(-xo, y - 0.08, ff["z0"] + 0.02), (-xo, y, z0 + 0.08), (-xo, y, z1b - 0.08),
                      (-xo + 0.10, y + 0.01, z1b), (xo - 0.10, y + 0.01, z1b), (xo, y, z1b - 0.08),
                      (xo, y, z0 + 0.08), (xo, y - 0.08, ff["z0"] + 0.02)], r, mat)
        mb.cyl((-xo, y + 0.01, (z0 + z1b) / 2 + 0.06), (xo, y + 0.01, (z0 + z1b) / 2 + 0.06), r * 0.85, mat, seg=8)
        if bb.get("style", "full") == "full":
            xw = S.nose_half_width() - 0.04
            for sx in (-1, 1):
                mb.tube_path([(sx * xo, y, z1b - 0.12), (sx * (xw - 0.02), y - 0.05, z1b - 0.16),
                              (sx * xw, y - 0.10, ff["z0"] + 0.04)], r * 0.85, mat)
        # bumper plate
        mb.box((0, y - 0.04, (ff["z0"] + ff["z1"]) / 2), (2 * xo + 0.1, 0.06, ff["z1"] - ff["z0"] + 0.02), M["trim"])
    if acc.get("winch"):
        y = ff["y"] + (0.07 if bb else 0.05)
        z = (ff["z0"] + ff["z1"]) / 2
        mb.box((0, y, z), (0.42, 0.13, 0.13), M["trim_grey"])
        mb.cyl((-0.15, y + 0.02, z), (0.15, y + 0.02, z), 0.055, M["trim"], seg=12)
        mb.box((0, y + 0.08, z), (0.09, 0.03, 0.05), M["chrome"])
        mb.cyl((0, y + 0.09, z - 0.02), (0, y + 0.13, z - 0.07), 0.012, M["accent"], seg=6)
    if acc.get("skid"):
        yb = S.y_max - 0.10
        zf = S.lerp_keys(S.y_max - 0.3)[0]
        rot = Euler((math.radians(-14), 0, 0)).to_quaternion()
        mb.box((0, yb - 0.30, zf - 0.035), (min(1.1, 2 * S.nose_half_width() - 0.2), 0.70, 0.03), M["trim_grey"],
               rot=rot)

    # ---------------------------------------------------------------- rock sliders / side steps
    st = acc.get("steps")
    if st:
        y0 = S.axle_r + S.arch_r_r + 0.06
        y1 = S.axle_f - S.arch_r - 0.06
        ym = (y0 + y1) / 2
        zb = S.lerp_keys(ym)[0]
        for sx in (-1, 1):
            xw = S.body_half_width_at(ym, zb + 0.07) + st.get("out", 0.07)
            z = zb + 0.02
            mb.cyl((sx * xw, y0, z), (sx * xw, y1, z), 0.035, M[st.get("mat", "trim")], seg=8)
            mb.box((sx * (xw - 0.03), ym, z + 0.035), (0.10, y1 - y0, 0.02), M["trim_grey"])
            for t in (0.1, 0.5, 0.9):
                y = y0 + (y1 - y0) * t
                mb.cyl((sx * (xw - 0.09), y, z + 0.02), (sx * xw, y, z), 0.022, M["trim"], seg=6)

    # ---------------------------------------------------------------- pickup bed liner + rails
    bed = acc.get("bed")
    if bed:
        ya, yb = bed["y0"], bed["y1"]
        ym = (ya + yb) / 2
        zf = bed.get("floor", S.lerp_keys(ym)[0] + 0.25)
        ztop = S.zbelt_at(ym) + 0.01
        xw = S.lerp_keys(ym)[6] - 0.03
        liner = mb.box((0, ym, (zf + ztop) / 2), (2 * xw, abs(ya - yb) - 0.02, ztop - zf), M["trim_grey"])
        bmesh.ops.recalc_face_normals(mb.bm, faces=liner)
        bmesh.ops.reverse_faces(mb.bm, faces=liner)
        bmesh.ops.delete(mb.bm, geom=[f for f in liner if f.calc_center_median().z > ztop - 0.01], context="FACES")
        for sx in (-1, 1):
            mb.box((sx * (xw + 0.005), ym, ztop + 0.012), (0.07, abs(ya - yb), 0.03), M["trim"])
        mb.box((0, yb + 0.02, ztop + 0.012), (2 * xw + 0.07, 0.07, 0.03), M["trim"])
        B.bed = {"y0": ya, "y1": yb, "floor": zf, "top": ztop, "x": xw}
        cage = bed.get("cage")
        if cage:
            cg = M[cage.get("mat", "cage")]
            zh = cage.get("z", g["z1"] - 0.05)
            yh = ya - 0.12
            xc = xw - 0.05
            r = 0.03
            mb.tube_path([(-xc, yh, ztop), (-xc + 0.05, yh, zh), (xc - 0.05, yh, zh), (xc, yh, ztop)], r, cg)
            for sx in (-1, 1):
                mb.cyl((sx * (xc - 0.05), yh, zh), (sx * (xc - 0.02), yb + 0.08, ztop + 0.02), r * 0.9, cg, seg=8)
                mb.cyl((sx * (xc - 0.05), yh, zh), (sx * (xc - 0.06), g["yR1"] + 0.05, g["z1"]), r * 0.8, cg, seg=8)
            mb.cyl((-(xc - 0.05), yh, zh), (xc - 0.02, yb + 0.08, ztop + 0.02), r * 0.8, cg, seg=8)

    # ---------------------------------------------------------------- suspension arms (wide-track wheels)
    arms = acc.get("arms")
    if arms:
        for slot, (wx, wy) in S.wheels.items():
            sx = 1 if wx > 0 else -1
            wr, tw, _rim, _ar, wxa = S.wdims(slot)
            hub = wxa - tw / 2 - 0.03
            zb = S.lerp_keys(wy)[0]
            xb = S.body_half_width_at(wy, zb + 0.08) - 0.06
            for dz, dy in ((-0.06, 0.20), (0.10, 0.16)):
                for yy in (dy, -dy):
                    mb.cyl((sx * xb, wy + yy, wr + dz + 0.02), (sx * hub, wy, wr + dz * 0.6), 0.02, M[arms.get("mat", "trim")],
                           seg=6)
            # coil-over: spring (accent) + body
            top = (sx * (xb - 0.06), wy - 0.05, S.zbelt_at(wy) + arms.get("shock_top", 0.05))
            bot = (sx * (hub - 0.08), wy - 0.02, wr + 0.06)
            mb.cyl(bot, top, 0.034, M["accent"], seg=8)
            mb.cyl(Vector(bot).lerp(Vector(top), 0.55), top, 0.024, M["chrome"], seg=8)
            mb.box((sx * (hub - 0.01), wy, wr), (0.06, 0.10, 0.14), M["trim_grey"])

    # ---------------------------------------------------------------- exposed rear engine (buggy)
    re_ = acc.get("rear_engine")
    if re_:
        y = re_.get("y", S.y_min + 0.55)
        zd = S.deck_z_at(y)
        mb.box((0, y, zd + 0.12), (0.46, 0.42, 0.24), M["trim_grey"])
        for sx in (-1, 1):
            mb.box((sx * 0.20, y, zd + 0.26), (0.10, 0.36, 0.10), M["chrome"], rot=Euler((0, math.radians(sx * 25), 0)).to_quaternion())
        mb.cyl((0, y + 0.05, zd + 0.27), (0, y + 0.05, zd + 0.37), 0.11, M["accent"], seg=14)
        for sx in (-1, 1):
            mb.tube_path([(sx * 0.12, y - 0.18, zd + 0.10), (sx * 0.20, y - 0.36, zd + 0.12),
                          (sx * 0.24, y - 0.50, zd + 0.30)], 0.03, M["chrome"])

    # ---------------------------------------------------------------- neon strips (EV)
    ne = acc.get("neon")
    if ne:
        if ne.get("sill"):
            y0 = S.axle_r + S.arch_r_r + 0.06
            y1 = S.axle_f - S.arch_r - 0.06
            ym = (y0 + y1) / 2
            zb = S.lerp_keys(ym)[0]
            for sx in (-1, 1):
                xw = S.body_half_width_at(ym, zb + 0.06)
                mb.box((sx * (xw + 0.006), ym, zb + 0.045), (0.012, y1 - y0, 0.018), M["neon"])
        if ne.get("roof"):
            for sx in (-1, 1):
                x = g["w1"] - 0.01
                mb.box((sx * x, (g["yF1"] + g["yR1"]) / 2, g["z1"] - 0.012), (0.012, g["yF1"] - g["yR1"], 0.012),
                       M["neon"])
    if acc.get("diffuser"):
        rf = B.rear_face
        z = rf["z0"] - 0.03
        w = 2 * S.tail_half_width() - 0.3
        mb.box((0, rf["y"] - 0.06, z), (w, 0.30, 0.03), M["trim"], rot=Euler((math.radians(-10), 0, 0)).to_quaternion())
        for i in range(5):
            x = -w / 2 + w * (i + 0.5) / 5
            mb.box((x, rf["y"] - 0.08, z - 0.05), (0.012, 0.24, 0.09), M["trim"])
    hs = acc.get("hood_scoop")
    if hs:
        y = hs.get("y", (S.y_max + g["yF0"]) / 2)
        zd = S.deck_z_at(y)
        mb.box((0, y, zd + 0.03), (hs.get("w", 0.5), hs.get("l", 0.55), 0.09), M[hs.get("mat", "paint")],
               top_scale=(0.85, 0.75))
        mb.box((0, y + hs.get("l", 0.55) * 0.36, zd + 0.045), (hs.get("w", 0.5) * 0.7, 0.012, 0.05), M["trim"])

    if mb.bm.faces:
        o = mb.finish(B.cols["Exterior"], recalc=False)
        vb.add_bevel(o, width=0.004, segments=1, angle=35)
        objs.append(vb.apply_modifiers(o))
    else:
        mb.bm.free()

    sp = acc.get("spare")
    if sp:
        objs.append(spare_wheel(B, sp))
    return objs


def spare_wheel(B, sp):
    """Spare wheel: 'tailgate' (face to the rear), 'bed' (upright across the bed) or 'flat' (lying in the bed)."""
    import bpy
    S, M = B.S, B.M
    C = B.C
    mount = sp.get("mount", "tailgate")
    wr, tw, _rim, _ar, _x = S.wdims("RL")
    parts = []
    for kind, fn in (("Tire", B.tire_mesh), ("Rim", B.rim_mesh)):
        mbw = fn(f"{C}_Spare_{kind}", "RL")
        o = mbw.finish(B.cols["Exterior"], recalc=False)
        parts.append(o)
    if mount == "tailgate":
        R = Matrix.Rotation(math.radians(-90), 4, "Z")       # rim face (+X) -> -Y
        z = sp.get("z", S.zbelt_at(S.y_min) + 0.05)
        y = B.y_face(z, False) - tw / 2 - sp.get("gap", 0.06)
        loc = Vector((sp.get("x", 0.0), y, z))
    elif mount == "bed":
        R = Matrix.Rotation(math.radians(-90), 4, "Z")       # upright, rim face to the rear
        bed = getattr(B, "bed", None)
        y = (bed["y0"] - tw / 2 - 0.14) if bed else S.y_min + 0.6
        loc = Vector((0, y, (bed["floor"] if bed else 0.8) + wr + 0.01))
    else:  # flat in the bed / on a deck
        R = Matrix.Rotation(math.radians(-90), 4, "Y")       # rim face (+X) -> +Z
        bed = getattr(B, "bed", None)
        yy = sp.get("y", (bed["y0"] + bed["y1"]) / 2 if bed else S.y_min + 0.7)
        zz = sp.get("z", (bed["floor"] if bed else S.deck_z_at(yy)) + tw / 2 + 0.01)
        loc = Vector((0, yy, zz))
    T = Matrix.Translation(loc) @ R
    for o in parts:
        o.data.transform(T)
        o.data.uv_layers.new(name="UVMap")
    # carrier bracket
    br = MeshBuilder(f"{C}_Spare_Bracket")
    if mount == "tailgate":
        br.box((loc.x, loc.y + tw / 2 + 0.03, loc.z), (0.16, 0.08, 0.16), M["trim"])
        br.cyl((loc.x, loc.y + tw / 2 + 0.06, loc.z), (loc.x, loc.y - tw / 2 + 0.02, loc.z), 0.03, M["chrome"], seg=8)
    else:
        br.box((loc.x, loc.y, loc.z), (0.08, 0.08, 0.08), M["trim"])
    bo = br.finish(B.cols["Exterior"], recalc=False)
    bo.data.uv_layers.new(name="UVMap")
    parts.append(bo)
    for o in parts:
        vb.smooth_by_angle(o, 40)
    obj = vb.join(parts, f"{C}_SpareWheel")
    return obj
