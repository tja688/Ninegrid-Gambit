# -*- coding: utf-8 -*-
"""
palette-map-apollo.py — 全量素材 Apollo 调色板映射工具

把指定目录下所有 PNG 的像素颜色映射到 Apollo 46 色调色板（Oklab 最近色），
支持按"区域"给不同色阶(ramp)加权，让不同怪物卡组/地区呈现不同色彩偏重。

用法示例：
  python palette-map-apollo.py --pilot          # 试跑：只处理 --only 匹配的文件，输出对比图到 .scratch
  python palette-map-apollo.py --run            # 全量就地覆写
  python palette-map-apollo.py --run --only 像素怪物合集
"""
import argparse
import concurrent.futures as cf
import csv
import io
import json
import os
import re
import sys
import fnmatch

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GPL_PATH = os.path.join(ROOT, r"Assets", "Arts", "配色文件", "apollo.gpl")
TARGET_DIRS = [
    os.path.join(ROOT, "Assets", "Arts", "Images"),
    os.path.join(ROOT, "Assets", "Resources", "ContentArt"),
]
SCRATCH = os.path.join(ROOT, ".scratch", "palette-map")

# 完全跳过的路径片段（参考图、非游戏素材）
SKIP_PATTERNS = [
    os.path.join("Arts", "Images", "参考"),
]

# ---------------------------------------------------------------- palette

def load_palette(path):
    colors = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            parts = line.split()
            if len(parts) >= 4 and parts[0].isdigit():
                colors.append((int(parts[0]), int(parts[1]), int(parts[2])))
    return np.array(colors, dtype=np.float64)


# Apollo 46 色按色阶分组（与 gpl 行序一致）
RAMPS = {
    "blue":   [0, 1, 2, 3, 4, 5],
    "green":  [6, 7, 8, 9, 10, 11],
    "tan":    [12, 13, 14, 15, 16, 17],
    "gold":   [18, 19, 20, 21, 22, 23],
    "red":    [24, 25, 26, 27, 28, 29],
    "purple": [30, 31, 32, 33, 34, 35],
    "gray":   [36, 37, 38, 39, 40, 41, 42, 43, 44, 45],
}

# 区域加权方案：<1 吸引（偏向该色阶），>1 排斥。温和取值，只掰"摇摆色"。
REGION_PROFILES = {
    # 中性：全调色板等权
    "neutral":  {},
    # 教学关：浅岩层 —— 暖砂岩、亲和、可读性第一
    "qianyanceng": {"tan": 0.82, "gold": 0.92, "gray": 0.95, "blue": 1.10, "green": 1.12, "purple": 1.22, "red": 1.15},
    # 链接卡组：岩洞 —— 幽蓝冷光、石壁
    "yandong":  {"blue": 0.82, "gray": 0.90, "purple": 1.02, "tan": 1.10, "green": 1.15, "gold": 1.18, "red": 1.22},
    # 翻面卡组：沼泽 —— 湿绿、腐殖暗棕
    "zhaoze":   {"green": 0.82, "tan": 0.95, "blue": 1.06, "gray": 1.00, "gold": 1.15, "purple": 1.15, "red": 1.22},
    # 烈焰卡组：熔岩之地 —— 炽红熔金、黑岩
    "rongyan":  {"red": 0.82, "gold": 0.85, "gray": 1.00, "tan": 1.06, "purple": 1.15, "blue": 1.22, "green": 1.25},
    # 旋转卡组：地下丛林 —— 苍翠深绿、蓝雾
    "conglin":  {"green": 0.80, "blue": 0.95, "gray": 1.05, "tan": 1.10, "gold": 1.15, "purple": 1.15, "red": 1.22},
    # 召唤卡组：藏骨堂 —— 骨白冷灰、死灵紫
    "canggutang": {"gray": 0.85, "purple": 0.88, "blue": 1.05, "tan": 1.06, "green": 1.12, "gold": 1.15, "red": 1.15},
    # 快斗卡组：失落遗迹 —— 古金、遗迹蓝
    "yiji":     {"gold": 0.85, "blue": 0.90, "gray": 0.95, "tan": 1.05, "purple": 1.12, "green": 1.15, "red": 1.22},
}

