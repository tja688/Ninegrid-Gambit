# -*- coding: utf-8 -*-
import json, io
from pathlib import Path
from collections import defaultdict

ROOT = Path(r"c:\Users\jinji\Documents\GitHub\Ninegrid-Gambit")
CARDS = ROOT / "Assets/Arts/ContentVisual/cards"

out = io.open(r"CliEval/monsters_by_deck.txt", "w", encoding="utf-8")
decks = defaultdict(list)
for f in sorted(CARDS.glob("monster_*.json")):
    d = json.loads(f.read_text(encoding="utf-8"))
    decks[d.get("deckId") or "-"].append((d["contentId"], d.get("displayName", ""), f.name))

for deck, lst in sorted(decks.items()):
    out.write(f"== {deck} ({len(lst)})\n")
    for cid, name, fn in lst:
        out.write(f"  {cid}\t{name}\t{fn}\n")
out.close()
