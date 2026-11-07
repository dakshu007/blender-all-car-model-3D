"""livery_car009.py - paint the CAR-009 base-colour livery atlas (original design).

Plain Python + Pillow (no Blender needed):
    python3 livery_car009.py --out ../../../VehicleLibrary/CAR-009/Textures/CAR-009_BaseColor.png

Every graphic is defined in vehicle world space (metres) and pushed through the
same UV functions that build_car009.py uses to unwrap the body, so stripes line
up across panels. Fonts: DejaVu Sans (free licence), all names are fictional.
"""

import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image, ImageDraw, ImageFont  # noqa: E402

import car009_spec as S  # noqa: E402

SS = 2  # supersampling for clean edges


def hexrgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


class Painter:
    def __init__(self, size):
        self.size = size * SS
        self.img = Image.new("RGB", (self.size, self.size), hexrgb(S.PAINT))
        self.d = ImageDraw.Draw(self.img)

    def px(self, uv):
        u, v = uv
        return (u * self.size, (1.0 - v) * self.size)

    def poly(self, region, pts, color, axis):
        """pts are 2D in the region's world plane: side=(y,z), top=(x,y), front/rear=(x,z)."""
        f = S.UV_FUNCS[region]
        out = []
        for a, b in pts:
            if axis == "yz":
                out.append(self.px(f(0, a, b)))
            elif axis == "xy":
                out.append(self.px(f(a, b, 0)))
            else:
                out.append(self.px(f(a, 0, b)))
        self.d.polygon(out, fill=hexrgb(color))

    def rect_uv(self, rect, color, outline=None, width=0):
        u0, v0, u1, v1 = rect
        a = self.px((u0, v1))
        b = self.px((u1, v0))
        self.d.rectangle([a, b], fill=hexrgb(color), outline=hexrgb(outline) if outline else None,
                         width=width * SS)

    def text(self, region, world_xy, axis, txt, height_m, color, scale, bold=True, anchor="mm"):
        f = S.UV_FUNCS[region]
        a, b = world_xy
        p = f(0, a, b) if axis == "yz" else (f(a, b, 0) if axis == "xy" else f(a, 0, b))
        px_per_m = scale * self.size
        font = ImageFont.truetype("DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf",
                                  max(6, int(height_m * px_per_m * 1.36)))
        self.d.text(self.px(p), txt, font=font, fill=hexrgb(color), anchor=anchor)

    def circle(self, region, world_xy, axis, r_m, fill, scale, outline=None, width_m=0.0):
        f = S.UV_FUNCS[region]
        a, b = world_xy
        p = self.px(f(0, a, b) if axis == "yz" else (f(a, b, 0) if axis == "xy" else f(a, 0, b)))
        r = r_m * scale * self.size
        w = max(1, int(width_m * scale * self.size)) if outline else 0
        self.d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=hexrgb(fill),
                       outline=hexrgb(outline) if outline else None, width=w)

    def line(self, region, pts, axis, color, width_m, scale):
        f = S.UV_FUNCS[region]
        out = []
        for a, b in pts:
            out.append(self.px(f(0, a, b) if axis == "yz" else (f(a, b, 0) if axis == "xy" else f(a, 0, b))))
        self.d.line(out, fill=hexrgb(color), width=max(1, int(width_m * scale * self.size)))


def side_band(lo, hi, flick=0.14, y_flick=1.30, y_end=2.2, y_start=-2.2):
    """A horizontal band that sweeps upward toward the nose."""
    return [(y_start, lo), (y_flick, lo), (y_end, lo + flick + (y_end - 1.98) * 0.2),
            (y_end, hi + flick + (y_end - 1.98) * 0.2), (y_flick, hi), (y_start, hi)]


