"""carspec.py - turns a compact car description (collection_specs.py) into the derived
geometry helpers and livery atlas shared by build_generic.py (Blender) and
livery_generic.py (Pillow). Pure Python, no bpy.

Conventions: metres, +Y forward, +Z up, +X = driver's right, design space
(the builder recentres the finished car on its bounding box).
"""

import math


class Atlas:
    """Per-car livery atlas layout: left side, right side, top, front, rear,
    plates and an underbody block in one square texture."""

    V_RIGHT = 0.302
    V_LEFT = 0.651
    S_END = 0.10
    UNDER_UV = (0.95, 0.25)
    UNDER_RECT = (0.90, 0.20, 1.00, 0.30)
    PLATE_FRONT_RECT = (0.875, 0.010, 0.995, 0.040)
    PLATE_REAR_RECT = (0.875, 0.060, 0.995, 0.090)

    def __init__(self, y_min, y_max, half_w, z_top=1.45):
        self.U_Y0 = y_min - 0.12
        span = (y_max - y_min) + 0.24
        # tall vehicles: shrink the side scale so a side (0..z_top) still fits its 0.345-high band
        self.S_SIDE = min(1.0 / span, 0.2326, 0.345 / max(1.45, z_top))
        self.half_w = half_w + 0.06
        self.S_TOP = min(0.300 / (2 * self.half_w), 0.645 / span)
        self.UV_FUNCS = {"right": self.uv_right, "left": self.uv_left, "top": self.uv_top,
                         "front": self.uv_front, "rear": self.uv_rear}
        self.SCALES = {"right": self.S_SIDE, "left": self.S_SIDE, "top": self.S_TOP,
                       "front": self.S_END, "rear": self.S_END}

    def uv_right(self, x, y, z):
        return ((y - self.U_Y0) * self.S_SIDE, self.V_RIGHT + z * self.S_SIDE)

    def uv_left(self, x, y, z):
        y_max = self.U_Y0 + 1.0 / self.S_SIDE
        return ((y_max - y) * self.S_SIDE, self.V_LEFT + z * self.S_SIDE)

    def uv_top(self, x, y, z):
        return ((y - self.U_Y0) * self.S_TOP, (self.half_w - x) * self.S_TOP)

    def uv_front(self, x, y, z):
        return (0.655 + (1.0 - x) * self.S_END, z * self.S_END)

    def uv_rear(self, x, y, z):
        return (0.655 + (x + 1.0) * self.S_END, 0.15 + z * self.S_END)

    @staticmethod
    def region_for_normal(nx, ny, nz):
        ax, ay, az = abs(nx), abs(ny), abs(nz)
        if ax >= ay and ax >= az:
            return "right" if nx > 0 else "left"
        if az >= ay:
            return "top" if nz > 0 else "under"
        return "front" if ny > 0 else "rear"


