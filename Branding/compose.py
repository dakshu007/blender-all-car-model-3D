import math, random, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops
D = sys.argv[1]
random.seed(7)
FB = "/usr/share/fonts/opentype/inter/InterDisplay-BlackItalic.otf"
FCJK = None
import subprocess
try:
    FCJK = subprocess.check_output(["fc-match", "-f", "%{file}", "WenQuanYi Zen Hei:style=Regular"]).decode()
except Exception: pass

car = Image.open(f"{D}/car.png").convert("RGBA")
car = car.crop(car.getbbox())

def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))

def vgrad(w, h, stops):
    arr = np.zeros((h, w, 4), np.uint8)
    for y in range(h):
        t = y / (h - 1)
        for i in range(len(stops) - 1):
            t0, c0 = stops[i]; t1, c1 = stops[i + 1]
            if t0 <= t <= t1:
                c = lerp(c0, c1, (t - t0) / max(1e-6, t1 - t0)); break
        arr[y, :, :] = c
    return Image.fromarray(arr, "RGBA")

def ridge(w, h, base, amp, seed, rough=6, peaks=None):
    rnd = random.Random(seed)
    pts = []
    phases = [rnd.random() * 6.28 for _ in range(rough)]
    for x in range(0, w + 8, 8):
        y = base
        for k in range(rough):
            y -= amp / (k + 1.3) * math.sin(x / w * math.pi * (k + 1.5) + phases[k])
        if peaks:
            for (px, ph, pw) in peaks:
                y -= ph * math.exp(-((x - px) / pw) ** 2)
        pts.append((x, y))
    return [(0, h)] + pts + [(w, h)]

def blossom(draw, cx, cy, r, col, centre=(255, 236, 160, 255)):
    for k in range(5):
        a = k / 5 * math.tau - math.pi / 2
        px, py = cx + math.cos(a) * r * 0.55, cy + math.sin(a) * r * 0.55
        draw.ellipse([px - r * 0.52, py - r * 0.52, px + r * 0.52, py + r * 0.52], fill=col)
    draw.ellipse([cx - r * 0.22, cy - r * 0.22, cx + r * 0.22, cy + r * 0.22], fill=centre)

