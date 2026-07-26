# -*- coding: utf-8 -*-
import json, io

d = json.load(open(r"Assets/Arts/Images/Png/像素怪物合集/sprites/_card_fit/catalog.json", encoding="utf-8"))
out = io.open(r"CliEval/idle_usable.txt", "w", encoding="utf-8")
rows = [c for c in d["clips"] if "idle" in c["folder"].lower()]
ok = [c for c in rows if c.get("usable")]
out.write(f"idle total {len(rows)} usable {len(ok)}\n")
for c in sorted(ok, key=lambda x: x["folder"]):
    u = c["opaqueUnion"]
    off = c.get("offsetPx") or {"x": 0, "y": 0}
    out.write(
        f"{c['folder']}\tunion={u['width']:.0f}x{u['height']:.0f}"
        f"\tscale={c.get('suggestedUniformScale', 1):.2f}"
        f"\toff=({off['x']:.1f},{off['y']:.1f})\tscore={c.get('score', 0):.2f}\n"
    )
out.close()
