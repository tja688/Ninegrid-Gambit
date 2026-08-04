# -*- coding: utf-8 -*-
"""Apply #118 keeper audit fixes to Arts + StreamingAssets card JSON and effect templates."""
import json
import shutil
from pathlib import Path

root = Path(r"C:\Users\jinji\Documents\GitHub\Ninegrid Gambit")
arts_cards = root / "Assets/Arts/ContentVisual/cards"
stream_cards = root / "Assets/StreamingAssets/ContentVisual/cards"
arts_tpl = root / "Assets/Arts/ContentVisual/tables/effect_templates.json"
stream_tpl = root / "Assets/StreamingAssets/ContentVisual/tables/effect_templates.json"

DESCRIPTIONS = {
    "relic.craving": "血量上限+10；所有恢复血量效果翻倍",
    "relic.dragon_scale_armor": "基础护甲+1；血量上限+6；所有怪物卡的攻击-1",
    "relic.gold_armor": "基础护甲+1；[受到伤害时] 每5金币抵消1点当前护甲伤害（对血量伤害不生效）",
    "relic.gold_knife": "[击杀怪物时] 获得2金币",
    "relic.heavy_armor": "基础护甲+1；[每关卡开始时] 每有两点基础护甲，额外获得1点当前护甲",
    "relic.junk_coating": "[使用道具卡时] 获得1点当前护甲",
    "relic.junk_launcher": "[使用道具卡时] 对随机一张怪物卡造成2点伤害",
    "relic.junk_recycler": "[使用道具卡时] 恢复2点血量",
    "relic.lucky_coin": "[击杀怪物时] 将一张金币卡加入战斗卡组（仅对层主生效）",
    "relic.phoenix_feather": "血量上限+8；[受到致命伤害时] 恢复50%血量并永久移除本遗物",
    "relic.potion_bag": "[每关卡开始时] 将两张恢复药水加入到玩家侧卡组",
    "relic.rotation_button": "[每关卡开始时] 将一张旋转轮放入到道具牌格",
    "relic.shield_knife": "[击杀怪物时] 获得1点当前护甲",
    "relic.sling": "[击杀怪物时] 对随机一张怪物卡造成4点伤害",
    "relic.swap_button": "[每关卡开始时] 将一张交换卡放入到道具牌格",
    "relic.throwing_knife_bag": "[每关卡开始时] 将两张飞刀加入到玩家侧卡组",
    "relic.vitality_amulet": "血量上限+6；[每关卡结束时] 恢复6点血量",
    "relic.wood_armor": "血量上限+2；同时拥有木盾、木剑、木甲时，木甲血量额外+8",
    "relic.wood_shield": "基础护甲+1；同时拥有木盾、木剑、木甲时，木盾基础护甲额外+2",
    "relic.wood_sword": "攻击+1；同时拥有木盾、木剑、木甲时，木剑攻击额外+2",
}


def load(path: Path):
    with path.open(encoding="utf-8") as f:
        return json.load(f)


