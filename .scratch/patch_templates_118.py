# -*- coding: utf-8 -*-
"""Surgical patch for effect_templates.json — no full reformat."""
import json
import shutil
from pathlib import Path

root = Path(r"C:\Users\jinji\Documents\GitHub\Ninegrid Gambit")
arts = root / "Assets/Arts/ContentVisual/tables/effect_templates.json"
stream = root / "Assets/StreamingAssets/ContentVisual/tables/effect_templates.json"

NEW_COATING = {
    "id": "tpl.relic.junk_coating.use",
    "state": "Implemented",
    "design_text": "[使用道具卡时] 获得1点当前护甲",
    "requires_json": '["NoOwnerEntity"]',
    "conditions_json": "[]",
    "body": '{"kind":"Triggered","trigger":{"atom":"OnAnyHelpCardUsed"},"target":{"atom":"Player"},"action":{"atom":"GainArmor","amount":"{{amount}}"}}',
}
NEW_RECYCLER = {
    "id": "tpl.relic.junk_recycler.use",
    "state": "Implemented",
    "design_text": "[使用道具卡时] 恢复2点血量",
    "requires_json": '["NoOwnerEntity"]',
    "conditions_json": "[]",
    "body": '{"kind":"Triggered","trigger":{"atom":"OnAnyHelpCardUsed"},"target":{"atom":"Player"},"action":{"atom":"Heal","amount":"{{amount}}","actor":"Player"}}',
}


def patch(path: Path):
    text = path.read_text(encoding="utf-8")
    data = json.loads(text)
    by_id = {t["id"]: i for i, t in enumerate(data)}

    # Patch junk_launcher.use in place
    i = by_id["tpl.relic.junk_launcher.use"]
    jl = data[i]
    jl["design_text"] = "[使用道具卡时] 对随机一张怪物卡造成2点伤害"
    jl["body"] = jl["body"].replace('"atom":"OnSelfUsed"', '"atom":"OnAnyHelpCardUsed"')

    # Insert new templates after junk_launcher.use if missing
    insert_at = i + 1
    for tpl in (NEW_COATING, NEW_RECYCLER):
        if tpl["id"] in by_id:
            data[by_id[tpl["id"]]].update(tpl)
        else:
            data.insert(insert_at, tpl)
            insert_at += 1
            # refresh indices after insert
            by_id = {t["id"]: idx for idx, t in enumerate(data)}

    # Write with original-like compact indent (1 space) matching repo style
    path.write_text(
        json.dumps(data, ensure_ascii=False, indent=1) + "\n",
        encoding="utf-8",
        newline="\n",
    )


patch(arts)
shutil.copy2(arts, stream)
print("patched templates surgically")
# sanity
data = json.loads(arts.read_text(encoding="utf-8"))
ids = [t["id"] for t in data if "junk_recycler" in t["id"] or "junk_coating" in t["id"] or t["id"] == "tpl.relic.junk_launcher.use"]
for tid in ids:
    t = next(x for x in data if x["id"] == tid)
    print(tid, "OnAny" in t["body"], t["design_text"][:20])
