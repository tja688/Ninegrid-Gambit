# -*- coding: utf-8 -*-
"""#68: Upgrade Monster/Skill/Relic/Deck/Room JSON to schema≥2; fill 21 SO-only contentIds."""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3] if (Path(__file__).name == "upgrade_content_json_schema2.py") else Path.cwd()
ARTS = ROOT / "Assets/Arts/ContentVisual/cards"
STREAM = ROOT / "Assets/StreamingAssets/ContentVisual/cards"
LUBAN = ROOT / "Assets/Tools/Luban/Datas"

ANIM_SLOTS = [
    {"id": "idle", "sourceType": "none", "path": "", "offsetX": 0.0, "offsetY": 0.0},
    {"id": "attack", "sourceType": "none", "path": "", "offsetX": 0.0, "offsetY": 0.0},
    {"id": "hurt", "sourceType": "none", "path": "", "offsetX": 0.0, "offsetY": 0.0},
    {"id": "death", "sourceType": "none", "path": "", "offsetX": 0.0, "offsetY": 0.0},
    {"id": "lunch", "sourceType": "none", "path": "", "offsetX": 0.0, "offsetY": 0.0},
]


def split_tokens(raw: str | None) -> list[str]:
    if not raw:
        return []
    return [t.strip() for t in re.split(r"[;|,]", str(raw)) if t.strip()]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def blank_dto(content_id: str, kind: str) -> dict:
    return {
        "schemaVersion": 2,
        "contentId": content_id,
        "kind": kind,
        "deckId": "",
        "displayName": content_id,
        "description": "",
        "gold": 0,
        "stats": {"hp": 0, "armor": 0, "attack": 0, "action": 0, "recovery": 0},
        "sprites": {
            "mainIcon": "",
            "faceBackground": "",
            "cardFrame": "",
            "banner": "",
            "backBorder": "",
            "backShirt": "",
            "backLogo": "",
        },
        "mainVisual": {"offsetX": 0.0, "offsetY": 0.0, "uniformScale": 1.0},
        "animations": {"defaultFps": 8.0, "slots": [dict(s) for s in ANIM_SLOTS]},
        "extraSlots": [],
        "rarity": "",
        "tags": [],
        "effectIds": [],
        "skillIds": [],
        "level": 0,
        "isElite": False,
        "isBoss": False,
        "isReserve": False,
        "containerType": "",
        "deckKind": "",
        "monsterDefIds": [],
        "weight": 0,
        "goldDelta": 0,
        "maxHpDelta": 0,
        "healToFull": False,
        "rewardPoolId": "",
        "shopOfferCount": 0,
    }


def ensure_stats(dto: dict) -> dict:
    stats = dto.get("stats") or {}
    return {
        "hp": int(stats.get("hp") or 0),
        "armor": int(stats.get("armor") or 0),
        "attack": int(stats.get("attack") or 0),
        "action": int(stats.get("action") or 0),
        "recovery": int(stats.get("recovery") or 0),
    }


def merge_base(existing: dict | None, content_id: str, kind: str) -> dict:
    dto = blank_dto(content_id, kind)
    if not existing:
        return dto
    for key in (
        "displayName",
        "description",
        "deckId",
        "gold",
        "sprites",
        "mainVisual",
        "animations",
        "extraSlots",
    ):
        if key in existing and existing[key] is not None:
            dto[key] = existing[key]
    dto["stats"] = ensure_stats(existing)
    # keep presentation gold even when Luban price is 0 (monster KillGold)
    return dto


def write_both(dto: dict) -> None:
    content_id = dto["contentId"]
    safe = content_id.replace(".", "_")
    text = json.dumps(dto, ensure_ascii=False, indent=4) + "\n"
    for folder in (ARTS, STREAM):
        folder.mkdir(parents=True, exist_ok=True)
        (folder / f"{safe}.json").write_text(text, encoding="utf-8")


def load_existing(content_id: str) -> dict | None:
    path = ARTS / f"{content_id.replace('.', '_')}.json"
    if path.exists():
        return load_json(path)
    path = STREAM / f"{content_id.replace('.', '_')}.json"
    if path.exists():
        return load_json(path)
    return None


