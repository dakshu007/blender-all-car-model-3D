"""livery_bike.py - paint a bike's livery atlas from its spec (bike_specs.py).

    python3 livery_bike.py --bike BIKE-001 --out ../../../VehicleLibrary/BIKE-001/Textures/BIKE-001_BaseColor.png

Same idea as cars/livery_generic.py: graphics are defined in vehicle space (side view y/z, top view x/y)
and pushed through the BikeAtlas mapping that build_bike.py uses for the UVs, so stripes line up across the
tank, fairing and tail. Fonts: DejaVu Sans (free licence). All names are fictional.
"""

import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image, ImageDraw, ImageFont  # noqa: E402

from bike_specs import BIKES, BikeAtlas  # noqa: E402

SS = 2
TEX = 1024
INK = "#1E1D1F"


def rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def darker(h, k=0.8):
    return "#%02X%02X%02X" % tuple(int(c * k) for c in rgb(h))


class Painter:
    def __init__(self, base):
        self.A = BikeAtlas()
        self.size = TEX * SS
        self.img = Image.new("RGB", (self.size, self.size), rgb(base))
        self.d = ImageDraw.Draw(self.img)

    def px(self, uv):
        return (uv[0] * self.size, (1.0 - uv[1]) * self.size)

    def map(self, region, a, b):
        f = self.A.UV_FUNCS[region]
        if region in ("left", "right"):
            return self.px(f(0, a, b))      # (y, z)
        if region == "top":
            return self.px(f(a, b, 0))      # (x, y)
        return self.px(f(a, 0, b))          # (x, z)

    def poly(self, region, pts, color):
        self.d.polygon([self.map(region, a, b) for a, b in pts], fill=rgb(color))

    def line(self, region, pts, color, width_m):
        w = max(1, int(width_m * self.A.SCALES[region] * self.size))
        self.d.line([self.map(region, a, b) for a, b in pts], fill=rgb(color), width=w, joint="curve")

    def circle(self, region, c, r_m, fill, outline=None, width_m=0.0):
        p = self.map(region, *c)
        r = r_m * self.A.SCALES[region] * self.size
        w = max(1, int(width_m * self.A.SCALES[region] * self.size)) if outline else 0
        self.d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=rgb(fill),
                       outline=rgb(outline) if outline else None, width=w)

    def text(self, region, c, txt, h_m, color, bold=True):
        if not txt:
            return
        p = self.map(region, *c)
        size = max(6, int(h_m * self.A.SCALES[region] * self.size * 1.36))
        font = ImageFont.truetype("DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf", size)
        self.d.text(p, txt, font=font, fill=rgb(color), anchor="mm")

    def rect_uv(self, rect, color, outline=None, width=0):
        u0, v0, u1, v1 = rect
        self.d.rectangle([self.px((u0, v1)), self.px((u1, v0))], fill=rgb(color),
                         outline=rgb(outline) if outline else None, width=width * SS)

    def blossom(self, region, c, r, petal="#F7C6D3", heart="#E26A8A"):
        """Five-petal cherry blossom (the Blossom Rally motif)."""
        for k in range(5):
            a = math.radians(90 + 72 * k)
            self.circle(region, (c[0] + math.cos(a) * r * 0.55, c[1] + math.sin(a) * r * 0.55), r * 0.5, petal)
        self.circle(region, c, r * 0.28, heart)


