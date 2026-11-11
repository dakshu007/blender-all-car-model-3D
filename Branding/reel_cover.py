# Instagram reel cover 1080x1920 from the gameplay screenshot.
# Usage: python3 reel_cover.py <gameplay screenshot> Logo_Wordmark.png <out.jpg>
import math, random, sys
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance, ImageFont, ImageChops

SRC = sys.argv[1]; LOGO = sys.argv[2]; OUT = sys.argv[3]
W, H = 1080, 1920
FB = "/usr/share/fonts/opentype/inter/InterDisplay-BlackItalic.otf"
FBI = "/usr/share/fonts/opentype/inter/Inter-BoldItalic.otf"
FX = "/usr/share/fonts/opentype/inter/Inter-ExtraBold.otf"
PINK = (255, 105, 160); DEEP = (226, 32, 58); CREAM = (255, 246, 236); INK = (30, 14, 24)

src = Image.open(SRC).convert("RGB")
# clean capture artefacts: mouse cursor and the window's bottom bar
src.paste(src.crop((1095, 972, 1121, 1000)), (1122, 974))
src.paste(src.crop((940, 1100, 980, 1118)), (980, 1100))
src.paste(src.crop((940, 1100, 980, 1118)), (1010, 1100))

# 9:16 crop around the car, slightly zoomed so the car reads big
s = H / src.height * 1.08
big = src.resize((round(src.width * s), round(src.height * s)), Image.LANCZOS)
cx = 0.497 * big.width
top = round(0.035 * big.height)
bg = big.crop((round(cx - W / 2), top, round(cx + W / 2), top + H))

# grade: punchier, a touch warmer
bg = ImageEnhance.Contrast(bg).enhance(1.12)
bg = ImageEnhance.Color(bg).enhance(1.18)
r, g, b = bg.split()
r = r.point(lambda v: min(255, int(v * 1.03 + 3)))
b = b.point(lambda v: int(v * 0.97))
bg = Image.merge("RGB", (r, g, b))
# sharpen the car area a little (upscaled source)
bg = bg.filter(ImageFilter.UnsharpMask(radius=2.2, percent=70, threshold=2))

cv = bg.convert("RGBA")

