# -*- coding: utf-8 -*-
import json, os, glob, re

cards_dir = r"Assets/Arts/ContentVisual/cards"
tpl_path = r"Assets/Arts/ContentVisual/tables/effect_templates.json"
with open(tpl_path, encoding="utf-8") as f:
    templates = {t["id"]: t for t in json.load(f)}

EXCLUDE = {
    "除甲刀",
    "腐朽顺劈斧",
    "荆棘甲",
    "肌肉反击",
    "金色宝箱",
    "废物增幅器",
    "铁盾",
    "身体潜力",
    "超越维度",
    "废物剑",
    "血液循环",
    "血液暴力",
    "血液迸发",
    "血再生",
    "复合盔甲",
    "金血",
    "陷阱格",
    "旋转倒刺",
    "锐利长剑",
    "金属血液",
    "狂战士斧",
    "转速引擎",
    "打卡刀",
    "旋转技巧",
    "恐怖面罩",
    "废物循环机",
    "废物躯体",
    "血魔",
    "泡沫盔甲",
    "锻造器具",
    "金剑",
    "偶数仇恨",
    "军械库",
    "刺皮",
    "历战",
    "塔之子",
    "废物老虎机",
    "硬皮",
    "血液冲击波",
    "轻车熟路",
}

design = {}
with open(r"Assets/Docs/九宫格登神/05-遗物/遗物.md", encoding="utf-8") as f:
    text = f.read()
cur = None
for line in text.splitlines():
    m = re.match(r"^### (.+)$", line)
    if m:
        cur = m.group(1).strip()
        design[cur] = {"attrs": "", "effect": "", "rarity": ""}
        continue
    if not cur:
        continue
    if line.startswith("- 属性："):
        design[cur]["attrs"] = line[len("- 属性：") :].strip()
    elif line.startswith("- 效果"):
        design[cur]["effect"] = line.split("：", 1)[-1].strip() if "：" in line else line
    elif line.startswith("- 品质："):
        design[cur]["rarity"] = line[len("- 品质：") :].strip()

out = []
for path in sorted(glob.glob(os.path.join(cards_dir, "relic_*.json"))):
    with open(path, encoding="utf-8") as f:
        d = json.load(f)
    name = d.get("displayName", "")
    if name in EXCLUDE or name not in design:
        continue
    des = design[name]
    issues = []
    if not (d.get("description") or "").strip():
        issues.append("EMPTY_DESC")
    assemblies = []
    for a in d.get("effectAssemblies") or []:
        tid = a.get("templateId") or ""
        args = json.loads(a.get("argsJson") or "{}")
        tpl = templates.get(tid, {})
        body = tpl.get("body", "")
        assemblies.append(
            {
                "templateId": tid,
                "args": args,
                "design_text": tpl.get("design_text"),
                "trigger": "OnSelfUsed" if "OnSelfUsed" in body else (
                    "OnAnyHelpCardUsed" if "OnAnyHelpCardUsed" in body else None
                ),
            }
        )
        if "OnSelfUsed" in body and name in ("废物发射器", "废物利用机", "废物涂层"):
            issues.append(f"TRIGGER_OnSelfUsed:{tid}")

    # craving maxhp
    if name == "渴望":
        has_max = any(
            "shared.5" in (a["templateId"] or "") or a["args"].get("delta") == 10
            for a in assemblies
        )
        if not has_max:
            issues.append("MISSING_MAXHP+10")

    # lucky coin: design says 层主 only
    if name == "幸运硬币":
        tids = [a["templateId"] for a in assemblies]
        if any("elite" in t for t in tids):
            issues.append("ELITE_KILL_EXTRA_vs_层主")

    out.append(
        {
            "id": d["contentId"],
            "name": name,
            "desc": d.get("description", ""),
            "design_attrs": des["attrs"],
            "design_effect": des["effect"],
            "assemblies": assemblies,
            "issues": issues,
        }
    )

with open(r".scratch/keeper_audit.json", "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=2)

print("keepers", len(out))
for r in out:
    flag = "!!" if r["issues"] else "ok"
    print(flag, r["id"], r["name"], r["issues"])
