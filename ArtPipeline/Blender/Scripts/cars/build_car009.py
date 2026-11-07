"""build_car009.py - build the original CAR-009 "Brenner S1" stylised rally car.

Usage:
    blender -b -P build_car009.py -- --texture ../../../VehicleLibrary/CAR-009/Textures/CAR-009_BaseColor.png \
        --out ../../../VehicleLibrary/CAR-009/Working/CAR-009_Working_v001.blend \
        --report ../../Reports/CAR-009_build.json [--overwrite]

(or `python build_car009.py -- ...` with the bpy module). Paint the livery first
with livery_car009.py. Never overwrites an existing .blend unless --overwrite is
given; otherwise the next free _vNNN is used.

Hierarchy produced (objects; matching collections in the outliner):
    CAR-009_Root
      CAR-009_Body            shell + glasshouse + box flares (livery texture)
      CAR-009_Exterior        bumpers, spoiler, mirrors, trim, mud flaps
      CAR-009_Glass
      CAR-009_Interior        tub, dash, seats, roll cage (+ CAR-009_SteeringWheel)
      CAR-009_Lights          head/tail/indicator/aux lamps
      CAR-009_Wheels
        CAR-009_Wheel_FL      empty at axle centre (rotate THIS in Unity)
          CAR-009_Wheel_FL_Tire, CAR-009_Wheel_FL_Rim
        ... FR, RL, RR
    CAR-009_Collision (collection, not exported in the visual FBX)
"""

import argparse
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))

import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Euler, Matrix, Vector  # noqa: E402

import car009_spec as S  # noqa: E402
import pipeline_common as pc  # noqa: E402
import vehicle_builder as vb  # noqa: E402
from vehicle_builder import MeshBuilder  # noqa: E402

C = S.CAR_ID


