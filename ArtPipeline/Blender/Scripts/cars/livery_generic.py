"""livery_generic.py - paint a car's livery atlas from its spec (collection_specs.py).

    python3 livery_generic.py --car CAR-002 --out ../../../VehicleLibrary/CAR-002/Textures/CAR-002_BaseColor.png

Graphics are defined in vehicle world space and pushed through the same atlas
mapping build_generic.py uses for UVs, so stripes line up across panels.
Fonts: DejaVu Sans (free licence). All names are fictional.
"""

import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image, ImageDraw, ImageFont  # noqa: E402

from carspec import CarSpec  # noqa: E402
from collection_specs import SPECS  # noqa: E402

SS = 2
TEX = 2048


def rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def darker(h, k=0.78):
    return "#%02X%02X%02X" % tuple(int(c * k) for c in rgb(h))


class Painter:
    def __init__(self, spec):
        self.S = spec
        self.A = spec.atlas
        self.size = TEX * SS
        self.img = Image.new("RGB", (self.size, self.size), rgb(spec.d["livery"]["base"]))
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
        self.d.line([self.map(region, a, b) for a, b in pts], fill=rgb(color), width=w)

    def circle(self, region, c, r_m, fill, outline=None, width_m=0.0, rect=False):
        p = self.map(region, *c)
        r = r_m * self.A.SCALES[region] * self.size
        w = max(1, int(width_m * self.A.SCALES[region] * self.size)) if outline else 0
        box = [p[0] - r * (1.25 if rect else 1), p[1] - r, p[0] + r * (1.25 if rect else 1), p[1] + r]
        if rect:
            self.d.rounded_rectangle(box, radius=r * 0.25, fill=rgb(fill), outline=rgb(outline) if outline else None,
                                     width=w)
        else:
            self.d.ellipse(box, fill=rgb(fill), outline=rgb(outline) if outline else None, width=w)

    def text(self, region, c, txt, h_m, color, bold=True):
        p = self.map(region, *c)
        size = max(6, int(h_m * self.A.SCALES[region] * self.size * 1.36))
        font = ImageFont.truetype("DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf", size)
        self.d.text(p, txt, font=font, fill=rgb(color), anchor="mm")

    def rect_uv(self, rect, color, outline=None, width=0):
        u0, v0, u1, v1 = rect
        self.d.rectangle([self.px((u0, v1)), self.px((u1, v0))], fill=rgb(color),
                         outline=rgb(outline) if outline else None, width=width * SS)


def band_poly(b, y_lo, y_hi):
    z0, z1 = b["z0"], b["z1"]
    fl = b.get("flick")
    if not fl:
        return [(y_lo, z0), (y_hi, z0), (y_hi, z1), (y_lo, z1)]
    ys, rise = fl
    k = rise / max(1e-3, (y_hi - ys))
    return [(y_lo, z0), (ys, z0), (y_hi, z0 + rise), (y_hi, z1 + rise), (ys, z1), (y_lo, z1)]


