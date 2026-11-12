"""build_generic.py - build any car described in collection_specs.py.

    blender -b -P build_generic.py -- --car CAR-002 \
        --texture ../../../VehicleLibrary/CAR-002/Textures/CAR-002_BaseColor.png \
        --out ../../../VehicleLibrary/CAR-002/Working/CAR-002_Working_v001.blend \
        --report ../../Reports/CAR-002_build.json [--overwrite]

Produces the same hierarchy and conventions as build_car009.py (validated):
    <CAR>_Root > Body, Exterior(empty) > parts, Glass, Interior(+SteeringWheel), Lights,
    Wheels(empty) > Wheel_FL/FR/RL/RR (empty at axle centre) > _Tire, _Rim
    <CAR>_Collision collection (not exported in the visual FBX)
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

import pipeline_common as pc  # noqa: E402
import vehicle_builder as vb  # noqa: E402
import build_extras  # noqa: E402
from carspec import CarSpec  # noqa: E402
from collection_specs import SPECS  # noqa: E402
from vehicle_builder import MeshBuilder  # noqa: E402


def parse():
    p = argparse.ArgumentParser()
    p.add_argument("--car", required=True)
    p.add_argument("--texture", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--report", required=True)
    p.add_argument("--overwrite", action="store_true")
    return p.parse_args(pc.script_args())


class Builder:
    def __init__(self, spec, tex):
        self.S = spec
        self.C = spec.id
        self.d = spec.d
        self.cols = self.collections()
        self.M = self.materials(tex)
        self.front_face = {}  # filled by bumpers: (z0, z1, y_front)
        self.rear_face = {}

    # ---------------------------------------------------------------- setup ---
    def collections(self):
        root = bpy.data.collections.new(self.C)
        bpy.context.scene.collection.children.link(root)
        subs = {"Root": root}
        for n in ("Body", "Exterior", "Glass", "Interior", "Lights", "Wheels"):
            c = bpy.data.collections.new(f"{self.C}_{n}")
            root.children.link(c)
            subs[n] = c
        col = bpy.data.collections.new(f"{self.C}_Collision")
        bpy.context.scene.collection.children.link(col)
        subs["Collision"] = col
        return subs

    def materials(self, tex):
        img = bpy.data.images.load(os.path.abspath(tex), check_existing=True)
        img.colorspace_settings.name = "sRGB"
        c = self.d["colors"]
        C = self.C
        base = self.d["livery"]["base"]
        return {
            "paint": vb.material(f"M_{C}_Paint", base, rough=0.45, image=img, specular=0.35),
            "plate": vb.material(f"M_{C}_Plate", "#F4F1E8", rough=0.5, image=img),
            "trim": vb.material("M_Trim_Black", "#1E1D1F", rough=0.55),
            "trim_grey": vb.material("M_Trim_Grey", "#4A4744", rough=0.5),
            "chrome": vb.material("M_Chrome", "#D4D2CE", rough=0.22, metal=1.0),
            "glass": vb.material("M_Glass_Tint", "#0B1013", rough=0.05, alpha=0.86, specular=0.6),
            "tire": vb.material("M_Tire_Rubber", "#232120", rough=0.92, specular=0.25),
            "rim": vb.material(f"M_{C}_Rim", self.d["wheels"]["rim_color"], rough=0.36, metal=0.85),
            "disc": vb.material("M_Brake_Disc", "#6A6764", rough=0.45, metal=0.8),
            "head": vb.material("M_Light_Head", "#F3EFE3", rough=0.15, emission="#FFF2D2", emission_strength=1.5),
            "aux": vb.material("M_Light_Aux", "#F6EBC4", rough=0.15, emission="#FFE7A0", emission_strength=2.0),
            "tail": vb.material("M_Light_Tail", "#A3171B", rough=0.2, emission="#FF2A1A", emission_strength=0.8),
            "ind": vb.material("M_Light_Indicator", "#E07A1A", rough=0.2, emission="#FF8A1A", emission_strength=0.6),
            "interior": vb.material("M_Interior_Dark", "#2E2C2D", rough=0.85),
            "seat": vb.material(f"M_{C}_Seat", c["seat"], rough=0.8),
            "cage": vb.material(f"M_{C}_Cage", c["cage"], rough=0.5),
            "accent": vb.material(f"M_{C}_Accent", c["accent"], rough=0.75),
            "neon": vb.material("M_Light_Neon", c.get("neon", "#38E8FF"), rough=0.2, emission=c.get("neon", "#38E8FF"),
                                emission_strength=3.0),
            "tent": vb.material(f"M_{C}_Tent", c.get("tent", "#C9B48A"), rough=0.85),
            "can": vb.material(f"M_{C}_Can", c.get("can", "#B5352B"), rough=0.6),
        }

    # ----------------------------------------------------------------- body ---
    def sections(self):
        S, g = self.S, self.S.gh
        ys = {k[0] for k in S.keys}
        ty0, _ty1 = S.tub_y()
        ys |= {g["yF0"], ty0, S.axle_f, S.axle_r}
        ys = sorted(ys, reverse=True)
        out = []
        for a, b in zip(ys, ys[1:]):
            out.append(a)
            n = int((a - b) / 0.32)
            for i in range(1, n + 1):
                out.append(a - (a - b) * i / (n + 1))
        out.append(ys[-1])
        return out

    def shell(self):
        S, M = self.S, self.M
        for name, y, slot in (("front", S.axle_f, "FL"), ("rear", S.axle_r, "RL")):
            wr, _tw, _rim, ar, _x = S.wdims(slot)
            margin = S.lerp_keys(y)[7] - (wr + 0.01 + ar)
            if margin < 0.02:
                print(f"[warning] {self.C} {name} arch top is only {margin * 100:.1f} cm below the wing top")
        mb = MeshBuilder(f"{self.C}_Shell")
        mb.loft([S.ring(y) for y in self.sections()], M["paint"])
        shell = mb.finish(self.cols["Body"])
        shell.data.materials.append(M["trim"])
        if self.d.get("arches", True):
            cb = MeshBuilder(f"{self.C}_ArchCutter")
            for slot, (wx, wy) in S.wheels.items():
                sx = 1 if wx > 0 else -1
                wr, tw, _rim, ar, wxa = S.wdims(slot)
                x_in = wxa - tw / 2 - 0.06
                cb.cyl((sx * x_in, wy, wr + 0.01), (sx * 1.6, wy, wr + 0.01), ar, M["trim"], seg=40)
            cutter = cb.finish(self.cols["Body"])
            mod = shell.modifiers.new("Arches", "BOOLEAN")
            mod.operation = "DIFFERENCE"
            mod.solver = "EXACT"
            mod.object = cutter
            if hasattr(mod, "material_mode"):
                mod.material_mode = "TRANSFER"
            vb.apply_modifiers(shell)
            bpy.data.objects.remove(cutter)
        # open the deck over the cabin tub (and over a pickup bed)
        bed = (self.d.get("acc") or {}).get("bed")
        bm = bmesh.new()
        bm.from_mesh(shell.data)
        y0, _ = S.tub_y()
        y1 = S.gh["yF0"]
        kill = []
        for f in bm.faces:
            c = f.calc_center_median()
            if y0 + 1e-4 < c.y < y1 - 1e-4 and f.normal.z > 0.6 and c.z > S.zbelt_at(c.y) - 0.03 \
                    and abs(c.x) < S.lerp_keys(c.y)[6] - 0.004:
                kill.append(f)
            elif bed and bed["y1"] + 1e-4 < c.y < bed["y0"] - 1e-4 and f.normal.z > 0.6 \
                    and c.z > S.zbelt_at(c.y) - 0.03 and abs(c.x) < S.lerp_keys(c.y)[6] - 0.004:
                kill.append(f)
        bmesh.ops.delete(bm, geom=kill, context="FACES")
        bm.to_mesh(shell.data)
        bm.free()
        vb.add_bevel(shell, width=0.013, segments=2, angle=30)
        vb.apply_modifiers(shell)
        return shell

    def glasshouse(self):
        S, M, g = self.S, self.M, self.S.gh
        if g.get("open"):
            return self.open_frame(), None
        mb = MeshBuilder(f"{self.C}_Glasshouse")
        bm = mb.bm
        feet, tops = S.gh_feet(), S.gh_tops()
        V = {}
        for sx, side in ((-1, "L"), (1, "R")):
            V[side + "b"] = [bm.verts.new((sx * g["w0"], y, S.zbelt_at(y) - 0.006)) for y in feet]
            V[side + "t"] = [bm.verts.new((sx * g["w1"], y, g["z1"])) for y in tops]
        F = bm.faces.new
        Lb, Lt, Rb, Rt = V["Lb"], V["Lt"], V["Rb"], V["Rt"]
        windshield = F((Lb[0], Rb[0], Rt[0], Lt[0]))
        backlight = F((Rb[-1], Lb[-1], Lt[-1], Rt[-1]))
        F(tuple(Rt) + tuple(reversed(Lt)))
        sides = []
        for i in range(len(feet) - 1):
            sides.append(F((Rb[i], Rb[i + 1], Rt[i + 1], Rt[i])))
            sides.append(F((Lb[i + 1], Lb[i], Lt[i], Lt[i + 1])))
        windows = [windshield, backlight] + sides
        inside = Vector((0, (feet[0] + feet[-1]) / 2, (g["z1"] + S.zbelt_at(0)) / 2))
        vb.orient_faces(bm, list(bm.faces), lambda f: f.calc_center_median() - inside)
        pi, ri = mb.mi(M["paint"]), mb.mi(M["trim"])
        for f in bm.faces:
            f.material_index = pi
        bmesh.ops.inset_individual(bm, faces=windows, thickness=g.get("pillar", 0.032), depth=0.0,
                                   use_even_offset=True)
        rub = bmesh.ops.inset_individual(bm, faces=windows, thickness=g.get("rubber", 0.015), depth=-0.006,
                                         use_even_offset=True)["faces"]
        for f in rub:
            f.material_index = ri
        gb = MeshBuilder(f"{self.C}_Glass")
        gi = gb.mi(M["glass"])
        for w in windows:
            w.normal_update()
            n = w.normal.copy()
            gf = gb.bm.faces.new([gb.bm.verts.new(v.co - n * 0.004) for v in w.verts])
            gf.material_index = gi
        glass = gb.finish(self.cols["Glass"], recalc=False)
        bmesh.ops.delete(bm, geom=windows, context="FACES")
        gh = mb.finish(self.cols["Body"], recalc=False)
        vb.add_bevel(gh, width=0.011, segments=2, angle=30)
        sol = gh.modifiers.new("Solidify", "SOLIDIFY")
        sol.thickness = 0.024
        sol.offset = -1.0
        sol.use_even_offset = True
        vb.apply_modifiers(gh)
        return gh, glass

    def open_frame(self):
        """Open cockpit (buggy): a tube frame along the glasshouse edges instead of glass and roof."""
        S, M, g = self.S, self.M, self.S.gh
        mb = MeshBuilder(f"{self.C}_Cage")
        r = g.get("tube", 0.034)
        cg = M["cage"]
        z1 = g["z1"]
        zf, zr = S.zbelt_at(g["yF0"]), S.zbelt_at(g["yR0"])
        for sx in (-1, 1):
            # A-pillar, roof rail, main hoop leg, door bar
            mb.tube_path([(sx * g["w0"], g["yF0"], zf - 0.02), (sx * g["w1"], g["yF1"], z1),
                          (sx * g["w1"], g["yR1"], z1), (sx * g["w0"], g["yR0"], zr - 0.02)], r, cg)
            mb.cyl((sx * g["w0"], g["yF0"], zf - 0.02), (sx * g["w0"], g["yR0"], zr - 0.02), r * 0.85, cg, seg=8)
            # rear braces down to the tail
            yb = S.y_min + 0.35
            mb.cyl((sx * g["w1"], g["yR1"], z1), (sx * (g["w0"] - 0.08), yb, S.zbelt_at(yb) + 0.02), r * 0.85, cg, seg=8)
        mb.cyl((-g["w1"], g["yF1"], z1), (g["w1"], g["yF1"], z1), r, cg, seg=8)
        mb.cyl((-g["w1"], g["yR1"], z1), (g["w1"], g["yR1"], z1), r, cg, seg=8)
        mb.cyl((-g["w1"], g["yF1"], z1), (g["w1"], g["yR1"], z1), r * 0.8, cg, seg=8)
        mb.cyl((g["w1"], g["yF1"], z1), (-g["w1"], g["yR1"], z1), r * 0.8, cg, seg=8)
        mb.cyl((-g["w0"], g["yR0"], zr - 0.02), (g["w1"], g["yR1"], z1), r * 0.8, cg, seg=8)
        mb.cyl((-g["w0"], g["yR0"], zr - 0.02), (g["w0"], g["yR0"], zr - 0.02), r * 0.85, cg, seg=8)
        # windscreen-frame net (stylised mesh panel)
        for t in (0.33, 0.66):
            ya, za = g["yF0"] + (g["yF1"] - g["yF0"]) * t, zf + (z1 - zf) * t
            xa = g["w0"] + (g["w1"] - g["w0"]) * t
            mb.cyl((-xa, ya, za), (xa, ya, za), 0.008, M["trim"], seg=6)
        o = mb.finish(self.cols["Exterior"], recalc=False)
        return o

    def flares(self):
        S, M, fl = self.S, self.M, self.d.get("flares")
        if not fl:
            return None
        mb = MeshBuilder(f"{self.C}_Flares")
        bm = mb.bm
        pi, ti = mb.mi(M["paint"]), mb.mi(M["trim"])
        if fl.get("mat") == "trim":
            pi = ti
        n = 17
        for slot, (wx, wy) in S.wheels.items():
            wz, _tw, _rim, ar, wxa = S.wdims(slot)
            r_in = ar + 0.007
            sx = 1 if wx > 0 else -1
            bz = S.lerp_keys(wy)[0] + 0.02
            phi0 = math.asin(max(-0.99, (bz - wz) / r_in))
            inner = []
            for k in range(n):
                phi = (math.pi - phi0) + (phi0 - (math.pi - phi0)) * k / (n - 1)
                inner.append(Vector((r_in * math.cos(phi), wz + r_in * math.sin(phi))))
            if fl["type"] == "box":
                o = ar + 0.175
                poly = [(-o, bz), (-o, wz + 0.6 * ar), (-(o - 0.12), wz + ar + 0.06), (o - 0.12, wz + ar + 0.06),
                        (o, wz + 0.6 * ar), (o, bz)]
                outer = resample(poly, n)
                flip = fl["lip"] if slot.startswith("F") else fl.get("lip_rear", fl["lip"])
                lip, shelf, t = flip, flip - 0.008, 0.55
            else:
                ro = ar + 0.10
                ph0 = math.asin(max(-0.99, (bz - wz) / ro))
                outer = [Vector((ro * math.cos(p), wz + ro * math.sin(p))) for p in
                         [(math.pi - ph0) + (ph0 - (math.pi - ph0)) * k / (n - 1) for k in range(n)]]
                flip = fl["lip"] if slot.startswith("F") else fl.get("lip_rear", fl["lip"])
                lip, shelf, t = flip, flip - 0.012, 0.45
            rows = {"ret": [], "lip": [], "shelf": [], "base": []}
            for a, b in zip(inner, outer):
                mid = a.lerp(b, t)
                y_out = wy + b.x
                z_out = min(b.y, S.zbelt_at(y_out) - 0.012)
                rows["ret"].append(bm.verts.new((sx * (wxa - 0.02), wy + a.x, a.y)))
                rows["lip"].append(bm.verts.new((sx * lip, wy + a.x, a.y)))
                rows["shelf"].append(bm.verts.new((sx * shelf, wy + mid.x, min(mid.y, S.zbelt_at(wy + mid.x)))))
                rows["base"].append(bm.verts.new((sx * (S.body_half_width_at(y_out, z_out) - 0.004), y_out, z_out)))
            outf, retf = [], []
            for ra, rb, bucket in (("ret", "lip", retf), ("lip", "shelf", outf), ("shelf", "base", outf)):
                A, B = rows[ra], rows[rb]
                for k in range(n - 1):
                    bucket.append(bm.faces.new((A[k], A[k + 1], B[k + 1], B[k])))
            wc = Vector((sx * (wxa - 0.15), wy, wz))
            vb.orient_faces(bm, outf, lambda f, wc=wc, sx=sx: (f.calc_center_median() - wc).normalized()
                            + Vector((sx * 0.8, 0, 0)))
            wcy = Vector((0, wy, wz))
            vb.orient_faces(bm, retf, lambda f, wcy=wcy: -(Vector((0, f.calc_center_median().y,
                                                                    f.calc_center_median().z)) - wcy))
            for f in outf:
                f.material_index = pi
            for f in retf:
                f.material_index = ti
        obj = mb.finish(self.cols["Body"], recalc=False)
        vb.add_bevel(obj, width=0.007, segments=1, angle=35)
        vb.apply_modifiers(obj)
        return obj

    # ------------------------------------------------------------- exterior ---
    def bumper(self, mb, spec, front):
        S, M = self.S, self.M
        style, (z0, z1), d = spec["style"], spec["z"], spec["depth"]
        hw = (S.nose_half_width() if front else S.tail_half_width()) + 0.03
        sgn = 1 if front else -1
        yb = (S.y_max if front else S.y_min)
        back, fr = yb - sgn * 0.10, yb + sgn * d * 0.6
        c = min(0.09, d + 0.03)
        pts = [(-hw, back), (hw, back), (hw, fr - sgn * c * 0.6), (hw - c, fr), (-(hw - c), fr),
               (-hw, fr - sgn * c * 0.6)]
        mat = {"chrome": M["chrome"], "black": M["trim"], "paint": M["paint"]}[style]
        mb.prism(pts, "XY", z0, z1, mat)
        if style == "paint":
            sp = [(x, y + sgn * 0.025 if abs(y - fr) < 1e-6 else y) for x, y in pts]
            mb.prism(sp, "XY", z0 - 0.015, z0 + 0.012, M["trim"])
        if style == "chrome":  # black rubber insert line
            ins = [(x * 0.995, y + sgn * 0.004 if abs(y - fr) < 1e-6 else y) for x, y in pts]
            zm = (z0 + z1) / 2
            mb.prism(ins, "XY", zm - 0.006, zm + 0.006, M["trim"])
        face = {"z0": z0, "z1": z1, "y": fr}
        if front:
            self.front_face = face
        else:
            self.rear_face = face

    def y_face(self, z, front=True):
        S = self.S
        f = self.front_face if front else self.rear_face
        if f and f["z0"] - 0.01 <= z <= f["z1"] + 0.01:
            return f["y"]
        return S.y_max if front else S.y_min

    def exterior(self):
        S, M, d = self.S, self.M, self.d
        parts = []
        fb = MeshBuilder(f"{self.C}_Bumper_Front")
        self.bumper(fb, d["front"]["bumper"], True)
        o = fb.finish(self.cols["Exterior"])
        vb.add_bevel(o, width=0.008, segments=2, angle=30)
        parts.append(vb.apply_modifiers(o))
        rb = MeshBuilder(f"{self.C}_Bumper_Rear")
        self.bumper(rb, d["rear"]["bumper"], False)
        o = rb.finish(self.cols["Exterior"])
        vb.add_bevel(o, width=0.008, segments=2, angle=30)
        parts.append(vb.apply_modifiers(o))

        sp = self.spoilers()
        if sp:
            parts.append(sp)

        g = S.gh
        mi = MeshBuilder(f"{self.C}_Mirrors")
        y_m = d["doors"][0][0] - 0.05
        z_m = S.zbelt_at(y_m) + 0.075
        bw = S.body_half_width_at(y_m, S.zbelt_at(y_m) - 0.03)
        for sx in ((-1, 1) if d.get("mirrors") else ()):
            mi.cyl((sx * (bw - 0.02), y_m, z_m - 0.05), (sx * (bw + 0.035), y_m - 0.01, z_m), 0.011, M["trim"], seg=8)
            if d.get("mirrors") == "round":
                mi.cyl((sx * (bw + 0.06), y_m + 0.03, z_m + 0.01), (sx * (bw + 0.06), y_m - 0.04, z_m + 0.01),
                       0.045, M["trim"], seg=14, r2=0.05)
                mi.cyl((sx * (bw + 0.06), y_m - 0.040, z_m + 0.01), (sx * (bw + 0.06), y_m - 0.044, z_m + 0.01),
                       0.042, M["chrome"], seg=14)
            else:
                mi.box((sx * (bw + 0.06), y_m - 0.005, z_m + 0.012), (0.12, 0.065, 0.078), M["trim"],
                       top_scale=(0.9, 0.85))
                mi.box((sx * (bw + 0.06), y_m - 0.039, z_m + 0.012), (0.10, 0.004, 0.058), M["chrome"])
        if d.get("mirrors"):
            o = mi.finish(self.cols["Exterior"])
            vb.add_bevel(o, width=0.006, segments=2, angle=30)
            parts.append(vb.apply_modifiers(o))
        else:
            mi.bm.free()

        tr = MeshBuilder(f"{self.C}_Trim")
        fr = d["front"]
        for gr in fr.get("grilles", []):
            y = (self.front_face["y"] if gr.get("on") == "bumper" else S.y_max) + 0.006 + gr.get("dy", 0.0)
            mat = M[gr.get("mat", "trim")]
            tr.box((0, y, gr["z"]), (gr["w"], 0.02, gr["h"]), mat)
            n = gr.get("slats", 0)
            for i in range(n):
                z = gr["z"] - gr["h"] / 2 + (i + 1) * gr["h"] / (n + 1)
                tr.box((0, y + 0.011, z), (gr["w"] - 0.04, 0.008, 0.012),
                       M["trim_grey"] if gr.get("mat", "trim") == "trim" else M["chrome"])
        if d.get("skirts"):
            y0 = S.axle_r + S.arch_r + 0.17
            y1 = S.axle_f - S.arch_r - 0.17
            for sx in (-1, 1):
                zb = S.lerp_keys((y0 + y1) / 2)[0]
                bw = S.body_half_width_at((y0 + y1) / 2, zb + 0.07)
                tr.box((sx * (bw + 0.012), (y0 + y1) / 2, zb + 0.045), (0.05, y1 - y0, 0.11), M["trim"])
        yh = d["doors"][0][1] + 0.13
        zh = S.zbelt_at(yh) - 0.09
        hw = S.body_half_width_at(yh, zh)
        for sx in ((-1, 1) if d.get("handles", True) else ()):
            tr.box((sx * (hw + 0.004), yh, zh), (0.016, 0.10, 0.022), M["trim"])
        if d.get("roof_vent"):
            tr.box((0, g["yF1"] - 0.12, g["z1"] + 0.012), (0.26, 0.16, 0.035), M["trim"], top_scale=(0.85, 0.6))
        # exhaust
        ex = d["rear"].get("exhaust_x")
        exs = [ex] if ex is not None else []
        for px in d["rear"].get("exhaust_pairs", []):
            exs += [px, -px]
        for ex in exs:
            zr = min(self.rear_face["z0"] - 0.03, S.lerp_keys(S.y_min + 0.2)[0] + 0.02)
            zr = d["rear"].get("exhaust_z", zr)
            y_end = self.rear_face["y"] - 0.05
            er = d["rear"].get("exhaust_r", 0.038)
            tr.cyl((ex, S.y_min + 0.25, zr), (ex, y_end, zr), er, M["chrome"], seg=14)
            tr.cyl((ex, y_end + 0.004, zr), (ex, y_end - 0.004, zr), er * 0.74, M["trim"], seg=14)
        # tow hooks
        for (x, y, z) in ((0.6 * S.nose_half_width(), self.front_face["y"] + 0.03, self.front_face["z0"] - 0.01),
                          (-0.6 * S.tail_half_width(), self.rear_face["y"] - 0.03, self.rear_face["z0"] - 0.01)):
            prof = [(0.009 * math.cos(a), 0.03 + 0.009 * math.sin(a)) for a in [2 * math.pi * k / 8 for k in range(8)]]
            tr.revolve(prof, 12, M["accent"], center=(x, y, z), closed=True)
        # plates
        self.plates = []
        for front, z in ((True, fr.get("plate_z")), (False, d["rear"].get("plate_z"))):
            if z is None:
                continue
            y = self.y_face(z, front) + (0.006 if front else -0.006)
            tr.box((0, y, z), (0.52, 0.010, 0.115), M["plate"])
            self.plates.append((front, z))
        o = tr.finish(self.cols["Exterior"])
        parts.append(o)

        if d.get("mudflaps", True) is False:
            return parts
        mf = MeshBuilder(f"{self.C}_MudFlaps")
        big = d.get("mudflaps") == "big"
        for slot, (wx, wy) in S.wheels.items():
            sx = 1 if wx > 0 else -1
            wr, tw, _rim, _ar, wxa = S.wdims(slot)
            zb = S.lerp_keys(wy - wr - 0.11)[0]
            top = zb + 0.10
            low = 0.10 if big else 0.06
            mf.box((sx * wxa, wy - wr - 0.11, (low + top) / 2), (tw + (0.08 if big else 0.04), 0.014 if big else 0.012,
                                                                   top - low), M["accent"])
        o = mf.finish(self.cols["Exterior"])
        vb.add_bevel(o, width=0.004, segments=1, angle=35)
        parts.append(vb.apply_modifiers(o))
        return parts

    def roof_z_at(self, y):
        """Outer glasshouse height at y (roof, windscreen or rear window slope)."""
        g, S = self.S.gh, self.S
        if g["yR1"] <= y <= g["yF1"]:
            return g["z1"]
        if y > g["yF1"]:
            t = min(1.0, (y - g["yF1"]) / max(1e-3, g["yF0"] - g["yF1"]))
            return g["z1"] + (S.zbelt_at(g["yF0"]) - g["z1"]) * t
        t = min(1.0, (g["yR1"] - y) / max(1e-3, g["yR1"] - g["yR0"]))
        return g["z1"] + (S.zbelt_at(g["yR0"]) - g["z1"]) * t

    def spoilers(self):
        S, M, d = self.S, self.M, self.d
        sp, rs = d.get("spoiler"), d.get("roof_spoiler")
        if not sp and not rs:
            return None
        mb = MeshBuilder(f"{self.C}_Spoiler")
        if sp and sp["type"] == "wing":
            c, s, aoa = sp["chord"], sp["span"], math.radians(sp["aoa"])
            le_y, le_z = sp["le_y"], sp["le_z"]
            k = c / 0.34
            foil = [(0.0, 0.0), (-0.02, 0.022), (-0.09, 0.036), (-0.20, 0.034), (-0.32, 0.016), (-0.34, 0.010),
                    (-0.34, 0.000), (-0.22, -0.006), (-0.08, -0.009), (-0.02, -0.007)]
            foil = [(a * k, b * k) for a, b in foil]

            def fp(x, p):
                yy, zz = p
                return (x, le_y + yy * math.cos(aoa) + zz * math.sin(aoa), le_z + zz * math.cos(aoa) - yy * math.sin(aoa))
            mb.loft([[fp(x, p) for p in foil] for x in (-s, s)], M["paint"])
            te = fp(0, foil[5])
            mb.box((0, te[1] - 0.004, te[2] + 0.014 * k), (2 * s, 0.008, 0.026 * k), M["paint"])
            for sx in (-1, 1):
                if sp.get("endplate"):
                    mb.prism([(le_y + 0.03, le_z - 0.05), (le_y - c - 0.04, le_z - 0.03),
                              (le_y - c - 0.045, le_z + 0.07), (le_y + 0.01, le_z + 0.05)], "YZ",
                             sx * (s + 0.004), sx * (s + 0.016), M["paint"])
                ys0, ys1 = le_y - 0.08 * k, le_y - 0.24 * k
                if sp.get("mount") == "roof":
                    dz0, dz1 = self.roof_z_at(ys0) - 0.02, self.roof_z_at(ys1) - 0.02
                else:
                    dz0 = S.deck_z_at(ys0) - 0.02
                    dz1 = S.deck_z_at(ys1) - 0.02
                mb.prism([(ys0, dz0), (ys1, dz1), (ys1 + 0.03 * k, le_z + 0.005), (ys0 - 0.03 * k, le_z + 0.005)],
                         "YZ", sx * sp["stand_x"], sx * (sp["stand_x"] + 0.015), M["trim"])
        elif sp:  # ducktail / lip
            y0, y1, h, w = sp["y0"], sp["y1"], sp["h"], sp["w"]
            mb.prism([(y0, S.deck_z_at(y0) - 0.012), (y1, S.deck_z_at(y1) + h), (y1, S.deck_z_at(y1) - 0.05)],
                     "YZ", -w, w, M["paint"])
        if rs:
            g = S.gh
            y0 = g["yR1"] + 0.02
            mb.prism([(y0, g["z1"] - 0.004), (y0 - rs["len"], g["z1"] - rs["drop"]),
                      (y0 - rs["len"], g["z1"] - rs["drop"] - 0.02), (y0, g["z1"] - 0.026)], "YZ",
                     -(g["w1"] - 0.02), g["w1"] - 0.02, M["paint"])
        o = mb.finish(self.cols["Exterior"])
        vb.add_bevel(o, width=0.004, segments=1, angle=35)
        return vb.apply_modifiers(o)

    # --------------------------------------------------------------- lights ---
    def lights(self):
        S, M, d = self.S, self.M, self.d
        lb = MeshBuilder(f"{self.C}_Lights")
        fr = d["front"]
        for lp in fr.get("headlights", []):
            if lp["shape"] == "bar":   # one centred full-width LED strip
                y = self.y_face(lp["z"], True) + 0.004 + lp.get("dy", 0.0)
                lb.box((0, y - 0.006, lp["z"]), (lp["w"] + 0.02, 0.02, lp["h"] + 0.016), M["trim"])
                lb.box((0, y + 0.006, lp["z"]), (lp["w"], 0.012, lp["h"]), M[lp.get("mat", "head")])
                continue
            for sx in (-1, 1):
                x = sx * lp["x"]
                y = S.y_max + 0.004 + lp.get("dy", 0.0)
                if lp["shape"] == "strip":
                    y = self.y_face(lp["z"], True) + 0.004 + lp.get("dy", 0.0)
                    lb.box((x, y - 0.006, lp["z"]), (lp["w"] + 0.02, 0.02, lp["h"] + 0.02), M["trim"])
                    lb.box((x, y + 0.006, lp["z"]), (lp["w"], 0.012, lp["h"]), M[lp.get("mat", "head")])
                elif lp["shape"] == "round":
                    r = lp["r"]
                    lb.cyl((x, y - lp.get("depth", 0.10), lp["z"]), (x, y, lp["z"]), r, M["trim"], seg=20)
                    lb.cyl((x, y - 0.004, lp["z"]), (x, y + 0.009, lp["z"]), r * 1.07, M["chrome"], seg=20)
                    lb.cyl((x, y, lp["z"]), (x, y + 0.013, lp["z"]), r * 0.86, M["head"], seg=20)
                else:
                    lb.box((x, y - 0.01, lp["z"]), (lp["w"] + 0.03, 0.03, lp["h"] + 0.03), M["trim"])
                    lb.box((x, y + 0.008, lp["z"]), (lp["w"], 0.012, lp["h"]), M["head"])
                    lb.box((x, y + 0.0145, lp["z"]), (0.012, 0.004, lp["h"]), M["trim"])
        for ind in fr.get("indicators", []):
            for sx in (-1, 1):
                y = self.y_face(ind["z"], True) + 0.004
                lb.box((sx * ind["x"], y, ind["z"]), (ind["w"], 0.012, ind["h"]), M["ind"])
        aux = fr.get("aux") or {}
        lamps = aux.get("lamps", [])
        if lamps:
            if aux.get("bar") is not None:
                xmax = max(x + r for x, z, r in lamps)
                yb = self.y_face(aux["bar"], True)
                lb.box((0, yb + 0.02, aux["bar"]), (2 * xmax, 0.07, 0.024), M["trim"])
            for (x0, z, r) in lamps:
                for sx in (-1, 1):
                    x = sx * x0
                    yb = max(self.y_face(z, True), self.y_face(z - r * 0.8, True)) + 0.005
                    lb.cyl((x, yb, z), (x, yb + 0.075, z), r, M["trim"], seg=20)
                    lb.cyl((x, yb + 0.07, z), (x, yb + 0.086, z), r * 1.065, M["chrome"], seg=20)
                    lb.cyl((x, yb + 0.08, z), (x, yb + 0.091, z), r * 0.86, M["aux"], seg=20)
        rr = d["rear"]
        pan = rr.get("panel")
        if pan:
            lb.box((0, S.y_min - 0.004, pan["z"]), (pan["w"], 0.012, pan["h"]), M["trim"])
        for lp in rr.get("lights", []):
            if lp["shape"] == "bar":
                y = self.y_face(lp["z"], False) - 0.008
                lb.box((0, y, lp["z"]), (lp["w"], 0.012, lp["h"]), M[lp.get("mat", "tail")])
                continue
            for sx in (-1, 1):
                x = sx * lp["x"]
                y = self.y_face(lp["z"], False) - 0.008
                mat = M[lp.get("mat", "tail")]
                if lp["shape"] == "round":
                    lb.cyl((x, y + 0.01, lp["z"]), (x, y - 0.006, lp["z"]), lp["r"] * 1.15, M["chrome"], seg=16)
                    lb.cyl((x, y, lp["z"]), (x, y - 0.012, lp["z"]), lp["r"], mat, seg=16)
                else:
                    lb.box((x, y, lp["z"]), (lp["w"], 0.012, lp["h"]), mat)
        o = lb.finish(self.cols["Lights"])
        vb.add_bevel(o, width=0.003, segments=1, angle=35)
        return vb.apply_modifiers(o)

    # ------------------------------------------------------------- interior ---
    def interior(self):
        S, M, g = self.S, self.M, self.S.gh
        ib = MeshBuilder(f"{self.C}_Interior")
        ty0, ty1 = S.tub_y()
        zb = S.lerp_keys((ty0 + ty1) / 2)[0]
        zbelt = S.zbelt_at((ty0 + ty1) / 2)
        hw = g["w0"] - 0.03
        tub = ib.box((0, (ty0 + ty1) / 2, (zb + 0.04 + zbelt) / 2), (2 * hw, ty1 - ty0, zbelt - zb - 0.04),
                     M["interior"])
        bmesh.ops.recalc_face_normals(ib.bm, faces=tub)
        bmesh.ops.reverse_faces(ib.bm, faces=tub)
        bmesh.ops.delete(ib.bm, geom=[f for f in tub if f.calc_center_median().z > zbelt - 0.01], context="FACES")
        yF0 = g["yF0"]
        ib.box((0, yF0 - 0.14, zbelt - 0.04), (2 * hw - 0.06, 0.22, 0.13), M["interior"], top_scale=(1.0, 0.7))
        seat_x = min(0.37, hw * 0.52)
        seat_y = yF0 - 0.85
        ib.box((-seat_x, yF0 - 0.22, zbelt + 0.03), (0.28, 0.09, 0.05), M["trim"])
        cab_h = g["z1"] - zb
        back_h = min(0.62, cab_h * 0.55)
        z_seat = zb + 0.15
        rot = Euler((math.radians(-12), 0, 0)).to_quaternion()
        for sx in (-1, 1):
            x = sx * seat_x
            ib.box((x, seat_y, z_seat), (0.44, 0.48, 0.10), M["seat"])
            ib.box((x, seat_y - 0.30, z_seat + back_h / 2), (0.44, 0.10, back_h), M["seat"], rot=rot,
                   top_scale=(0.78, 1.0))
            for bx in (-1, 1):
                ib.box((x + bx * 0.20, seat_y, z_seat + 0.07), (0.06, 0.44, 0.10), M["seat"])
                ib.box((x + bx * 0.20, seat_y - 0.27, z_seat + back_h * 0.45), (0.06, 0.13, back_h * 0.7),
                       M["seat"], rot=rot)
        if self.d.get("cage", True):
            r = 0.018
            cg = M["cage"]
            xb, xr = g["w0"] - 0.07, g["w1"] - 0.06
            y_main = max(ty0 + 0.06, seat_y - 0.42)
            zt_main = min(g["z1"] - 0.05, S.gh_inner_top(y_main))
            ztf = g["z1"] - 0.05
            yf_top = g["yF1"] + 0.04
            zfl = S.zbelt_at(yF0 - 0.08)
            zml = S.zbelt_at(y_main)
            ib.tube_path([(-xb, y_main, zb + 0.08), (-xb, y_main, zml), (-xr, y_main + 0.02, zt_main),
                          (xr, y_main + 0.02, zt_main), (xb, y_main, zml), (xb, y_main, zb + 0.08)], r, cg)
            ib.tube_path([(-xb, yF0 - 0.08, zb + 0.30), (-xb, yF0 - 0.08, zfl), (-xr, yf_top, ztf),
                          (xr, yf_top, ztf), (xb, yF0 - 0.08, zfl), (xb, yF0 - 0.08, zb + 0.30)], r, cg)
            for sx in (-1, 1):
                ib.cyl((sx * xr, yf_top, ztf), (sx * xr, y_main + 0.02, zt_main), r, cg, seg=8)
                ib.cyl((sx * (xb + 0.01), yF0 - 0.10, zb + 0.38), (sx * (xb + 0.01), y_main, zml - 0.05), r, cg, seg=8)
            ib.cyl((-xb, y_main, zb + 0.10), (xr, y_main + 0.02, zt_main), r, cg, seg=8)
        self.steer_pos = (-seat_x, yF0 - 0.34, zbelt - 0.05)
        return ib.finish(self.cols["Interior"], recalc=False)

    def steering(self):
        M = self.M
        sw = MeshBuilder(f"{self.C}_SteeringWheel")
        prof = [(0.016 * math.cos(a), 0.165 + 0.016 * math.sin(a)) for a in [2 * math.pi * k / 8 for k in range(8)]]
        sw.revolve(prof, 24, M["trim"], closed=True)
        for ang in (90, 210, 330):
            a = math.radians(ang)
            sw.cyl((0, 0, 0), (0, 0.155 * math.cos(a), 0.155 * math.sin(a)), 0.011, M["trim_grey"], seg=6)
        sw.cyl((-0.03, 0, 0), (0.02, 0, 0), 0.038, M["trim_grey"], seg=12)
        o = sw.finish(self.cols["Interior"], recalc=False)
        o.data.transform(Matrix.Rotation(math.radians(90), 4, "Z"))
        o.location = self.steer_pos
        o.rotation_euler = (math.radians(-22), 0, 0)
        return o

    # --------------------------------------------------------------- wheels ---
    def tire_mesh(self, name, slot="FL"):
        S, M = self.S, self.M
        R, tw, rim, _ar, _x = S.wdims(slot)
        hw = tw / 2
        style = self.d["wheels"].get("tyre", "road")
        if style != "road":
            return self.tire_mesh_offroad(name, R, rim, hw, style)
        TD = 0.011
        axial = [0.783, 0.936, 1.0, 0.979, 0.885, 0.613, 0.0]
        tfrac = [0.0, 0.136, 0.424, 0.696, 0.856, None, None]
        half = []
        for a, t in zip(axial, tfrac):
            if t is None:
                rr = R - TD - (0.0005 if a > 0 else 0.0)
            else:
                rr = rim + t * (R - TD - rim) / 0.912
            half.append((a * hw, rr))
        prof = half + [(-w, r) for (w, r) in reversed(half[:-1])]
        tb = MeshBuilder(name)
        seg = 40
        grid = tb.revolve(prof, seg, M["tire"])
        blocks = []
        for i in range(seg):
            blocks.append(grid[i][5] if i % 2 == 0 else grid[i][6])
            if i % 2 == 0:
                blocks += [grid[i][4], grid[i][7]]
        res = bmesh.ops.extrude_discrete_faces(tb.bm, faces=blocks)
        for f in res["faces"]:
            c = f.calc_center_median()
            radial = Vector((0, c.y, c.z)).normalized()
            for v in f.verts:
                v.co += radial * TD
        vb.orient_faces(tb.bm, list(tb.bm.faces), lambda f: f.calc_center_median())
        return tb

    def tire_mesh_offroad(self, name, R, rim, hw, style):
        """Chunkier tyres: 'at' (staggered all-terrain blocks + sidewall lugs), 'sand' (paddles),
        'studded' (fine blocks with steel studs)."""
        M = self.M
        TD = {"at": 0.024, "sand": 0.030, "studded": 0.012}.get(style, 0.016)
        # squarer shoulder than the road tyre
        axial = [0.80, 0.95, 1.0, 0.99, 0.93, 0.62, 0.0]
        tfrac = [0.0, 0.14, 0.45, 0.74, 0.92, None, None]
        half = []
        for a_, t in zip(axial, tfrac):
            rr = R - TD - (0.0005 if a_ > 0 else 0.0) if t is None else rim + t * (R - TD - rim) / 0.95
            half.append((a_ * hw, rr))
        prof = half + [(-w, r) for (w, r) in reversed(half[:-1])]
        tb = MeshBuilder(name)
        seg = 36 if style != "studded" else 40
        grid = tb.revolve(prof, seg, M["tire"])
        blocks, lugs = [], []
        for i in range(seg):
            if style == "at":
                blocks += [grid[i][4], grid[i][5]] if i % 2 == 0 else [grid[i][6], grid[i][7]]
                lugs.append(grid[i][3] if i % 2 == 0 else grid[i][8])
            elif style == "sand":
                if i % 3 == 0:
                    blocks += [grid[i][4], grid[i][5], grid[i][6], grid[i][7]]
            else:
                blocks.append(grid[i][5] if i % 2 == 0 else grid[i][6])
                if i % 2 == 0:
                    blocks += [grid[i][4], grid[i][7]]
        res = bmesh.ops.extrude_discrete_faces(tb.bm, faces=blocks + lugs)
        lugset = set()
        nb = len(blocks)
        for k, f in enumerate(res["faces"]):
            c = f.calc_center_median()
            radial = Vector((0, c.y, c.z)).normalized()
            d = TD if k < nb else TD * 0.6
            for v in f.verts:
                v.co += radial * d
            if k >= nb:
                lugset.add(f)
        if style == "studded":
            for i in range(seg):
                ang = 2 * math.pi * (i + 0.5) / seg
                radial = Vector((0, math.cos(ang), math.sin(ang)))
                for xs in ((0.3, 0.75) if i % 2 == 0 else (-0.3, -0.75)):
                    p0 = Vector((xs * hw, 0, 0)) + radial * (R - 0.003)
                    tb.cyl(p0, p0 + radial * 0.009, 0.0045, M["chrome"], seg=4)
        vb.orient_faces(tb.bm, list(tb.bm.faces), lambda f: f.calc_center_median())
        return tb

    def rim_mesh(self, name, slot="FL"):
        S, M = self.S, self.M
        _R, tw, rim_r, _ar, _x = S.wdims(slot)
        k = rim_r / 0.205
        a = tw / 0.235
        rb = MeshBuilder(name)

        def P(pts):
            return [(w * a, r * k) for w, r in pts]
        rb.revolve(P([(-0.095, 0.186), (-0.095, 0.214), (-0.085, 0.214), (-0.080, 0.198), (0.080, 0.198),
                      (0.088, 0.214), (0.100, 0.214), (0.100, 0.186)]), 32, M["rim"], closed=True)
        rb.revolve(P([(0.100, 0.187), (0.072, 0.176), (0.066, 0.150), (0.058, 0.150), (0.058, 0.187)]),
                   32, M["rim"], closed=True)
        style = self.d["wheels"]["rim"]
        if style[0] == "spoke":
            n, wdt = style[1], style[2]
            for i in range(n):
                q = Euler((2 * math.pi * i / n, 0, 0)).to_quaternion()
                rb.box(q @ Vector((0.064 * a, 0, 0.105 * k)), (0.024 * a, wdt * k, 0.11 * k), M["rim"], rot=q,
                       top_scale=(1.0, 0.75))
        elif style[0] == "aero":  # flat aero disc with dark slots (EV)
            rb.revolve(P([(0.076, 0.188), (0.084, 0.150), (0.088, 0.070), (0.070, 0.070), (0.062, 0.188)]),
                       32, M["rim"], closed=True)
            n = style[1]
            for i in range(n):
                q = Euler((2 * math.pi * i / n, 0, 0)).to_quaternion()
                rb.box(q @ Vector((0.087 * a, 0, 0.125 * k)), (0.006, 0.016 * k, 0.07 * k), M["trim"], rot=q)
        else:  # dish with vents (beadlock: + an outer ring of bolts)
            n = style[1]
            rb.revolve(P([(0.064, 0.150), (0.062, 0.066), (0.052, 0.066), (0.052, 0.150)]), 32, M["rim"],
                       closed=True)
            for i in range(n):
                ang = 2 * math.pi * (i + 0.5) / n
                c = Vector((0.0645 * a, 0.108 * k * math.cos(ang), 0.108 * k * math.sin(ang)))
                rb.cyl(c, c + Vector((0.003, 0, 0)), 0.027 * k, M["trim"], seg=12)
            if style[0] == "beadlock":
                rb.revolve(P([(0.100, 0.172), (0.116, 0.172), (0.116, 0.212), (0.100, 0.212)]), 32, M["trim"],
                           closed=True)
                for i in range(12):
                    ang = 2 * math.pi * i / 12
                    c = Vector((0.116 * a, 0.192 * k * math.cos(ang), 0.192 * k * math.sin(ang)))
                    rb.cyl(c, c + Vector((0.01, 0, 0)), 0.0075 * k, M["chrome"], seg=6)
        rb.cyl((0.050 * a, 0, 0), (0.074 * a, 0, 0), 0.064 * k, M["rim"], seg=20)
        rb.cyl((0.074 * a, 0, 0), (0.092 * a, 0, 0), 0.046 * k, M["rim"], seg=20, r2=0.03 * k)
        nl = 5 if style[0] in ("dish", "beadlock", "aero") else 6
        for i in range(nl):
            ang = 2 * math.pi * (i + 0.5) / nl
            c = Vector((0.084 * a, 0.044 * k * math.cos(ang), 0.044 * k * math.sin(ang)))
            rb.cyl(c, c + Vector((0.012, 0, 0)), 0.008 * k, M["chrome"], seg=6)
        rb.cyl((-0.03 * a, 0, 0), (-0.008 * a, 0, 0), 0.165 * k, M["disc"], seg=28)
        rb.cyl((-0.008 * a, 0, 0), (0.05 * a, 0, 0), 0.07 * k, M["disc"], seg=16)
        bmesh.ops.recalc_face_normals(rb.bm, faces=rb.bm.faces)
        return rb

    def wheels(self):
        S = self.S
        out = {}
        for slot, (wx, wy) in S.wheels.items():
            sx = 1 if wx > 0 else -1
            wr, tw, _rim, _ar, _x = S.wdims(slot)
            piv = bpy.data.objects.new(f"{self.C}_Wheel_{slot}", None)
            piv.empty_display_type = "CIRCLE"
            piv.empty_display_size = wr
            piv.location = (wx, wy, wr)
            piv["wheel_slot"] = slot
            piv["steer"] = slot.startswith("F")
            dt = self.d.get("drivetrain", "AWD")
            piv["drive"] = dt == "AWD" or (dt == "RWD" and slot.startswith("R")) or (dt == "FWD" and slot.startswith("F"))
            piv["radius"] = wr
            piv["width"] = tw
            self.cols["Wheels"].objects.link(piv)
            parts = []
            for kind, fn in (("Tire", self.tire_mesh), ("Rim", self.rim_mesh)):
                mb = fn(f"{self.C}_Wheel_{slot}_{kind}", slot)
                if sx < 0:
                    bmesh.ops.rotate(mb.bm, verts=mb.bm.verts, cent=(0, 0, 0),
                                     matrix=Matrix.Rotation(math.pi, 3, "Z"))
                o = mb.finish(self.cols["Wheels"], recalc=False)
                o.parent = piv
                vb.smooth_by_angle(o, 40 if kind == "Tire" else 35)
                o.data.uv_layers.new(name="UVMap")
                parts.append(o)
            out[slot] = (piv, parts)
        return out

    def collision(self):
        S, g = self.S, self.S.gh
        objs = []
        zb = min(k[1] for k in self.S.keys)
        zbelt = sum(k[6] for k in S.keys) / len(S.keys)
        boxes = ((f"{self.C}_Collider_Body", (0, (S.y_max + S.y_min) / 2, (zb + 0.04 + zbelt) / 2),
                  (2 * (S.half_w - 0.04), S.y_max - S.y_min + 0.1, zbelt - zb - 0.04)),
                 (f"{self.C}_Collider_Cabin", (0, (g["yF0"] + g["yR0"] + g["yF1"] + g["yR1"]) / 4,
                                               (zbelt + g["z1"]) / 2),
                  (2 * g["w1"] + 0.1, ((g["yF0"] + g["yF1"]) - (g["yR0"] + g["yR1"])) / 2, g["z1"] - zbelt - 0.02)))
        for name, c, s in boxes:
            mb = MeshBuilder(name)
            bmesh.ops.create_cube(mb.bm, size=1.0, matrix=Matrix.LocRotScale(Vector(c), None, Vector(s)))
            o = mb.finish(self.cols["Collision"])
            o.display_type = "WIRE"
            o.hide_render = True
            objs.append(o)
        return objs


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


def main():
    a = parse()
    spec = CarSpec(SPECS[a.car])
    C = spec.id
    out = os.path.abspath(a.out)
    if "Incoming" in out.split(os.sep):
        raise SystemExit("Refusing to write into Incoming.")
    if os.path.exists(out) and not a.overwrite:
        out = pc.next_version(out)
    pc.reset_scene()
    B = Builder(spec, a.texture)
    A = spec.atlas

    shell = B.shell()
    gh, glass = B.glasshouse()
    fl = B.flares()
    vb.project_uvs(shell, A)
    vb.project_uvs(gh, A)
    open_cage = None
    if glass is None:          # open cockpit: the frame is an exterior part, not body
        open_cage = gh
        parts = [shell]
    else:
        parts = [shell, gh]
    if fl:
        vb.project_uvs(fl, A, force_side=True)
        parts.append(fl)
    body = vb.join(parts, f"{C}_Body") if len(parts) > 1 else parts[0]
    body.name = body.data.name = f"{C}_Body"
    exterior = B.exterior()
    exterior += build_extras.build(B)
    if open_cage is not None:
        exterior.append(open_cage)
    lights = B.lights()
    interior = B.interior()
    steering = B.steering()
    wheels = B.wheels()
    colliders = B.collision()

    bpy.context.view_layer.update()
    for o in [x for x in [glass, lights, interior, steering] + exterior if x is not None]:
        vb.project_uvs(o, A)
    trim = bpy.data.objects[f"{C}_Trim"]
    me = trim.data
    uv = me.uv_layers["UVMap"]
    pidx = list(me.materials).index(B.M["plate"]) if B.M["plate"].name in [m.name for m in me.materials] else -1
    for poly in me.polygons:
        if poly.material_index != pidx:
            continue
        front = poly.center.y > 0
        zc = next(z for f, z in B.plates if f == front)
        u0, v0, u1, v1 = A.PLATE_FRONT_RECT if front else A.PLATE_REAR_RECT
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            tx = (co.x + 0.26) / 0.52
            if front:
                tx = 1 - tx
            tz = (co.z - (zc - 0.0575)) / 0.115
            uv.data[li].uv = (u0 + (u1 - u0) * tx, v0 + (v1 - v0) * tz)
    for o in [x for x in [body, glass, lights, interior, steering] + exterior if x is not None]:
        if not o.name.endswith("_SpareWheel"):
            vb.smooth_by_angle(o, 32)

    root = bpy.data.objects.new(f"{C}_Root", None)
    root.empty_display_type = "ARROWS"
    for k, v in (("car_id", C), ("display_name", spec.name), ("vehicle_class", spec.d["class"]),
                 ("drivetrain", spec.d.get("drivetrain", "AWD")), ("forward_axis", "+Y"), ("up_axis", "+Z"),
                 ("units", "m")):
        root[k] = v
    B.cols["Root"].objects.link(root)
    groups = {}
    for gname in ("Exterior", "Wheels"):
        e = bpy.data.objects.new(f"{C}_{gname}", None)
        e.empty_display_size = 0.2
        B.cols[gname].objects.link(e)
        e.parent = root
        groups[gname] = e
    for o in (body, glass, lights, interior):
        if o is not None:
            o.parent = root
    steering.parent = interior
    for o in exterior:
        o.parent = groups["Exterior"]
    for slot, (piv, _p) in wheels.items():
        piv.parent = groups["Wheels"]
    for o in colliders:
        o.parent = root

    bpy.context.view_layer.update()
    vis = [o for o in B.cols["Root"].all_objects if o.type == "MESH"]
    bb = pc.world_bbox(vis)
    off = Vector((bb["center"][0], bb["center"][1], 0.0))
    T = Matrix.Translation(-off)
    for o in list(B.cols["Root"].all_objects) + colliders:
        if o in groups.values() or o is root:
            continue
        if o.parent is root or o.parent in groups.values():
            if o.type == "MESH":
                o.data.transform(T)
            else:
                o.location -= off
    steering.location -= off
    # camera anchors for the game (Blender space, after recentring): driver's eye and bonnet camera
    sp_ = B.steer_pos
    eye = Vector((sp_[0], sp_[1] - 0.46, sp_[2] + 0.31)) - off
    g_ = spec.gh
    hood = Vector((0.0, g_["yF0"] + 0.30, max(1.25, spec.zbelt_at(g_["yF0"]) + 0.45))) - off
    root["driver_eye"] = [round(v, 4) for v in eye]
    root["hood_cam"] = [round(v, 4) for v in hood]
    bpy.context.view_layer.update()
    bb = pc.world_bbox(vis)

    objs, total = [], 0
    for o in sorted(B.cols["Root"].all_objects, key=lambda o: o.name):
        t = vb.tri_count(o)
        total += t
        objs.append({"name": o.name, "type": o.type, "parent": o.parent.name if o.parent else None, "tris": t,
                     "materials": [m.name for m in o.data.materials] if o.type == "MESH" else []})
    wheel_info = {slot: {"blender": [round(x, 4) for x in piv.matrix_world.translation],
                         "unity": pc.blender_to_unity(piv.matrix_world.translation)}
                  for slot, (piv, _p) in wheels.items()}
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.context.scene.render.engine = "CYCLES"
    bpy.ops.wm.save_as_mainfile(filepath=out, compress=True)
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_as_mainfile(filepath=out, compress=True)
    pc.write_json(a.report, {
        "car_id": C, "display_name": spec.name, "tool": "build_generic.py", "generated_utc": pc.now_iso(),
        "blender": pc.blender_info(), "saved_file": out, "reference_class": spec.d.get("reference"),
        "design_to_final_offset": [round(x, 4) for x in off], "bbox": bb, "total_tris": total,
        "wheels": wheel_info, "wheel_radius": spec.wheel_r, "tire_width": spec.tire_w,
        "materials": sorted(m.name for m in bpy.data.materials if m.users), "objects": objs})
    print(f"Saved {out}\nTotal tris: {total}  bbox size: {[round(x, 3) for x in bb['size']]}")


if __name__ == "__main__":
    main()