def dump(path: Path, data):
    with path.open("w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=4)
        f.write("\n")


def patch_card(data: dict) -> bool:
    cid = data.get("contentId")
    changed = False
    if cid in DESCRIPTIONS and data.get("description") != DESCRIPTIONS[cid]:
        data["description"] = DESCRIPTIONS[cid]
        changed = True

    if cid == "relic.craving":
        ass = data.setdefault("effectAssemblies", [])
        has_max = any(a.get("id") == "relic.craving.max_hp" for a in ass)
        if not has_max:
            ass.insert(
                0,
                {
                    "id": "relic.craving.max_hp",
                    "templateId": "tpl.shared.5.relic_blood_shockwave_max_hp",
                    "containerType": "Relic",
                    "argsJson": '{"delta":10,"reason":"relic.craving.max_hp"}',
                },
            )
            changed = True

    if cid == "relic.lucky_coin":
        before = len(data.get("effectAssemblies") or [])
        data["effectAssemblies"] = [
            a
            for a in (data.get("effectAssemblies") or [])
            if a.get("id") != "relic.lucky_coin.elite_kill"
            and "elite" not in (a.get("templateId") or "")
        ]
        if len(data["effectAssemblies"]) != before:
            changed = True

    if cid == "relic.junk_recycler":
        for a in data.get("effectAssemblies") or []:
            if a.get("id") == "relic.junk_recycler.use" and a.get("templateId") != "tpl.relic.junk_recycler.use":
                a["templateId"] = "tpl.relic.junk_recycler.use"
                changed = True

    if cid == "relic.junk_coating":
        for a in data.get("effectAssemblies") or []:
            if a.get("id") == "relic.junk_coating.use" and a.get("templateId") != "tpl.relic.junk_coating.use":
                a["templateId"] = "tpl.relic.junk_coating.use"
                changed = True

    return changed


NEW_TEMPLATES = [
    {
        "id": "tpl.relic.junk_recycler.use",
        "state": "Implemented",
        "design_text": "[使用道具卡时] 恢复2点血量",
        "requires_json": '["NoOwnerEntity"]',
        "conditions_json": "[]",
        "body": '{"kind":"Triggered","trigger":{"atom":"OnAnyHelpCardUsed"},"target":{"atom":"Player"},"action":{"atom":"Heal","amount":"{{amount}}","actor":"Player"}}',
    },
    {
        "id": "tpl.relic.junk_coating.use",
        "state": "Implemented",
        "design_text": "[使用道具卡时] 获得1点当前护甲",
        "requires_json": '["NoOwnerEntity"]',
        "conditions_json": "[]",
        "body": '{"kind":"Triggered","trigger":{"atom":"OnAnyHelpCardUsed"},"target":{"atom":"Player"},"action":{"atom":"GainArmor","amount":"{{amount}}"}}',
    },
]


def patch_templates(path: Path):
    templates = load(path)
    by_id = {t["id"]: t for t in templates}
    changed = False

    # junk launcher: OnSelfUsed -> OnAnyHelpCardUsed
    jl = by_id.get("tpl.relic.junk_launcher.use")
    if jl:
        if "OnSelfUsed" in jl.get("body", ""):
            jl["body"] = jl["body"].replace(
                '"atom":"OnSelfUsed"', '"atom":"OnAnyHelpCardUsed"'
            )
            changed = True
        if jl.get("design_text") != "[使用道具卡时] 对随机一张怪物卡造成2点伤害":
            jl["design_text"] = "[使用道具卡时] 对随机一张怪物卡造成2点伤害"
            changed = True

    for tpl in NEW_TEMPLATES:
        if tpl["id"] not in by_id:
            # insert near junk_launcher
            insert_at = next(
                (i for i, t in enumerate(templates) if t["id"] == "tpl.relic.junk_launcher.use"),
                len(templates),
            )
            templates.insert(insert_at + 1, tpl)
            by_id[tpl["id"]] = tpl
            changed = True
        else:
            existing = by_id[tpl["id"]]
            for k, v in tpl.items():
                if existing.get(k) != v:
                    existing[k] = v
                    changed = True

    if changed:
        dump(path, templates)
    return changed


def sync_card(cid_file: str):
    src = arts_cards / cid_file
    dst = stream_cards / cid_file
    if src.exists():
        shutil.copy2(src, dst)


def main():
    patched_cards = []
    for path in sorted(arts_cards.glob("relic_*.json")):
        data = load(path)
        if data.get("contentId") not in DESCRIPTIONS and data.get("contentId") not in (
            "relic.craving",
            "relic.lucky_coin",
            "relic.junk_recycler",
            "relic.junk_coating",
            "relic.junk_launcher",
        ):
            continue
        if patch_card(data):
            dump(path, data)
            patched_cards.append(path.name)
            sync_card(path.name)

    # Always sync craving/lucky/junk even if only template side changed later
    for name in (
        "relic_craving.json",
        "relic_lucky_coin.json",
        "relic_junk_recycler.json",
        "relic_junk_coating.json",
        "relic_junk_launcher.json",
        "relic_dragon_scale_armor.json",
        "relic_gold_armor.json",
        "relic_gold_knife.json",
        "relic_heavy_armor.json",
        "relic_phoenix_feather.json",
        "relic_potion_bag.json",
        "relic_rotation_button.json",
        "relic_shield_knife.json",
        "relic_sling.json",
        "relic_swap_button.json",
        "relic_throwing_knife_bag.json",
        "relic_vitality_amulet.json",
        "relic_wood_armor.json",
        "relic_wood_shield.json",
        "relic_wood_sword.json",
    ):
        arts = arts_cards / name
        if arts.exists():
            data = load(arts)
            if patch_card(data):
                dump(arts, data)
                if name not in patched_cards:
                    patched_cards.append(name)
            sync_card(name)

    t1 = patch_templates(arts_tpl)
    shutil.copy2(arts_tpl, stream_tpl)

    print("patched cards:", len(patched_cards))
    for n in patched_cards:
        print(" ", n)
    print("templates changed:", t1)


if __name__ == "__main__":
    main()