def parse():
    p = argparse.ArgumentParser()
    p.add_argument("--texture", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--report", required=True)
    p.add_argument("--overwrite", action="store_true", help="Allow replacing --out (scratch iterations only)")
    return p.parse_args(pc.script_args())


# ------------------------------------------------------------------ setup ---
def collections():
    root = bpy.data.collections.new(C)
    bpy.context.scene.collection.children.link(root)
    subs = {}
    for n in ("Body", "Exterior", "Glass", "Interior", "Lights", "Wheels"):
        c = bpy.data.collections.new(f"{C}_{n}")
        root.children.link(c)
        subs[n] = c
    col = bpy.data.collections.new(f"{C}_Collision")
    bpy.context.scene.collection.children.link(col)
    subs["Collision"] = col
    subs["Root"] = root
    return subs


def materials(tex_path):
    img = bpy.data.images.load(os.path.abspath(tex_path), check_existing=True)
    img.colorspace_settings.name = "sRGB"
    M = {
        "paint": vb.material(f"M_{C}_Paint", S.PAINT, rough=0.42, image=img),
        "trim": vb.material("M_Trim_Black", "#1E1D1F", rough=0.55),
        "trim_grey": vb.material("M_Trim_Grey", "#4A4744", rough=0.5),
        "chrome": vb.material("M_Chrome", "#D4D2CE", rough=0.22, metal=1.0),
        "glass": vb.material("M_Glass_Tint", "#0B1013", rough=0.05, alpha=0.86, specular=0.6),
        "tire": vb.material("M_Tire_Rubber", "#232120", rough=0.92, specular=0.25),
        "rim": vb.material(f"M_{C}_Rim", S.RIM, rough=0.36, metal=0.85),
        "disc": vb.material("M_Brake_Disc", "#6A6764", rough=0.45, metal=0.8),
        "head": vb.material("M_Light_Head", "#F3EFE3", rough=0.15, emission="#FFF2D2", emission_strength=1.5),
        "aux": vb.material("M_Light_Aux", "#F6EBC4", rough=0.15, emission="#FFE7A0", emission_strength=2.0),
        "tail": vb.material("M_Light_Tail", "#A3171B", rough=0.2, emission="#FF2A1A", emission_strength=0.8),
        "ind": vb.material("M_Light_Indicator", "#E07A1A", rough=0.2, emission="#FF8A1A", emission_strength=0.6),
        "interior": vb.material("M_Interior_Dark", "#2E2C2D", rough=0.85),
        "seat": vb.material(f"M_{C}_Seat", S.SEAT, rough=0.8),
        "cage": vb.material(f"M_{C}_Cage", S.CAGE, rough=0.5),
        "accent": vb.material(f"M_{C}_Accent", S.FLAP, rough=0.75),
        "plate": None,  # plates use the paint atlas (see project_uvs)
    }
    # Plates: same texture, separate material so UVs can be mapped to the plate rects.
    M["plate"] = vb.material(f"M_{C}_Plate", "#F4F1E8", rough=0.5, image=img)
    return M, img


# ------------------------------------------------------------------- body ---
def body_ring(y):
    zb, ws, wsh, zsh, wbelt, zbelt, wdeck, zdeck, zc = S.lerp_keys(y)
    half = [(0.0, zb), (ws - 0.05, zb), (ws, zb + 0.06), (wsh, zsh), (wbelt, zbelt), (wdeck, zdeck), (0.0, zc)]
    ring = half + [(-x, z) for (x, z) in reversed(half[1:-1])]
    return [(x, y, z) for (x, z) in ring]


BODY_SECTIONS = [1.98, 1.95, 1.82, 1.72, 1.60, 1.40, 1.18, 0.96, 0.74, 0.60, 0.10, -0.36, -0.84,
                 -1.00, -1.18, -1.40, -1.60, -1.72, -1.82, -1.89, -1.92]


def build_shell(M, coll):
    mb = MeshBuilder(f"{C}_Shell")
    mb.loft([body_ring(y) for y in BODY_SECTIONS], M["paint"])
    shell = mb.finish(coll)
    shell.data.materials.append(M["trim"])

    # Wheel arches: boolean with cylinders; the cut walls inherit the black trim.
    cb = MeshBuilder(f"{C}_ArchCutter")
    for slot, (wx, wy) in S.WHEELS.items():
        sx = 1 if wx > 0 else -1
        cb.cyl((sx * 0.56, wy, S.WHEEL_Z + 0.01), (sx * 1.15, wy, S.WHEEL_Z + 0.01), S.ARCH_R, M["trim"], seg=40)
    cutter = cb.finish(coll)
    mod = shell.modifiers.new("Arches", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.solver = "EXACT"
    mod.object = cutter
    if hasattr(mod, "material_mode"):
        mod.material_mode = "TRANSFER"
    vb.apply_modifiers(shell)
    bpy.data.objects.remove(cutter)

    # Open the deck under the glasshouse so the interior is visible.
    bm = bmesh.new()
    bm.from_mesh(shell.data)
    y0, y1 = S.CABIN_Y
    kill = []
    for f in bm.faces:
        c = f.calc_center_median()
        if y0 + 1e-4 < c.y < y1 - 1e-4 and c.z > 0.85 and f.normal.z > 0.6 and abs(c.x) < 0.74:
            kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bm.to_mesh(shell.data)
    bm.free()
    vb.add_bevel(shell, width=0.014, segments=2, angle=30)
    vb.apply_modifiers(shell)
    return shell


def build_glasshouse(M, coll, glass_coll):
    g = S.GH
    mb = MeshBuilder(f"{C}_Glasshouse")
    bm = mb.bm
    V = {}
    for sx, side in ((-1, "L"), (1, "R")):
        for key, y, z, w in (("F0", g["yF0"], g["z0"], g["w0"]), ("B0", g["yB0"], g["z0"], g["w0"]),
                             ("R0", g["yR0"], g["z0"], g["w0"]), ("F1", g["yF1"], g["z1"], g["w1"]),
                             ("B1", g["yB1"], g["z1"], g["w1"]), ("R1", g["yR1"], g["z1"], g["w1"])):
            V[side + key] = bm.verts.new((sx * w, y, z))
    F = bm.faces.new
    windshield = F((V["LF0"], V["RF0"], V["RF1"], V["LF1"]))
    backlight = F((V["RR0"], V["LR0"], V["LR1"], V["RR1"]))
    roof = F((V["LF1"], V["RF1"], V["RB1"], V["RR1"], V["LR1"], V["LB1"]))
    sides = [F((V["RF0"], V["RB0"], V["RB1"], V["RF1"])), F((V["RB0"], V["RR0"], V["RR1"], V["RB1"])),
             F((V["LB0"], V["LF0"], V["LF1"], V["LB1"])), F((V["LR0"], V["LB0"], V["LB1"], V["LR1"]))]
    windows = [windshield, backlight] + sides
    inside = Vector((0, -0.03, 1.0))
    vb.orient_faces(bm, list(bm.faces), lambda f: f.calc_center_median() - inside)
    paint_i, rub_i = mb.mi(M["paint"]), mb.mi(M["trim"])
    for f in bm.faces:
        f.material_index = paint_i
    bmesh.ops.inset_individual(bm, faces=windows, thickness=g["pillar"], depth=0.0, use_even_offset=True)
    rubber = bmesh.ops.inset_individual(bm, faces=windows, thickness=g["rubber"], depth=-0.006,
                                        use_even_offset=True)["faces"]
    for f in rubber:
        f.material_index = rub_i
    # Glass: copy the window centres into their own object, then open the holes.
    gb = MeshBuilder(f"{C}_Glass")
    gi = gb.mi(M["glass"])
    for w in windows:
        w.normal_update()
        n = w.normal.copy()
        vs = [gb.bm.verts.new(v.co - n * 0.004) for v in w.verts]
        gf = gb.bm.faces.new(vs)
        gf.material_index = gi
    glass = gb.finish(glass_coll, recalc=False)
    bmesh.ops.delete(bm, geom=windows, context="FACES")
    gh = mb.finish(coll, recalc=False)
    vb.add_bevel(gh, width=0.012, segments=2, angle=30)
    sol = gh.modifiers.new("Solidify", "SOLIDIFY")
    sol.thickness = 0.025
    sol.offset = -1.0
    sol.use_even_offset = True
    vb.apply_modifiers(gh)
    return gh, glass


def build_flares(M, coll):
    F = S.FLARE
    mb = MeshBuilder(f"{C}_Flares")
    bm = mb.bm
    paint_i, trim_i = mb.mi(M["paint"]), mb.mi(M["trim"])
    n = F["samples"]
    r = F["inner_r"]
    phi0 = math.asin((F["bottom_z"] - S.WHEEL_Z) / r)  # negative (front-bottom)

    def resample(poly, count):
        segs = [(Vector(a) - Vector(b)).length for a, b in zip(poly, poly[1:])]
        total = sum(segs)
        out = []
        for k in range(count):
            t = total * k / (count - 1)
            for (a, b), L in zip(zip(poly, poly[1:]), segs):
                if t <= L + 1e-9:
                    out.append(Vector(a).lerp(Vector(b), t / L if L else 0))
                    break
                t -= L
            else:
                out.append(Vector(poly[-1]))
        return out

    for slot, (wx, wy) in S.WHEELS.items():
        sx = 1 if wx > 0 else -1
        inner = []
        for k in range(n):  # rear-bottom -> over the top -> front-bottom
            phi = (math.pi - phi0) + (phi0 - (math.pi - phi0)) * k / (n - 1)
            inner.append(Vector((r * math.cos(phi), S.WHEEL_Z + r * math.sin(phi))))
        outer = resample(F["outer"], n)
        rows = {"ret": [], "lip": [], "shelf": [], "base": []}
        for a, b in zip(inner, outer):
            y_in, z_in = wy + a.x, a.y
            mid = a.lerp(b, F["shelf_t"])
            y_md, z_md = wy + mid.x, mid.y
            y_out = wy + b.x
            z_out = min(b.y, S.lerp_keys(y_out)[5] - 0.012)
            rows["ret"].append(bm.verts.new((sx * 0.78, y_in, z_in)))
            rows["lip"].append(bm.verts.new((sx * F["lip_x"], y_in, z_in)))
            rows["shelf"].append(bm.verts.new((sx * F["shelf_x"], y_md, z_md)))
            rows["base"].append(bm.verts.new((sx * (S.body_half_width_at(y_out, z_out) - 0.004), y_out, z_out)))
        faces_out, faces_ret = [], []
        for ra, rb, bucket in (("ret", "lip", faces_ret), ("lip", "shelf", faces_out), ("shelf", "base", faces_out)):
            A, B = rows[ra], rows[rb]
            for k in range(n - 1):
                bucket.append(bm.faces.new((A[k], A[k + 1], B[k + 1], B[k])))
        wc = Vector((sx * 0.6, wy, S.WHEEL_Z))
        vb.orient_faces(bm, faces_out, lambda f: (f.calc_center_median() - wc).normalized()
                        + Vector((sx * 0.8, 0, 0)))
        wcy = Vector((0, wy, S.WHEEL_Z))
        vb.orient_faces(bm, faces_ret, lambda f: -(Vector((0, f.calc_center_median().y, f.calc_center_median().z)) - wcy))
        for f in faces_out:
            f.material_index = paint_i
        for f in faces_ret:
            f.material_index = trim_i
    obj = mb.finish(coll, recalc=False)
    vb.add_bevel(obj, width=0.008, segments=1, angle=35)
    vb.apply_modifiers(obj)
    return obj


# --------------------------------------------------------------- exterior ---
def build_exterior(M, coll):
    parts = []
    # Front bumper (painted) with black splitter and intakes.
    fb = MeshBuilder(f"{C}_Bumper_Front")
    plan = [(-0.86, 1.90), (0.86, 1.90), (0.86, 1.99), (0.75, 2.10), (-0.75, 2.10), (-0.86, 1.99)]
    fb.prism(plan, "XY", 0.18, 0.40, M["paint"])
    fb.prism([(-0.84, 1.95), (0.84, 1.95), (0.84, 2.02), (0.76, 2.135), (-0.76, 2.135), (-0.84, 2.02)],
             "XY", 0.165, 0.19, M["trim"])
    for sx in (-1, 1):
        fb.box((sx * 0.56, 2.098, 0.27), (0.26, 0.014, 0.075), M["trim"])
        fb.box((sx * 0.67, 2.099, 0.355), (0.10, 0.012, 0.035), M["ind"])
    fb.box((0, 2.099, 0.245), (0.30, 0.012, 0.045), M["trim"])
    o = fb.finish(coll)
    vb.add_bevel(o, width=0.01, segments=2, angle=30)
    vb.apply_modifiers(o)
    parts.append(o)

    # Rear bumper (black plastic)
    rb = MeshBuilder(f"{C}_Bumper_Rear")
    rb.prism([(-0.84, -1.86), (0.84, -1.86), (0.84, -1.95), (0.76, -2.03), (-0.76, -2.03), (-0.84, -1.95)],
             "XY", 0.21, 0.375, M["trim"])
    o = rb.finish(coll)
    vb.add_bevel(o, width=0.012, segments=2, angle=30)
    vb.apply_modifiers(o)
    parts.append(o)

    # Rear wing: airfoil loft + end plates + stands
    sp = MeshBuilder(f"{C}_Spoiler")
    foil = [(0.0, 0.0), (-0.02, 0.022), (-0.09, 0.036), (-0.20, 0.034), (-0.32, 0.016), (-0.34, 0.010),
            (-0.34, 0.000), (-0.22, -0.006), (-0.08, -0.009), (-0.02, -0.007)]
    aoa = math.radians(7)
    le_y, le_z = -1.56, 1.065

    def foil_pt(x, p):
        yy, zz = p  # rotate about the leading edge so the trailing edge rises
        return (x, le_y + yy * math.cos(aoa) + zz * math.sin(aoa), le_z + zz * math.cos(aoa) - yy * math.sin(aoa))
    sp.loft([[foil_pt(x, p) for p in foil] for x in (-0.80, 0.80)], M["paint"])
    te = foil_pt(0, foil[5])
    sp.box((0, te[1] - 0.004, te[2] + 0.018), (1.60, 0.008, 0.032), M["paint"])  # gurney lip
    for sx in (-1, 1):
        sp.prism([(-1.53, 1.02), (-1.96, 1.045), (-1.965, 1.125), (-1.55, 1.105)], "YZ",
                 sx * 0.806, sx * 0.818, M["paint"])
        sp.prism([(-1.64, 0.87), (-1.80, 0.87), (-1.77, 1.07), (-1.67, 1.07)], "YZ",
                 sx * 0.47, sx * 0.485, M["trim"])
    o = sp.finish(coll)
    vb.add_bevel(o, width=0.004, segments=1, angle=35)
    vb.apply_modifiers(o)
    parts.append(o)

    # Mirrors
    mi = MeshBuilder(f"{C}_Mirrors")
    for sx in (-1, 1):
        mi.cyl((sx * 0.79, 0.63, 0.885), (sx * 0.86, 0.615, 0.94), 0.011, M["trim"], seg=8)
        mi.box((sx * 0.90, 0.605, 0.952), (0.12, 0.065, 0.078), M["trim"], top_scale=(0.9, 0.85))
        mi.box((sx * 0.90, 0.571, 0.952), (0.10, 0.004, 0.058), M["chrome"])
    o = mi.finish(coll)
    vb.add_bevel(o, width=0.008, segments=2, angle=30)
    vb.apply_modifiers(o)
    parts.append(o)

    # Trim: grille, side skirts, door handles, roof vent, exhaust, tow hooks, plates
    tr = MeshBuilder(f"{C}_Trim")
    tr.box((0, 1.988, 0.545), (0.70, 0.02, 0.13), M["trim"])
    for z in (0.505, 0.545, 0.585):
        tr.box((0, 1.999, z), (0.66, 0.012, 0.013), M["trim_grey"])
    for sx in (-1, 1):
        bw = S.body_half_width_at(0.0, 0.27)
        tr.box((sx * (bw + 0.012), 0.0, 0.255), (0.05, 1.26, 0.11), M["trim"])
        hw = S.body_half_width_at(S.HANDLE_Y, 0.77)
        tr.box((sx * (hw + 0.004), S.HANDLE_Y, 0.77), (0.016, 0.11, 0.022), M["trim"])
    tr.box((0, 0.10, 1.352), (0.26, 0.16, 0.035), M["trim"], top_scale=(0.85, 0.6))
    tr.cyl((0.50, -1.80, 0.245), (0.50, -2.075, 0.245), 0.042, M["chrome"], seg=14)
    tr.cyl((0.50, -2.070, 0.245), (0.50, -2.078, 0.245), 0.031, M["trim"], seg=14)
    for (x, y, z) in ((0.66, 2.135, 0.215), (-0.62, -2.04, 0.25)):
        prof = [(0.009 * math.cos(a), 0.032 + 0.009 * math.sin(a))
                for a in [2 * math.pi * k / 8 for k in range(8)]]
        tr.revolve(prof, 12, M["accent"], center=(x, y, z), closed=True)
    tr.box((0, 2.106, 0.295), (0.52, 0.010, 0.115), M["plate"])
    tr.box((0, -1.935, 0.51), (0.52, 0.010, 0.115), M["plate"])
    o = tr.finish(coll)
    parts.append(o)

    # Mud flaps
    mf = MeshBuilder(f"{C}_MudFlaps")
    for slot, (wx, wy) in S.WHEELS.items():
        sx = 1 if wx > 0 else -1
        rear = wy < 0
        mf.box((sx * 0.80, wy - 0.44, 0.195 if not rear else 0.19), (0.25, 0.012, 0.27 if not rear else 0.29),
               M["accent"])
    o = mf.finish(coll)
    vb.add_bevel(o, width=0.004, segments=1, angle=35)
    vb.apply_modifiers(o)
    parts.append(o)
    return parts


# ----------------------------------------------------------------- lights ---
def build_lights(M, coll):
    lb = MeshBuilder(f"{C}_Lights")
    for sx in (-1, 1):
        lb.box((sx * 0.555, 1.986, 0.545), (0.34, 0.03, 0.14), M["trim"])
        lb.box((sx * 0.555, 1.999, 0.545), (0.31, 0.012, 0.11), M["head"])
        lb.box((sx * 0.555, 2.0055, 0.545), (0.014, 0.004, 0.11), M["trim"])  # quad-lamp divider
        lb.box((sx * 0.54, -1.934, 0.69), (0.28, 0.012, 0.11), M["tail"])
        lb.box((sx * 0.355, -1.934, 0.69), (0.08, 0.012, 0.11), M["ind"])
    lb.box((0, -1.926, 0.69), (1.40, 0.014, 0.15), M["trim"])
    # Four-lamp pod on a bar above the bumper
    lb.box((0, 2.065, 0.413), (1.12, 0.07, 0.026), M["trim"])
    for x in (-0.43, -0.15, 0.15, 0.43):
        lb.cyl((x, 2.085, 0.50), (x, 2.165, 0.50), 0.078, M["trim"], seg=20)
        lb.cyl((x, 2.160, 0.50), (x, 2.176, 0.50), 0.083, M["chrome"], seg=20)
        lb.cyl((x, 2.170, 0.50), (x, 2.181, 0.50), 0.067, M["aux"], seg=20)
    o = lb.finish(coll)
    vb.add_bevel(o, width=0.004, segments=1, angle=35)
    vb.apply_modifiers(o)
    return o


# --------------------------------------------------------------- interior ---
def build_interior(M, coll):
    ib = MeshBuilder(f"{C}_Interior")
    tub = ib.box((0, -0.035, 0.56), (1.48, 1.51, 0.60), M["interior"])
    bmesh.ops.recalc_face_normals(ib.bm, faces=tub)
    bmesh.ops.reverse_faces(ib.bm, faces=tub)  # visible from inside
    top = [f for f in tub if f.calc_center_median().z > 0.85]
    bmesh.ops.delete(ib.bm, geom=top, context="FACES")
    ib.box((0, 0.60, 0.80), (1.40, 0.24, 0.14), M["interior"], top_scale=(1.0, 0.7))
    ib.box((-0.37, 0.52, 0.875), (0.30, 0.10, 0.05), M["trim"])
    for sx in (-1, 1):
        x = sx * 0.37
        ib.box((x, -0.16, 0.37), (0.46, 0.50, 0.10), M["seat"])
        back_rot = Euler((math.radians(-12), 0, 0))
        ib.box((x, -0.47, 0.68), (0.46, 0.10, 0.62), M["seat"], rot=back_rot.to_quaternion(), top_scale=(0.78, 1.0))
        for bx in (-1, 1):
            ib.box((x + bx * 0.21, -0.16, 0.44), (0.06, 0.46, 0.10), M["seat"])
            ib.box((x + bx * 0.21, -0.43, 0.68), (0.06, 0.13, 0.44), M["seat"], rot=back_rot.to_quaternion())
    r = 0.019
    cg = M["cage"]
    ib.tube_path([(-0.70, -0.52, 0.30), (-0.70, -0.52, 0.86), (-0.56, -0.50, 1.29), (0.56, -0.50, 1.29),
                  (0.70, -0.52, 0.86), (0.70, -0.52, 0.30)], r, cg)
    ib.tube_path([(-0.70, 0.64, 0.50), (-0.70, 0.64, 0.86), (-0.56, 0.25, 1.29), (0.56, 0.25, 1.29),
                  (0.70, 0.64, 0.86), (0.70, 0.64, 0.50)], r, cg)
    for sx in (-1, 1):
        ib.cyl((sx * 0.56, 0.25, 1.29), (sx * 0.56, -0.50, 1.29), r, cg, seg=8)
        ib.cyl((sx * 0.71, 0.62, 0.58), (sx * 0.71, -0.52, 0.80), r, cg, seg=8)
        ib.cyl((sx * 0.70, -0.52, 0.86), (sx * 0.62, -0.78, 0.56), r, cg, seg=8)
    ib.cyl((-0.70, -0.52, 0.32), (0.56, -0.50, 1.29), r, cg, seg=8)
    o = ib.finish(coll, recalc=False)
    # Only the closed parts need outward normals; the tub was oriented above.
    return o


def build_steering_wheel(M, coll):
    sw = MeshBuilder(f"{C}_SteeringWheel")
    prof = [(0.018 * math.cos(a), 0.17 + 0.018 * math.sin(a)) for a in [2 * math.pi * k / 8 for k in range(8)]]
    grid = sw.revolve(prof, 24, M["trim"], closed=True)
    for ang in (90, 210, 330):
        a = math.radians(ang)
        sw.cyl((0, 0, 0), (0, 0.16 * math.cos(a), 0.16 * math.sin(a)), 0.012, M["trim_grey"], seg=6)
    sw.cyl((-0.03, 0, 0), (0.02, 0, 0), 0.04, M["trim_grey"], seg=12)
    o = sw.finish(coll, recalc=False)
    # Revolve axis is X; turn it so the column axis is local +Y pointing at the driver.
    o.data.transform(Matrix.Rotation(math.radians(90), 4, "Z"))
    o.location = (-0.37, 0.40, 0.81)
    o.rotation_euler = (math.radians(-22), 0, 0)
    return o


# ----------------------------------------------------------------- wheels ---
TREAD_DEPTH = 0.011  # gravel blocks stand proud of the carcass; their tops sit exactly at WHEEL_R
TIRE_PROFILE = [(0.092, 0.205), (0.110, 0.222), (0.1175, 0.258), (0.115, 0.292), (0.104, 0.312),
                (0.072, 0.3185), (0.0, S.WHEEL_R - TREAD_DEPTH), (-0.072, 0.3185), (-0.104, 0.312),
                (-0.115, 0.292), (-0.1175, 0.258), (-0.110, 0.222), (-0.092, 0.205)]


def build_tire_mesh(M, name):
    tb = MeshBuilder(name)
    seg = 40
    grid = tb.revolve(TIRE_PROFILE, seg, M["tire"])
    blocks = []
    for i in range(seg):
        blocks.append(grid[i][5] if i % 2 == 0 else grid[i][6])
        if i % 2 == 0:
            blocks += [grid[i][4], grid[i][7]]
    res = bmesh.ops.extrude_discrete_faces(tb.bm, faces=blocks)
    for f in res["faces"]:
        f.normal_update()
        c = f.calc_center_median()
        radial = Vector((0, c.y, c.z)).normalized()
        for v in f.verts:
            v.co += radial * TREAD_DEPTH
    vb.orient_faces(tb.bm, list(tb.bm.faces), lambda f: f.calc_center_median())
    return tb


def build_rim_mesh(M, name):
    rb = MeshBuilder(name)
    barrel = [(-0.095, 0.186), (-0.095, 0.214), (-0.085, 0.214), (-0.080, 0.198), (0.080, 0.198),
              (0.088, 0.214), (0.100, 0.214), (0.100, 0.186)]
    rb.revolve(barrel, 32, M["rim"], closed=True)
    rb.revolve([(0.100, 0.187), (0.072, 0.176), (0.066, 0.150), (0.058, 0.150), (0.058, 0.187)],
               32, M["rim"], closed=True)
    for k in range(6):
        a = 2 * math.pi * k / 6
        q = Euler((a, 0, 0)).to_quaternion()
        c = q @ Vector((0.064, 0, 0.105))
        rb.box(c, (0.024, 0.042, 0.11), M["rim"], rot=q, top_scale=(1.0, 0.75))
    rb.cyl((0.050, 0, 0), (0.074, 0, 0), 0.064, M["rim"], seg=20)
    rb.cyl((0.074, 0, 0), (0.092, 0, 0), 0.046, M["rim"], seg=20, r2=0.03)
    for k in range(6):  # lug nuts
        a = 2 * math.pi * (k + 0.5) / 6
        c = Vector((0.084, 0.044 * math.cos(a), 0.044 * math.sin(a)))
        rb.cyl(c, c + Vector((0.012, 0, 0)), 0.008, M["chrome"], seg=6)
    rb.cyl((-0.03, 0, 0), (-0.008, 0, 0), 0.165, M["disc"], seg=28)
    rb.cyl((-0.008, 0, 0), (0.05, 0, 0), 0.07, M["disc"], seg=16)
    bmesh.ops.recalc_face_normals(rb.bm, faces=rb.bm.faces)
    return rb


def build_wheels(M, coll):
    out = {}
    for slot, (wx, wy) in S.WHEELS.items():
        sx = 1 if wx > 0 else -1
        piv = bpy.data.objects.new(f"{C}_Wheel_{slot}", None)
        piv.empty_display_type = "CIRCLE"
        piv.empty_display_size = S.WHEEL_R
        piv.location = (wx, wy, S.WHEEL_Z)
        piv["wheel_slot"] = slot
        piv["steer"] = slot.startswith("F")
        piv["drive"] = True  # AWD
        piv["radius"] = S.WHEEL_R
        piv["width"] = S.TIRE_W
        coll.objects.link(piv)
        parts = []
        for kind, fn in (("Tire", build_tire_mesh), ("Rim", build_rim_mesh)):
            mb = fn(M, f"{C}_Wheel_{slot}_{kind}")
            if sx < 0:  # left wheels: outboard face points to -X
                bmesh.ops.rotate(mb.bm, verts=mb.bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi, 3, "Z"))
            o = mb.finish(coll, recalc=False)
            o.parent = piv
            vb.smooth_by_angle(o, 40 if kind == "Tire" else 35)
            parts.append(o)
        out[slot] = (piv, parts)
    return out


# -------------------------------------------------------------- collision ---
def build_collision(coll):
    objs = []
    for name, c, s in ((f"{C}_Collider_Body", (0, 0.03, 0.55), (1.76, 4.10, 0.62)),
                       (f"{C}_Collider_Cabin", (0, -0.04, 1.10), (1.36, 1.46, 0.48))):
        mb = MeshBuilder(name)
        verts = bmesh.ops.create_cube(mb.bm, size=1.0, matrix=Matrix.LocRotScale(Vector(c), None, Vector(s)))
        o = mb.finish(coll)
        o.display_type = "WIRE"
        o.hide_render = True
        objs.append(o)
    return objs


# ------------------------------------------------------------------- main ---
def main():
    a = parse()
    out = os.path.abspath(a.out)
    if "Incoming" in out.split(os.sep):
        raise SystemExit("Refusing to write into Incoming.")
    if os.path.exists(out) and not a.overwrite:
        out = pc.next_version(out)
    pc.reset_scene()
    cols = collections()
    M, img = materials(a.texture)

    shell = build_shell(M, cols["Body"])
    gh, glass = build_glasshouse(M, cols["Body"], cols["Glass"])
    flares = build_flares(M, cols["Body"])
    vb.project_uvs(shell, S)
    vb.project_uvs(gh, S)
    vb.project_uvs(flares, S, force_side=True)
    body = vb.join([shell, gh, flares], f"{C}_Body")
    exterior = build_exterior(M, cols["Exterior"])
    lights = build_lights(M, cols["Lights"])
    interior = build_interior(M, cols["Interior"])
    steering = build_steering_wheel(M, cols["Interior"])
    wheels = build_wheels(M, cols["Wheels"])
    colliders = build_collision(cols["Collision"])

    # UVs: every mesh gets the world-space atlas projection (only paint/plate sample it).
    bpy.context.view_layer.update()
    plate_rects = {M["plate"].name: S.PLATE_FRONT_RECT}
    for o in [glass, lights, interior, steering] + exterior:
        if o.name == f"{C}_Trim":
            continue
        vb.project_uvs(o, S)
    trim = bpy.data.objects[f"{C}_Trim"]
    vb.project_uvs(trim, S, plate_rects=None)
    # Plates: front and rear faces separately mapped to their rects
    me = trim.data
    uv = me.uv_layers["UVMap"]
    pidx = list(me.materials).index(M["plate"])
    for poly in me.polygons:
        if poly.material_index != pidx:
            continue
        front = poly.center.y > 0
        u0, v0, u1, v1 = S.PLATE_FRONT_RECT if front else S.PLATE_REAR_RECT
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            tx = (co.x + 0.26) / 0.52
            if front:
                tx = 1 - tx
            zc = 0.295 if front else 0.51
            tz = (co.z - (zc - 0.0575)) / 0.115
            uv.data[li].uv = (u0 + (u1 - u0) * tx, v0 + (v1 - v0) * tz)
    for slot, (piv, parts) in wheels.items():
        for o in parts:
            o.data.uv_layers.new(name="UVMap")

    for o in [body, glass, lights, interior, steering] + exterior:
        vb.smooth_by_angle(o, 32)

    # Hierarchy
    root = bpy.data.objects.new(f"{C}_Root", None)
    root.empty_display_type = "ARROWS"
    root["car_id"] = C
    root["display_name"] = S.DISPLAY_NAME
    root["vehicle_class"] = S.CLASS
    root["forward_axis"] = "+Y"
    root["up_axis"] = "+Z"
    root["units"] = "m"
    cols["Root"].objects.link(root)
    groups = {}
    for gname, coll in (("Exterior", cols["Exterior"]), ("Wheels", cols["Wheels"])):
        e = bpy.data.objects.new(f"{C}_{gname}", None)
        e.empty_display_size = 0.2
        coll.objects.link(e)
        e.parent = root
        groups[gname] = e
    for o in (body, glass, lights, interior):
        o.parent = root
    steering.parent = interior
    for o in exterior:
        o.parent = groups["Exterior"]
    for slot, (piv, parts) in wheels.items():
        piv.parent = groups["Wheels"]
    for o in colliders:
        o.parent = root

    # Recentre so the root is at the ground-plane centre of the visual bounding box.
    bpy.context.view_layer.update()
    vis = [o for o in cols["Root"].all_objects if o.type == "MESH"]
    bb = pc.world_bbox(vis)
    off = Vector((bb["center"][0], bb["center"][1], 0.0))
    T = Matrix.Translation(-off)
    for o in list(cols["Root"].all_objects) + colliders:
        if o in groups.values() or o is root:
            continue
        if o.parent is root or o.parent in groups.values():
            if o.type == "MESH":
                o.data.transform(T)
            else:
                o.location -= off
    # steering wheel is parented to interior (mesh moved, object origin did not) -> move it too
    steering.location -= off
    bpy.context.view_layer.update()
    bb = pc.world_bbox(vis)

    # Report
    objs = []
    total = 0
    for o in sorted(cols["Root"].all_objects, key=lambda o: o.name):
        t = vb.tri_count(o)
        total += t
        objs.append({"name": o.name, "type": o.type, "parent": o.parent.name if o.parent else None,
                     "tris": t, "materials": [m.name for m in o.data.materials] if o.type == "MESH" else [],
                     "world_origin": [round(x, 4) for x in o.matrix_world.translation]})
    wheel_info = {slot: {"blender": [round(x, 4) for x in piv.matrix_world.translation],
                         "unity": pc.blender_to_unity(piv.matrix_world.translation)}
                  for slot, (piv, _p) in wheels.items()}
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.context.scene.render.engine = "CYCLES"
    bpy.ops.wm.save_as_mainfile(filepath=out, compress=True)
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_as_mainfile(filepath=out, compress=True)
    pc.write_json(a.report, {
        "car_id": C, "display_name": S.DISPLAY_NAME, "tool": "build_car009.py", "generated_utc": pc.now_iso(),
        "blender": pc.blender_info(), "saved_file": out, "texture": os.path.abspath(a.texture),
        "design_to_final_offset": [round(x, 4) for x in off], "bbox": bb, "total_tris": total,
        "wheels": wheel_info, "wheel_radius": S.WHEEL_R, "tire_width": S.TIRE_W,
        "materials": sorted(m.name for m in bpy.data.materials if m.users),
        "objects": objs,
    })
    print(f"Saved {out}\nTotal tris: {total}  bbox size: {[round(x, 3) for x in bb['size']]}")


if __name__ == "__main__":
    main()