def petal(draw, cx, cy, s, ang, col):
    pts = []
    for i in range(16):
        t = i / 15 * math.pi
        x = math.cos(t) * s; y = math.sin(t) * s * 0.45 * (1 if i < 8 else 0.8)
        pts.append((x, y))
    pts += [(x, -y) for (x, y) in reversed(pts)]
    ca, sa = math.cos(ang), math.sin(ang)
    draw.polygon([(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in pts], fill=col)

def scene(W, H, car_scale, car_pos, tilt=-5, with_road=True, horizon=0.58):
    S = 2  # supersample
    w, h = W * S, H * S
    img = vgrad(w, h, [(0.0, (34, 20, 58, 255)), (0.32, (122, 42, 96, 255)), (0.55, (238, 112, 108, 255)),
                       (0.70, (255, 178, 120, 255)), (1.0, (255, 214, 170, 255))])
    d = ImageDraw.Draw(img, "RGBA")
    # sun with glow
    sx, sy, sr = w * 0.62, h * (horizon - 0.18), min(w, h) * 0.20
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0)); gd = ImageDraw.Draw(glow)
    gd.ellipse([sx - sr * 1.9, sy - sr * 1.9, sx + sr * 1.9, sy + sr * 1.9], fill=(255, 210, 150, 120))
    glow = glow.filter(ImageFilter.GaussianBlur(sr * 0.6))
    img = Image.alpha_composite(img, glow); d = ImageDraw.Draw(img, "RGBA")
    d.ellipse([sx - sr, sy - sr, sx + sr, sy + sr], fill=(255, 230, 170, 255))
    for i in range(5):   # retro sun bands
        yy = sy + sr * (0.15 + i * 0.17)
        d.rectangle([sx - sr, yy, sx + sr, yy + sr * 0.05 * (1 + i * 0.6)], fill=(238, 112, 108, 255))
    d.ellipse([sx - sr, sy - sr, sx + sr, sy + sr], outline=None)
    # mountains: three ink layers, far ones paler; pagoda on the middle ridge
    layers = [((176, 92, 136, 255), horizon - 0.05, 0.10, 3), ((118, 52, 104, 255), horizon + 0.02, 0.09, 11), ((66, 28, 70, 255), horizon + 0.10, 0.06, 23)]
    for li, (col, base, amp, seed) in enumerate(layers):
        peaks = [(w * 0.22, h * 0.12, w * 0.07)] if li == 0 else ([(w * 0.83, h * 0.10, w * 0.06)] if li == 1 else None)
        d.polygon(ridge(w, h, h * base, h * amp, seed, peaks=peaks), fill=col)
        if li == 1:   # pagoda silhouette on the right peak
            px, py = w * 0.83, h * (base) - h * 0.10 - h * 0.012
            tw = w * 0.05
            for t in range(5):
                ww = tw * (1 - t * 0.14); yy = py - t * h * 0.022
                d.polygon([(px - ww * 0.75, yy), (px + ww * 0.75, yy), (px + ww * 0.42, yy - h * 0.01), (px - ww * 0.42, yy - h * 0.01)], fill=col)
                d.rectangle([px - ww * 0.28, yy - h * 0.022, px + ww * 0.28, yy - h * 0.01], fill=col)
            d.line([(px, py - 5 * h * 0.022), (px, py - 5 * h * 0.022 - h * 0.03)], fill=col, width=max(2, int(w * 0.004)))
        # mist band at the foot of each layer
        mist = Image.new("RGBA", (w, h), (0, 0, 0, 0)); md = ImageDraw.Draw(mist)
        md.rectangle([0, h * (base + 0.03), w, h * (base + 0.07)], fill=(255, 200, 190, 70))
        img = Image.alpha_composite(img, mist.filter(ImageFilter.GaussianBlur(h * 0.02))); d = ImageDraw.Draw(img, "RGBA")
    # ground + sweeping road
    gy = h * (horizon + 0.14)
    d.rectangle([0, gy, w, h], fill=(40, 22, 48, 255))
    if with_road:
        road = [(-w * 0.1, h), (w * 0.30, gy + h * 0.01), (w * 0.42, gy), (w * 1.1, h * 0.98), (w * 1.1, h)]
        d.polygon(road, fill=(28, 26, 38, 255))
        for i in range(7):   # dashed centre line converging to the vanishing point
            t0, t1 = i / 7, i / 7 + 0.07
            def pt(t): return (w * 0.36 + (w * 0.5 - w * 0.36) * t * 1.4, gy + (h - gy) * t)
            a, b = pt(t0), pt(t1)
            d.line([a, b], fill=(255, 236, 200, 220), width=int(2 + 10 * t1 * S))
    # speed streaks behind the car
    for i in range(14):
        y = car_pos[1] * S - h * 0.08 + i * h * 0.012 + random.uniform(-6, 6)
        x0 = random.uniform(0, w * 0.15); x1 = x0 + random.uniform(w * 0.18, w * 0.36)
        d.line([(x0, y), (x1, y)], fill=(255, 240, 230, random.randint(60, 140)), width=random.randint(2, 5) * S // 2 + 1)
    # drift dust cloud behind the rear wheels
    dust = Image.new("RGBA", (w, h), (0, 0, 0, 0)); dd = ImageDraw.Draw(dust)
    for i in range(26):
        cx = car_pos[0] * S - w * 0.22 + random.uniform(-w * 0.1, w * 0.08)
        cy = car_pos[1] * S + h * 0.07 + random.uniform(-h * 0.04, h * 0.03)
        r = random.uniform(w * 0.03, w * 0.075)
        dd.ellipse([cx - r, cy - r * 0.7, cx + r, cy + r * 0.7], fill=(255, 214, 200, random.randint(60, 120)))
    img = Image.alpha_composite(img, dust.filter(ImageFilter.GaussianBlur(w * 0.012))); d = ImageDraw.Draw(img, "RGBA")
    # car with a soft ground shadow
    cw = int(w * car_scale); ch = int(car.height * cw / car.width)
    c = car.resize((cw, ch), Image.LANCZOS).rotate(tilt, resample=Image.BICUBIC, expand=True)
    cx, cy = int(car_pos[0] * S - c.width / 2), int(car_pos[1] * S - c.height / 2)
    sh = Image.new("RGBA", (w, h), (0, 0, 0, 0)); sd = ImageDraw.Draw(sh)
    sd.ellipse([cx + c.width * 0.05, cy + c.height * 0.78, cx + c.width * 0.95, cy + c.height * 1.02], fill=(10, 4, 16, 170))
    img = Image.alpha_composite(img, sh.filter(ImageFilter.GaussianBlur(w * 0.012)))
    img.alpha_composite(c, (cx, cy))
    carbox = (cx + c.width * 0.05, cy + c.height * 0.15, cx + c.width * 0.95, cy + c.height * 0.95)
    d = ImageDraw.Draw(img, "RGBA")
    # blossom branch from the top-left corner
    br = [(-w * 0.02, h * 0.10), (w * 0.10, h * 0.13), (w * 0.20, h * 0.11), (w * 0.30, h * 0.15), (w * 0.38, h * 0.14)]
    d.line(br, fill=(52, 26, 34, 255), width=int(w * 0.012), joint="curve")
    d.line([(w * 0.10, h * 0.13), (w * 0.15, h * 0.21)], fill=(52, 26, 34, 255), width=int(w * 0.008))
    for (bx, by, r) in [(0.06, 0.09, 0.030), (0.12, 0.16, 0.026), (0.17, 0.08, 0.028), (0.22, 0.14, 0.024), (0.15, 0.22, 0.022),
                        (0.27, 0.11, 0.026), (0.33, 0.17, 0.022), (0.36, 0.11, 0.020), (0.03, 0.15, 0.022)]:
        blossom(d, w * bx, h * by, w * r, (255, 182, 204, 255))
    # flying petals
    for i in range(40):
        px_, py_ = random.uniform(0, w), random.uniform(h * 0.05, h * 0.95)
        if carbox[0] < px_ < carbox[2] and carbox[1] < py_ < carbox[3]: continue
        petal(d, px_, py_, random.uniform(w * 0.006, w * 0.014),
              random.uniform(0, 6.28), random.choice([(255, 190, 210, 230), (255, 222, 232, 230), (255, 160, 190, 220)]))
    return img, S

def outline_text(img, xy, text, font, fill, stroke, sw, shadow=True, skew=0.0):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0)); ld = ImageDraw.Draw(layer)
    if shadow:
        ld.text((xy[0] + sw * 0.9, xy[1] + sw * 1.2), text, font=font, fill=(20, 6, 24, 200), stroke_width=sw, stroke_fill=(20, 6, 24, 200))
    ld.text(xy, text, font=font, fill=fill, stroke_width=sw, stroke_fill=stroke)
    img.alpha_composite(layer)

