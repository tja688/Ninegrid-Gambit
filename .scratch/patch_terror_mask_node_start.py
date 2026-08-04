# -*- coding: utf-8 -*-
import json
from pathlib import Path

root = Path(r"C:\Users\jinji\Documents\GitHub\Ninegrid Gambit")
body = json.dumps(
    {
        "kind": "Triggered",
        "trigger": {"atom": "OnNodeStart"},
        "target": {"atom": "Player"},
        "action": {
            "atom": "SetCounter",
            "key": "relic.terror_mask.remove",
            "value": 0,
        },
    },
    ensure_ascii=False,
    separators=(",", ":"),
)
tpl = {
    "id": "tpl.relic.terror_mask.node_start",
    "state": "Implemented",
    "design_text": "[每关卡开始时] 清零本关怪物移除累计",
    "requires_json": '["NoOwnerEntity"]',
    "conditions_json": "[]",
    "body": body,
}

for rel in (
    "Assets/Arts/ContentVisual/tables/effect_templates.json",
    "Assets/StreamingAssets/ContentVisual/tables/effect_templates.json",
):
    path = root / rel
    data = json.loads(path.read_text(encoding="utf-8"))
    by_id = {row["id"]: i for i, row in enumerate(data)}
    if tpl["id"] in by_id:
        data[by_id[tpl["id"]]] = tpl
    else:
        data.append(tpl)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    print("tpl", rel)

node = {
    "id": "relic.terror_mask.node_start",
    "templateId": "tpl.relic.terror_mask.node_start",
    "containerType": "Relic",
    "argsJson": "{}",
}

for folder in (
    "Assets/Arts/ContentVisual/cards",
    "Assets/StreamingAssets/ContentVisual/cards",
):
    path = root / folder / "relic_terror_mask.json"
    card = json.loads(path.read_text(encoding="utf-8"))
    assemblies = [a for a in card["effectAssemblies"] if a["id"] != node["id"]]
    out = []
    for a in assemblies:
        out.append(a)
        if a["id"] == "relic.terror_mask.attack":
            out.append(node)
    if not any(a["id"] == node["id"] for a in out):
        out.insert(0, node)
    card["effectAssemblies"] = out
    path.write_text(json.dumps(card, ensure_ascii=False, indent=4) + "\n", encoding="utf-8")
    print("card", path)
