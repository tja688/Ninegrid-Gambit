# -*- coding: utf-8 -*-
import json, io

out = io.open("CliEval/luban_names.txt", "w", encoding="utf-8")
for fn in ["skills.json", "relics.json", "cards.json", "monster_decks.json"]:
    d = json.load(open("Assets/Tools/Luban/Datas/" + fn, encoding="utf-8"))
    out.write("== %s type=%s\n" % (fn, type(d).__name__))
    if isinstance(d, dict):
        out.write(str(list(d.keys())[:5]) + "\n")
        first = list(d.values())[0]
        out.write(json.dumps(first, ensure_ascii=False)[:250] + "\n")
    else:
        out.write("len=%d\n" % len(d))
        out.write(json.dumps(d[0], ensure_ascii=False)[:250] + "\n")
out.close()
print("ok")