def upgrade_cards(cards: list[dict]) -> int:
    n = 0
    for row in cards:
        cid = row["def_id"]
        kind = row["kind"]
        existing = load_existing(cid)
        dto = merge_base(existing, cid, kind)
        dto["schemaVersion"] = 2
        dto["kind"] = kind
        dto["displayName"] = row.get("display_name") or dto["displayName"]
        # Keep JSON description if already authored; else leave (cards table has no design_text)
        dto["deckId"] = row.get("deck_id") or ""
        dto["rarity"] = row.get("rarity") or "None"
        dto["tags"] = split_tokens(row.get("tags"))
        dto["effectIds"] = split_tokens(row.get("effect_ids"))
        dto["skillIds"] = split_tokens(row.get("skill_ids"))
        dto["level"] = int(row.get("level") or 0)
        dto["isElite"] = bool(row.get("is_elite"))
        dto["isBoss"] = bool(row.get("is_boss"))
        dto["isReserve"] = bool(row.get("is_reserve"))
        # HelpCard price from Luban; Monster kill gold stays from existing JSON gold when >0
        if kind == "HelpCard":
            dto["gold"] = int(row.get("price") or 0)
        elif kind == "Monster":
            # Prefer existing presentation gold (KillGold); else Luban price
            if not existing or int(existing.get("gold") or 0) <= 0:
                dto["gold"] = int(row.get("price") or 0)
        dto["stats"] = {
            "hp": int(row.get("max_hp") or 0),
            "armor": int(row.get("armor") or 0),
            "attack": int(row.get("attack") or 0),
            "action": int((existing or {}).get("stats", {}).get("action") or 0) if existing else 0,
            "recovery": int(row.get("recovery") or 0),
        }
        # Preserve authored displayName from help schema2 if already different SSOT
        if existing and existing.get("schemaVersion", 1) >= 2 and existing.get("displayName"):
            dto["displayName"] = existing["displayName"]
            if existing.get("description"):
                dto["description"] = existing["description"]
            if kind == "HelpCard" and existing.get("gold") is not None:
                dto["gold"] = int(existing.get("gold") or 0)
            if existing.get("tags"):
                dto["tags"] = list(existing["tags"])
            if existing.get("effectIds"):
                dto["effectIds"] = list(existing["effectIds"])
            if existing.get("rarity"):
                dto["rarity"] = existing["rarity"]
        write_both(dto)
        n += 1
    return n


def upgrade_skills(skills: list[dict], luban_ids: set[str]) -> int:
    n = 0
    for row in skills:
        cid = row["def_id"]
        existing = load_existing(cid)
        dto = merge_base(existing, cid, "Skill")
        dto["schemaVersion"] = 2
        dto["kind"] = "Skill"
        dto["displayName"] = row.get("display_name") or dto["displayName"]
        dto["description"] = row.get("design_text") or (
            (existing.get("description") if existing else "") or ""
        )
        dto["containerType"] = row.get("container_type") or "MonsterSkill"
        dto["effectIds"] = split_tokens(row.get("effect_ids"))
        write_both(dto)
        n += 1

    # Keep orphan former-player-skill JSON at schema 1 (do not project as skills)
    for path in ARTS.glob("skill_*.json"):
        dto = load_json(path)
        cid = dto.get("contentId") or ""
        if cid and cid not in luban_ids and int(dto.get("schemaVersion") or 1) >= 2:
            dto["schemaVersion"] = 1
            write_both(dto)
    return n


def upgrade_relics(relics: list[dict]) -> int:
    n = 0
    for row in relics:
        cid = row["def_id"]
        existing = load_existing(cid)
        dto = merge_base(existing, cid, "Relic")
        dto["schemaVersion"] = 2
        dto["kind"] = "Relic"
        dto["displayName"] = row.get("display_name") or dto["displayName"]
        dto["description"] = row.get("design_text") or (existing.get("description") if existing else "") or ""
        dto["rarity"] = row.get("rarity") or "None"
        dto["tags"] = split_tokens(row.get("tags"))
        dto["effectIds"] = split_tokens(row.get("effect_ids"))
        write_both(dto)
        n += 1
    return n


