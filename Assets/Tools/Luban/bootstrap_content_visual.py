#!/usr/bin/env python3
"""Bootstrap content_visual.xlsx from existing Luban JSON sources (one-time / regen)."""

from __future__ import annotations

import json
import sys
from pathlib import Path

try:
    from openpyxl import Workbook
except ImportError:
    print("openpyxl is required: pip install openpyxl", file=sys.stderr)
    sys.exit(1)

SCRIPT_DIR = Path(__file__).resolve().parent
DATAS_DIR = SCRIPT_DIR / "Datas"
OUTPUT = DATAS_DIR / "content_visual.xlsx"

HEADERS = ("content_id", "content_kind", "description", "face_key", "frame_key", "icon_key")


def load_json(name: str):
    path = DATAS_DIR / name
    with path.open(encoding="utf-8") as f:
        return json.load(f)


def split_tokens(raw: str) -> list[str]:
    if not raw:
        return []
    for sep in ("|", ";", ","):
        if sep in raw:
            return [t.strip() for t in raw.split(sep) if t.strip()]
    return [raw.strip()] if raw.strip() else []


def join_descriptions(parts: list[str]) -> str:
    cleaned = [p for p in parts if p]
    return "；".join(cleaned)


def prepend_name(display_name: str, description: str) -> str:
    display_name = (display_name or "").strip()
    description = (description or "").strip()
    if not display_name:
        return description
    if not description:
        return display_name
    if description.startswith(display_name):
        return description
    for sep in ("：", ":", "—", "-", " "):
        prefix = f"{display_name}{sep}"
        if description.startswith(prefix):
            return description
    return f"{display_name}：{description}"


def main() -> int:
    cards = load_json("cards.json")
    skills = load_json("skills.json")
    relics = load_json("relics.json")
    effects = load_json("effects.json")
    rooms = load_json("rooms.json")
    decks = load_json("monster_decks.json")

    effect_by_id = {row["id"]: row.get("design_text", "") for row in effects}
    skill_by_id = {row["def_id"]: row.get("design_text", "") for row in skills}

    rows: list[tuple[str, str, str, str, str, str]] = []

    for card in cards:
        kind = card["kind"]
        if kind == "HelpCard":
            content_kind = "HelpCard"
            effect_ids = split_tokens(card.get("effect_ids", ""))
            desc_parts = [effect_by_id.get(eid, "") for eid in effect_ids]
            description = join_descriptions(desc_parts)
            face_key = ""
        elif kind == "Monster":
            content_kind = "Monster"
            skill_ids = split_tokens(card.get("skill_ids", ""))
            desc_parts = [skill_by_id.get(sid, "") for sid in skill_ids]
            description = join_descriptions(desc_parts)
            face_key = card.get("deck_id", "") or ""
        else:
            continue

        rows.append(
            (
                card["def_id"],
                content_kind,
                prepend_name(card.get("display_name", ""), description),
                face_key,
                "",
                "",
            )
        )

    for relic in relics:
        rows.append(
            (
                relic["def_id"],
                "Relic",
                prepend_name(relic.get("display_name", ""), relic.get("design_text", "")),
                "",
                "",
                "",
            )
        )

    for skill in skills:
        container = skill.get("container_type", "")
        if container == "PlayerSkill":
            content_kind = "Skill"
        elif container == "MonsterSkill":
            content_kind = "Skill"
        else:
            content_kind = "Skill"
        rows.append(
            (
                skill["def_id"],
                content_kind,
                prepend_name(skill.get("display_name", ""), skill.get("design_text", "")),
                "",
                "",
                "",
            )
        )

    for room in rooms:
        rows.append(
            (
                room["kind"],
                "Room",
                prepend_name(room.get("display_name", ""), room.get("display_name", "")),
                "",
                "",
                "",
            )
        )

    for deck in decks:
        rows.append(
            (
                deck["id"],
                "MonsterDeck",
                prepend_name(deck.get("display_name", ""), deck.get("display_name", "")),
                "",
                "",
                "",
            )
        )

    rows.append(("avatar.default", "Avatar", "玩家化身", "", "", ""))

    rows.sort(key=lambda r: (r[1], r[0]))

    wb = Workbook()
    ws = wb.active
    ws.title = "TbContentVisual"
    ws.append(["##var", *HEADERS])
    ws.append(["##type", "string", "string", "string", "string", "string", "string"])
    for row in rows:
        ws.append(["", *row])

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    wb.save(OUTPUT)
    print(f"Wrote {len(rows)} rows to {OUTPUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
