# -*- coding: utf-8 -*-
"""
set-monster-face-backgrounds.py — 【已废弃】

怪物卡卡面背景已改由运行时按 Run 进度覆盖（Floor + 层内 Room + 困难档），
见 ADR-0053 与 DungeonEnvironmentCatalog / CoreCardPresentationMapper。

本脚本按 deck 静态填 sprites.faceBackground 的做法不再维护。
请勿再运行写入；若需清空历史错误路径，用手动或一次性清理脚本。

原用法（仅供考古）：
  py -3.12 scripts/set-monster-face-backgrounds.py
  py -3.12 scripts/set-monster-face-backgrounds.py --clear
"""
import sys

if __name__ == "__main__":
    print(
        "DEPRECATED: monster faceBackground is runtime-overridden (ADR-0053). "
        "Do not run this script.",
        file=sys.stderr,
    )
    sys.exit(1)
