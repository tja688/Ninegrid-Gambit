# -*- coding: utf-8 -*-
"""
set-monster-face-backgrounds.py — 怪物卡卡面背景按主题卡组接线

给七套正式主题卡组的怪物卡 JSON 填 sprites.faceBackground（岩层/密林环境图），
表现管线已支持：faceBackground → CoreCardPresentationMapper → 卡面「背景」节点
（怪物卡标准模板.prefab，槽位映射见 CardFaceSlotNodeMap）。

- 文本级替换，保留原文件格式与换行（LF），改完把 Arts 侧字节原样镜像到
  StreamingAssets（ADR-0029 双侧字节一致）。
- 幂等：faceBackground 已是目标值时跳过。

用法：
  py -3.12 scripts/set-monster-face-backgrounds.py          # 应用
  py -3.12 scripts/set-monster-face-backgrounds.py --clear  # 清空（还原为 ""）
"""
import glob
import json
import os
import shutil
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ARTS_CARDS = os.path.join(ROOT, "Assets", "Arts", "ContentVisual", "cards")
STREAMING_CARDS = os.path.join(ROOT, "Assets", "StreamingAssets", "ContentVisual", "cards")
TILE_FOLDER = "Assets/Resources/ContentArt/Png/Other/"

# deckId → 环境图。主题语义（基础/旋转/召唤/烈焰/决斗/链接/翻面）对照
# docs/feedback-analysis/content-audit-report.md；岩层_血色 暂为备用变体未接。
DECK_TILES = {
    "deck.dragon": "岩层_浅岩层.png",
    "deck.orc_legion": "密林_地下丛林.png",
    "deck.insect": "岩层_藏骨堂.png",
    "deck.stone_legion": "岩层_熔岩之地.png",
    "deck.void": "密林_失落遗迹.png",
    "deck.smallanimal": "密林_岩洞区.png",
    "deck.skeleton_legion": "密林_血色.png",
}


def main():
    clear = "--clear" in sys.argv
    changed = 0
    for path in sorted(glob.glob(os.path.join(ARTS_CARDS, "*.json"))):
        name = os.path.basename(path)
        if name.startswith("_"):
            continue
        with open(path, "rb") as f:
            raw = f.read()
        try:
            card = json.loads(raw.decode("utf-8"))
        except Exception as e:
            print(f"WARN 无法解析 {name}: {e}", file=sys.stderr)
            continue
        if card.get("kind") != "Monster":
            continue
        tile = DECK_TILES.get((card.get("deckId") or "").strip())
        if not tile:
            continue

        target = "" if clear else TILE_FOLDER + tile
        current = ((card.get("sprites") or {}).get("faceBackground") or "").strip()
        if current == target:
            continue

        old_field = json.dumps("faceBackground") + ": " + json.dumps(current)
        new_field = json.dumps("faceBackground") + ": " + json.dumps(target)
        old_bytes = old_field.encode("utf-8")
        new_bytes = new_field.encode("utf-8")
        if raw.count(old_bytes) != 1:
            print(f"WARN 字段定位失败（{raw.count(old_bytes)} 处），跳过 {name}", file=sys.stderr)
            continue

        patched = raw.replace(old_bytes, new_bytes, 1)
        with open(path, "wb") as f:
            f.write(patched)

        mirror = os.path.join(STREAMING_CARDS, name)
        if os.path.exists(mirror):
            shutil.copyfile(path, mirror)
        else:
            print(f"WARN StreamingAssets 缺镜像: {name}", file=sys.stderr)
        changed += 1
        print(f"{'清空' if clear else '接线'} {name} ← {tile if not clear else ''}")

    print(f"done: {changed} 张卡已更新（Arts + StreamingAssets 镜像）")


if __name__ == "__main__":
    main()