class CarSpec:
    def __init__(self, d):
        self.d = d
        self.id = d["id"]
        self.name = d["name"]
        w = d["wheels"]
        self.axle_f, self.axle_r = w["axle_f"], w["axle_r"]
        self.wheel_x, self.wheel_r = w["x"], w["r"]
        self.tire_w, self.rim_r = w["tire_w"], w["rim_r"]
        self.arch_r = w.get("arch_r", self.wheel_r + 0.045)
        # optional rear-axle overrides (bigger rear tyres / wider rear track); default = same as the front
        self.wheel_x_r = w.get("x_rear", self.wheel_x)
        self.wheel_r_r = w.get("r_rear", self.wheel_r)
        self.tire_w_r = w.get("tire_w_rear", self.tire_w)
        self.rim_r_r = w.get("rim_r_rear", self.rim_r)
        self.arch_r_r = w.get("arch_r_rear", self.wheel_r_r + 0.045 if "r_rear" in w else self.arch_r)
        self.wheels = {"FL": (-self.wheel_x, self.axle_f), "FR": (self.wheel_x, self.axle_f),
                       "RL": (-self.wheel_x_r, self.axle_r), "RR": (self.wheel_x_r, self.axle_r)}
        b = d["body"]
        self.round = b.get("round", 0.2)
        self.keys = []
        for st in b["stations"]:
            y, zbelt, crown, scale, zb = st[:5]
            zsh = st[5] if len(st) > 5 and st[5] is not None else b["shoulder"]
            wsh = b["half_w"] * scale
            ws = wsh - b.get("sill_in", 0.04)
            wbelt = wsh - b.get("tumble", 0.04)
            wdeck = wbelt - b.get("deck_in", 0.07)
            zdeck = zbelt + 0.015
            zc = zdeck + 0.008 + crown
            zsh = max(zb + 0.12, min(zsh, zbelt - 0.06))
            self.keys.append((y, zb, ws, wsh, zsh, wbelt, zbelt, wdeck, zdeck, zc))
        self.keys.sort(key=lambda k: -k[0])
        self.y_max, self.y_min = self.keys[0][0], self.keys[-1][0]
        self.half_w = max(k[3] for k in self.keys)
        fl = d.get("flares")
        if fl:
            self.half_w = max(self.half_w, fl["lip"])
        self.gh = d["glasshouse"]
        self.atlas = Atlas(self.y_min, self.y_max, self.half_w, self.gh["z1"] + 0.05)

    def wdims(self, slot):
        """(radius, tyre width, rim radius, arch radius, |x|) of the wheel in this slot."""
        if slot.startswith("F"):
            return self.wheel_r, self.tire_w, self.rim_r, self.arch_r, self.wheel_x
        return self.wheel_r_r, self.tire_w_r, self.rim_r_r, self.arch_r_r, self.wheel_x_r

    # --- section helpers ---------------------------------------------------
    def lerp_keys(self, y):
        k = self.keys
        if y >= k[0][0]:
            return list(k[0][1:])
        if y <= k[-1][0]:
            return list(k[-1][1:])
        for a, b in zip(k, k[1:]):
            if a[0] >= y >= b[0]:
                t = (a[0] - y) / (a[0] - b[0]) if a[0] != b[0] else 0
                return [pa + (pb - pa) * t for pa, pb in zip(a[1:], b[1:])]
        raise ValueError(y)

    def half_profile(self, y):
        """Right-half section from the floor centre round to the deck centre: [(x, z)]."""
        zb, ws, wsh, zsh, wbelt, zbelt, wdeck, zdeck, zc = self.lerp_keys(y)
        P = [(0.0, zb), (ws - 0.05, zb), (ws, zb + 0.06)]
        pts = [(ws, zb + 0.06), (wsh, zsh), (wbelt, zbelt)]
        out = list(P)
        for a, b in zip(pts, pts[1:]):
            out.append(self._bulge(a, b))
            out.append(b)
        out.append((wdeck, zdeck))
        out.append(((wdeck * 0.5), (zdeck + zc) / 2 + 0.004 * self.round))
        out.append((0.0, zc))
        return out

    def _bulge(self, a, b):
        dx, dz = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dz) or 1.0
        nx, nz = dz / L, -dx / L
        k = self.round * L * 0.16
        return ((a[0] + b[0]) / 2 + nx * k, (a[1] + b[1]) / 2 + nz * k)

    def ring(self, y):
        half = self.half_profile(y)
        full = half + [(-x, z) for (x, z) in reversed(half[1:-1])]
        return [(x, y, z) for (x, z) in full]

    def body_half_width_at(self, y, z):
        prof = self.half_profile(y)[1:7]  # sill corner .. belt
        if z <= prof[0][1]:
            return prof[0][0]
        for (x0, z0), (x1, z1) in zip(prof, prof[1:]):
            if min(z0, z1) <= z <= max(z0, z1):
                t = (z - z0) / (z1 - z0) if z1 != z0 else 0
                return x0 + (x1 - x0) * t
        return prof[-1][0]

    def zbelt_at(self, y):
        return self.lerp_keys(y)[5]

    def deck_z_at(self, y):
        return self.lerp_keys(y)[8]

    def nose_half_width(self):
        return self.lerp_keys(self.y_max - 0.04)[2]

    def tail_half_width(self):
        return self.lerp_keys(self.y_min + 0.04)[2]

    # --- glasshouse --------------------------------------------------------
    def gh_feet(self):
        g = self.gh
        return [g["yF0"]] + [p[0] for p in g.get("pillars", [])] + [g["yR0"]]

    def gh_tops(self):
        g = self.gh
        return [g["yF1"]] + [p[1] for p in g.get("pillars", [])] + [g["yR1"]]

    def tub_y(self):
        g = self.gh
        y0 = max(g["yR0"], self.axle_r + self.arch_r_r + 0.03)
        y1 = g["yF0"] - 0.02
        return y0, y1

    def gh_inner_top(self, y):
        """Inner roof height of the glasshouse at y (follows windscreen / rear window)."""
        g = self.gh
        z1 = g["z1"] - 0.05
        if y > g["yF1"]:
            z0 = self.zbelt_at(g["yF0"])
            t = (y - g["yF1"]) / (g["yF0"] - g["yF1"])
            return z1 - t * (z1 - z0) - 0.04
        if y < g["yR1"]:
            z0 = self.zbelt_at(g["yR0"])
            t = (g["yR1"] - y) / (g["yR1"] - g["yR0"])
            return z1 - t * (z1 - z0) - 0.04
        return z1
