#!/usr/bin/env python3
"""Read / patch content_visual.xlsx for Unity ContentVisualEditor."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

try:
    from openpyxl import load_workbook
except ImportError:
    print("openpyxl is required: pip install openpyxl", file=sys.stderr)
    sys.exit(1)

SHEET_NAME = "TbContentVisual"
COL_MARKER = 0
COL_CONTENT_ID = 1
COL_CONTENT_KIND = 2
COL_DESCRIPTION = 3
COL_FACE_KEY = 4
COL_FRAME_KEY = 5
COL_ICON_KEY = 6


def _cell_str(value) -> str:
    if value is None:
        return ""
    return str(value).strip()


def read_all(path: Path) -> list[dict]:
    wb = load_workbook(path, read_only=False, data_only=True)
    ws = wb[SHEET_NAME] if SHEET_NAME in wb.sheetnames else wb.active
    rows: list[dict] = []
    for row_index, row in enumerate(ws.iter_rows(values_only=True)):
        marker = _cell_str(row[COL_MARKER] if len(row) > COL_MARKER else "")
        if marker.startswith("##"):
            continue
        content_id = _cell_str(row[COL_CONTENT_ID] if len(row) > COL_CONTENT_ID else "")
        if not content_id:
            continue
        rows.append(
            {
                "content_id": content_id,
                "content_kind": _cell_str(row[COL_CONTENT_KIND] if len(row) > COL_CONTENT_KIND else ""),
                "description": _cell_str(row[COL_DESCRIPTION] if len(row) > COL_DESCRIPTION else ""),
                "face_key": _cell_str(row[COL_FACE_KEY] if len(row) > COL_FACE_KEY else ""),
                "frame_key": _cell_str(row[COL_FRAME_KEY] if len(row) > COL_FRAME_KEY else ""),
                "icon_key": _cell_str(row[COL_ICON_KEY] if len(row) > COL_ICON_KEY else ""),
                "sheet_row_index": row_index,
            }
        )
    wb.close()
    return rows


def read_header_rows(path: Path) -> tuple[str, str]:
    wb = load_workbook(path, read_only=True, data_only=True)
    ws = wb[SHEET_NAME] if SHEET_NAME in wb.sheetnames else wb.active
    var_row = ws[1]
    type_row = ws[2]
    var_parts = [_cell_str(var_row[i].value if i < len(var_row) else "") for i in range(COL_ICON_KEY + 1)]
    type_parts = [_cell_str(type_row[i].value if i < len(type_row) else "") for i in range(COL_ICON_KEY + 1)]
    wb.close()
    return "|".join(var_parts), "|".join(type_parts)


def patch_visual_keys(path: Path, patches: list[dict]) -> None:
    wb = load_workbook(path)
    ws = wb[SHEET_NAME] if SHEET_NAME in wb.sheetnames else wb.active
    row_by_id: dict[str, int] = {}
    for row_index, row in enumerate(ws.iter_rows(values_only=True)):
        marker = _cell_str(row[COL_MARKER] if len(row) > COL_MARKER else "")
        if marker.startswith("##"):
            continue
        content_id = _cell_str(row[COL_CONTENT_ID] if len(row) > COL_CONTENT_ID else "")
        if content_id:
            row_by_id[content_id] = row_index

    for patch in patches:
        content_id = patch.get("content_id", "")
        if not content_id:
            continue
        sheet_row_index = patch.get("sheet_row_index", -1)
        if sheet_row_index < 0:
            sheet_row_index = row_by_id.get(content_id, -1)
        if sheet_row_index < 0:
            continue
        excel_row = sheet_row_index + 1
        ws.cell(row=excel_row, column=COL_FACE_KEY + 1, value=patch.get("face_key", "") or "")
        ws.cell(row=excel_row, column=COL_FRAME_KEY + 1, value=patch.get("frame_key", "") or "")
        ws.cell(row=excel_row, column=COL_ICON_KEY + 1, value=patch.get("icon_key", "") or "")

    wb.save(path)
    wb.close()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["read", "patch", "headers"])
    parser.add_argument("--path", required=True)
    parser.add_argument("--patches", default="")
    args = parser.parse_args()
    path = Path(args.path)
    if not path.exists():
        print(f"File not found: {path}", file=sys.stderr)
        return 1

    if args.mode == "read":
        print(json.dumps(read_all(path), ensure_ascii=False))
        return 0

    if args.mode == "headers":
        var_row, type_row = read_header_rows(path)
        print(json.dumps({"var_row": var_row, "type_row": type_row}, ensure_ascii=False))
        return 0

    if args.mode == "patch":
        patches = json.loads(args.patches) if args.patches else []
        patch_visual_keys(path, patches)
        return 0

    return 1


if __name__ == "__main__":
    raise SystemExit(main())