def seal(img, cx, cy, size):
    s = Image.new("RGBA", (int(size), int(size)), (0, 0, 0, 0)); sd = ImageDraw.Draw(s)
    sd.rounded_rectangle([0, 0, size - 1, size - 1], radius=size * 0.12, fill=(196, 30, 42, 255))
    sd.rounded_rectangle([size * 0.08, size * 0.08, size * 0.92, size * 0.92], radius=size * 0.08, outline=(255, 236, 220, 255), width=max(2, int(size * 0.04)))
    if FCJK:
        f = ImageFont.truetype(FCJK, int(size * 0.62))
        sd.text((size / 2, size / 2), "花", font=f, fill=(255, 240, 228, 255), anchor="mm")
    img.alpha_composite(s.rotate(-8, resample=Image.BICUBIC, expand=True), (int(cx - size / 2), int(cy - size / 2)))

# ---------------- app icon 1024 (rounded square) ----------------
W = H = 1024
img, S = scene(W, H, 0.80, (W * 0.54, H * 0.60), tilt=-4, horizon=0.50)
w, h = img.size
fbig = ImageFont.truetype(FB, int(w * 0.135))
# title banner across the bottom
d = ImageDraw.Draw(img, "RGBA")
d.polygon([(0, h * 0.80), (w, h * 0.76), (w, h), (0, h)], fill=(30, 12, 40, 235))
d.line([(0, h * 0.80), (w, h * 0.76)], fill=(255, 196, 92, 255), width=int(w * 0.008))
outline_text(img, (w * 0.07, h * 0.770), "BLOSSOM", fbig, (255, 182, 204, 255), (40, 10, 30, 255), int(w * 0.007))
fr = ImageFont.truetype(FB, int(w * 0.11))
outline_text(img, (w * 0.38, h * 0.862), "RALLY", fr, (255, 255, 255, 255), (196, 30, 42, 255), int(w * 0.009))
seal(img, w * 0.885, h * 0.115, w * 0.13)
icon = img.resize((W, H), Image.LANCZOS)
mask = Image.new("L", (W, H), 0); ImageDraw.Draw(mask).rounded_rectangle([0, 0, W - 1, H - 1], radius=int(W * 0.2), fill=255)
square = icon.copy()                      # full-bleed square (Windows / Unity icon)
icon.putalpha(mask)                       # rounded (macOS look, store pages)
square.save(f"{D}/AppIcon_1024_square.png")
icon.save(f"{D}/AppIcon_1024_rounded.png")
square.save(f"{D}/BlossomRally.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])

# ---------------- wide logo / social banner 1920x1080 ----------------
W, H = 1920, 1080
img, S = scene(W, H, 0.44, (W * 0.66, H * 0.62), tilt=-4, horizon=0.52)
w, h = img.size
f1 = ImageFont.truetype(FB, int(h * 0.17)); f2 = ImageFont.truetype(FB, int(h * 0.20))
outline_text(img, (w * 0.05, h * 0.10), "BLOSSOM", f1, (255, 182, 204, 255), (40, 10, 30, 255), int(h * 0.008))
outline_text(img, (w * 0.05, h * 0.26), "RALLY", f2, (255, 255, 255, 255), (196, 30, 42, 255), int(h * 0.011))
ft = ImageFont.truetype("/usr/share/fonts/opentype/inter/Inter-BoldItalic.otf", int(h * 0.045))
outline_text(img, (w * 0.055, h * 0.50), "DRIFT THROUGH THE CHERRY-BLOSSOM VALLEY", ft, (255, 236, 200, 255), (40, 10, 30, 255), int(h * 0.004))
seal(img, w * 0.47, h * 0.37, h * 0.14)
img.resize((W, H), Image.LANCZOS).save(f"{D}/Logo_Banner_1920x1080.png")
img.resize((1200, 630), Image.LANCZOS).save(f"{D}/Logo_Social_1200x630.png")

# ---------------- transparent wordmark for the title screen ----------------
WM = Image.new("RGBA", (1600, 520), (0, 0, 0, 0))
f1 = ImageFont.truetype(FB, 200); f2 = ImageFont.truetype(FB, 240)
outline_text(WM, (30, 10), "BLOSSOM", f1, (255, 182, 204, 255), (40, 10, 30, 255), 12)
outline_text(WM, (300, 230), "RALLY", f2, (255, 255, 255, 255), (196, 30, 42, 255), 14)
seal(WM, 1450, 140, 200)
WM = WM.crop(WM.getbbox()); WM.save(f"{D}/Logo_Wordmark.png")
print("ok", WM.size)
