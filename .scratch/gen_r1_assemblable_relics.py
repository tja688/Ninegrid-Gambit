# -*- coding: utf-8 -*-
import json
import uuid
from pathlib import Path

root = Path(r"C:\Users\jinji\Documents\GitHub\Ninegrid Gambit")
arts_cards = root / "Assets/Arts/ContentVisual/cards"
stream_cards = root / "Assets/StreamingAssets/ContentVisual/cards"


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


relics = [
    (
        "relic_composite_armor",
        "relic.composite_armor",
        "Defense",
        "复合盔甲",
        "[每关卡开始时] 每有三点攻击，获得1点当前护甲",
        "White",
        ["tag.armor", "tag.attack"],
        [asm("relic.composite_armor.node_start", "tpl.relic.composite_armor.node_start", {})],
    ),
    (
        "relic_gold_blood",
        "relic.gold_blood",
        "Utility",
        "金血",
        "血量上限+2；[受到伤害时] 每损失1点血量，获得等量金币",
        "White",
        ["tag.hp"],
        [
            asm("relic.gold_blood.max_hp", "tpl.relic.wood_armor.base", {"value": 2}),
            asm("relic.gold_blood.taken", "tpl.relic.gold_blood.taken", {"reason": "goldBlood"}),
        ],
    ),
    (
        "relic_rpm_engine",
        "relic.rpm_engine",
        "Utility",
        "转速引擎",
        "基础护甲+1；[旋转时] 多旋转一次",
        "White",
        ["tag.armor"],
        [
            asm("relic.rpm_engine.base", "tpl.shared.4.relic_dragon_scale_armor_base", {"value": 1}),
            asm("relic.rpm_engine.rotate", "tpl.relic.rpm_engine.rotate", {"count": 1}),
        ],
    ),
    (
        "relic_trap_cell",
        "relic.trap_cell",
        "Attack",
        "陷阱格",
        "当怪物卡移动到格1时，对该怪物卡造成4点伤害",
        "White",
        ["tag.attack"],
        [asm("relic.trap_cell.move", "tpl.relic.trap_cell.move", {"amount": 4})],
    ),
    (
        "relic_junk_sword",
        "relic.junk_sword",
        "Attack",
        "废物剑",
        "攻击+2；[击杀怪物时] 恢复2点血量",
        "Blue",
        ["tag.attack", "tag.hp"],
        [
            asm("relic.junk_sword.base", "tpl.relic.wood_sword.base", {"value": 2}),
            asm("relic.junk_sword.kill", "tpl.relic.junk_sword.kill", {"amount": 2}),
        ],
    ),
    (
        "relic_blood_cycle",
        "relic.blood_cycle",
        "Utility",
        "血液循环",
        "血量上限+4；[受到伤害时] 恢复2点血量",
        "Blue",
        ["tag.hp"],
        [
            asm("relic.blood_cycle.max_hp", "tpl.relic.wood_armor.base", {"value": 4}),
            asm("relic.blood_cycle.taken", "tpl.relic.blood_cycle.taken", {"amount": 2}),
        ],
    ),
    (
        "relic_blood_violence",
        "relic.blood_violence",
        "Attack",
        "血液暴力",
        "血量上限+4；当前血量低于血量上限的50%时，玩家攻击+2",
        "Blue",
        ["tag.hp", "tag.attack"],
        [
            asm("relic.blood_violence.max_hp", "tpl.relic.wood_armor.base", {"value": 4}),
            asm(
                "relic.blood_violence.atk",
                "tpl.relic.blood_violence.atk",
                {"value": 2, "source": "relic.blood_violence"},
            ),
        ],
    ),
    (
        "relic_blood_burst",
        "relic.blood_burst",
        "Attack",
        "血液迸发",
        "血量上限+4；[受到伤害时] 对随机怪物卡造成等量伤害",
        "Blue",
        ["tag.hp", "tag.attack"],
        [
            asm("relic.blood_burst.max_hp", "tpl.relic.wood_armor.base", {"value": 4}),
            asm("relic.blood_burst.taken", "tpl.relic.blood_burst.taken", {}),
        ],
    ),
    (
        "relic_spinning_barb",
        "relic.spinning_barb",
        "Attack",
        "旋转倒刺",
        "怪物卡每移动一次，受到1点伤害",
        "Blue",
        ["tag.attack"],
        [asm("relic.spinning_barb.move", "tpl.relic.spinning_barb.move", {"amount": 1})],
    ),
    (
        "relic_blood_regen",
        "relic.blood_regen",
        "Utility",
        "血再生",
        "血量上限+4；[战斗时] 当前血量低于血量上限的50%时，每次战斗恢复2点血量",
        "Blue",
        ["tag.hp"],
        [
            asm("relic.blood_regen.max_hp", "tpl.relic.wood_armor.base", {"value": 4}),
            asm(
                "relic.blood_regen.battle",
                "tpl.relic.blood_regen.battle",
                {"amount": 2, "sourceAction": "CombatHit"},
            ),
        ],
    ),
    (
        "relic_sharp_longsword",
        "relic.sharp_longsword",
        "Attack",
        "锐利长剑",
        "攻击+1；[击杀怪物时] 本关卡每移除1张怪物卡，玩家攻击+1，离开关卡后攻击复原",
        "Blue",
        ["tag.attack"],
        [
            asm("relic.sharp_longsword.base", "tpl.relic.wood_sword.base", {"value": 1}),
            asm(
                "relic.sharp_longsword.kill",
                "tpl.relic.sharp_longsword.kill",
                {"value": 1, "source": "relic.sharp_longsword"},
            ),
        ],
    ),
    (
        "relic_berserker_axe",
        "relic.berserker_axe",
        "Attack",
        "狂战士斧",
        "攻击+1；当前血量低于血量上限的50%时，玩家攻击翻倍",
        "Gold",
        ["tag.attack", "tag.hp"],
        [
            asm("relic.berserker_axe.base", "tpl.relic.wood_sword.base", {"value": 1}),
            asm(
                "relic.berserker_axe.double",
                "tpl.relic.berserker_axe.double",
                {"value": 2, "source": "relic.berserker_axe"},
            ),
        ],
    ),
    (
        "relic_metal_blood",
        "relic.metal_blood",
        "Defense",
        "金属血液",
        "[受到伤害时] 损失血量后获得等量的当前护甲",
        "Gold",
        ["tag.armor", "tag.hp"],
        [asm("relic.metal_blood.taken", "tpl.relic.metal_blood.taken", {})],
    ),
]