def paint(bike_id, out):
    spec = BIKES[bike_id]
    L = spec["livery"]
    c = spec["colors"]
    P = Painter(L["base"])
    sides = ("left", "right")
    P.rect_uv(P.A.UNDER_RECT, "#2A2725")

    for pg in L.get("polys", []):
        for r in sides:
            P.poly(r, pg["pts"], pg["color"])

    for s in L.get("top_stripes", []):
        P.poly("top", [(s["x0"], -1.4), (s["x1"], -1.4), (s["x1"], 1.4), (s["x0"], 1.4)], s["color"])
        for r in ("front", "rear"):
            P.poly(r, [(s["x0"], 0.0), (s["x1"], 0.0), (s["x1"], 1.6), (s["x0"], 1.6)], s["color"])

    if L.get("pinstripes"):     # cruiser: gold coach lines on the tank and the fenders
        gold = c["accent"]
        for r in sides:
            P.line(r, [(-0.12, 0.84), (0.08, 0.88), (0.34, 0.86), (0.48, 0.80)], gold, 0.008)
            P.line(r, [(-0.10, 0.80), (0.10, 0.83), (0.32, 0.81), (0.44, 0.76)], gold, 0.004)
            P.line(r, [(-1.18, 0.46), (-1.02, 0.62), (-0.80, 0.70), (-0.56, 0.66)], gold, 0.007)
            P.line(r, [(0.62, 0.30), (0.78, 0.40), (1.00, 0.42), (1.16, 0.32)], gold, 0.006)
        P.poly("top", [(-0.012, -1.4), (0.012, -1.4), (0.012, 1.4), (-0.012, 1.4)], gold)

    for (y, z) in L.get("lanterns", []):     # red-lantern emblem: gold ring, cream glow, tassel
        for r in sides:
            P.circle(r, (y, z), 0.052, c["accent"])
            P.circle(r, (y, z), 0.042, c["accent2"])
            P.circle(r, (y, z), 0.030, "#D8452F")
            P.line(r, [(y, z - 0.052), (y, z - 0.085)], c["accent"], 0.008)

    for (y, z, rr) in L.get("blossoms", []):
        for r in sides:
            P.blossom(r, (y, z), rr)
    fb = L.get("front_badge")
    if fb:
        P.blossom("front", (0.0, fb["z"]), fb["r"] * 1.4)

    sp = L.get("side_plate")
    if sp:
        for r in sides:
            P.poly(r, [(sp["y0"], sp["z0"]), (sp["y1"], sp["z0"]), (sp["y1"], sp["z1"]), (sp["y0"], sp["z1"])], sp["fill"])
            P.text(r, ((sp["y0"] + sp["y1"]) / 2, (sp["z0"] + sp["z1"]) / 2), L["number"], (sp["z1"] - sp["z0"]) * 0.62,
                   sp["ink"])
    fn = L.get("front_number")
    if fn:
        P.circle("front", (0.0, fn["z"]), 0.075, fn["fill"])
        P.text("front", (0.0, fn["z"]), L["number"], 0.07, fn["ink"])

    if L.get("spots"):          # snow leopard rosettes on the tank shrouds
        import random
        rnd = random.Random(5)
        for r in sides:
            for _ in range(26):
                y = rnd.uniform(-0.05, 0.55)
                z = rnd.uniform(0.82, 1.08) - (0.0 if y < 0.35 else (y - 0.35) * 0.4)
                P.circle(r, (y, z), 0.012, "#9A927F")
                P.circle(r, (y, z), 0.006, L["base"])

    rd = L.get("roundel")
    if rd:
        for r in sides:
            P.circle(r, (rd["y"], rd["z"]), rd["r"], rd.get("fill", "#FFFFFF"), outline=rd.get("ink", INK), width_m=0.008)
            P.text(r, (rd["y"], rd["z"] - 0.003), L["number"], rd["r"] * 0.8, rd.get("ink", INK))

    for t in L.get("texts", []):
        for r in sides:
            P.text(r, (t["y"], t["z"]), t["text"], t["h"], t["color"], t.get("bold", True))

    # plates (rear; the front rect only exists for tooling symmetry)
    for rect in (P.A.PLATE_REAR_RECT, P.A.PLATE_FRONT_RECT):
        P.rect_uv(rect, "#F4F1E8", outline=INK, width=3)
        u0, v0, u1, v1 = rect
        font = ImageFont.truetype("DejaVuSans-Bold.ttf", int((v1 - v0) * P.size * 0.36))
        txt = L.get("plate", bike_id)
        a, b = (txt.split(" ", 1) + [""])[:2]
        P.d.text(P.px(((u0 + u1) / 2, v0 + (v1 - v0) * 0.70)), a, font=font, fill=rgb(INK), anchor="mm")
        P.d.text(P.px(((u0 + u1) / 2, v0 + (v1 - v0) * 0.30)), b, font=font, fill=rgb(INK), anchor="mm")

    img = P.img.resize((TEX, TEX), Image.LANCZOS)
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    img.save(out, optimize=True)
    print(f"Livery texture written: {out}")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--bike", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    paint(a.bike, a.out)
