#!/usr/bin/env python3
"""Read content_visual.xlsx for Unity ContentVisualEditor (text-only columns)."""

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
    var_parts = [_cell_str(var_row[i].value if i < len(var_row) else "") for i in range(COL_DESCRIPTION + 1)]
    type_parts = [_cell_str(type_row[i].value if i < len(type_row) else "") for i in range(COL_DESCRIPTION + 1)]
    wb.close()
    return "|".join(var_parts), "|".join(type_parts)


def strip_visual_key_columns(path: Path) -> dict:
    """Drop face_key/frame_key/icon_key columns if present; keep text-only schema."""
    wb = load_workbook(path)
    ws = wb[SHEET_NAME] if SHEET_NAME in wb.sheetnames else wb.active
    max_col = ws.max_column or 0
    removed = 0
    # Delete columns from right to left so indices stay stable.
    for col in range(max_col, COL_DESCRIPTION + 1, -1):
        ws.delete_cols(col)
        removed += 1

    # Rewrite header rows to known text-only schema.
    headers = ("##var", "content_id", "content_kind", "description")
    types = ("##type", "string", "string", "string")
    for index, value in enumerate(headers, start=1):
        ws.cell(row=1, column=index, value=value)
    for index, value in enumerate(types, start=1):
        ws.cell(row=2, column=index, value=value)

    wb.save(path)
    wb.close()
    return {"removed_columns": removed}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["read", "headers", "strip_visual_keys"])
    parser.add_argument("--path", required=True)
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

    if args.mode == "strip_visual_keys":
        result = strip_visual_key_columns(path)
        print(json.dumps(result, ensure_ascii=False))
        return 0

    return 1


if __name__ == "__main__":
    raise SystemExit(main())
