"""env_textures.py - small generated textures for the environment kit (Pillow, no Blender).

    python3 env_textures.py --out-dir ../../../EnvironmentLibrary/Textures

Signs use WenQuanYi Zen Hei (GPL + font exception, OK for rendered images).
Road textures tile seamlessly.
"""

import argparse
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

CJK = "/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc"


def font(size, cjk=False):
    try:
        return ImageFont.truetype(CJK if cjk else "DejaVuSans-Bold.ttf", size)
    except OSError:
        return ImageFont.load_default()


def plaque(path, text, w=1024, h=384, bg=(28, 52, 92), fg=(232, 190, 92)):
    im = Image.new("RGB", (w, h), bg)
    d = ImageDraw.Draw(im)
    d.rectangle([12, 12, w - 13, h - 13], outline=fg, width=14)
    d.rectangle([40, 40, w - 41, h - 41], outline=fg, width=4)
    d.text((w / 2, h / 2 + 6), text, font=font(int(h * 0.62), cjk=True), fill=fg, anchor="mm")
    im.save(path)


def vertical_sign(path, text, w=256, h=768):
    im = Image.new("RGB", (w, h), (120, 28, 24))
    d = ImageDraw.Draw(im)
    d.rectangle([8, 8, w - 9, h - 9], outline=(232, 190, 92), width=10)
    n = len(text)
    for i, ch in enumerate(text):
        d.text((w / 2, h * (i + 0.5) / n), ch, font=font(int(min(w * 0.7, h / n * 0.8)), cjk=True),
               fill=(245, 214, 120), anchor="mm")
    im.save(path)


def start_banner(path, w=1024, h=256):
    im = Image.new("RGB", (w, h), (245, 240, 230))
    d = ImageDraw.Draw(im)
    s = 32
    for x in range(0, w, s):
        for y in (0, s):
            if (x // s + y // s) % 2 == 0:
                d.rectangle([x, y, x + s - 1, y + s - 1], fill=(28, 28, 30))
            if (x // s + (y // s)) % 2 == 1:
                d.rectangle([x, h - 2 * s + y, x + s - 1, h - 2 * s + y + s - 1], fill=(28, 28, 30))
    d.text((w / 2, h / 2), "START  起点", font=font(int(h * 0.36), cjk=True), fill=(200, 40, 34), anchor="mm")
    im.save(path)


def noise_tex(path, base, var, speck, size=512, seed=1, blur=0.6, streaks=None):
    rng = random.Random(seed)
    im = Image.new("RGB", (size, size), base)
    px = im.load()
    for y in range(size):
        for x in range(size):
            v = rng.uniform(-var, var)
            px[x, y] = tuple(max(0, min(255, int(c + v))) for c in base)
    d = ImageDraw.Draw(im)
    for _ in range(int(size * size * speck)):
        x, y = rng.randrange(size), rng.randrange(size)
        r = rng.choice((1, 1, 2))
        dv = rng.uniform(-55, 55)
        c = tuple(max(0, min(255, int(b + dv))) for b in base)
        d.ellipse([x - r, y - r, x + r, y + r], fill=c)
        for dx in (-size, size):  # wrap for seamless tiling
            d.ellipse([x - r + dx, y - r, x + r + dx, y + r], fill=c)
    if streaks:
        for col, x0, x1 in streaks:  # tyre ruts / grass strip, vertical (along the road)
            d.rectangle([int(x0 * size), 0, int(x1 * size), size], fill=col)
    im = im.filter(ImageFilter.GaussianBlur(blur))
    im.save(path)


def paving(path, size=512, seed=4):
    rng = random.Random(seed)
    im = Image.new("RGB", (size, size), (120, 114, 104))
    d = ImageDraw.Draw(im)
    rows = 8
    rh = size // rows
    for r in range(rows):
        x = -rng.randint(0, 40) if r % 2 else 0
        while x < size:
            w = rng.randint(48, 96)
            g = rng.randint(150, 190)
            c = (g, g - 6, g - 16)
            d.rounded_rectangle([x + 3, r * rh + 3, x + w - 3, r * rh + rh - 3], radius=6, fill=c)
            x += w
    im = im.filter(ImageFilter.GaussianBlur(0.8))
    im.save(path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out-dir", required=True)
    a = ap.parse_args()
    os.makedirs(a.out_dir, exist_ok=True)
    o = lambda n: os.path.join(a.out_dir, n)  # noqa: E731
    plaque(o("ENV_Plaque_BlossomVillage.png"), "花村")
    vertical_sign(o("ENV_Sign_Teahouse.png"), "茶馆")
    start_banner(o("ENV_Banner_Start.png"))
    noise_tex(o("ENV_Road_Asphalt.png"), (78, 80, 86), 10, 0.010, seed=1)
    noise_tex(o("ENV_Road_Gravel.png"), (196, 178, 140), 16, 0.06, seed=2, blur=0.4,
              streaks=[((176, 158, 122), 0.22, 0.32), ((176, 158, 122), 0.68, 0.78)])
    noise_tex(o("ENV_Road_Dirt.png"), (150, 112, 74), 14, 0.02, seed=3, blur=0.9,
              streaks=[((128, 94, 60), 0.20, 0.33), ((128, 94, 60), 0.67, 0.80), ((110, 150, 70), 0.44, 0.56)])
    paving(o("ENV_Road_Paving.png"))
    print(f"textures written to {a.out_dir}")


if __name__ == "__main__":
    main()