# 路径规则 → 区域。按顺序匹配，先中者胜；不中则 neutral。
# pattern 为 fnmatch 通配（对归一化为 / 分隔的相对路径小写匹配）。
# 由主脚本根据内容配置生成/维护（见 region-rules.json 覆盖机制）。
DEFAULT_REGION_RULES = [
    ("*/other/岩层_浅岩层.png",   "qianyanceng"),
    ("*/other/岩层_熔岩之地.png", "rongyan"),
    ("*/other/岩层_藏骨堂.png",   "canggutang"),
    ("*/other/岩层_血色.png",     "rongyan"),
    ("*/other/密林_地下丛林.png", "conglin"),
    ("*/other/密林_失落遗迹.png", "yiji"),
    ("*/other/密林_岩洞区.png",   "yandong"),
    ("*/other/密林_血色.png",     "zhaoze"),
]

# ---------------------------------------------------------------- oklab

def srgb_to_oklab(rgb):
    """rgb: (N,3) 0-255 -> oklab (N,3)"""
    c = rgb / 255.0
    lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    l = 0.4122214708 * lin[:, 0] + 0.5363325363 * lin[:, 1] + 0.0514459929 * lin[:, 2]
    m = 0.2119034982 * lin[:, 0] + 0.6806995451 * lin[:, 1] + 0.1073969566 * lin[:, 2]
    s = 0.0883024619 * lin[:, 0] + 0.2817188376 * lin[:, 1] + 0.6299787005 * lin[:, 2]
    l_, m_, s_ = np.cbrt(l), np.cbrt(m), np.cbrt(s)
    return np.stack([
        0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,
        1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,
        0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_,
    ], axis=1)


# 亮度权重略高：保持像素画的明度结构 / 可读性
L_WEIGHT = 1.15
# 色度权重更高：近中性色优先落灰阶，避免头骨/石头上出现蓝绿杂点
AB_WEIGHT = 1.30


def build_weights(profile_name):
    w = np.ones(46, dtype=np.float64)
    prof = REGION_PROFILES[profile_name]
    for ramp, mult in prof.items():
        for idx in RAMPS[ramp]:
            w[idx] = mult
    return w


class Mapper:
    def __init__(self, palette):
        self.palette = palette  # (46,3) 0-255
        self.pal_lab = srgb_to_oklab(palette)
        self.pal_lab_w = self.pal_lab.copy()
        self.pal_lab_w[:, 0] *= L_WEIGHT
        self.pal_lab_w[:, 1:] *= AB_WEIGHT
        self._weights = {name: build_weights(name) for name in REGION_PROFILES}

    def map_colors(self, rgb_unique, profile_name):
        """rgb_unique: (N,3) uint8 -> (N,3) uint8 palette colors"""
        lab = srgb_to_oklab(rgb_unique.astype(np.float64))
        lab[:, 0] *= L_WEIGHT
        lab[:, 1:] *= AB_WEIGHT
        # (N,46) 距离
        d = np.linalg.norm(lab[:, None, :] - self.pal_lab_w[None, :, :], axis=2)
        d *= self._weights[profile_name][None, :]
        idx = np.argmin(d, axis=1)
        return self.palette[idx].astype(np.uint8)


# ---------------------------------------------------------------- io

def region_for(relpath, rules):
    p = relpath.replace("\\", "/").lower()
    for pattern, region in rules:
        if fnmatch.fnmatch(p, pattern.lower()):
            return region
    return "neutral"


