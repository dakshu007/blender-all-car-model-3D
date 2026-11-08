"""make_anim.py - turn a rendered frame folder into a looping GIF (+ MP4 when ffmpeg exists).

    python3 make_anim.py --frames anim/dio_hero_frames --out Previews/Animation/ENV_Diorama_Loop --fps 12
"""

import argparse
import glob
import os
import shutil
import subprocess

from PIL import Image


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--frames", required=True)
    ap.add_argument("--out", required=True, help="output path without extension")
    ap.add_argument("--fps", type=int, default=12)
    ap.add_argument("--gif-width", type=int, default=560)
    a = ap.parse_args()
    files = sorted(glob.glob(os.path.join(a.frames, "f*.png")))
    if not files:
        raise SystemExit("no frames")
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    ims = []
    for f in files:
        im = Image.open(f).convert("RGB")
        h = round(im.height * a.gif_width / im.width)
        ims.append(im.resize((a.gif_width, h), Image.LANCZOS).quantize(colors=200, method=Image.MEDIANCUT))
    ims[0].save(a.out + ".gif", save_all=True, append_images=ims[1:], duration=int(1000 / a.fps), loop=0,
                optimize=True)
    print(f"GIF: {a.out}.gif ({len(files)} frames)")
    if shutil.which("ffmpeg"):
        cmd = ["ffmpeg", "-y", "-loglevel", "error", "-framerate", str(a.fps), "-i", os.path.join(a.frames, "f%03d.png"),
               "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20",
               a.out + ".mp4"]
        subprocess.run(cmd, check=True)
        print(f"MP4: {a.out}.mp4")


if __name__ == "__main__":
    main()