def paint(out):
    P = Painter(S.TEX_SIZE)
    sc_side, sc_top, sc_end = S.S_SIDE, S.S_TOP, S.S_END

    # Underbody block (faces pointing down)
    P.rect_uv(S.UNDER_RECT, S.UNDERBODY)

    for region in ("left", "right"):
        # Stripe stack: mustard pin, teal band, orange pin
        P.poly(region, side_band(0.405, 0.425), S.MUSTARD, "yz")
        P.poly(region, side_band(0.440, 0.520), S.TEAL, "yz")
        P.poly(region, side_band(0.535, 0.560), S.ORANGE, "yz")
        # Panel seams (door, front wing edge, rear quarter)
        for y in S.DOOR_Y:
            P.line(region, [(y, 0.27), (y, 0.855)], "yz", S.SEAM, 0.006, sc_side)
        # Door number roundel
        P.circle(region, (0.08, 0.675), "yz", 0.118, "#FFFFFF", sc_side, outline=S.INK, width_m=0.010)
        P.text(region, (0.08, 0.672), "yz", S.RACE_NUMBER, 0.088, S.INK, sc_side)
        # Crew names under the side window
        P.text(region, (0.10, 0.818), "yz", S.CREW, 0.026, S.INK, sc_side, bold=False)
        # Sponsor on the rear quarter above the flare
        P.text(region, (-1.14, 0.812), "yz", S.SPONSOR, 0.050, S.TEAL, sc_side)
        # Small sponsor on the lower door, between skirt and stripe stack
        P.text(region, (0.10, 0.355), "yz", "ORSA TYRES", 0.032, S.TEAL, sc_side)
    # Fuel filler on the right rear quarter only
    P.circle("right", (-1.52, 0.80), "yz", 0.042, "#D8CFBF", sc_side, outline=S.SEAM, width_m=0.006)

    # --- Top: twin stripes nose to tail, hood vents, seams, roof number --------
    for sgn in (-1, 1):
        x0, x1 = 0.10 * sgn, 0.24 * sgn
        P.poly("top", [(x0, -2.2), (x1, -2.2), (x1, 2.2), (x0, 2.2)], S.TEAL, "xy")
        e0, e1 = 0.245 * sgn, 0.268 * sgn
        P.poly("top", [(e0, -2.2), (e1, -2.2), (e1, 2.2), (e0, 2.2)], S.ORANGE, "xy")
        # bonnet louvres
        vx0, vx1 = 0.34 * sgn, 0.56 * sgn
        P.poly("top", [(vx0, 1.12), (vx1, 1.12), (vx1, 1.46), (vx0, 1.46)], S.INK, "xy")
        for i in range(6):
            yy = 1.15 + i * 0.053
            P.line("top", [(vx0 * 0.97, yy), (vx1 * 0.985, yy)], "xy", "#55504A", 0.008, sc_top)
    # hood + trunk shut lines
    P.line("top", [(-0.70, 0.84), (-0.70, 1.93), (0.70, 1.93), (0.70, 0.84)], "xy", S.SEAM, 0.006, sc_top)
    P.line("top", [(-0.68, -0.88), (-0.68, -1.86), (0.68, -1.86), (0.68, -0.88)], "xy", S.SEAM, 0.006, sc_top)
    # roof number roundel
    P.circle("top", (0.0, -0.15), "xy", 0.21, "#FFFFFF", sc_top, outline=S.INK, width_m=0.016)
    P.text("top", (0.0, -0.15), "xy", S.RACE_NUMBER, 0.155, S.INK, sc_top)

    # --- Front + rear faces: stripe stack continues around the car -------------
    flick = 0.14  # side band rise at the nose (y = 1.98)
    P.poly("front", [(-1, 0.405 + flick), (1, 0.405 + flick), (1, 0.425 + flick), (-1, 0.425 + flick)], S.MUSTARD, "xz")
    P.poly("front", [(-1, 0.44 + flick), (1, 0.44 + flick), (1, 0.52 + flick), (-1, 0.52 + flick)], S.TEAL, "xz")
    P.poly("front", [(-1, 0.535 + flick), (1, 0.535 + flick), (1, 0.56 + flick), (-1, 0.56 + flick)], S.ORANGE, "xz")
    P.poly("rear", [(-1, 0.405), (1, 0.405), (1, 0.425), (-1, 0.425)], S.MUSTARD, "xz")
    P.poly("rear", [(-1, 0.44), (1, 0.44), (1, 0.52), (-1, 0.52)], S.TEAL, "xz")
    P.poly("rear", [(-1, 0.535), (1, 0.535), (1, 0.56), (-1, 0.56)], S.ORANGE, "xz")

    # --- Number plates (fictional) ------------------------------------------------
    for rect in (S.PLATE_FRONT_RECT, S.PLATE_REAR_RECT):
        P.rect_uv(rect, "#F4F1E8", outline=S.INK, width=3)
        u0, v0, u1, v1 = rect
        font = ImageFont.truetype("DejaVuSans-Bold.ttf", int((v1 - v0) * P.size * 0.62))
        P.d.text(P.px(((u0 + u1) / 2, (v0 + v1) / 2)), S.PLATE_TEXT, font=font, fill=hexrgb(S.INK), anchor="mm")

    img = P.img.resize((S.TEX_SIZE, S.TEX_SIZE), Image.LANCZOS)
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    img.save(out, optimize=True)
    print(f"Livery texture written: {out} ({S.TEX_SIZE}x{S.TEX_SIZE})")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    paint(ap.parse_args().out)