for file, cid, role, name, desc, rarity, tags, assemblies in relics:
    obj = relic_json(cid, role, name, desc, rarity, tags, assemblies)
    text = json.dumps(obj, ensure_ascii=False, indent=4) + "\n"
    for folder in (arts_cards, stream_cards):
        path = folder / f"{file}.json"
        path.write_text(text, encoding="utf-8")
        meta = path.with_suffix(".json.meta")
        if not meta.exists():
            meta.write_text(meta_text(), encoding="utf-8")
    print("rewrote", cid, name)

design_fixes = {
    "tpl.relic.composite_armor.node_start": "[每关卡开始时] 每有三点攻击，获得1点当前护甲",
    "tpl.relic.gold_blood.taken": "[受到伤害时] 每损失1点血量获得等量金币",
    "tpl.relic.rpm_engine.rotate": "[旋转时] 多旋转一次（排除本遗物引发的旋转）",
    "tpl.relic.trap_cell.move": "怪物卡移动到格1时对该怪物造成4点伤害",
    "tpl.relic.junk_sword.kill": "[击杀怪物时] 恢复2点血量",
    "tpl.relic.blood_cycle.taken": "[受到伤害时] 恢复2点血量",
    "tpl.relic.blood_violence.atk": "当前血量低于上限50%时攻击+2",
    "tpl.relic.blood_burst.taken": "[受到伤害时] 对随机怪物造成等量伤害",
    "tpl.relic.spinning_barb.move": "怪物卡每移动一次受到1点伤害",
    "tpl.relic.blood_regen.battle": "[战斗时] 血量低于上限50%时恢复2点血量",
    "tpl.relic.sharp_longsword.kill": "[击杀怪物时] 本关卡攻击+1",
    "tpl.relic.berserker_axe.double": "当前血量低于上限50%时攻击翻倍",
    "tpl.relic.metal_blood.taken": "[受到伤害时] 损失血量后获得等量当前护甲",
}

for tpl_path in (
    root / "Assets/Arts/ContentVisual/tables/effect_templates.json",
    root / "Assets/StreamingAssets/ContentVisual/tables/effect_templates.json",
):
    data = json.loads(tpl_path.read_text(encoding="utf-8"))
    by_id = {row["id"]: row for row in data}
    for tid, text in design_fixes.items():
        if tid not in by_id:
            raise SystemExit(f"missing template {tid} in {tpl_path}")
        by_id[tid]["design_text"] = text
    # Keep array formatting close to existing (indent=1 objects)
    tpl_path.write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    print("fixed templates", tpl_path)

obj = json.loads((arts_cards / "relic_junk_sword.json").read_text(encoding="utf-8"))
assert obj["displayName"] == "废物剑", obj["displayName"]
print("verify ok", obj["displayName"])
