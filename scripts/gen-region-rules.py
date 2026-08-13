# -*- coding: utf-8 -*-
"""
gen-region-rules.py — 从内容配置生成 palette-map 的区域规则

扫描 Assets/Arts/ContentVisual/cards/*.json，把七套正式主题卡组的怪物动画帧目录
映射到对应环境色域（与 GroundSlotEnvironmentSkin 的 deck→地块 对应关系同源），
输出 scripts/region-rules.json 供 palette-map-apollo.py --rules 消费。

同一帧目录被多套卡组共用时回退中性（不写规则）并打印警告。

用法：
  py -3.12 scripts/gen-region-rules.py
"""
import glob
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CARDS_DIR = os.path.join(ROOT, "Assets", "Arts", "ContentVisual", "cards")
OUT_PATH = os.path.join(ROOT, "scripts", "region-rules.json")

# deckId → 区域（区域定义见 palette-map-apollo.py REGION_PROFILES）。
# 主题语义：基础/旋转/召唤/烈焰/决斗/链接/翻面，
# 对照 docs/feedback-analysis/content-audit-report.md 的 deck↔设计案表名表。
DECK_REGIONS = {
    "deck.dragon": "qianyanceng",       # 基础 → 浅岩层
    "deck.orc_legion": "conglin",       # 旋转 → 地下丛林
    "deck.insect": "canggutang",        # 召唤 → 藏骨堂
    "deck.stone_legion": "rongyan",     # 烈焰 → 熔岩之地
    "deck.void": "yiji",                # 决斗 → 失落遗迹
    "deck.smallanimal": "yandong",      # 链接 → 岩洞区
    "deck.skeleton_legion": "zhaoze",   # 翻面 → 密林血色(沼泽)
}


def collect_anim_folders(card):
    """卡 JSON → 动画帧目录集合（只收 sourceType=folder 的路径）。"""
    folders = set()
    anims = card.get("animations") or {}
    slot_lists = [anims.get("slots") or []]
    for extra in card.get("extraSlots") or []:
        if isinstance(extra, dict) and isinstance(extra.get("slots"), list):
            slot_lists.append(extra["slots"])
    for slots in slot_lists:
        for slot in slots:
            if not isinstance(slot, dict):
                continue
            if slot.get("sourceType") != "folder":
                continue
            path = (slot.get("path") or "").strip()
            if path:
                folders.add(path)
    return folders


def to_pattern(folder):
    """资产目录 → fnmatch 模式（对归一化小写相对路径）。"""
    p = folder.replace("\\", "/").strip("/").lower()
    if p.startswith("assets/"):
        p = p[len("assets/"):]
    return "*" + p + "/*"


def main():
    folder_claims = {}  # folder → set(region)
    for path in glob.glob(os.path.join(CARDS_DIR, "*.json")):
        try:
            with open(path, encoding="utf-8") as f:
                card = json.load(f)
        except Exception as e:
            print(f"WARN 无法解析 {os.path.basename(path)}: {e}", file=sys.stderr)
            continue
        region = DECK_REGIONS.get((card.get("deckId") or "").strip())
        if not region:
            # 非七套正式卡组（Reserve/机关/遗物等）也登记，用于冲突检测
            region = None
        for folder in collect_anim_folders(card):
            folder_claims.setdefault(folder, set()).add(region)

    rules = []
    for folder in sorted(folder_claims):
        regions = {r for r in folder_claims[folder] if r}
        has_neutral_user = None in folder_claims[folder]
        if not regions:
            continue
        if len(regions) > 1 or has_neutral_user:
            print(f"WARN 共用目录回退中性: {folder} ← {sorted(folder_claims[folder], key=str)}")
            continue
        rules.append([to_pattern(folder), regions.pop()])

    with open(OUT_PATH, "w", encoding="utf-8") as f:
        json.dump(rules, f, ensure_ascii=False, indent=1)
    print(f"规则 {len(rules)} 条 → {OUT_PATH}")

    from collections import Counter
    print(Counter(r for _, r in rules))


if __name__ == "__main__":
    main()