def paint(car_id, out):
    S = CarSpec(SPECS[car_id])
    L = S.d["livery"]
    P = Painter(S)
    g = S.gh
    ink = "#1E1D1F"
    y_lo, y_hi = S.y_min - 0.3, S.y_max + 0.3
    P.rect_uv(S.atlas.UNDER_RECT, "#2A2725")

    lower = L.get("lower")
    if lower:
        for r in ("left", "right"):
            P.poly(r, [(y_lo, -0.1), (y_hi, -0.1), (y_hi, lower["z"]), (y_lo, lower["z"])], lower["color"])
        for r in ("front", "rear"):
            P.poly(r, [(-1.1, -0.1), (1.1, -0.1), (1.1, lower["z"]), (-1.1, lower["z"])], lower["color"])

    roof = L.get("roof")
    if roof:
        ys = [g["yR0"] - 0.03 + i * (g["yF0"] - g["yR0"] + 0.06) / 24 for i in range(25)]
        for r in ("left", "right"):
            pts = [(y, S.zbelt_at(y) + 0.012) for y in ys] + [(ys[-1], 1.8), (ys[0], 1.8)]
            P.poly(r, pts, roof)
        P.poly("top", [(-(g["w0"] + 0.01), g["yR0"] - 0.02), (g["w0"] + 0.01, g["yR0"] - 0.02),
                       (g["w0"] + 0.01, g["yF0"] + 0.02), (-(g["w0"] + 0.01), g["yF0"] + 0.02)], roof)
        zf = S.zbelt_at(g["yF0"]) + 0.01
        zr = S.zbelt_at(g["yR0"]) + 0.01
        P.poly("front", [(-1.1, zf), (1.1, zf), (1.1, 1.8), (-1.1, 1.8)], roof)
        P.poly("rear", [(-1.1, zr), (1.1, zr), (1.1, 1.8), (-1.1, 1.8)], roof)

    for b in L.get("bands", []):
        for r in ("left", "right"):
            P.poly(r, band_poly(b, b.get("y0", y_lo), b.get("y1", y_hi)), b["color"])
        if b.get("wrap", True):
            rise = b["flick"][1] if b.get("flick") else 0.0
            P.poly("front", [(-1.1, b["z0"] + rise), (1.1, b["z0"] + rise), (1.1, b["z1"] + rise),
                             (-1.1, b["z1"] + rise)], b["color"])
            P.poly("rear", [(-1.1, b["z0"]), (1.1, b["z0"]), (1.1, b["z1"]), (-1.1, b["z1"])], b["color"])

    for pg in L.get("polys", []):
        for r in ("left", "right"):
            P.poly(r, pg["pts"], pg["color"])

    for s in L.get("top_stripes", []):
        y0, y1 = s.get("y0", y_lo), s.get("y1", y_hi)
        P.poly("top", [(s["x0"], y0), (s["x1"], y0), (s["x1"], y1), (s["x0"], y1)], s["color"])
        # carry the stripe over the nose / tail faces
        top_f = S.zbelt_at(S.y_max) + 0.03
        top_r = S.zbelt_at(S.y_min) + 0.03
        if y1 >= S.y_max:
            P.poly("front", [(s["x0"], top_f - 0.12), (s["x1"], top_f - 0.12), (s["x1"], top_f), (s["x0"], top_f)],
                   s["color"])
        if y0 <= S.y_min:
            P.poly("rear", [(s["x0"], top_r - 0.12), (s["x1"], top_r - 0.12), (s["x1"], top_r), (s["x0"], top_r)],
                   s["color"])

    bon = L.get("bonnet")
    if bon:
        P.poly("top", [(-bon["x"], bon["y0"]), (bon["x"], bon["y0"]), (bon["x"], bon["y1"]), (-bon["x"], bon["y1"])],
               bon["color"])

    for v in L.get("top_vents", []):
        P.poly("top", [(v["x0"], v["y0"]), (v["x1"], v["y0"]), (v["x1"], v["y1"]), (v["x0"], v["y1"])], ink)
        n = v.get("slats", 5)
        for i in range(n):
            yy = v["y0"] + (i + 0.5) * (v["y1"] - v["y0"]) / n
            P.line("top", [(v["x0"] + 0.01, yy), (v["x1"] - 0.01, yy)], "#5A554F", 0.008)

    # seams
    seam = L.get("seam", darker(L["base"]))
    for (yf, yr) in S.d.get("doors", []):
        for y in (yf, yr):
            zb = S.lerp_keys(y)[0]
            for r in ("left", "right"):
                P.line(r, [(y, zb + 0.06), (y, S.zbelt_at(y) - 0.004)], seam, 0.005)
    hood_y0 = g["yF0"] + 0.10
    hx = S.lerp_keys((hood_y0 + S.y_max) / 2)[6] - 0.03
    P.line("top", [(-hx, hood_y0), (-hx, S.y_max - 0.05), (hx, S.y_max - 0.05), (hx, hood_y0)], seam, 0.005)
    if g["yR0"] - S.y_min > 0.5:
        tx = S.lerp_keys((g["yR0"] + S.y_min) / 2)[6] - 0.03
        ty0 = g["yR0"] - 0.08
        P.line("top", [(-tx, ty0), (-tx, S.y_min + 0.06), (tx, S.y_min + 0.06), (tx, ty0)], seam, 0.005)

    rd = L.get("roundel")
    if rd:
        for r in ("left", "right"):
            P.circle(r, (rd["y"], rd["z"]), rd["r"], rd.get("fill", "#FFFFFF"), outline=rd.get("ink", ink),
                     width_m=0.009, rect=rd.get("shape") == "rect")
            P.text(r, (rd["y"], rd["z"] - 0.003), L["number"], rd["r"] * 0.75, rd.get("ink", ink))
    rr = L.get("roof_roundel")
    if rr:
        P.circle("top", (0.0, rr["y"]), rr["r"], "#FFFFFF", outline=ink, width_m=0.014)
        P.text("top", (0.0, rr["y"]), L["number"], rr["r"] * 0.75, ink)

    for t in L.get("texts", []):
        for r in ("left", "right"):
            P.text(r, (t["y"], t["z"]), t["text"], t["h"], t["color"], t.get("bold", True))

    for rect in (S.atlas.PLATE_FRONT_RECT, S.atlas.PLATE_REAR_RECT):
        P.rect_uv(rect, "#F4F1E8", outline=ink, width=3)
        u0, v0, u1, v1 = rect
        font = ImageFont.truetype("DejaVuSans-Bold.ttf", int((v1 - v0) * P.size * 0.62))
        P.d.text(P.px(((u0 + u1) / 2, (v0 + v1) / 2)), L.get("plate", car_id), font=font, fill=rgb(ink),
                 anchor="mm")

    img = P.img.resize((TEX, TEX), Image.LANCZOS)
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    img.save(out, optimize=True)
    print(f"Livery texture written: {out}")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--car", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    paint(a.car, a.out)
