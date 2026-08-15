#!/usr/bin/env python3
"""Export wired primary card descriptions, apply stat-only icon token replacements, dual-write JSON."""

from __future__ import annotations

import json
import re
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
CARDS_DIR = ROOT / "Assets" / "Arts" / "ContentVisual" / "cards"
STREAMING_DIR = ROOT / "Assets" / "StreamingAssets" / "ContentVisual" / "cards"
CATALOG_PATH = ROOT / "Assets" / "Resources" / "Arts" / "Cards" / "CardFaceDescriptionIconCatalog.asset"
OUT_DIR = Path(__file__).resolve().parent

SCHEMA = "table-nine.card-descriptions.v1"
ARCHIVE_DECKS = {"deck.relic_archive", "deck.help_archive", "deck.transition"}
PRIMARY_KINDS = {"Monster", "Relic", "HelpCard", "Trap"}

# Icon codes with sprite (stat tokens), longest zh first.
ICON_REPLACEMENTS = [
    ("血量上限", "[MHP]"),
    ("基础护甲", "[basic_armor]"),
    ("正交相邻", "[adjacent]"),
    ("行动计数", "[action]"),
    ("移动计数", "[move]"),
    ("攻击", "[attack]"),
    ("护甲", "[armor]"),
    ("血量", "[HP]"),
    ("金币", "[money]"),
    ("死亡", "[death]"),
]

GLOSSARY_REPLACEMENTS = [
    ("普通攻击", "[[普通攻击]]"),
]

TOKEN_SPLIT = re.compile(r"(\[\[[^\]]+\]\]|\{[^}]+\}|\[[^\]]+\])")


def count_units(text: str) -> int:
    if not text:
        return 0
    units = 0
    i = 0
    n = len(text)
    while i < n:
        if text.startswith("[[", i):
            close = text.find("]]", i + 2)
            if close >= 0:
                i = close + 2
                units += 1
                continue
        if text[i] == "{":
            close = text.find("}", i + 1)
            if close >= 0:
                i = close + 1
                units += 1
                continue
        if text[i] == "[":
            close = text.find("]", i + 1)
            if close >= 0:
                i = close + 1
                units += 1
                continue
        units += 1
        i += 1
    return units


def should_replace_attack(segment: str, start: int) -> bool:
    after = segment[start + 2 :]
    if after.startswith(("者", "范围", "后")):
        return False
    if start >= 2 and segment[start - 2 : start] == "发起":
        return False
    return True


def should_replace_money(segment: str, start: int) -> bool:
    if start <= 0 or not segment[start - 1].isdigit():
        return False
    after = segment[start + 2 :]
    return not after.startswith("卡")


def should_replace_hp(segment: str, start: int) -> bool:
    after = segment[start + 2 :]
    if after.startswith("上限"):
        return False
    if after.startswith("的"):
        return False
    if start >= 2 and segment[start - 2 : start] == "恢复":
        return False
    return True


def should_replace_armor(segment: str, start: int) -> bool:
    after = segment[start + 2 :]
    if after.startswith("卡"):
        return False
    before = segment[:start]
    if after[:1] in "+-":
        return True
    if after.startswith(("降低", "归零", "破碎")):
        return True
    if before.endswith(("损失", "获得", "点", "和", "全部")):
        return True
    return False


def replace_icons_in_plain_segment(segment: str) -> str:
    for zh, code in ICON_REPLACEMENTS:
        pieces = []
        idx = 0
        while True:
            pos = segment.find(zh, idx)
            if pos < 0:
                pieces.append(segment[idx:])
                break
            ok = True
            if zh == "攻击":
                ok = should_replace_attack(segment, pos)
            elif zh == "金币":
                ok = should_replace_money(segment, pos)
            elif zh == "血量":
                ok = should_replace_hp(segment, pos)
            elif zh == "护甲":
                ok = should_replace_armor(segment, pos)
            if ok:
                pieces.append(segment[idx:pos])
                pieces.append(code)
            else:
                pieces.append(segment[idx : pos + len(zh)])
            idx = pos + len(zh)
        segment = "".join(pieces)
    return segment


def replace_in_plain_segment(segment: str) -> str:
    # Glossary terms first (only if not already [[term]]).
    for zh, rep in GLOSSARY_REPLACEMENTS:
        out = []
        idx = 0
        while True:
            pos = segment.find(zh, idx)
            if pos < 0:
                out.append(segment[idx:])
                break
            if pos >= 2 and segment[pos - 2 : pos] == "[[":
                out.append(segment[idx : pos + len(zh)])
            else:
                out.append(segment[idx:pos])
                out.append(rep)
            idx = pos + len(zh)
        segment = "".join(out)

    # Protect [[…]] produced above before icon pass.
    parts = TOKEN_SPLIT.split(segment)
    rebuilt: list[str] = []
    for part in parts:
        if not part:
            continue
        if TOKEN_SPLIT.fullmatch(part):
            rebuilt.append(part)
        else:
            rebuilt.append(replace_icons_in_plain_segment(part))
    return "".join(rebuilt)


