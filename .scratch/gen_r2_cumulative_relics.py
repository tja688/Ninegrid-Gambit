# -*- coding: utf-8 -*-
"""Generate R2 cumulative relics (#119) card JSON + effect templates."""
import json
import uuid
from pathlib import Path

root = Path(r"C:\Users\jinji\Documents\GitHub\Ninegrid Gambit")
arts_cards = root / "Assets/Arts/ContentVisual/cards"
stream_cards = root / "Assets/StreamingAssets/ContentVisual/cards"
arts_tpl = root / "Assets/Arts/ContentVisual/tables/effect_templates.json"
stream_tpl = root / "Assets/StreamingAssets/ContentVisual/tables/effect_templates.json"


def meta_text():
    return (
        "fileFormatVersion: 2\n"
        f"guid: {uuid.uuid4().hex}\n"
        "TextScriptImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


def relic_json(content_id, role, display_name, description, rarity, tags, assemblies):
    return {
        "schemaVersion": 2,
        "contentId": content_id,
        "kind": "Relic",
        "deckId": "deck.relic",
        "role": role,
        "displayName": display_name,
        "description": description,
        "faceIntro": "",
        "attackPattern": "",
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
        "animations": {
            "defaultFps": 8.0,
            "slots": [
                {"id": sid, "sourceType": "none", "path": "", "offsetX": 0.0, "offsetY": 0.0}
                for sid in ("idle", "attack", "hurt", "death", "lunch")
            ],
        },
        "extraSlots": [],
        "rarity": rarity,
        "tags": tags,
        "effectAssemblies": assemblies,
        "effectIds": [],
        "skillIds": [],
        "level": "0",
        "sequence": 0,
        "isElite": False,
        "isBoss": False,
        "isReserve": False,
        "containerType": "",
        "deckKind": "",
        "monsterDefIds": [],
        "weight": 0,
        "rewardPoolId": "",
        "shopOfferCount": 0,
        "openingInjects": [],
        "iconPrefab": "",
        "boardSlot": 0,
    }


def asm(eid, tid, args):
    return {
        "id": eid,
        "templateId": tid,
        "containerType": "Relic",
        "argsJson": json.dumps(args, ensure_ascii=False, separators=(",", ":")),
    }


templates = [
    {
        "id": "tpl.relic.punch_card_knife.remove",
        "state": "Implemented",
        "design_text": "[击杀怪物时] 每移除5张怪物卡，随机洗入一张常规道具卡到战斗卡组",
        "requires_json": '["NoOwnerEntity"]',
        "conditions_json": "[]",
        "body": json.dumps(
            {
                "kind": "Triggered",
                "trigger": {
                    "atom": "OnCumulative",
                    "metric": "monsterRemoved",
                    "threshold": 5,
                    "counterKey": "relic.punch_card_knife.remove",
                },
                "target": {"atom": "Player"},
                "action": {
                    "atom": "ShuffleRandomContent",
                    "kind": "HelpCard",
                    "count": "{{count}}",
                    "top": False,
                },
            },
            ensure_ascii=False,
            separators=(",", ":"),
        ),
    },
    {
        "id": "tpl.relic.rotation_trick.remove",
        "state": "Implemented",
        "design_text": "[击杀怪物时] 每移除6张怪物卡，洗入一张旋转轮到战斗卡组",
        "requires_json": '["NoOwnerEntity"]',
        "conditions_json": "[]",
        "body": json.dumps(
            {
                "kind": "Triggered",
                "trigger": {
                    "atom": "OnCumulative",
                    "metric": "monsterRemoved",
                    "threshold": 6,
                    "counterKey": "relic.rotation_trick.remove",
                },
                "target": {"atom": "Player"},
                "action": {
                    "atom": "ShuffleInto",
                    "defId": "help.rotation_wheel",
                    "kind": "HelpCard",
                    "count": "{{count}}",
                    "top": False,
                },
            },
            ensure_ascii=False,
            separators=(",", ":"),
        ),
    },
    {
        "id": "tpl.relic.terror_mask.remove",
        "state": "Implemented",
        "design_text": "[击杀怪物时] 本关卡每移除6张怪物卡，随机移除一张九宫格上的普通等级怪物卡",
        "requires_json": '["NoOwnerEntity"]',
        "conditions_json": "[]",
        "body": json.dumps(
            {
                "kind": "Triggered",
                "trigger": {
                    "atom": "OnCumulative",
                    "metric": "monsterRemoved",
                    "threshold": 6,
                    "counterKey": "relic.terror_mask.remove",
                },
                "target": {
                    "atom": "FilteredCards",
                    "kind": "Monster",
                    "zone": "Board",
                    "excludeBoss": True,
                    "random": True,
                    "count": "{{count}}",
                },
                "action": {
                    "atom": "RemoveCard",
                    "destination": "Removed",
                    "reason": "{{reason}}",
                },
            },
            ensure_ascii=False,
            separators=(",", ":"),
        ),
    },
    {
        "id": "tpl.relic.junk_cycler.use",
        "state": "Implemented",
        "design_text": "[使用道具卡时] 每使用9张道具卡，随机洗入一张道具卡到战斗卡组",
        "requires_json": '["NoOwnerEntity"]',
        "conditions_json": "[]",
        "body": json.dumps(
            {
                "kind": "Triggered",
                "trigger": {
                    "atom": "OnCumulative",
                    "metric": "helpCardUsed",
                    "threshold": 9,
                    "counterKey": "relic.junk_cycler.use",
                },
                "target": {"atom": "Player"},
                "action": {
                    "atom": "ShuffleRandomContent",
                    "kind": "HelpCard",
                    "count": "{{count}}",
                    "top": False,
                },
            },
            ensure_ascii=False,
            separators=(",", ":"),
        ),
    },
    {
        "id": "tpl.relic.junk_body.use",
        "state": "Implemented",
        "design_text": "[使用道具卡时] 每使用3张道具卡，获得1点血量上限",
        "requires_json": '["NoOwnerEntity"]',
        "conditions_json": "[]",
        "body": json.dumps(
            {
                "kind": "Triggered",
                "trigger": {
                    "atom": "OnCumulative",
                    "metric": "helpCardUsed",
                    "threshold": 3,
                    "counterKey": "relic.junk_body.use",
                },
                "target": {"atom": "Player"},
                "action": {
                    "atom": "ModifyBaseStat",
                    "stat": "MaxHp",
                    "delta": "{{delta}}",
                    "reason": "{{reason}}",
                },
            },
            ensure_ascii=False,
            separators=(",", ":"),
        ),
    },
    {
        "id": "tpl.relic.blood_demon.damage",
        "state": "Implemented",
        "design_text": "[受到伤害时] 每受到5点伤害，获得1点血量上限",
        "requires_json": '["NoOwnerEntity"]',
        "conditions_json": "[]",
        "body": json.dumps(
            {
                "kind": "Triggered",
                "trigger": {
                    "atom": "OnCumulative",
                    "metric": "damageTaken",
                    "threshold": 5,
                    "targetIsPlayer": True,
                    "counterKey": "relic.blood_demon.damage",
                },
                "target": {"atom": "Player"},
                "action": {
                    "atom": "ModifyBaseStat",
                    "stat": "MaxHp",
                    "delta": "{{delta}}",
                    "reason": "{{reason}}",
                },
            },
            ensure_ascii=False,
            separators=(",", ":"),
        ),
    },
]

relics = [
    (
        "relic_punch_card_knife",
        "relic.punch_card_knife",
        "Attack",
        "打卡刀",
        "攻击+1；[击杀怪物时] 每移除5张怪物卡，随机洗入一张常规道具卡到战斗卡组",
        "White",
        ["tag.attack", "tag.help"],
        [
            asm("relic.punch_card_knife.attack", "tpl.relic.wood_sword.base", {"value": 1}),
            asm("relic.punch_card_knife.remove", "tpl.relic.punch_card_knife.remove", {"count": 1}),
        ],
    ),
    (
        "relic_rotation_trick",
        "relic.rotation_trick",
        "Utility",
        "旋转技巧",
        "[击杀怪物时] 每移除6张怪物卡，洗入一张旋转轮到战斗卡组",
        "White",
        ["tag.move"],
        [asm("relic.rotation_trick.remove", "tpl.relic.rotation_trick.remove", {"count": 1})],
    ),
    (
        "relic_terror_mask",
        "relic.terror_mask",
        "Attack",
        "恐怖面罩",
        "攻击+1；[击杀怪物时] 本关卡每移除6张怪物卡，随机移除一张九宫格上的普通等级怪物卡",
        "Blue",
        ["tag.attack"],
        [
            asm("relic.terror_mask.attack", "tpl.relic.wood_sword.base", {"value": 1}),
            asm(
                "relic.terror_mask.remove",
                "tpl.relic.terror_mask.remove",
                {"count": 1, "reason": "relic.terror_mask"},
            ),
        ],
    ),
    (
        "relic_junk_cycler",
        "relic.junk_cycler",
        "Utility",
        "废物循环机",
        "[使用道具卡时] 每使用9张道具卡，随机洗入一张道具卡到战斗卡组",
        "Blue",
        ["tag.help"],
        [asm("relic.junk_cycler.use", "tpl.relic.junk_cycler.use", {"count": 1})],
    ),
    (
        "relic_junk_body",
        "relic.junk_body",
        "Utility",
        "废物躯体",
        "[使用道具卡时] 每使用3张道具卡，获得1点血量上限",
        "Blue",
        ["tag.help", "tag.hp"],
        [
            asm(
                "relic.junk_body.use",
                "tpl.relic.junk_body.use",
                {"delta": 1, "reason": "relic.junk_body"},
            )
        ],
    ),
    (
        "relic_blood_demon",
        "relic.blood_demon",
        "Utility",
        "血魔",
        "[受到伤害时] 每受到5点伤害，获得1点血量上限",
        "White",
        ["tag.hp"],
        [
            asm(
                "relic.blood_demon.damage",
                "tpl.relic.blood_demon.damage",
                {"delta": 1, "reason": "relic.blood_demon"},
            )
        ],
    ),
]


def upsert_templates(path: Path):
    data = json.loads(path.read_text(encoding="utf-8"))
    by_id = {row["id"]: i for i, row in enumerate(data)}
    for tpl in templates:
        if tpl["id"] in by_id:
            data[by_id[tpl["id"]]] = tpl
        else:
            data.append(tpl)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    print(f"templates -> {path} (+{len(templates)})")


def write_relic(file_stem, content_id, role, name, desc, rarity, tags, assemblies):
    payload = relic_json(content_id, role, name, desc, rarity, tags, assemblies)
    text = json.dumps(payload, ensure_ascii=False, indent=4) + "\n"
    for folder in (arts_cards, stream_cards):
        target = folder / f"{file_stem}.json"
        target.write_text(text, encoding="utf-8")
        meta = folder / f"{file_stem}.json.meta"
        if not meta.exists():
            meta.write_text(meta_text(), encoding="utf-8")
    print(f"wrote {content_id}")


def update_index():
    new_ids = [r[1] for r in relics]
    for folder in (arts_cards, stream_cards):
        index_path = folder / "_index.json"
        index = json.loads(index_path.read_text(encoding="utf-8"))
        cards = index.get("cards", {})
        relics_list = list(cards.get("Relic", []))
        for rid in new_ids:
            if rid not in relics_list:
                relics_list.append(rid)
        cards["Relic"] = sorted(relics_list)
        index["cards"] = cards
        index_path.write_text(json.dumps(index, ensure_ascii=False, indent=4) + "\n", encoding="utf-8")
        print(f"index -> {index_path}")


def main():
    upsert_templates(arts_tpl)
    upsert_templates(stream_tpl)
    for row in relics:
        write_relic(*row)
    update_index()


if __name__ == "__main__":
    main()
