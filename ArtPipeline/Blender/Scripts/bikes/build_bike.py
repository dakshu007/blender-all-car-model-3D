"""build_bike.py - build one of the Blossom Rally motorbikes described in bike_specs.py.

    blender -b -P build_bike.py -- --bike BIKE-001 \
        --texture ../../../VehicleLibrary/BIKE-001/Textures/BIKE-001_BaseColor.png \
        --out ../../../VehicleLibrary/BIKE-001/Working/BIKE-001_Working_v001.blend \
        --report ../../Reports/BIKE-001_build.json [--overwrite]

Same conventions as cars/build_generic.py (so export_vehicle_fbx.py / validate_vehicle.py / the game read it
the same way), adapted to two wheels:
    <BIKE>_Root > Body (painted panels), Glass, Lights, Fork (steers: origin on the steering axis),
                  Exterior(empty) > Frame (frame, engine, exhaust, swingarm, seat, pegs, plate ...),
                  Wheels(empty) > Wheel_F / Wheel_R (empty at the axle centre, x = 0) > _Tire, _Rim
    <BIKE>_Collision collection (not exported in the visual FBX)
Root custom properties: two_wheeler, wheel_slots "F,R", steer_rake (deg), driver_eye, hood_cam and the rider
anchors rider_seat / rider_grip (right grip) / rider_peg (right peg) - all in Blender space.
Clean low-poly: boxes, 6-12 sided tubes and lofted superellipse sections, flat-shaded by angle like the cars.
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
from bike_specs import BIKES, BikeAtlas, rake_axis  # noqa: E402
from vehicle_builder import MeshBuilder  # noqa: E402


def parse():
    p = argparse.ArgumentParser()
    p.add_argument("--bike", required=True)
    p.add_argument("--texture", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--report", required=True)
    p.add_argument("--overwrite", action="store_true")
    return p.parse_args(pc.script_args())


# ------------------------------------------------------------------------------------------ geometry helpers
def spow(c, e):
    return math.copysign(abs(c) ** e, c)


def ring(y, cz, hw, ht, hb=None, p=2.6, n=16, cx=0.0, pb=None, tilt=0.0):
    """Superellipse section at y (centre x=cx, z=cz): half width hw, half heights up (ht) / down (hb).
    p = squareness (2 = ellipse, 4 = rounded box); pb = squareness of the lower half. tilt shears the
    section along y with height (positive = the top leans forward)."""
    hb = ht if hb is None else hb
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        ca, sa = math.cos(a), math.sin(a)
        e = 2.0 / (p if (sa >= 0 or pb is None) else pb)
        x = cx + hw * spow(ca, e)
        dz = (ht if sa >= 0 else hb) * spow(sa, e)
        pts.append((x, y + tilt * dz, cz + dz))
    return pts


def loft(mb, secs, mat, n=16, cap=True):
    """secs: dicts with keys y, z, w, t (top), b (bottom), p, pb, cx, tilt."""
    rings = [ring(s["y"], s["z"], s["w"], s["t"], s.get("b"), s.get("p", 2.6), n, s.get("cx", 0.0), s.get("pb"),
                  s.get("tilt", 0.0)) for s in secs]
    return mb.loft(rings, mat, cap=cap)


def arc_strip(mb, cy, cz, r, w, a0, a1, t, mat, seg=12, x=0.0, lip=0.6):
    """Curved fender / mudguard: a U-profile swept around the axle (angles from +Y toward +Z, degrees)."""
    prof = [(-w / 2, 0.0), (-w / 2, t * lip), (-w * 0.32, t), (w * 0.32, t), (w / 2, t * lip), (w / 2, 0.0)]
    rings = []
    for i in range(seg + 1):
        a = math.radians(a0 + (a1 - a0) * i / seg)
        ca, sa = math.cos(a), math.sin(a)
        rings.append([(x + dx, cy + (r + dr) * ca, cz + (r + dr) * sa) for dx, dr in prof])
    return mb.loft(rings, mat, cap=True)


def bar(mb, p1, p2, wx, wz, mat, top_scale=None):
    """Box from p1 to p2 (any direction), cross-section wx (along the local x) by wz."""
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    q = Vector((0, 1, 0)).rotation_difference(d.normalized())
    return mb.box((p1 + p2) / 2, (wx, d.length, wz), mat, rot=q, top_scale=top_scale)


def quad(mb, pts, mat):
    vs = [mb.bm.verts.new(Vector(p)) for p in pts]
    f = mb.bm.faces.new(vs)
    f.material_index = mb.mi(mat)
    return f


def V(*a):
    return Vector(a)


# ---------------------------------------------------------------------------------------------- builder
class BikeBuilder:
    def __init__(self, bid, spec, tex):
        self.C = bid
        self.d = spec
        self.kind = spec["kind"]
        self.A = BikeAtlas()
        self.cols = self.collections()
        self.M = self.materials(tex)
        wb = spec["wb"]
        self.yr, self.yf = -wb / 2, wb / 2
        self.wf, self.wr = spec["wheel_f"], spec["wheel_r"]
        self.Af = V(0, self.yf, self.wf["R"])
        self.Ar = V(0, self.yr, self.wr["R"])
        self.u = V(*rake_axis(spec["rake"]))                       # up the steering axis
        self.n = V(0, math.cos(math.radians(spec["rake"])), math.sin(math.radians(spec["rake"])))  # forward normal
        self.H = self.Af + self.u * spec["fork_len"]               # top clamp centre (fork pivot)
        self.body = MeshBuilder(f"{bid}_Body")
        self.frame = MeshBuilder(f"{bid}_Frame")
        self.fork = MeshBuilder(f"{bid}_Fork")
        self.lights = MeshBuilder(f"{bid}_Lights")
        self.glass = MeshBuilder(f"{bid}_Glass")
        self.anchor = {}
        self.plate_quads = []     # (mesh builder, centre) - plate faces get the plate rect UVs

    # ---------------------------------------------------------------- setup
    def collections(self):
        root = bpy.data.collections.new(self.C)
        bpy.context.scene.collection.children.link(root)
        subs = {"Root": root}
        for n in ("Body", "Exterior", "Glass", "Lights", "Fork", "Wheels"):
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
        fork_col = {"gold": "#D9A43A", "chrome": "#D4D2CE", "black": "#2A2A2D"}[self.d.get("fork", "chrome")]
        return {
            "paint": vb.material(f"M_{C}_Paint", self.d["livery"]["base"], rough=0.42, image=img, specular=0.4),
            "plate": vb.material(f"M_{C}_Plate", "#F4F1E8", rough=0.5, image=img),
            "trim": vb.material("M_Trim_Black", "#1E1D1F", rough=0.55),
            "trim_grey": vb.material("M_Trim_Grey", "#4A4744", rough=0.5),
            "frame": vb.material(f"M_Trim_Frame_{C}", c["frame"], rough=0.45),
            "engine": vb.material(f"M_Trim_Engine_{C}", c["engine"], rough=0.5),
            "alu": vb.material("M_Trim_Alu", "#B9BBBE", rough=0.35),
            "white": vb.material("M_Trim_Whitewall", "#F1EBDD", rough=0.6),
            "chrome": vb.material("M_Chrome", "#D4D2CE", rough=0.22, metal=1.0),
            "stanchion": vb.material(f"M_Trim_Fork_{C}", fork_col, rough=0.3),
            "glass": vb.material("M_Glass_Tint", "#0B1013", rough=0.05, alpha=0.86, specular=0.6),
            "tire": vb.material("M_Tire_Rubber", "#232120", rough=0.92, specular=0.25),
            "rim": vb.material(f"M_{C}_Rim", c["rim"], rough=0.36, metal=0.85),
            "disc": vb.material("M_Brake_Disc", "#6A6764", rough=0.45, metal=0.8),
            "head": vb.material("M_Light_Head", "#F3EFE3", rough=0.15, emission="#FFF2D2", emission_strength=1.5),
            "aux": vb.material("M_Light_Aux", "#F6EBC4", rough=0.15, emission="#FFE7A0", emission_strength=2.0),
            "tail": vb.material("M_Light_Tail", "#A3171B", rough=0.2, emission="#FF2A1A", emission_strength=0.8),
            "ind": vb.material("M_Light_Indicator", "#E07A1A", rough=0.2, emission="#FF8A1A", emission_strength=0.6),
            "seat": vb.material(f"M_{C}_Seat", c["seat"], rough=0.8),
            "accent": vb.material(f"M_{C}_Accent", c["accent"], rough=0.6),
            "accent2": vb.material(f"M_Trim_Accent2_{C}", c["accent2"], rough=0.55),
        }

    # ---------------------------------------------------------------- shared parts
    def on_axis(self, s, fwd=0.0, x=0.0):
        """Point s metres up the steering axis from the front axle, fwd in front of it, x to the side."""
        return self.Af + self.u * s + self.n * fwd + V(x, 0, 0)

    def fork_legs(self, spacing, r_up, r_low, low_len, usd=False, lower_mat="trim", gaiter=False):
        M, mb = self.M, self.fork
        L = self.d["fork_len"]
        q = V(0, 0, 1).rotation_difference(self.u)
        for sx in (-1, 1):
            x = sx * spacing
            a = self.on_axis(-0.03, 0.0, x)
            m = self.on_axis(low_len, 0.0, x)
            t = self.on_axis(L + 0.025, 0.0, x)
            if usd:   # upside-down: thin stanchion below, thick tube above
                mb.cyl(a, m, r_low, M["stanchion"], seg=10)
                mb.cyl(m - self.u * 0.04, t, r_up, M[lower_mat], seg=12)
            else:
                mb.cyl(a, m, r_low, M[lower_mat], seg=10)
                mb.cyl(m - self.u * 0.03, t, r_up, M["stanchion"], seg=10)
                if gaiter:
                    mb.cyl(m - self.u * 0.01, m + self.u * 0.12, r_up * 1.35, M["trim"], seg=10)
            # axle lug
            mb.box(self.on_axis(0.0, 0.0, x), (0.03, 0.06, 0.06), M[lower_mat], rot=q)
        w = 2 * spacing + 0.07
        mb.box(self.on_axis(L, 0.0), (w, 0.08, 0.035), M["alu"], rot=q)                 # top clamp
        mb.box(self.on_axis(L - 0.15, 0.0), (w, 0.09, 0.04), M["alu"], rot=q)           # bottom clamp
        mb.cyl(self.on_axis(0.0, 0.0, -spacing - 0.02), self.on_axis(0.0, 0.0, spacing + 0.02), 0.012, M["alu"], seg=6)

    def caliper(self, x, radius, both=False):
        """Front brake caliper(s) behind the fork leg, on the disc."""
        M = self.M
        q = V(0, 0, 1).rotation_difference(self.u)
        for sx in ((-1, 1) if both else (1,)):
            c = self.Af + self.u * (radius * 0.55) - self.n * (radius * 0.62) + V(sx * x, 0, 0)
            self.fork.box(c, (0.035, 0.06, 0.09), M["trim_grey"], rot=q)

    def steering_head(self, r=0.038, mat="frame"):
        L = self.d["fork_len"]
        self.frame.cyl(self.on_axis(L - 0.20), self.on_axis(L - 0.02), r, self.M[mat], seg=10)

    def handlebar(self, pts_right, r=0.011, mat="trim", grip_len=0.11, lever=True, riser=0.0):
        """Symmetric bar through the points given for the right half (from the centre outwards)."""
        M = self.M
        pts = [V(-p[0], p[1], p[2]) for p in reversed(pts_right)] + [V(*p) for p in pts_right]
        self.fork.tube_path(pts, r, M[mat], seg=8)
        if riser > 0:
            for sx in (-1, 1):
                b = V(sx * abs(pts_right[0][0]) + sx * 0.04, pts_right[0][1], pts_right[0][2])
                self.fork.cyl(b - V(0, 0, riser), b, r * 1.4, M[mat], seg=8)
        for sx in (-1, 1):
            e, f = pts[-1] if sx > 0 else pts[0], pts[-2] if sx > 0 else pts[1]
            d = (e - f).normalized()
            g0 = e - d * grip_len
            self.fork.cyl(g0, e + d * 0.008, r * 2.1, M["trim"], seg=8)
            if lever:
                lv = g0 - d * 0.02 + V(0, 0.04, 0.0)
                self.fork.cyl(g0 - d * 0.03, lv + d * 0.13 + V(0, 0.03, -0.01), 0.006, M["alu"], seg=4)
                self.fork.box(g0 - d * 0.035, (0.045, 0.04, 0.035), M["trim"])
        right = pts[-1]
        d = (pts[-1] - pts[-2]).normalized()
        self.anchor["rider_grip"] = right - d * grip_len * 0.5

    def mirrors(self, mb, base_pts, stalk=0.20, head=(0.11, 0.025, 0.07), mat="trim", round_=False):
        M = self.M
        for bp in base_pts:
            for sx in (-1, 1):
                b = V(sx * bp[0], bp[1], bp[2])
                tip = b + V(sx * 0.08, -0.02, stalk)
                mb.cyl(b, tip, 0.007, M["alu"] if mat == "chrome" else M["trim"], seg=4)
                if round_:
                    mb.cyl(tip + V(0, 0.012, 0), tip - V(0, 0.012, 0), head[0] / 2, M[mat], seg=12)
                    mb.cyl(tip - V(0, 0.010, 0), tip - V(0, 0.014, 0), head[0] / 2 * 0.85, M["chrome"], seg=12)
                else:
                    mb.box(tip + V(sx * 0.02, 0, 0.01), head, M[mat], top_scale=(0.8, 1.0))

    def round_lamp(self, mb, c, r, depth=0.10, shell="trim", bezel="chrome", facing=(0, 1, 0), lens="head"):
        c, f = V(*c), V(*facing).normalized()
        mb.cyl(c - f * depth, c, r * 1.02, self.M[shell], seg=16, r2=r * 1.08)
        mb.cyl(c - f * 0.004, c + f * 0.010, r * 1.10, self.M[bezel], seg=16)
        mb.cyl(c, c + f * 0.016, r * 0.90, self.M[lens], seg=16)

    def indicator(self, mb, c, side, facing=1):
        c = V(*c)
        mb.cyl(c, c + V(side * 0.05, 0, 0), 0.006, self.M["trim"], seg=4)
        e = c + V(side * 0.06, 0, 0)
        mb.cyl(e - V(0, facing * 0.02, 0), e + V(0, facing * 0.02, 0), 0.016, self.M["ind"], seg=8, r2=0.010)

    def tail_light(self, c, size=(0.12, 0.03, 0.04), indicators_y=None, ind_x=0.07):
        c = V(*c)
        self.lights.box(c, size, self.M["tail"])
        if indicators_y is not None:
            for sx in (-1, 1):
                self.indicator(self.lights, (sx * ind_x, indicators_y[0], indicators_y[1]), sx, facing=-1)

    def plate(self, c, w=0.17, h=0.11, tilt=12.0, bracket_to=None):
        """Rear number plate facing back (-Y) with a black backing and an optional hanger."""
        M = self.M
        c = V(*c)
        rot = Euler((math.radians(-tilt), 0, 0)).to_quaternion()
        self.frame.box(c + V(0, 0.006, 0), (w + 0.012, 0.008, h + 0.012), M["trim"], rot=rot)
        hw, hh = w / 2, h / 2
        pts = [rot @ V(x, 0, z) + c for x, z in ((hw, -hh), (-hw, -hh), (-hw, hh), (hw, hh))]
        quad(self.frame, pts, M["plate"])
        if bracket_to is not None:
            bar(self.frame, c + V(0, 0.01, hh * 0.6), V(*bracket_to), 0.05, 0.012, M["trim"])

    def pegs(self, y, z, x=0.19, mat="alu"):
        for sx in (-1, 1):
            self.frame.cyl(V(sx * (x - 0.07), y, z), V(sx * x, y, z), 0.012, self.M[mat], seg=6)
            self.frame.box(V(sx * (x - 0.08), y + 0.03, z + 0.03), (0.02, 0.10, 0.06), self.M["trim_grey"])
        self.anchor["rider_peg"] = V(x - 0.035, y, z)

    def swingarm(self, pivot, w, h=0.06, mat="alu", chain=True, shocks=None, shock_top=None, mono_to=None):
        M = self.M
        piv = V(*pivot)
        ax = self.Ar
        for sx in (-1, 1):
            x = sx * w
            bar(self.frame, V(x, piv.y, piv.z), V(x, ax.y - 0.03, ax.z), 0.035, h, M[mat], top_scale=(1.0, 0.75))
            self.frame.box(V(x, ax.y, ax.z), (0.04, 0.07, 0.05), M[mat])
        self.frame.cyl(V(-w - 0.02, piv.y, piv.z), V(w + 0.02, piv.y, piv.z), 0.025, M["trim_grey"], seg=8)
        self.frame.cyl(V(-w - 0.02, ax.y, ax.z), V(w + 0.02, ax.y, ax.z), 0.012, M["alu"], seg=6)
        if chain:   # chain run on the left between the sprockets
            xs = -w + 0.035
            front = V(xs, piv.y + 0.06, piv.z - 0.02)
            for dz in (0.045, -0.045):
                bar(self.frame, front + V(0, 0, dz * 0.7), V(xs, ax.y, ax.z + dz * 1.9), 0.012, 0.014, M["trim_grey"])
        if shocks:  # twin shocks (classic / cruiser)
            for sx in (-1, 1):
                lo = V(sx * (w + 0.03), ax.y + 0.05, ax.z + 0.02)
                hi = V(sx * (w + 0.03), shocks[0], shocks[1])
                self.frame.cyl(lo, hi, 0.022, M["chrome"], seg=8)
                self.frame.cyl(lo + (hi - lo) * 0.2, lo + (hi - lo) * 0.8, 0.032, M[shocks[2]], seg=8)
        if mono_to is not None:
            lo = piv + (ax - piv) * 0.3 + V(0, 0, 0.04)
            hi = V(*mono_to)
            self.frame.cyl(lo, hi, 0.018, M["trim_grey"], seg=8)
            self.frame.cyl(lo + (hi - lo) * 0.25, lo + (hi - lo) * 0.75, 0.034, M["accent"], seg=8)

    def exhaust(self, pts, r, can_from, can_to, can_r, pipe_mat="chrome", can_mat="chrome", tip_mat="trim_grey"):
        M = self.M
        self.frame.tube_path([V(*p) for p in pts], r, M[pipe_mat], seg=8)
        a, b = V(*can_from), V(*can_to)
        self.frame.cyl(a, b, can_r, M[can_mat], seg=12, r2=can_r * 0.95)
        d = (b - a).normalized()
        self.frame.cyl(b, b + d * 0.02, can_r * 0.65, M[tip_mat], seg=12)
        self.frame.cyl(a - d * 0.04, a, can_r * 0.7, M[can_mat], seg=12, r2=can_r)

    def fins(self, c, size, n, axis_tilt=0.0, mat="engine", fin_mat="alu"):
        """Air-cooled cylinder: a stack of fins (boxes) along a tilted axis in the side plane."""
        c = V(*c)
        q = Euler((math.radians(axis_tilt), 0, 0)).to_quaternion()
        w, d, h = size
        self.frame.box(c, (w * 0.7, d * 0.7, h), self.M[mat], rot=q)
        for i in range(n):
            z = -h / 2 + h * (i + 0.5) / n
            self.frame.box(c + q @ V(0, 0, z), (w, d, h / n * 0.45), self.M[fin_mat], rot=q)

    def seat(self, secs, mat="seat"):
        loft(self.frame, secs, self.M[mat], n=16)

    # ---------------------------------------------------------------- wheels
    def tire_mesh(self, name, W):
        M = self.M
        R, w, rim = W["R"], W["w"], W["rim"]
        style = self.d.get("tyre", "road")
        h = R - rim
        knob = 0.016 if style == "knobby" else 0.009 if style == "semi" else 0.0
        Rt = R - knob
        half = [(0.40 * w, rim + 0.004), (0.50 * w, rim + 0.28 * h), (0.48 * w, rim + 0.58 * h),
                (0.38 * w, rim + 0.82 * h), (0.21 * w, Rt - 0.035 * h), (0.0, Rt)]
        if style == "scooter":
            half = [(0.42 * w, rim + 0.004), (0.50 * w, rim + 0.35 * h), (0.47 * w, rim + 0.72 * h),
                    (0.33 * w, Rt - 0.06 * h), (0.15 * w, Rt - 0.01 * h), (0.0, Rt)]
        prof = half + [(-a, r) for (a, r) in reversed(half[:-1])]
        tb = MeshBuilder(name)
        seg = 36
        edge_mats = None
        if style == "whitewall":
            edge_mats = [None, M["white"], None, None, None, None, None, None, M["white"], None]
        grid = tb.revolve(prof, seg, M["tire"], edge_mats=edge_mats)
        if knob > 0:
            blocks = []
            ne = len(prof) - 1
            mid = ne // 2
            for i in range(seg):
                if style == "knobby":
                    if i % 2 == 0:
                        blocks += [grid[i][mid - 1], grid[i][mid], grid[i][1], grid[i][ne - 2]]
                    else:
                        blocks += [grid[i][mid - 2], grid[i][mid + 1]]
                else:
                    blocks += [grid[i][mid - 1] if i % 2 == 0 else grid[i][mid]]
                    if i % 3 == 0:
                        blocks += [grid[i][mid - 2], grid[i][mid + 1]]
            blocks = list(dict.fromkeys(blocks))
            res = bmesh.ops.extrude_discrete_faces(tb.bm, faces=blocks)
            for f in res["faces"]:
                c = f.calc_center_median()
                radial = V(0, c.y, c.z).normalized()
                for v in f.verts:
                    v.co += radial * knob
        vb.orient_faces(tb.bm, list(tb.bm.faces), lambda f: f.calc_center_median())
        return tb

    def rim_mesh(self, name, W, front):
        M = self.M
        R, w, rim = W["R"], W["w"], W["rim"]
        style = W["rim_style"]
        rb = MeshBuilder(name)
        hw = 0.40 * w
        rb.revolve([(-hw, rim - 0.022), (hw, rim - 0.022), (hw, rim + 0.006), (hw * 0.8, rim + 0.006),
                    (hw * 0.8, rim - 0.006), (-hw * 0.8, rim - 0.006), (-hw * 0.8, rim + 0.006), (-hw, rim + 0.006)],
                   32, M["rim"], closed=True)
        hub_r = 0.05 if style[0] != "disc" else 0.045
        ax = 0.5 * w + 0.012          # symmetric spindle so the wheel's bbox stays centred on its pivot
        rb.cyl((-ax, 0, 0), (ax, 0, 0), 0.016, M["alu"], seg=8)
        rb.cyl((-0.045, 0, 0), (0.045, 0, 0), hub_r, M["rim"] if style[0] != "spoke" else M["alu"], seg=14)
        if style[0] == "cast":
            n = style[1]
            for i in range(n):
                for off in (-7.0, 7.0):
                    a = 2 * math.pi * i / n
                    q = Euler((a + math.radians(off) * 0.6, 0, 0)).to_quaternion()
                    q2 = Euler((a + math.radians(off), 0, 0)).to_quaternion()
                    mid = (hub_r + rim - 0.02) / 2
                    rb.box(q2 @ V(0, 0, mid), (0.022, 0.018, rim - 0.02 - hub_r + 0.01), M["rim"], rot=q,
                           top_scale=(0.8, 1.6))
        elif style[0] == "disc":
            n = style[1]
            rb.revolve([(0.02, rim - 0.02), (0.03, rim * 0.6), (0.034, hub_r), (0.024, hub_r), (0.012, rim - 0.02)],
                       28, M["rim"], closed=True)
            for i in range(n):
                a = 2 * math.pi * (i + 0.5) / n
                c = V(0.034, (hub_r + rim) * 0.48 * math.cos(a), (hub_r + rim) * 0.48 * math.sin(a))
                rb.cyl(c, c + V(0.004, 0, 0), 0.016, M["trim"], seg=10)
        else:   # wire spokes, cross-laced from both hub flanges
            n = style[1]
            for i in range(n):
                a = 2 * math.pi * i / n
                side = 1 if i % 2 == 0 else -1
                twist = math.radians(18) * (1 if (i // 2) % 2 == 0 else -1)
                h0 = V(side * 0.04, 0.042 * math.cos(a + twist), 0.042 * math.sin(a + twist))
                h1 = V(side * 0.006, (rim - 0.02) * math.cos(a), (rim - 0.02) * math.sin(a))
                rb.cyl(h0, h1, 0.0032, M["chrome"], seg=4, cap=False)
            for sx in (-1, 1):
                rb.cyl((sx * 0.036, 0, 0), (sx * 0.044, 0, 0), 0.05, M["alu"], seg=14)
        # brake disc(s) and the rear sprocket
        discs = (1,) if not front else ((-1, 1) if self.kind in ("sport", "naked", "adventure", "cruiser") else (1,))
        dr = min(rim - 0.035, 0.16 if front else 0.12)
        if self.kind == "scooter":
            dr = 0.10
        for sx in discs:   # floating disc: an annulus on a small carrier, so the spokes stay visible
            x0, x1 = sorted((sx * 0.040, sx * 0.046))
            rb.revolve([(x0, dr * 0.72), (x1, dr * 0.72), (x1, dr), (x0, dr)], 28, M["disc"], closed=True)
            for k in range(5):
                q = Euler((2 * math.pi * k / 5, 0, 0)).to_quaternion()
                rb.box(q @ V(sx * 0.043, 0, dr * 0.5), (0.006, 0.014, dr * 0.5), M["trim_grey"], rot=q)
        if not front and self.kind != "scooter":
            rb.cyl((-0.042, 0, 0), (-0.036, 0, 0), min(0.11, rim - 0.04), M["trim_grey"], seg=20)
        bmesh.ops.recalc_face_normals(rb.bm, faces=rb.bm.faces)
        return rb

    def wheels(self):
        out = {}
        for slot, W, A in (("F", self.wf, self.Af), ("R", self.wr, self.Ar)):
            piv = bpy.data.objects.new(f"{self.C}_Wheel_{slot}", None)
            piv.empty_display_type = "CIRCLE"
            piv.empty_display_size = W["R"]
            piv.location = A
            piv["wheel_slot"] = slot
            piv["steer"] = slot == "F"
            piv["drive"] = slot == "R"
            piv["radius"] = W["R"]
            piv["width"] = W["w"]
            self.cols["Wheels"].objects.link(piv)
            parts = []
            for kind in ("Tire", "Rim"):
                mb = self.tire_mesh(f"{self.C}_Wheel_{slot}_Tire", W) if kind == "Tire" else \
                    self.rim_mesh(f"{self.C}_Wheel_{slot}_Rim", W, slot == "F")
                o = mb.finish(self.cols["Wheels"], recalc=False)
                o.parent = piv
                vb.smooth_by_angle(o, 40 if kind == "Tire" else 35)
                o.data.uv_layers.new(name="UVMap")
                parts.append(o)
            out[slot] = (piv, parts)
        return out

    # ================================================================ the six designs
    def build(self):
        getattr(self, "build_" + self.kind)()

    # ---------------------------------------------------------------- sport bike
    def build_sport(self):
        M, B, F, K = self.M, self.body, self.frame, self.fork
        L = self.d["fork_len"]
        H = self.H
        self.fork_legs(0.095, 0.030, 0.022, 0.30, usd=True, lower_mat="trim")
        self.caliper(0.095, 0.16, both=True)
        arc_strip(K, self.Af.y, self.Af.z, self.wf["R"] + 0.03, 0.13, 30, 125, 0.012, M["paint"], seg=10)
        # clip-on bars under the top clamp
        cb = self.on_axis(L - 0.05)
        self.handlebar([(0.09, cb.y, cb.z), (0.20, cb.y - 0.06, cb.z - 0.02), (0.31, cb.y - 0.12, cb.z - 0.03)],
                       r=0.012, grip_len=0.10)
        self.steering_head()
        # twin-spar frame from the steering head to the swingarm pivot
        sh = self.on_axis(L - 0.12)
        piv = V(0, -0.14, 0.44)
        for sx in (-1, 1):
            bar(F, V(sx * 0.09, sh.y, sh.z), V(sx * 0.14, 0.10, 0.70), 0.05, 0.11, M["frame"])
            bar(F, V(sx * 0.14, 0.10, 0.70), V(sx * 0.13, piv.y + 0.02, piv.z + 0.06), 0.05, 0.13, M["frame"])
            bar(F, V(sx * 0.12, -0.12, 0.74), V(sx * 0.08, -0.80, 0.86), 0.025, 0.03, M["trim_grey"])   # subframe
        # inline four (mostly behind the fairing) + radiator
        F.box(V(0, 0.02, 0.33), (0.34, 0.42, 0.22), M["engine"])
        F.box(V(0, 0.16, 0.53), (0.36, 0.20, 0.26), M["engine"], rot=Euler((math.radians(-28), 0, 0)).to_quaternion())
        F.box(V(0, 0.33, 0.55), (0.40, 0.04, 0.30), M["trim"], rot=Euler((math.radians(-14), 0, 0)).to_quaternion())
        F.box(V(0.19, 0.0, 0.32), (0.03, 0.16, 0.14), M["alu"])      # clutch cover
        F.cyl(V(-0.18, 0.0, 0.30), V(-0.20, 0.0, 0.30), 0.07, M["alu"], seg=14)
        self.swingarm((0, piv.y, piv.z), 0.105, h=0.075, mono_to=(0, -0.20, 0.70))
        # exhaust: headers under the engine to a short angular can on the right
        self.exhaust([(0.06, 0.30, 0.42), (0.07, 0.30, 0.24), (0.06, 0.10, 0.17), (0.08, -0.20, 0.19), (0.15, -0.34, 0.30)],
                     0.026, (0.16, -0.36, 0.32), (0.17, -0.66, 0.50), 0.055, pipe_mat="trim_grey", can_mat="trim_grey",
                     tip_mat="chrome")
        # bodywork: tank, fairing (upper + lower), belly pan, tail unit
        loft(B, [dict(y=-0.10, z=0.86, w=0.12, t=0.05, b=0.05), dict(y=0.02, z=0.92, w=0.17, t=0.09, b=0.10),
                 dict(y=0.20, z=0.94, w=0.18, t=0.08, b=0.12), dict(y=0.34, z=0.90, w=0.14, t=0.06, b=0.10)],
             M["paint"])
        loft(B, [dict(y=-0.02, z=0.50, w=0.15, t=0.17, b=0.22, p=3.4),
                 dict(y=0.16, z=0.53, w=0.195, t=0.25, b=0.27, p=3.4),
                 dict(y=0.36, z=0.64, w=0.205, t=0.22, b=0.34, p=3.2),
                 dict(y=0.52, z=0.78, w=0.18, t=0.17, b=0.26, p=3.0, tilt=-0.3),
                 dict(y=0.66, z=0.82, w=0.12, t=0.10, b=0.16, p=2.6, tilt=-0.4),
                 dict(y=0.79, z=0.79, w=0.05, t=0.04, b=0.06, p=2.2)], M["paint"])
        loft(B, [dict(y=-0.30, z=0.22, w=0.07, t=0.05, b=0.04, p=3.0), dict(y=-0.10, z=0.19, w=0.15, t=0.08, b=0.06, p=3.0),
                 dict(y=0.12, z=0.19, w=0.17, t=0.09, b=0.06, p=3.0), dict(y=0.34, z=0.27, w=0.15, t=0.08, b=0.05, p=3.0)],
             M["paint"])
        loft(B, [dict(y=-0.10, z=0.76, w=0.13, t=0.05, b=0.07, p=3.0), dict(y=-0.40, z=0.80, w=0.14, t=0.07, b=0.09, p=3.0),
                 dict(y=-0.62, z=0.85, w=0.11, t=0.07, b=0.08, p=2.8), dict(y=-0.82, z=0.90, w=0.06, t=0.04, b=0.05, p=2.4),
                 dict(y=-0.90, z=0.92, w=0.03, t=0.02, b=0.025, p=2.2)], M["paint"])
        # windscreen
        q = Euler((math.radians(-58), 0, 0)).to_quaternion()
        loft(self.glass, [dict(y=0.50, z=1.00, w=0.17, t=0.012, b=0.012, p=2.0, tilt=0.0),
                          dict(y=0.42, z=1.08, w=0.14, t=0.010, b=0.010, p=2.0),
                          dict(y=0.34, z=1.12, w=0.09, t=0.008, b=0.008, p=2.0)], M["glass"], n=12)
        # headlights in the nose + nose intake
        for sx in (-1, 1):
            self.lights.box(V(sx * 0.065, 0.76, 0.85), (0.12, 0.05, 0.05), M["head"],
                            rot=Euler((0, math.radians(-sx * 18), math.radians(sx * 20))).to_quaternion())
            self.indicator(self.lights, (sx * 0.19, 0.62, 0.86), sx)
        F.box(V(0, 0.84, 0.74), (0.07, 0.04, 0.05), M["trim"])
        self.mirrors(F, [(0.20, 0.55, 0.98)], stalk=0.05, head=(0.10, 0.03, 0.055))
        self.seat([dict(y=-0.06, z=0.84, w=0.13, t=0.025, b=0.03, p=3.2), dict(y=-0.24, z=0.855, w=0.15, t=0.03, b=0.03, p=3.2),
                   dict(y=-0.44, z=0.875, w=0.13, t=0.03, b=0.03, p=3.2)])
        self.tail_light((0, -0.905, 0.91), size=(0.06, 0.03, 0.03))
        self.plate((0, -0.86, 0.64), tilt=20, bracket_to=(0, -0.72, 0.76))
        for sx in (-1, 1):
            self.indicator(self.lights, (sx * 0.07, -0.84, 0.72), sx, facing=-1)
        self.pegs(-0.30, 0.36, x=0.18)
        self.anchor["rider_seat"] = V(0, -0.22, 0.86)
        self.anchor["driver_eye"] = V(0, 0.02, 1.38)
        self.anchor["hood_cam"] = V(0, 0.30, 1.22)

    # ---------------------------------------------------------------- street naked
    def build_naked(self):
        M, B, F, K = self.M, self.body, self.frame, self.fork
        L = self.d["fork_len"]
        self.fork_legs(0.095, 0.030, 0.022, 0.32, usd=True, lower_mat="trim")
        self.caliper(0.095, 0.16, both=True)
        arc_strip(K, self.Af.y, self.Af.z, self.wf["R"] + 0.03, 0.13, 35, 120, 0.012, M["paint"], seg=10)
        # wide flat bar on risers
        tb = self.on_axis(L + 0.06)
        self.handlebar([(0.06, tb.y, tb.z), (0.20, tb.y - 0.05, tb.z + 0.01), (0.37, tb.y - 0.10, tb.z + 0.02)],
                       r=0.012, riser=0.05, grip_len=0.11)
        # round LED headlamp + small instrument
        hl = self.on_axis(L - 0.08, 0.13)
        self.round_lamp(K, hl, 0.085, depth=0.11, shell="trim")
        K.box(self.on_axis(L + 0.07, -0.02), (0.13, 0.06, 0.08), M["trim"],
              rot=Euler((math.radians(-30), 0, 0)).to_quaternion())
        for sx in (-1, 1):
            self.indicator(K, (sx * 0.07, hl.y - 0.02, hl.z + 0.02), sx)
        self.steering_head()
        # orange trellis frame
        sh0, sh1 = self.on_axis(L - 0.18), self.on_axis(L - 0.03)
        piv = V(0, -0.14, 0.46)
        for sx in (-1, 1):
            x0, x1 = sx * 0.07, sx * 0.15
            pts_top = [V(x0, sh1.y, sh1.z), V(x1, 0.08, 0.80), V(x1, -0.12, 0.74)]
            pts_bot = [V(x0, sh0.y, sh0.z), V(x1, 0.12, 0.58), V(x1, -0.10, 0.52)]
            F.tube_path(pts_top, 0.016, M["frame"], seg=6)
            F.tube_path(pts_bot, 0.016, M["frame"], seg=6)
            for a, b in ((pts_top[1], pts_bot[0]), (pts_top[1], pts_bot[1]), (pts_top[2], pts_bot[1]), (pts_top[2], pts_bot[2])):
                F.cyl(a, b, 0.012, M["frame"], seg=6)
            F.cyl(V(x1, -0.12, 0.74), V(sx * 0.08, -0.78, 0.84), 0.014, M["frame"], seg=6)
            F.cyl(V(x1, -0.10, 0.52), V(sx * 0.08, -0.62, 0.80), 0.012, M["frame"], seg=6)
        # exposed triple: crankcase, finned-look block, cam cover, radiator
        F.box(V(0, 0.02, 0.34), (0.32, 0.40, 0.22), M["engine"])
        F.box(V(0, 0.14, 0.56), (0.30, 0.20, 0.26), M["engine"], rot=Euler((math.radians(-22), 0, 0)).to_quaternion())
        F.box(V(0, 0.18, 0.70), (0.26, 0.18, 0.06), M["alu"], rot=Euler((math.radians(-22), 0, 0)).to_quaternion())
        F.box(V(0, 0.34, 0.58), (0.34, 0.035, 0.24), M["trim"], rot=Euler((math.radians(-12), 0, 0)).to_quaternion())
        for sx in (-1, 1):
            F.cyl(V(sx * 0.165, 0.0, 0.33), V(sx * 0.18, 0.0, 0.33), 0.08, M["alu"], seg=14)
        self.swingarm((0, piv.y, piv.z), 0.10, h=0.07, mono_to=(0, -0.18, 0.72))
        # three headers sweeping under to a stubby belly can
        for i, x in enumerate((-0.07, 0.0, 0.07)):
            F.tube_path([V(x, 0.26, 0.52), V(x * 1.1, 0.36, 0.36), V(x * 0.8, 0.30, 0.18), V(x * 0.4, 0.10, 0.12)],
                        0.018, M["chrome"], seg=8)
        F.box(V(0, -0.06, 0.18), (0.24, 0.30, 0.10), M["trim_grey"], top_scale=(0.9, 0.85))
        F.cyl(V(0.08, -0.21, 0.18), V(0.08, -0.24, 0.18), 0.03, M["chrome"], seg=10)
        # muscular tank with side scoops, short tail
        loft(B, [dict(y=-0.10, z=0.84, w=0.12, t=0.05, b=0.05), dict(y=0.02, z=0.90, w=0.19, t=0.10, b=0.10),
                 dict(y=0.20, z=0.92, w=0.21, t=0.09, b=0.13), dict(y=0.36, z=0.88, w=0.16, t=0.06, b=0.11)], M["paint"])
        for sx in (-1, 1):
            loft(B, [dict(y=0.20, z=0.76, w=0.03, t=0.08, b=0.05, cx=sx * 0.19), dict(y=0.36, z=0.70, w=0.05, t=0.10, b=0.07, cx=sx * 0.19),
                     dict(y=0.46, z=0.66, w=0.03, t=0.07, b=0.05, cx=sx * 0.17)], M["paint"], n=12)
        loft(B, [dict(y=-0.30, z=0.80, w=0.13, t=0.05, b=0.08, p=3.0), dict(y=-0.58, z=0.86, w=0.11, t=0.06, b=0.08, p=3.0),
                 dict(y=-0.80, z=0.90, w=0.06, t=0.04, b=0.05, p=2.6)], M["paint"])
        self.seat([dict(y=-0.04, z=0.80, w=0.13, t=0.03, b=0.03, p=3.2), dict(y=-0.26, z=0.82, w=0.155, t=0.035, b=0.03, p=3.2),
                   dict(y=-0.50, z=0.86, w=0.14, t=0.04, b=0.03, p=3.2), dict(y=-0.70, z=0.90, w=0.09, t=0.03, b=0.03, p=3.0)])
        self.tail_light((0, -0.82, 0.90), size=(0.08, 0.03, 0.03))
        self.plate((0, -0.92, 0.58), tilt=18, bracket_to=(0, -0.72, 0.80))
        for sx in (-1, 1):
            self.indicator(self.lights, (sx * 0.06, -0.88, 0.66), sx, facing=-1)
        self.mirrors(K, [(0.26, tb.y - 0.08, tb.z + 0.02)], stalk=0.16, head=(0.10, 0.025, 0.06))
        self.pegs(-0.30, 0.36, x=0.18)
        self.anchor["rider_seat"] = V(0, -0.24, 0.84)
        self.anchor["driver_eye"] = V(0, 0.0, 1.45)
        self.anchor["hood_cam"] = V(0, 0.28, 1.20)

    # ---------------------------------------------------------------- retro scooter
    def build_scooter(self):
        M, B, F, K = self.M, self.body, self.frame, self.fork
        L = self.d["fork_len"]
        # single-sided trailing-link front: one leg on the left, chrome
        a = self.on_axis(0.0, 0.0, -0.075)
        K.cyl(a, self.on_axis(L - 0.10, 0.0, -0.02), 0.03, M["trim_grey"], seg=10)
        K.cyl(self.on_axis(0.0, 0.0, -0.08), self.on_axis(0.0, 0.0, 0.07), 0.012, M["alu"], seg=6)
        K.cyl(self.on_axis(L - 0.12), self.on_axis(L + 0.02), 0.03, M["trim_grey"], seg=10)
        # iconic front fender with a crest (steers)
        arc_strip(K, self.Af.y, self.Af.z, self.wf["R"] + 0.03, 0.15, 5, 125, 0.018, M["paint"], seg=12, lip=0.85)
        crest = self.Af + V(0, 0.0, self.wf["R"] + 0.058)
        K.cyl(crest + V(0, 0.07, -0.02), crest + V(0, -0.06, 0.0), 0.012, M["chrome"], seg=6)
        K.box(crest + V(0, 0.08, -0.03), (0.03, 0.05, 0.04), M["chrome"])
        # handlebar cowl with the round headlamp (steers)
        hc = self.on_axis(L + 0.08, 0.02)
        loft(K, [dict(y=hc.y - 0.12, z=hc.z, w=0.12, t=0.06, b=0.05, p=2.4),
                 dict(y=hc.y - 0.02, z=hc.z + 0.005, w=0.15, t=0.075, b=0.06, p=2.4),
                 dict(y=hc.y + 0.08, z=hc.z - 0.005, w=0.10, t=0.065, b=0.055, p=2.2)], M["paint"])
        self.round_lamp(K, hc + V(0, 0.08, 0.0), 0.06, depth=0.02, shell="paint")
        K.box(hc + V(0, -0.06, 0.07), (0.10, 0.07, 0.02), M["trim"], rot=Euler((math.radians(-18), 0, 0)).to_quaternion())
        self.handlebar([(0.15, hc.y - 0.02, hc.z + 0.01), (0.30, hc.y - 0.05, hc.z + 0.01)], r=0.012, grip_len=0.10)
        self.mirrors(K, [(0.17, hc.y - 0.04, hc.z + 0.05)], stalk=0.18, head=(0.10, 0.02, 0.1), mat="chrome", round_=True)
        # leg shield (concave apron) + horn cast + floorboard
        ys = self.yf - 0.26
        loft(B, [dict(y=ys - 0.035, z=0.55, w=0.22, t=0.40, b=0.32, p=3.5, pb=6.0, tilt=-0.20),
                 dict(y=ys + 0.00, z=0.56, w=0.235, t=0.40, b=0.33, p=3.5, pb=6.0, tilt=-0.20),
                 dict(y=ys + 0.03, z=0.58, w=0.20, t=0.37, b=0.30, p=3.5, pb=6.0, tilt=-0.20)], M["paint"])
        K.box(V(0, ys + 0.07, 0.86), (0.07, 0.05, 0.12), M["chrome"], top_scale=(0.6, 0.8))     # horn "tie"
        F.box(V(0, ys - 0.25, 0.235), (0.34, 0.52, 0.05), M["trim"])
        for i in range(5):
            F.box(V(-0.10 + 0.05 * i, ys - 0.25, 0.262), (0.012, 0.48, 0.008), M["alu"])
        loft(B, [dict(y=ys - 0.03, z=0.24, w=0.17, t=0.03, b=0.04, p=4.0),
                 dict(y=ys - 0.50, z=0.24, w=0.17, t=0.03, b=0.04, p=4.0)], M["paint"])
        # rear body: the bulbous side cowls + centre spine under the seat
        loft(B, [dict(y=-0.06, z=0.40, w=0.13, t=0.16, b=0.14, p=2.6),
                 dict(y=-0.20, z=0.47, w=0.22, t=0.24, b=0.20, p=2.4),
                 dict(y=-0.45, z=0.50, w=0.255, t=0.25, b=0.22, p=2.3),
                 dict(y=-0.68, z=0.52, w=0.22, t=0.22, b=0.18, p=2.2),
                 dict(y=-0.86, z=0.56, w=0.12, t=0.14, b=0.10, p=2.0),
                 dict(y=-0.92, z=0.58, w=0.05, t=0.06, b=0.05, p=2.0)], M["paint"])
        # engine + small exhaust on the right
        F.box(V(-0.10, -0.50, 0.22), (0.12, 0.40, 0.16), M["engine"])
        self.exhaust([(0.04, -0.30, 0.18), (0.10, -0.40, 0.16), (0.15, -0.50, 0.20)], 0.018,
                     (0.15, -0.50, 0.22), (0.15, -0.78, 0.26), 0.045)
        # seat (two-up bench, tan) with a chrome rack behind
        self.seat([dict(y=-0.06, z=0.755, w=0.13, t=0.035, b=0.04, p=3.0), dict(y=-0.22, z=0.785, w=0.16, t=0.045, b=0.04, p=3.0),
                   dict(y=-0.48, z=0.80, w=0.16, t=0.045, b=0.04, p=3.0), dict(y=-0.66, z=0.81, w=0.13, t=0.04, b=0.04, p=3.0)])
        F.tube_path([V(-0.09, -0.62, 0.80), V(-0.09, -0.86, 0.80), V(0.09, -0.86, 0.80), V(0.09, -0.62, 0.80)], 0.009,
                    M["chrome"], seg=6)
        F.cyl(V(-0.07, -0.86, 0.80), V(-0.07, -0.84, 0.68), 0.008, M["chrome"], seg=4)
        F.cyl(V(0.07, -0.86, 0.80), V(0.07, -0.84, 0.68), 0.008, M["chrome"], seg=4)
        self.tail_light((0, -0.925, 0.64), size=(0.10, 0.04, 0.05))
        self.plate((0, -0.935, 0.50), tilt=8)
        for sx in (-1, 1):
            self.indicator(self.lights, (sx * 0.17, -0.82, 0.66), sx, facing=-1)
            self.indicator(B, (sx * 0.20, ys + 0.04, 0.82), sx)
        # little blossom badge on the apron (accent)
        F.cyl(V(0, ys + 0.045, 0.70), V(0, ys + 0.055, 0.70), 0.03, M["accent2"], seg=10)
        self.anchor["rider_seat"] = V(0, -0.24, 0.82)
        self.anchor["rider_peg"] = V(0.10, ys - 0.22, 0.27)
        self.anchor["driver_eye"] = V(0, -0.10, 1.48)
        self.anchor["hood_cam"] = V(0, 0.22, 1.18)
        # side stand nub
        F.cyl(V(-0.12, -0.10, 0.20), V(-0.20, -0.14, 0.04), 0.012, M["trim"], seg=4)

    # ---------------------------------------------------------------- enduro / dirt bike
    def build_enduro(self):
        M, B, F, K = self.M, self.body, self.frame, self.fork
        L = self.d["fork_len"]
        self.fork_legs(0.085, 0.032, 0.024, 0.42, usd=True, lower_mat="trim")
        self.caliper(0.085, 0.13)
        # high front fender on the bottom clamp + number board / small headlamp
        fb = self.on_axis(L - 0.17, 0.05)
        loft(K, [dict(y=fb.y + 0.30, z=fb.z - 0.08, w=0.05, t=0.008, b=0.008, p=2.4),
                 dict(y=fb.y + 0.10, z=fb.z - 0.01, w=0.085, t=0.012, b=0.012, p=2.4),
                 dict(y=fb.y - 0.10, z=fb.z + 0.005, w=0.085, t=0.012, b=0.012, p=2.4),
                 dict(y=fb.y - 0.22, z=fb.z - 0.03, w=0.06, t=0.01, b=0.01, p=2.4)], M["accent"])
        nb = self.on_axis(L - 0.02, 0.08)
        q = Euler((math.radians(-self.d["rake"]), 0, 0)).to_quaternion()
        K.box(nb, (0.20, 0.03, 0.24), M["paint"], rot=q, top_scale=(0.85, 1.0))
        K.box(nb + V(0, 0.018, -0.03), (0.10, 0.012, 0.05), M["head"], rot=q)
        tb = self.on_axis(L + 0.07)
        self.handlebar([(0.06, tb.y, tb.z), (0.22, tb.y - 0.03, tb.z + 0.03), (0.40, tb.y - 0.08, tb.z + 0.05)],
                       r=0.012, riser=0.04, grip_len=0.11)
        for sx in (-1, 1):   # hand guards
            K.box(V(sx * 0.36, tb.y - 0.0, tb.z + 0.05), (0.12, 0.02, 0.07), M["accent"], top_scale=(0.7, 1.0))
        self.steering_head(r=0.034)
        # slim perimeter frame (blue)
        sh0, sh1 = self.on_axis(L - 0.18), self.on_axis(L - 0.03)
        piv = V(0, -0.16, 0.50)
        for sx in (-1, 1):
            F.tube_path([V(sx * 0.05, sh1.y, sh1.z), V(sx * 0.13, 0.15, 0.82), V(sx * 0.12, -0.12, 0.76), V(sx * 0.11, piv.y, piv.z + 0.02)],
                        0.018, M["frame"], seg=6)
            F.tube_path([V(sx * 0.04, sh0.y, sh0.z), V(sx * 0.06, 0.28, 0.36), V(sx * 0.07, 0.10, 0.22), V(sx * 0.09, -0.12, 0.30), V(sx * 0.11, piv.y, piv.z - 0.04)],
                        0.016, M["frame"], seg=6)
            F.cyl(V(sx * 0.12, -0.12, 0.76), V(sx * 0.08, -0.80, 0.92), 0.012, M["alu"], seg=6)
        # tall single + radiator shrouds, skid plate
        F.box(V(0, 0.06, 0.38), (0.22, 0.30, 0.22), M["engine"])
        F.box(V(0, 0.08, 0.60), (0.16, 0.16, 0.24), M["engine"], rot=Euler((math.radians(-10), 0, 0)).to_quaternion())
        F.box(V(0, 0.10, 0.74), (0.14, 0.14, 0.05), M["alu"], rot=Euler((math.radians(-10), 0, 0)).to_quaternion())
        F.box(V(0.12, 0.04, 0.37), (0.03, 0.14, 0.12), M["alu"])
        F.box(V(0, 0.08, 0.22), (0.20, 0.36, 0.03), M["alu"], rot=Euler((math.radians(6), 0, 0)).to_quaternion())
        for sx in (-1, 1):
            F.box(V(sx * 0.12, 0.30, 0.62), (0.03, 0.10, 0.22), M["trim"])
        self.swingarm((0, piv.y, piv.z), 0.085, h=0.06, mat="alu", mono_to=(0, -0.24, 0.80))
        # high exhaust: header round the right of the cylinder, up under the tail
        self.exhaust([(0.02, 0.22, 0.56), (0.10, 0.30, 0.44), (0.13, 0.12, 0.36), (0.13, -0.18, 0.56), (0.14, -0.34, 0.66)],
                     0.020, (0.14, -0.36, 0.68), (0.14, -0.74, 0.82), 0.045, pipe_mat="chrome", can_mat="alu")
        # body: slim tank, big radiator shrouds, side panels, rising tail fender
        loft(B, [dict(y=-0.02, z=0.90, w=0.10, t=0.05, b=0.06), dict(y=0.14, z=0.93, w=0.14, t=0.07, b=0.10),
                 dict(y=0.32, z=0.92, w=0.12, t=0.05, b=0.09)], M["paint"])
        for sx in (-1, 1):
            loft(B, [dict(y=0.10, z=0.82, w=0.015, t=0.09, b=0.13, cx=sx * 0.15, p=3.2, tilt=0.4),
                     dict(y=0.28, z=0.80, w=0.025, t=0.12, b=0.17, cx=sx * 0.165, p=3.2, tilt=0.4),
                     dict(y=0.44, z=0.82, w=0.012, t=0.09, b=0.12, cx=sx * 0.12, p=3.2, tilt=0.4)], M["paint"], n=12)
            loft(B, [dict(y=-0.28, z=0.82, w=0.012, t=0.07, b=0.10, cx=sx * 0.125, p=3.5),
                     dict(y=-0.60, z=0.86, w=0.012, t=0.05, b=0.06, cx=sx * 0.105, p=3.5)], M["accent"], n=12)
        loft(B, [dict(y=-0.40, z=0.90, w=0.11, t=0.012, b=0.012, p=2.6), dict(y=-0.70, z=0.92, w=0.10, t=0.012, b=0.012, p=2.6),
                 dict(y=-0.96, z=0.98, w=0.06, t=0.010, b=0.010, p=2.6)], M["accent"])
        # long flat seat
        self.seat([dict(y=0.24, z=0.94, w=0.06, t=0.02, b=0.03, p=3.0), dict(y=0.06, z=0.95, w=0.11, t=0.03, b=0.035, p=3.2),
                   dict(y=-0.40, z=0.95, w=0.12, t=0.03, b=0.035, p=3.2), dict(y=-0.70, z=0.95, w=0.09, t=0.025, b=0.03, p=3.0)])
        self.tail_light((0, -0.90, 0.93), size=(0.07, 0.03, 0.025))
        self.plate((0, -0.94, 0.80), w=0.15, h=0.10, tilt=14)
        for sx in (-1, 1):
            self.indicator(self.lights, (sx * 0.05, -0.92, 0.86), sx, facing=-1)
        self.pegs(-0.24, 0.40, x=0.17)
        self.anchor["rider_seat"] = V(0, -0.14, 0.96)
        self.anchor["driver_eye"] = V(0, 0.02, 1.62)
        self.anchor["hood_cam"] = V(0, 0.30, 1.32)

    # ---------------------------------------------------------------- adventure tourer
    def build_adventure(self):
        M, B, F, K = self.M, self.body, self.frame, self.fork
        L = self.d["fork_len"]
        self.fork_legs(0.092, 0.034, 0.026, 0.38, usd=True, lower_mat="trim")
        self.caliper(0.092, 0.16, both=True)
        # high fender on the fork + the fixed beak above it
        arc_strip(K, self.Af.y, self.Af.z, self.wf["R"] + 0.05, 0.13, 20, 110, 0.012, M["trim"], seg=10)
        loft(B, [dict(y=0.78, z=0.80, w=0.04, t=0.012, b=0.012, p=2.4, tilt=0.6),
                 dict(y=0.64, z=0.86, w=0.11, t=0.02, b=0.02, p=2.4),
                 dict(y=0.48, z=0.90, w=0.16, t=0.03, b=0.03, p=2.4)], M["paint"])
        tb = self.on_axis(L + 0.08)
        self.handlebar([(0.06, tb.y, tb.z), (0.22, tb.y - 0.04, tb.z + 0.03), (0.40, tb.y - 0.10, tb.z + 0.04)],
                       r=0.013, riser=0.06, grip_len=0.11)
        for sx in (-1, 1):
            K.box(V(sx * 0.37, tb.y + 0.01, tb.z + 0.05), (0.13, 0.025, 0.08), M["trim"], top_scale=(0.7, 1.0))
        self.steering_head()
        piv = V(0, -0.18, 0.48)
        for sx in (-1, 1):
            F.tube_path([V(sx * 0.07, self.H.y - 0.05, self.H.z - 0.06), V(sx * 0.17, 0.10, 0.80), V(sx * 0.15, -0.14, 0.72),
                         V(sx * 0.12, piv.y, piv.z)], 0.02, M["frame"], seg=6)
            F.cyl(V(sx * 0.15, -0.14, 0.72), V(sx * 0.10, -0.86, 0.86), 0.016, M["frame"], seg=6)
        # parallel twin + sump guard
        F.box(V(0, 0.04, 0.36), (0.34, 0.40, 0.24), M["engine"])
        F.box(V(0, 0.18, 0.58), (0.32, 0.20, 0.26), M["engine"], rot=Euler((math.radians(-18), 0, 0)).to_quaternion())
        F.box(V(0, 0.22, 0.72), (0.30, 0.18, 0.05), M["alu"], rot=Euler((math.radians(-18), 0, 0)).to_quaternion())
        F.box(V(0, 0.38, 0.56), (0.36, 0.04, 0.26), M["trim"], rot=Euler((math.radians(-10), 0, 0)).to_quaternion())
        F.box(V(0, 0.10, 0.20), (0.30, 0.50, 0.03), M["alu"], rot=Euler((math.radians(4), 0, 0)).to_quaternion())
        for sx in (-1, 1):
            F.cyl(V(sx * 0.17, 0.02, 0.36), V(sx * 0.19, 0.02, 0.36), 0.08, M["alu"], seg=14)
        self.swingarm((0, piv.y, piv.z), 0.11, h=0.08, mono_to=(0, -0.24, 0.74))
        self.exhaust([(0.07, 0.30, 0.48), (0.10, 0.34, 0.26), (0.10, 0.10, 0.18), (0.16, -0.20, 0.30), (0.20, -0.42, 0.52)],
                     0.024, (0.21, -0.44, 0.54), (0.22, -0.74, 0.66), 0.058, pipe_mat="chrome", can_mat="trim_grey", tip_mat="alu")
        # crash bars around the engine and the upper tank shrouds (with fog lamps)
        for sx in (-1, 1):
            F.tube_path([V(sx * 0.10, 0.40, 0.78), V(sx * 0.27, 0.38, 0.70), V(sx * 0.30, 0.30, 0.42),
                         V(sx * 0.24, 0.18, 0.26), V(sx * 0.14, -0.04, 0.26)], 0.016, M["trim"], seg=6)
            F.tube_path([V(sx * 0.27, 0.38, 0.70), V(sx * 0.28, 0.14, 0.82), V(sx * 0.14, -0.06, 0.84)], 0.016, M["trim"], seg=6)
            self.round_lamp(self.lights, (sx * 0.27, 0.42, 0.64), 0.035, depth=0.05, lens="aux")
        # big tank + side shrouds + fairing + tall screen
        loft(B, [dict(y=-0.10, z=0.88, w=0.13, t=0.05, b=0.05), dict(y=0.04, z=0.94, w=0.20, t=0.10, b=0.12),
                 dict(y=0.24, z=0.94, w=0.22, t=0.10, b=0.15), dict(y=0.42, z=0.90, w=0.18, t=0.07, b=0.14)], M["paint"])
        for sx in (-1, 1):
            loft(B, [dict(y=0.06, z=0.78, w=0.02, t=0.10, b=0.10, cx=sx * 0.20, p=3.0),
                     dict(y=0.30, z=0.80, w=0.04, t=0.14, b=0.13, cx=sx * 0.22, p=3.0),
                     dict(y=0.52, z=0.86, w=0.03, t=0.10, b=0.10, cx=sx * 0.18, p=3.0)], M["paint"], n=12)
        loft(B, [dict(y=0.40, z=1.00, w=0.19, t=0.10, b=0.12, p=2.8), dict(y=0.56, z=1.00, w=0.17, t=0.09, b=0.12, p=2.6),
                 dict(y=0.66, z=0.98, w=0.11, t=0.06, b=0.09, p=2.4)], M["paint"])
        loft(self.glass, [dict(y=0.60, z=1.12, w=0.17, t=0.01, b=0.01, p=2.0),
                          dict(y=0.52, z=1.28, w=0.17, t=0.01, b=0.01, p=2.0),
                          dict(y=0.46, z=1.38, w=0.14, t=0.008, b=0.008, p=2.0)], M["glass"], n=12)
        for sx in (-1, 1):
            self.lights.box(V(sx * 0.065, 0.665, 0.98), (0.09, 0.03, 0.06), M["head"],
                            rot=Euler((0, 0, math.radians(sx * 10))).to_quaternion())
            self.indicator(self.lights, (sx * 0.16, 0.60, 0.96), sx)
        self.mirrors(F, [(0.17, 0.52, 1.06)], stalk=0.12, head=(0.12, 0.025, 0.07))
        # two-level seat, rear rack, panniers + top box (aluminium)
        self.seat([dict(y=-0.08, z=0.87, w=0.14, t=0.03, b=0.04, p=3.2), dict(y=-0.32, z=0.88, w=0.17, t=0.035, b=0.04, p=3.2),
                   dict(y=-0.50, z=0.93, w=0.15, t=0.035, b=0.04, p=3.2), dict(y=-0.74, z=0.95, w=0.13, t=0.03, b=0.03, p=3.0)])
        loft(B, [dict(y=-0.40, z=0.82, w=0.14, t=0.05, b=0.07, p=3.0), dict(y=-0.86, z=0.88, w=0.10, t=0.05, b=0.06, p=2.8)], M["paint"])
        F.box(V(0, -0.94, 1.08), (0.36, 0.38, 0.26), M["alu"], top_scale=(0.92, 0.92))
        F.box(V(0, -0.94, 1.20), (0.37, 0.39, 0.03), M["trim"])
        F.box(V(0, -0.90, 0.95), (0.22, 0.30, 0.02), M["trim"])
        for sx in (-1, 1):
            F.box(V(sx * 0.30, -0.66, 0.60), (0.16, 0.44, 0.32), M["alu"], top_scale=(0.92, 0.95))
            F.box(V(sx * 0.30, -0.66, 0.775), (0.17, 0.45, 0.03), M["trim"])
            F.box(V(sx * 0.385, -0.66, 0.60), (0.01, 0.26, 0.16), M["accent"])
            F.tube_path([V(sx * 0.12, -0.40, 0.84), V(sx * 0.22, -0.42, 0.74), V(sx * 0.22, -0.90, 0.74),
                         V(sx * 0.12, -0.92, 0.90)], 0.011, M["trim"], seg=6)
        self.tail_light((0, -0.89, 0.86), size=(0.10, 0.03, 0.04))
        self.plate((0, -0.98, 0.62), tilt=12, bracket_to=(0, -0.86, 0.82))
        for sx in (-1, 1):
            self.indicator(self.lights, (sx * 0.08, -0.94, 0.70), sx, facing=-1)
        self.pegs(-0.26, 0.40, x=0.18)
        self.anchor["rider_seat"] = V(0, -0.28, 0.90)
        self.anchor["driver_eye"] = V(0, 0.0, 1.55)
        self.anchor["hood_cam"] = V(0, 0.32, 1.30)

    # ---------------------------------------------------------------- cruiser
    def build_cruiser(self):
        M, B, F, K = self.M, self.body, self.frame, self.fork
        L = self.d["fork_len"]
        self.fork_legs(0.11, 0.024, 0.036, 0.30, usd=False, lower_mat="chrome")
        for sx in (-1, 1):     # fork shrouds
            K.cyl(self.on_axis(L - 0.24, 0.0, sx * 0.11), self.on_axis(L - 0.02, 0.0, sx * 0.11), 0.036, M["chrome"], seg=12)
        self.caliper(0.11, 0.16, both=True)
        # deep skirted front fender (steers) + chrome nacelle headlamp + pulled-back bars
        arc_strip(K, self.Af.y, self.Af.z, self.wf["R"] + 0.03, 0.17, 5, 150, 0.02, M["paint"], seg=14, lip=0.9)
        hl = self.on_axis(L - 0.10, 0.17)
        K.box(self.on_axis(L - 0.10, 0.06), (0.24, 0.12, 0.16), M["chrome"], top_scale=(0.8, 0.8),
              rot=Euler((math.radians(-self.d["rake"]), 0, 0)).to_quaternion())
        self.round_lamp(K, hl, 0.095, depth=0.14, shell="chrome")
        for sx in (-1, 1):
            self.round_lamp(K, hl + V(sx * 0.17, -0.02, -0.06), 0.035, depth=0.05, shell="chrome", lens="aux")
            self.indicator(K, (sx * 0.06, hl.y - 0.06, hl.z + 0.10), sx)
        tb = self.on_axis(L + 0.02)
        self.handlebar([(0.08, tb.y, tb.z + 0.16), (0.24, tb.y - 0.10, tb.z + 0.20), (0.34, tb.y - 0.30, tb.z + 0.18),
                        (0.38, tb.y - 0.40, tb.z + 0.16)], r=0.013, mat="chrome", riser=0.16, grip_len=0.12)
        self.mirrors(K, [(0.28, tb.y - 0.20, tb.z + 0.20)], stalk=0.18, head=(0.10, 0.02, 0.1), mat="chrome", round_=True)
        self.steering_head(r=0.045)
        # backbone + cradle frame (black)
        piv = V(0, -0.28, 0.36)
        top0 = self.on_axis(L - 0.04)
        bot0 = self.on_axis(L - 0.20)
        F.tube_path([top0, V(0, 0.10, 0.74), V(0, -0.30, 0.66), V(0, -0.62, 0.62)], 0.03, M["frame"], seg=8)
        for sx in (-1, 1):
            F.tube_path([V(sx * 0.04, bot0.y, bot0.z), V(sx * 0.10, 0.40, 0.24), V(sx * 0.10, 0.20, 0.13), V(sx * 0.11, -0.30, 0.14),
                         V(sx * 0.11, piv.y, piv.z)], 0.022, M["frame"], seg=8)
        # V-twin: chrome-finned cylinders in a 45 deg V, primary cover left, round air cleaner right
        F.box(V(0, -0.02, 0.27), (0.24, 0.40, 0.20), M["engine"])
        self.fins((0, 0.12, 0.50), (0.16, 0.16, 0.26), 7, axis_tilt=-24, mat="engine", fin_mat="chrome")
        self.fins((0, -0.12, 0.50), (0.16, 0.16, 0.26), 7, axis_tilt=24, mat="engine", fin_mat="chrome")
        F.box(V(0, 0.18, 0.64), (0.13, 0.12, 0.06), M["chrome"], rot=Euler((math.radians(-24), 0, 0)).to_quaternion())
        F.box(V(0, -0.18, 0.64), (0.13, 0.12, 0.06), M["chrome"], rot=Euler((math.radians(24), 0, 0)).to_quaternion())
        F.cyl(V(0.10, 0.0, 0.52), V(0.16, 0.0, 0.52), 0.11, M["chrome"], seg=18, r2=0.10)
        F.box(V(-0.15, -0.16, 0.26), (0.05, 0.52, 0.18), M["chrome"], top_scale=(1.0, 0.85))
        F.cyl(V(-0.13, 0.04, 0.27), V(-0.18, 0.04, 0.27), 0.10, M["chrome"], seg=16)
        self.swingarm((0, piv.y, piv.z), 0.14, h=0.05, mat="frame", chain=False, shocks=(-0.62, 0.62, "chrome"))
        F.tube_path([V(0.14, -0.30, 0.34), V(0.14, -0.80, 0.34)], 0.03, M["trim"], seg=6)   # belt guard
        # staggered shotgun pipes on the right
        self.exhaust([(0.06, 0.20, 0.52), (0.16, 0.26, 0.42), (0.20, 0.10, 0.24), (0.21, -0.30, 0.22)], 0.028,
                     (0.21, -0.30, 0.22), (0.22, -1.06, 0.24), 0.045)
        self.exhaust([(0.06, -0.20, 0.50), (0.17, -0.22, 0.40), (0.20, -0.34, 0.34)], 0.028,
                     (0.20, -0.34, 0.34), (0.21, -1.00, 0.36), 0.045)
        # teardrop tank (with a chrome dash) + deep rear fender
        loft(B, [dict(y=-0.24, z=0.74, w=0.10, t=0.04, b=0.04), dict(y=-0.08, z=0.80, w=0.18, t=0.09, b=0.08),
                 dict(y=0.16, z=0.83, w=0.20, t=0.10, b=0.09), dict(y=0.38, z=0.80, w=0.14, t=0.07, b=0.07),
                 dict(y=0.46, z=0.78, w=0.06, t=0.03, b=0.03)], M["paint"])
        F.box(V(0, 0.05, 0.915), (0.07, 0.36, 0.02), M["chrome"], top_scale=(0.8, 0.9))
        arc_strip(B, self.Ar.y, self.Ar.z, self.wr["R"] + 0.03, 0.26, 30, 175, 0.02, M["paint"], seg=14, lip=0.95)
        loft(B, [dict(y=-0.36, z=0.48, w=0.13, t=0.08, b=0.10, p=3.0), dict(y=-0.30, z=0.50, w=0.15, t=0.10, b=0.12, p=3.0)], M["paint"])
        # low solo seat (tan) with a sprung look
        self.seat([dict(y=-0.20, z=0.66, w=0.10, t=0.03, b=0.03, p=2.6), dict(y=-0.34, z=0.68, w=0.19, t=0.05, b=0.04, p=2.8),
                   dict(y=-0.50, z=0.71, w=0.20, t=0.06, b=0.04, p=2.8), dict(y=-0.58, z=0.74, w=0.15, t=0.04, b=0.03, p=2.6)])
        self.tail_light((0, -1.06, 0.62), size=(0.12, 0.03, 0.05), indicators_y=(-1.00, 0.56), ind_x=0.10)
        self.plate((0, -1.09, 0.46), tilt=6)
        # forward controls + floorboards
        for sx in (-1, 1):
            F.box(V(sx * 0.22, 0.26, 0.26), (0.12, 0.26, 0.02), M["chrome"])
            F.box(V(sx * 0.22, 0.26, 0.275), (0.10, 0.24, 0.012), M["trim"])
        self.anchor["rider_peg"] = V(0.20, 0.26, 0.29)
        self.anchor["rider_seat"] = V(0, -0.42, 0.72)
        self.anchor["driver_eye"] = V(0, -0.24, 1.36)
        self.anchor["hood_cam"] = V(0, 0.10, 1.14)

    # ---------------------------------------------------------------- collision proxies
    def collision(self):
        objs = []
        wb = self.d["wb"]
        top = self.d["seat"][0]
        for name, c, s in ((f"{self.C}_Collider_Body", (0, 0, (0.22 + top) / 2), (0.42, wb + 0.25, top - 0.22)),):
            mb = MeshBuilder(name)
            bmesh.ops.create_cube(mb.bm, size=1.0, matrix=Matrix.LocRotScale(Vector(c), None, Vector(s)))
            o = mb.finish(self.cols["Collision"])
            o.display_type = "WIRE"
            o.hide_render = True
            objs.append(o)
        return objs


def plate_uvs(obj, rect):
    me = obj.data
    if "UVMap" not in me.uv_layers:
        return
    uv = me.uv_layers["UVMap"]
    pidx = [i for i, m in enumerate(me.materials) if m and m.name.endswith("_Plate")]
    if not pidx:
        return
    polys = [p for p in me.polygons if p.material_index in pidx]
    cos = [me.vertices[me.loops[li].vertex_index].co for p in polys for li in p.loop_indices]
    if not cos:
        return
    x0, x1 = min(c.x for c in cos), max(c.x for c in cos)
    z0, z1 = min(c.z for c in cos), max(c.z for c in cos)
    u0, v0, u1, v1 = rect
    for p in polys:
        for li in p.loop_indices:
            c = me.vertices[me.loops[li].vertex_index].co
            tx = (c.x - x0) / (x1 - x0 or 1)
            tz = (c.z - z0) / (z1 - z0 or 1)
            uv.data[li].uv = (u0 + (u1 - u0) * tx, v0 + (v1 - v0) * tz)


def main():
    a = parse()
    spec = BIKES[a.bike]
    C = a.bike
    out = os.path.abspath(a.out)
    if "Incoming" in out.split(os.sep):
        raise SystemExit("Refusing to write into Incoming.")
    if os.path.exists(out) and not a.overwrite:
        out = pc.next_version(out)
    pc.reset_scene()
    B = BikeBuilder(C, spec, a.texture)
    B.build()
    A = B.A

    body = B.body.finish(B.cols["Body"])
    frame = B.frame.finish(B.cols["Exterior"])
    lights = B.lights.finish(B.cols["Lights"])
    if len(B.glass.bm.faces):
        glass = B.glass.finish(B.cols["Glass"])
    else:
        B.glass.bm.free()
        glass = None
    fork = B.fork.finish(B.cols["Fork"])
    wheels = B.wheels()
    colliders = B.collision()
    bpy.context.view_layer.update()

    for o in [x for x in (body, frame, lights, glass, fork) if x is not None]:
        vb.project_uvs(o, A)
        plate_uvs(o, A.PLATE_REAR_RECT)
        vb.smooth_by_angle(o, 32)

    # the fork steers in the game: put its origin on the steering axis (top clamp centre)
    H = B.H.copy()
    fork.data.transform(Matrix.Translation(-H))
    fork.location = H

    root = bpy.data.objects.new(f"{C}_Root", None)
    root.empty_display_type = "ARROWS"
    for k, v in (("car_id", C), ("display_name", spec["name"]), ("vehicle_class", spec["class"]),
                 ("drivetrain", spec.get("drivetrain", "RWD")), ("forward_axis", "+Y"), ("up_axis", "+Z"),
                 ("units", "m"), ("two_wheeler", True), ("wheel_slots", "F,R"), ("steer_rake", float(spec["rake"]))):
        root[k] = v
    B.cols["Root"].objects.link(root)
    groups = {}
    for gname in ("Exterior", "Wheels"):
        e = bpy.data.objects.new(f"{C}_{gname}", None)
        e.empty_display_size = 0.2
        B.cols[gname].objects.link(e)
        e.parent = root
        groups[gname] = e
    for o in (body, lights, glass, fork):
        if o is not None:
            o.parent = root
    frame.parent = groups["Exterior"]
    for slot, (piv, _p) in wheels.items():
        piv.parent = groups["Wheels"]
    for o in colliders:
        o.parent = root
    for key in ("driver_eye", "hood_cam", "rider_seat", "rider_grip", "rider_peg"):
        if key in B.anchor:
            root[key] = [round(v, 4) for v in B.anchor[key]]
    bpy.context.view_layer.update()

    vis = [o for o in B.cols["Root"].all_objects if o.type == "MESH"]
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
        "car_id": C, "display_name": spec["name"], "tool": "bikes/build_bike.py", "generated_utc": pc.now_iso(),
        "blender": pc.blender_info(), "saved_file": out, "kind": spec["kind"], "bbox": bb, "total_tris": total,
        "wheels": wheel_info, "anchors": {k: [round(x, 4) for x in v] for k, v in B.anchor.items()},
        "materials": sorted(m.name for m in bpy.data.materials if m.users), "objects": objs})
    print(f"Saved {out}\nTotal tris: {total}  bbox size: {[round(x, 3) for x in bb['size']]}")


if __name__ == "__main__":
    main()
