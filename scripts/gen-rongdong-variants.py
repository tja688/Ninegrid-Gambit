# -*- coding: utf-8 -*-
"""Generate Apollo-mapped 溶洞.png + theme recolor variants (same structure as 密林_*)."""
from __future__ import annotations

import os
import re
import uuid

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OTHER = os.path.join(ROOT, "Assets", "Resources", "ContentArt", "Png", "Other")
GPL = os.path.join(ROOT, "Assets", "Arts", "配色文件", "apollo.gpl")
SRC_NAME = "溶洞.png"


def load_palette(path: str) -> list[tuple[int, int, int]]:
    colors: list[tuple[int, int, int]] = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            parts = line.split()
            if len(parts) >= 4 and parts[0].isdigit():
                colors.append((int(parts[0]), int(parts[1]), int(parts[2])))
    return colors


def lum(c: tuple[int, int, int]) -> float:
    r, g, b = c
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


PAL = load_palette(GPL)
assert len(PAL) == 46
PAL_SET = set(PAL)

# 5 stops dark→light, Apollo-only. Luminance spaced near source (~34/50/73/101/134),
# capped so highlights stay cave-readable (not neon washout). Suffixes mirror 岩层/密林.
VARIANTS: dict[str, list[tuple[int, int, int]]] = {
    "溶洞": [
        (30, 29, 57),
        (64, 39, 81),
        (122, 54, 123),
        (162, 62, 140),
        (198, 81, 151),
    ],
    "溶洞_岩洞区": [
        (23, 32, 56),
        (37, 58, 94),
        (57, 74, 80),
        (87, 114, 119),
        (79, 143, 186),
    ],
    "溶洞_地下丛林": [
        (25, 51, 45),
        (37, 86, 46),
        (70, 130, 50),
        (117, 167, 67),
        (168, 202, 88),
    ],
    "溶洞_浅岩层": [
        (77, 43, 50),
        (122, 72, 65),
        (173, 119, 87),
        (192, 148, 115),
        (215, 181, 148),
    ],
    "溶洞_熔岩之地": [
        (36, 21, 39),
        (65, 29, 49),
        (96, 44, 44),
        (136, 75, 43),
        (218, 134, 62),
    ],
    "溶洞_藏骨堂": [
        (21, 29, 40),
        (32, 46, 55),
        (57, 74, 80),
        (129, 151, 150),
        (231, 213, 179),
    ],
    "溶洞_失落遗迹": [
        (52, 28, 39),
        (57, 74, 80),
        (87, 114, 119),
        (192, 148, 115),
        (232, 193, 112),
    ],
    "溶洞_血色": [
        (36, 21, 39),
        (65, 29, 49),
        (96, 44, 44),
        (165, 48, 48),
        (223, 132, 165),
    ],
}


def validate_ramps() -> None:
    for name, cols in VARIANTS.items():
        for c in cols:
            if c not in PAL_SET:
                raise SystemExit(f"{name}: {c} not in apollo.gpl")
        ls = [lum(c) for c in cols]
        if any(ls[i] >= ls[i + 1] for i in range(len(ls) - 1)):
            raise SystemExit(f"{name}: non-increasing L { [round(x, 1) for x in ls] }")
        print(f"{name}: L={[round(x, 1) for x in ls]} {cols}")


def new_guid() -> str:
    return uuid.uuid4().hex


def write_meta(stem: str, meta_path: str, template: str) -> str:
    g = new_guid()
    text = re.sub(r"^guid: .*$", f"guid: {g}", template, count=1, flags=re.M)
    text = text.replace("1786760093165_d_0", f"{stem}_0")
    text = text.replace("isReadable: 1", "isReadable: 0")
    with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    return g


def main() -> None:
    validate_ramps()
    src_path = os.path.join(OTHER, SRC_NAME)
    src = np.asarray(Image.open(src_path).convert("RGBA")).copy()
    vis = src[..., 3] > 0
    rgb = src[..., :3]
    uniq = np.unique(rgb[vis], axis=0)
    ul = np.array([lum(tuple(int(x) for x in c)) for c in uniq])
    order = np.argsort(ul)
    ranked = [tuple(int(x) for x in uniq[i]) for i in order]
    print("source ranks:", ranked, [round(float(ul[i]), 1) for i in order])
    if len(ranked) != 5:
        raise SystemExit(f"expected 5 unique colors, got {len(ranked)}")

    with open(src_path + ".meta", encoding="utf-8") as f:
        meta_template = f.read()

    for stem, cols in VARIANTS.items():
        remapped = rgb.copy()
        for src_c, dst_c in zip(ranked, cols):
            mask = (
                (rgb[:, :, 0] == src_c[0])
                & (rgb[:, :, 1] == src_c[1])
                & (rgb[:, :, 2] == src_c[2])
                & vis
            )
            remapped[mask] = dst_c
        out = np.concatenate([remapped, src[..., 3:4]], axis=2)
        out_path = os.path.join(OTHER, f"{stem}.png")
        Image.fromarray(out, "RGBA").save(out_path, optimize=True)

        meta_path = out_path + ".meta"
        if stem == "溶洞" and os.path.exists(meta_path):
            with open(meta_path, encoding="utf-8") as f:
                t = f.read()
            t = t.replace("1786760093165_d_0", "溶洞_0")
            t = t.replace("isReadable: 1", "isReadable: 0")
            with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
                f.write(t)
            guid = "kept"
        else:
            guid = write_meta(stem, meta_path, meta_template)

        a = np.asarray(Image.open(out_path).convert("RGBA"))
        u = {tuple(int(x) for x in c) for c in np.unique(a[a[..., 3] > 0][:, :3], axis=0)}
        bad = u - PAL_SET
        print(f"wrote {stem}.png uniq={len(u)} bad={bad} guid={guid}")


if __name__ == "__main__":
    main()