# radial speed blur toward the edges (car stays sharp)
blur = bg.filter(ImageFilter.GaussianBlur(7)).convert("RGBA")
mask = Image.new("L", (W, H), 0)
md = ImageDraw.Draw(mask)
car_c = (W // 2, int(H * 0.53))
for i in range(60, 0, -1):
    t = i / 60
    rx, ry = int(W * 0.95 * t), int(H * 0.62 * t)
    md.ellipse([car_c[0] - rx, car_c[1] - ry, car_c[0] + rx, car_c[1] + ry], fill=int(255 * min(1, max(0, (t - 0.45) / 0.55)) ** 1.3))
cv = Image.composite(blur, cv, mask)

# gradients: top sky lift for the logo, deep bottom for the text
grad = Image.new("RGBA", (W, H), (0, 0, 0, 0))
gd = ImageDraw.Draw(grad)
for y in range(H):
    if y < 640:
        a = int(70 * (1 - y / 640) ** 1.6)
        gd.line([(0, y), (W, y)], fill=(255, 236, 244, a))
    if y > 1180:
        t = (y - 1180) / (H - 1180)
        gd.line([(0, y), (W, y)], fill=(INK[0], INK[1], INK[2], int(235 * t ** 1.25)))
cv = Image.alpha_composite(cv, grad)

# vignette
vig = Image.new("L", (W, H), 0)
vd = ImageDraw.Draw(vig)
vd.ellipse([-260, -200, W + 260, H + 200], fill=255)
vig = vig.filter(ImageFilter.GaussianBlur(160))
dark = Image.new("RGBA", (W, H), (20, 6, 16, 150))
dark.putalpha(ImageChops.invert(vig).point(lambda v: int(v * 0.55)))
cv = Image.alpha_composite(cv, dark)

# drifting petals: a few soft notched petals at different depths
pet = Image.new("RGBA", (W, H), (0, 0, 0, 0))
random.seed(5)
spots = [(84, 700, 30, 1.6), (996, 600, 24, 1.0), (128, 1215, 40, 2.6), (962, 1060, 18, 0.6),
         (860, 520, 13, 0.0), (300, 610, 11, 0.0), (1000, 1460, 22, 1.2)]
for x, y, sz, bl in spots:
    p = Image.new("RGBA", (sz * 4, sz * 4), (0, 0, 0, 0))
    pd = ImageDraw.Draw(p)
    col = random.choice([(255, 186, 208), (255, 168, 198), (255, 206, 222)])
    pts = []
    for i in range(64):
        t = i / 64 * 2 * math.pi
        px_ = math.sin(t) * sz * 0.62 * (1 - 0.25 * math.cos(t))
        py_ = -math.cos(t) * sz
        pts.append((sz * 2 + px_, sz * 2 + py_))
    pd.polygon(pts, fill=col + (240,))
    pd.polygon([(sz * 2, sz * 1.35), (sz * 1.75, sz * 0.9), (sz * 2.25, sz * 0.9)], fill=(0, 0, 0, 0))   # notch
    pd.line([(sz * 2, sz * 2.8), (sz * 2, sz * 1.6)], fill=(255, 140, 180, 120), width=max(1, sz // 12))
    p = p.rotate(random.uniform(0, 360), resample=Image.BICUBIC)
    if bl > 0: p = p.filter(ImageFilter.GaussianBlur(bl))
    pet.alpha_composite(p, (x - sz * 2, y - sz * 2))
cv = Image.alpha_composite(cv, pet)

d = ImageDraw.Draw(cv)

def shadow_text(xy, text, font, fill, shadow=(0, 0, 0, 120), off=(0, 6), blur=10, anchor="la"):
    sh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(sh).text((xy[0] + off[0], xy[1] + off[1]), text, font=font, fill=shadow, anchor=anchor)
    sh = sh.filter(ImageFilter.GaussianBlur(blur))
    cv.alpha_composite(sh)
    ImageDraw.Draw(cv).text(xy, text, font=font, fill=fill, anchor=anchor)

# --- logo
logo = Image.open(LOGO).convert("RGBA")
lw = 880
logo = logo.resize((lw, round(logo.height * lw / logo.width)), Image.LANCZOS)
lsh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
lx, ly = (W - lw) // 2 + 6, 318
a = logo.split()[3].point(lambda v: int(v * 0.45))
lsh.paste(Image.new("RGBA", logo.size, (40, 0, 20, 255)), (lx + 4, ly + 16), a)
lsh = lsh.filter(ImageFilter.GaussianBlur(16))
cv.alpha_composite(lsh)
cv.alpha_composite(logo, (lx, ly))

# --- small kicker above the logo
fk = ImageFont.truetype(FX, 30)
kick = "NEW  ·  OPEN PLAYTEST"
tw = d.textlength(kick, font=fk)
px, py = (W - tw) / 2, 262
pill = Image.new("RGBA", (W, H), (0, 0, 0, 0))
ImageDraw.Draw(pill).rounded_rectangle([px - 26, py - 12, px + tw + 26, py + 46], radius=29, fill=DEEP + (240,))
cv.alpha_composite(pill)
ImageDraw.Draw(cv).text((W / 2, py + 17), kick, font=fk, fill=CREAM, anchor="mm")

# --- headline
f1 = ImageFont.truetype(FB, 132)
f2 = ImageFont.truetype(FB, 132)
y0 = 1352
shadow_text((W / 2, y0), "DRIFT THE", f1, CREAM, anchor="mm", shadow=(0, 0, 0, 150))
shadow_text((W / 2, y0 + 128), "SPRING", f2, PINK, anchor="mm", shadow=(0, 0, 0, 150))
shadow_text((W / 2, y0 + 256), "VALLEY", f2, PINK, anchor="mm", shadow=(0, 0, 0, 150))

# --- speed badge (top-right of the car)
fs = ImageFont.truetype(FB, 64); fu = ImageFont.truetype(FBI, 26)
bx, by = 742, 1150
badge = Image.new("RGBA", (W, H), (0, 0, 0, 0))
bd = ImageDraw.Draw(badge)
bd.polygon([(bx + 18, by), (bx + 300, by), (bx + 282, by + 96), (bx, by + 96)], fill=(20, 8, 16, 190))
bd.polygon([(bx + 4, by + 96), (bx + 282, by + 96), (bx + 279, by + 108), (bx + 2, by + 108)], fill=PINK + (255,))
cv.alpha_composite(badge)
d = ImageDraw.Draw(cv)
d.text((bx + 36, by + 48), "192", font=fs, fill=CREAM, anchor="lm")
d.text((bx + 166, by + 34), "KM/H", font=fu, fill=PINK, anchor="lm")
d.text((bx + 166, by + 64), "GEAR 5", font=fu, fill=CREAM, anchor="lm")

# --- footer
ff = ImageFont.truetype(FX, 30)
foot = "MAC  ·  WINDOWS  ·  NEW UPDATE EVERY THURSDAY"
d.text((W / 2, 1790), foot, font=ff, fill=(255, 236, 244, 230), anchor="mm")
d.line([(W / 2 - 60, 1832), (W / 2 + 60, 1832)], fill=PINK, width=5)

# subtle film grain
noise = Image.effect_noise((W, H), 18).convert("L").point(lambda v: v - 128 + 128)
grain = Image.merge("RGBA", (noise, noise, noise, Image.new("L", (W, H), 10)))
cv = Image.alpha_composite(cv, grain)

cv.convert("RGB").save(OUT, quality=95)
print("saved", OUT)
