"""make_fleet_sheet.py - one review image for several cars (hero + rear 3/4 per car, with name / class).

Plain Python + Pillow (not Blender); reads the per-car preview renders made by render_previews.py:
    python3 make_fleet_sheet.py --lib ../../VehicleLibrary --cars new \
        --out ../../VehicleLibrary/NEW_CARS_ContactSheet.png --title "Blossom Rally - new cars"
"""

import argparse
import os
import sys

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "cars"))
from collection_specs import NEW_ORDER, ORDER, SPECS  # noqa: E402


def font(size, bold=False):
    try:
        return ImageFont.truetype("DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf", size)
    except OSError:
        return ImageFont.load_default()


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--lib", required=True)
    p.add_argument("--cars", nargs="+", default=["new"])
    p.add_argument("--out", required=True)
    p.add_argument("--views", nargs="+", default=["hero", "rear34"])
    p.add_argument("--cols", type=int, default=3, help="cars per row")
    p.add_argument("--tile-width", type=int, default=520)
    p.add_argument("--title", default="Blossom Rally - new cars")
    a = p.parse_args()
    cars = NEW_ORDER if a.cars == ["new"] else ORDER if a.cars == ["all"] else a.cars
    tw = a.tile_width
    th = round(tw * 500 / 800)
    label = 46
    gap = 14
    cell_w = tw * len(a.views)
    cell_h = th + label
    rows = (len(cars) + a.cols - 1) // a.cols
    header = 70
    W = a.cols * cell_w + (a.cols + 1) * gap
    H = header + rows * (cell_h + gap) + gap
    sheet = Image.new("RGB", (W, H), (245, 238, 228))
    d = ImageDraw.Draw(sheet)
    d.text((gap, 18), a.title, fill=(40, 34, 30), font=font(30, True))
    for i, car in enumerate(cars):
        x0 = gap + (i % a.cols) * (cell_w + gap)
        y0 = header + (i // a.cols) * (cell_h + gap)
        for j, v in enumerate(a.views):
            f = os.path.join(a.lib, car, "Previews", f"{car}_{v}.png")
            if not os.path.exists(f):
                continue
            im = Image.open(f).convert("RGB")
            # crop to the 8:5 tile around the centre, then scale
            r = im.width / im.height
            if r > tw / th:
                nw = round(im.height * tw / th)
                im = im.crop(((im.width - nw) // 2, 0, (im.width + nw) // 2, im.height))
            sheet.paste(im.resize((tw, th), Image.LANCZOS), (x0 + j * tw, y0 + label))
        s = SPECS[car]
        d.rectangle([x0, y0, x0 + cell_w - 1, y0 + label - 1], fill=(40, 34, 30))
        d.text((x0 + 12, y0 + 9), f"{car}  {s['name']}", fill=(250, 244, 236), font=font(22, True))
        d.text((x0 + cell_w - 12, y0 + 13), f"{s['class']}  ·  {s.get('drivetrain', 'AWD')}", fill=(240, 160, 180),
               font=font(17), anchor="ra")
    sheet.save(a.out)
    print(f"Fleet sheet: {a.out}  ({W}x{H})")


if __name__ == "__main__":
    main()