def process_image(args):
    path, relpath, profile, mapper, dry_run, out_root = args
    try:
        with Image.open(path) as im:
            had_animation = getattr(im, "n_frames", 1) > 1
            im = im.convert("RGBA")
            a = np.asarray(im).copy()
    except Exception as e:
        return (relpath, profile, "ERROR", str(e))
    rgb = a[..., :3].reshape(-1, 3)
    alpha = a[..., 3].reshape(-1)
    visible = alpha > 0
    if not visible.any():
        return (relpath, profile, "SKIP_EMPTY", "")
    vis_rgb = rgb[visible]
    uniq, inverse = np.unique(vis_rgb, axis=0, return_inverse=True)
    mapped_uniq = mapper.map_colors(uniq, profile)
    changed = int((mapped_uniq != uniq).any(axis=1).sum())
    new_vis = mapped_uniq[inverse]
    rgb[visible] = new_vis
    out = np.concatenate([rgb.reshape(a.shape[0], a.shape[1], 3), a[..., 3:4]], axis=2)
    if dry_run:
        return (relpath, profile, "DRY", f"uniq={len(uniq)} changed={changed}")
    if out_root:
        target = os.path.join(out_root, relpath)
        os.makedirs(os.path.dirname(target), exist_ok=True)
    else:
        target = path
    Image.fromarray(out, "RGBA").save(target, optimize=True)
    return (relpath, profile, "OK", f"uniq={len(uniq)} changed={changed}")


def iter_targets(only=None, rules=None):
    for base in TARGET_DIRS:
        for dp, dn, fn in os.walk(base):
            for f in fn:
                if not f.lower().endswith(".png"):
                    continue
                full = os.path.join(dp, f)
                rel = os.path.relpath(full, ROOT)
                if any(sp in rel for sp in SKIP_PATTERNS):
                    continue
                if only and only not in rel:
                    continue
                yield full, rel, region_for(rel, rules)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", action="store_true", help="就地覆写")
    ap.add_argument("--pilot", action="store_true", help="输出到 .scratch/palette-map/mapped 供对比")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--only", default=None, help="只处理相对路径含该子串的文件")
    ap.add_argument("--rules", default=None, help="区域规则 JSON（[[pattern, region], ...]），追加在默认规则前")
    ap.add_argument("--jobs", type=int, default=os.cpu_count())
    ap.add_argument("--report", default=None, help="输出 CSV 报告路径")
    args = ap.parse_args()

    rules = list(DEFAULT_REGION_RULES)
    if args.rules:
        with open(args.rules, encoding="utf-8") as f:
            rules = [tuple(x) for x in json.load(f)] + rules

    palette = load_palette(GPL_PATH)
    mapper = Mapper(palette)

    out_root = None
    if args.pilot:
        out_root = os.path.join(SCRATCH, "mapped")
        os.makedirs(out_root, exist_ok=True)
    elif not args.run and not args.dry_run:
        ap.error("需要 --run / --pilot / --dry-run 之一")

    tasks = [(p, r, prof, mapper, args.dry_run, out_root) for p, r, prof in iter_targets(args.only, rules)]
    print(f"targets: {len(tasks)}")
    results = []
    # 线程池足够：瓶颈在 numpy/PIL，且 numpy 释放 GIL
    with cf.ThreadPoolExecutor(max_workers=args.jobs) as ex:
        for i, res in enumerate(ex.map(process_image, tasks)):
            results.append(res)
            if (i + 1) % 500 == 0:
                print(f"  {i+1}/{len(tasks)}")
    ok = sum(1 for r in results if r[2] == "OK")
    err = [r for r in results if r[2] == "ERROR"]
    print(f"done: ok={ok} err={len(err)} total={len(results)}")
    for r in err[:20]:
        print("  ERROR", r[0], r[3])
    if args.report:
        with open(args.report, "w", newline="", encoding="utf-8-sig") as f:
            w = csv.writer(f)
            w.writerow(["path", "region", "status", "detail"])
            w.writerows(results)
    # 按区域汇总
    from collections import Counter
    print(Counter(r[1] for r in results))


if __name__ == "__main__":
    main()
