# -*- coding: utf-8 -*-
"""
palette-compare.py — 生成 原图|映射后 对比图，供目检

复用 palette-map-apollo.py 的调色板与映射器，把指定素材按一个或多个区域
profile 映射后与原图并排拼图，输出到 .scratch/palette-map/compare/。

用法：
  py -3.12 scripts/palette-compare.py neutral "Assets/Resources/ContentArt/Png/Other/岩层_浅岩层.png"
  py -3.12 scripts/palette-compare.py neutral,rongyan <相对路径>...   # 多 profile 并排
"""
import os
import sys
import importlib.util

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
spec = importlib.util.spec_from_file_location(
    "pm", os.path.join(ROOT, "scripts", "palette-map-apollo.py"))
pm = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pm)

OUT = os.path.join(ROOT, ".scratch", "palette-map", "compare")
os.makedirs(OUT, exist_ok=True)

mapper = pm.Mapper(pm.load_palette(pm.GPL_PATH))

CHECKER = (90, 90, 90), (120, 120, 120)


def on_checker(im):
    bg = Image.new("RGBA", im.size)
    px = bg.load()
    for y in range(im.size[1]):
        for x in range(im.size[0]):
            px[x, y] = CHECKER[((x // 8) + (y // 8)) % 2] + (255,)
    bg.alpha_composite(im)
    return bg


def map_im(im, profile):
    a = np.asarray(im.convert("RGBA")).copy()
    rgb = a[..., :3].reshape(-1, 3)
    alpha = a[..., 3].reshape(-1)
    vis = alpha > 0
    if vis.any():
        uniq, inv = np.unique(rgb[vis], axis=0, return_inverse=True)
        rgb[vis] = mapper.map_colors(uniq, profile)[inv]
    out = np.concatenate([rgb.reshape(a.shape[:2] + (3,)), a[..., 3:4]], axis=2)
    return Image.fromarray(out, "RGBA")


def compare(relpath, profiles, scale=None, tag=""):
    src = os.path.join(ROOT, relpath)
    im = Image.open(src).convert("RGBA")
    if scale is None:
        scale = max(1, min(6, 480 // max(im.size)))
    cells = [on_checker(im)] + [on_checker(map_im(im, p)) for p in profiles]
    cells = [c.resize((c.size[0] * scale, c.size[1] * scale), Image.NEAREST) for c in cells]
    gap = 8
    w = sum(c.size[0] for c in cells) + gap * (len(cells) - 1)
    h = max(c.size[1] for c in cells)
    sheet = Image.new("RGBA", (w, h), (30, 30, 30, 255))
    x = 0
    for c in cells:
        sheet.paste(c, (x, 0))
        x += c.size[0] + gap
    name = tag or os.path.splitext(os.path.basename(relpath))[0]
    outp = os.path.join(OUT, name + ".png")
    sheet.convert("RGB").save(outp)
    print(outp)


if __name__ == "__main__":
    profiles = [p.strip() for p in sys.argv[1].split(",")]
    for rp in sys.argv[2:]:
        compare(rp, profiles)