def upgrade_decks(decks: list[dict]) -> int:
    n = 0
    for row in decks:
        cid = row["id"]
        existing = load_existing(cid)
        dto = merge_base(existing, cid, "Deck")
        dto["schemaVersion"] = 2
        dto["kind"] = "Deck"
        dto["displayName"] = row.get("display_name") or dto["displayName"]
        dto["deckKind"] = row.get("kind") or "Unknown"
        dto["monsterDefIds"] = split_tokens(row.get("monster_def_ids"))
        write_both(dto)
        n += 1
    return n


def upgrade_rooms(rooms: list[dict]) -> int:
    n = 0
    for row in rooms:
        cid = row["kind"]  # Fountain / Shop / ...
        existing = load_existing(cid)
        dto = merge_base(existing, cid, "Room")
        dto["schemaVersion"] = 2
        dto["kind"] = "Room"
        dto["displayName"] = row.get("display_name") or dto["displayName"]
        dto["weight"] = int(row.get("weight") or 0)
        dto["goldDelta"] = int(row.get("gold_delta") or 0)
        dto["maxHpDelta"] = int(row.get("max_hp_delta") or 0)
        dto["healToFull"] = bool(row.get("heal_to_full"))
        dto["rewardPoolId"] = row.get("reward_pool_id") or ""
        dto["shopOfferCount"] = int(row.get("shop_offer_count") or 0)
        write_both(dto)
        n += 1
    return n


def fill_choice_options() -> int:
    choices = [
        ("Attack", "攻击+1"),
        ("Armor", "护甲+1"),
        ("Hp", "血量+2"),
    ]
    for cid, name in choices:
        existing = load_existing(cid)
        dto = merge_base(existing, cid, "ChoiceOption")
        dto["schemaVersion"] = 2
        dto["kind"] = "ChoiceOption"
        dto["displayName"] = name
        write_both(dto)
    return len(choices)


def rewrite_index() -> int:
    ids = []
    for path in sorted(ARTS.glob("*.json")):
        if path.name.startswith("_"):
            continue
        dto = load_json(path)
        cid = dto.get("contentId")
        if cid:
            ids.append(cid)
    ids = sorted(set(ids))
    payload = {"schemaVersion": 1, "contentIds": ids}
    text = json.dumps(payload, ensure_ascii=False, indent=4) + "\n"
    for folder in (ARTS, STREAM):
        (folder / "_index.json").write_text(text, encoding="utf-8")
    return len(ids)


def main() -> None:
    cards = load_json(LUBAN / "cards.json")
    skills = load_json(LUBAN / "skills.json")
    relics = load_json(LUBAN / "relics.json")
    decks = load_json(LUBAN / "monster_decks.json")
    rooms = load_json(LUBAN / "rooms.json")
    luban_skill_ids = {s["def_id"] for s in skills}

    print("cards", upgrade_cards(cards))
    print("skills", upgrade_skills(skills, luban_skill_ids))
    print("relics", upgrade_relics(relics))
    print("decks", upgrade_decks(decks))
    print("rooms", upgrade_rooms(rooms))
    print("choices", fill_choice_options())
    print("index", rewrite_index())

    # Verify 21 SO-only set exists
    required = [
        "deck.dragon",
        "deck.orc_legion",
        "deck.skeleton_legion",
        "deck.stone_legion",
        "deck.void",
        "deck.wandering_legion",
        "Fountain",
        "Gold",
        "Shop",
        "Tavern",
        "Treasure",
        "Attack",
        "Armor",
        "Hp",
        "relic.arsenal",
        "relic.battle_hardened",
        "relic.easy_road",
        "relic.even_hatred",
        "relic.hard_skin",
        "relic.thorn_skin",
        "relic.tower_child",
    ]
    missing = [cid for cid in required if not (ARTS / f"{cid.replace('.', '_')}.json").exists()]
    print("missing_required", missing)


if __name__ == "__main__":
    main()
