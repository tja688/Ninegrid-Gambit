#!/usr/bin/env python3
"""Bootstrap visual_asset.xlsx and card_frame_style.xlsx for Phase A visual pipeline."""

from __future__ import annotations

import sys
from pathlib import Path

try:
    from openpyxl import Workbook
except ImportError:
    print("openpyxl is required: pip install openpyxl", file=sys.stderr)
    sys.exit(1)

SCRIPT_DIR = Path(__file__).resolve().parent
DATAS_DIR = SCRIPT_DIR / "Datas"
VISUAL_ASSET_OUTPUT = DATAS_DIR / "visual_asset.xlsx"
FRAME_STYLE_OUTPUT = DATAS_DIR / "card_frame_style.xlsx"

FRAME_STYLES = [
    ("frame.white", 0.92, 0.92, 0.92, 1.0),
    ("frame.blue", 0.35, 0.55, 0.95, 1.0),
    ("frame.gold", 0.9557673, 0.97735846, 0.0, 1.0),
    ("frame.red", 0.9, 0.25, 0.2, 1.0),
    ("frame.normal", 0.75, 0.75, 0.75, 1.0),
    ("frame.elite", 0.55, 0.35, 0.85, 1.0),
    ("frame.boss", 0.85, 0.15, 0.15, 1.0),
]

VISUAL_ASSETS = [
    ("visual.missing.sprite", "sprite", "content/fallback/missing_card", ""),
]


def write_visual_asset_xlsx() -> None:
    wb = Workbook()
    ws = wb.active
    ws.title = "TbVisualAsset"
    ws.append(["##var", "visual_id", "kind", "asset_key", "fallback_id"])
    ws.append(["##type", "string", "string", "string", "string"])
    for row in VISUAL_ASSETS:
        ws.append(["", *row])
    DATAS_DIR.mkdir(parents=True, exist_ok=True)
    wb.save(VISUAL_ASSET_OUTPUT)
    print(f"Wrote {len(VISUAL_ASSETS)} rows to {VISUAL_ASSET_OUTPUT}")


def write_card_frame_style_xlsx() -> None:
    wb = Workbook()
    ws = wb.active
    ws.title = "TbCardFrameStyle"
    ws.append(["##var", "style_id", "color_r", "color_g", "color_b", "color_a"])
    ws.append(["##type", "string", "float", "float", "float", "float"])
    for row in FRAME_STYLES:
        ws.append(["", *row])
    DATAS_DIR.mkdir(parents=True, exist_ok=True)
    wb.save(FRAME_STYLE_OUTPUT)
    print(f"Wrote {len(FRAME_STYLES)} rows to {FRAME_STYLE_OUTPUT}")


def main() -> int:
    write_visual_asset_xlsx()
    write_card_frame_style_xlsx()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