def apply_stat_only_icons(description: str) -> str:
    if not description:
        return description
    parts = TOKEN_SPLIT.split(description)
    out = []
    for part in parts:
        if not part:
            continue
        if TOKEN_SPLIT.fullmatch(part):
            out.append(part)
        else:
            out.append(replace_in_plain_segment(part))
    return "".join(out)


def load_deck_display_names() -> dict[str, str]:
    names: dict[str, str] = {}
    for path in CARDS_DIR.glob("*.json"):
        if path.name == "_index.json":
            continue
        data = json.loads(path.read_text(encoding="utf-8"))
        if data.get("kind") == "Deck" and data.get("contentId"):
            names[data["contentId"].strip()] = (data.get("displayName") or "").strip()
    return names


def is_archived_member(data: dict, deck_names: dict[str, str]) -> bool:
    deck_id = (data.get("deckId") or "").strip()
    if deck_id in ARCHIVE_DECKS:
        return True
    if deck_id == "deck.transition":
        return True
    if data.get("kind") == "Monster" and data.get("isReserve"):
        return True
    if deck_id and deck_names.get(deck_id, "").find("归档") >= 0:
        return True
    return False


def is_wired_primary_card(data: dict, deck_names: dict[str, str]) -> bool:
    kind = data.get("kind") or ""
    return kind in PRIMARY_KINDS and not is_archived_member(data, deck_names)


def collect_wired_entries() -> list[dict]:
    deck_names = load_deck_display_names()
    entries = []
    for path in sorted(CARDS_DIR.glob("*.json")):
        if path.name == "_index.json":
            continue
        data = json.loads(path.read_text(encoding="utf-8"))
        if not is_wired_primary_card(data, deck_names):
            continue
        deck_id = (data.get("deckId") or "").strip()
        entries.append(
            {
                "contentId": data.get("contentId") or "",
                "deckId": deck_id,
                "deckDisplayName": deck_names.get(deck_id, deck_id),
                "displayName": data.get("displayName") or "",
                "kind": data.get("kind") or "",
                "faceIntro": data.get("faceIntro") or "",
                "description": data.get("description") or "",
            }
        )
    entries.sort(key=lambda e: e["contentId"])
    return entries


def content_id_to_filename(content_id: str) -> str:
    return content_id.replace(".", "_") + ".json"


def dual_write_description(content_id: str, new_description: str) -> None:
    fname = content_id_to_filename(content_id)
    for folder in (CARDS_DIR, STREAMING_DIR):
        path = folder / fname
        if not path.exists():
            raise FileNotFoundError(path)
        data = json.loads(path.read_text(encoding="utf-8"))
        data["description"] = new_description
        path.write_text(
            json.dumps(data, ensure_ascii=False, indent=4) + "\n",
            encoding="utf-8",
        )


def main() -> None:
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    entries = collect_wired_entries()

    export_path = OUT_DIR / f"card-descriptions-{stamp}.json"
    export_payload = {
        "schema": SCHEMA,
        "exportedAt": datetime.now(timezone.utc).astimezone().isoformat(),
        "fields": ["displayName", "kind", "faceIntro", "description"],
        "entries": entries,
    }
    export_path.write_text(
        json.dumps(export_payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"Exported {len(entries)} entries -> {export_path}")

    changed = []
    fixed_entries = []
    for entry in entries:
        before = entry["description"]
        after = apply_stat_only_icons(before)
        fixed = dict(entry)
        fixed["description"] = after
        fixed_entries.append(fixed)
        if before != after:
            changed.append(
                {
                    "contentId": entry["contentId"],
                    "kind": entry["kind"],
                    "before": before,
                    "after": after,
                    "units_before": count_units(before),
                    "units_after": count_units(after),
                }
            )

    fixed_path = OUT_DIR / "card-descriptions-icon-fixed.json"
    fixed_payload = {
        "schema": SCHEMA,
        "exportedAt": datetime.now(timezone.utc).astimezone().isoformat(),
        "fields": ["displayName", "kind", "faceIntro", "description"],
        "entries": fixed_entries,
    }
    fixed_path.write_text(
        json.dumps(fixed_payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"Fixed bulk JSON -> {fixed_path} ({len(changed)} changed)")

    diff_lines = ["# Icon token diff\n", f"Changed: {len(changed)}\n\n"]
    over26 = []
    for item in changed:
        flag = ""
        if item["units_after"] > 26:
            flag = " **OVER 26**"
            over26.append(item["contentId"])
        diff_lines.append(f"## {item['contentId']} ({item['kind']}){flag}\n")
        diff_lines.append(f"- units: {item['units_before']} -> {item['units_after']}\n")
        diff_lines.append(f"- before: `{item['before']}`\n")
        diff_lines.append(f"- after: `{item['after']}`\n\n")

    diff_path = OUT_DIR / "icon-token-diff.md"
    diff_path.write_text("".join(diff_lines), encoding="utf-8")
    print(f"Diff report -> {diff_path}")

    if over26:
        print("WARNING: over 26 units:", ", ".join(over26))

    for item in changed:
        dual_write_description(item["contentId"], item["after"])
        print(f"  wrote {item['contentId']}")

    print("Done.")


if __name__ == "__main__":
    main()
