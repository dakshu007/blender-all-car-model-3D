"""make_contact_sheet.py - tile the preview renders into one review image.

Plain Python + Pillow (not Blender):
    python3 make_contact_sheet.py --dir Previews --prefix CAR-009 --out Previews/CAR-009_Preview.png
"""

import argparse
import os

from PIL import Image, ImageDraw, ImageFont

ORDER = ["hero", "front34", "rear34", "front", "rear", "left", "right", "top"]


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--dir", required=True)
    p.add_argument("--prefix", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--cols", type=int, default=2)
    p.add_argument("--tile-width", type=int, default=800)
    p.add_argument("--title", default=None)
    a = p.parse_args()
    files = [(v, os.path.join(a.dir, f"{a.prefix}_{v}.png")) for v in ORDER]
    files = [(v, f) for v, f in files if os.path.exists(f)]
    if not files:
        raise SystemExit("No preview renders found")
    ims = []
    for v, f in files:
        im = Image.open(f).convert("RGB")
        h = round(im.height * a.tile_width / im.width)
        ims.append((v, im.resize((a.tile_width, h), Image.LANCZOS)))
    th = ims[0][1].height
    rows = (len(ims) + a.cols - 1) // a.cols
    header = 56
    sheet = Image.new("RGB", (a.cols * a.tile_width, rows * th + header), (245, 238, 228))
    d = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("DejaVuSans.ttf", 22)
        small = ImageFont.truetype("DejaVuSans.ttf", 16)
    except OSError:
        font = small = ImageFont.load_default()
    d.text((16, 16), a.title or a.prefix, fill=(40, 34, 30), font=font)
    for i, (v, im) in enumerate(ims):
        x, y = (i % a.cols) * a.tile_width, header + (i // a.cols) * th
        sheet.paste(im, (x, y))
        d.text((x + 10, y + 8), v, fill=(60, 50, 44), font=small)
    sheet.save(a.out)
    print(f"Contact sheet: {a.out}")


if __name__ == "__main__":
    main()
