# -*- coding: utf-8 -*-
"""抽取全部 live 内容的中文可译字段 → _l10n_zh_extract.json（只读游戏内容）"""
import json, os, sys, io

ROOT = r"C:\Users\jinji\Documents\GitHub\Ninegrid Gambit"
CARDS_DIR = os.path.join(ROOT, "Assets", "Arts", "ContentVisual", "cards")
TABLES_DIR = os.path.join(ROOT, "Assets", "Arts", "ContentVisual", "tables")
OUT = os.path.join(ROOT, "Assets", "Notes", "Localization", "_l10n_zh_extract.json")

ARCHIVE_DECKS = {"deck.relic_archive", "deck.help_archive"}
TRANSITION_DECK = "deck.transition"

def classify(card):
    deck = card.get("deckId") or ""
    if deck in ARCHIVE_DECKS:
        return "archive"
    if deck == TRANSITION_DECK:
        return "transition"
    if card.get("isReserve") is True:
        return "isReserve"
    return "live"

live, skipped = {}, []
for fn in sorted(os.listdir(CARDS_DIR)):
    if not fn.endswith(".json") or fn == "_index.json":
        continue
    with io.open(os.path.join(CARDS_DIR, fn), "r", encoding="utf-8") as f:
        card = json.load(f)
    cid = card.get("contentId") or card.get("id")
    kind = card.get("kind", "?")
    bucket = classify(card)
    if bucket != "live":
        skipped.append({"contentId": cid, "kind": kind, "file": fn, "bucket": bucket,
                        "deckId": card.get("deckId"), "displayName": card.get("displayName")})
        continue
    entry = {"kind": kind, "file": fn}
    for k in ("displayName", "description", "faceIntro"):
        v = card.get(k)
        if isinstance(v, str) and v.strip():
            entry[k] = v
    live[cid] = entry

# merge monster_decks display_name onto deckId keys
with io.open(os.path.join(TABLES_DIR, "monster_decks.json"), "r", encoding="utf-8") as f:
    mdecks = json.load(f)
deck_table = []
for d in mdecks:
    did = d["deck_id"]
    if did == TRANSITION_DECK:
        skipped.append({"contentId": did, "kind": "DeckTable", "file": "monster_decks.json",
                        "bucket": "transition", "deckId": did, "displayName": d.get("display_name")})
        continue
    deck_table.append({"deckId": did, "display_name": d.get("display_name")})
    if did in live:
        live[did]["deckTableDisplayName"] = d.get("display_name")
    else:
        live[did] = {"kind": "Deck(table-only)", "file": "monster_decks.json",
                     "displayName": d.get("display_name")}

kinds = {}
for v in live.values():
    kinds[v["kind"]] = kinds.get(v["kind"], 0) + 1

out = {"liveCount": len(live), "kindCounts": kinds, "skipped": skipped, "entries": live}
with io.open(OUT, "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=1)
print("live:", len(live))
print("kinds:", json.dumps(kinds, ensure_ascii=False))
print("skipped:", len(skipped))
