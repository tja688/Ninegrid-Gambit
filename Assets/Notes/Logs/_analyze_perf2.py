import json, re
from collections import defaultdict

def load_doc(path):
    with open(path, "r", encoding="utf-8-sig") as f:
        return json.load(f)

def parse_cards_string(s):
    if not s or not isinstance(s, str):
        return []
    parts = s.split(";")
    cards = []
    for part in parts:
        part = part.strip()
        if not part:
            continue
        m = re.match(r"~?(\d+)", part)
        uid = int(m.group(1)) if m else None
        active_m = re.search(r"active=(\d+)", part)
        active = int(active_m.group(1)) if active_m else None
        slot_m = re.search(r"slot=([^,;]+)", part)
        slot = slot_m.group(1) if slot_m else None
        mode_m = re.search(r"mode=([^,;]+)", part)
        mode = mode_m.group(1) if mode_m else None
        cards.append({"uid": uid, "active": active, "slot": slot, "mode": mode, "raw": part[:80]})
    return cards

def beat_kind(e):
    p = e.get("payload") or {}
    return p.get("beatKind") or p.get("beatName") or ""

path = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\PerfLog\perflog-20260712-141249-seed1.json"
doc = load_doc(path)
evs = doc["events"]

# BeatOpen/Close with beatKind
print("=== BeatOpen/Close sequence (first 15) ===")
opens = [e for e in evs if e.get("kind")=="BeatOpen"]
for e in opens[:15]:
    p = e.get("payload") or {}
    print("Open idx=%s beatId=%s tMs=%s kind=%s node=%s" % (e["index"], e["beatId"], e["tMs"], p.get("beatKind"), p.get("nodeIndex")))

# OpeningDeal beat 1 window: idx 0-400
print("\n=== Events idx 345-420 (post OpeningDeal close) ===")
for e in evs:
    if 345 <= e["index"] <= 420:
        k = e["kind"]
        line = "idx=%s beatId=%s tMs=%s kind=%s" % (e["index"], e["beatId"], e["tMs"], k)
        if k in ("VisChange", "Despawn", "Spawn", "BoardSnap", "SnapSet", "Vacate", "RegistryAudit", "Anomaly", "BeatOpen", "BeatClose"):
            if k == "BoardSnap":
                p = e["payload"]
                cards = parse_cards_string(p.get("cards",""))
                act0 = [c for c in cards if c["active"]==0]
                neg = [c for c in cards if c["uid"] is None]
                print(line + " phase=%s nCards=%d active0=%d" % (p.get("phase"), len(cards), len(act0)))
            elif k == "VisChange":
                print(line + " uid=%s site=%s payload=%s" % (e.get("uid"), e.get("site"), e.get("payload")))
            elif k == "Despawn":
                print(line + " uid=%s payload=%s" % (e.get("uid"), e.get("payload")))
            elif k in ("BeatOpen","BeatClose"):
                print(line + " payload=%s" % e.get("payload"))
            elif k == "SnapSet":
                print(line + " uid=%s site=%s payload=%s" % (e.get("uid"), e.get("site"), e.get("payload")))

# BoardSnap beat 1 open vs close card stats
for e in evs:
    if e.get("kind")=="BoardSnap" and e.get("beatId")==1:
        p = e["payload"]
        cards = parse_cards_string(p.get("cards",""))
        on_field = [c for c in cards if c["slot"] not in ("-1", None) and c["slot"] != "-1"]
        print("\nBoardSnap beat1 idx=%s phase=%s total=%d onField=%d active0=%d modes=%s" % (
            e["index"], p.get("phase"), len(cards), len(on_field),
            sum(1 for c in cards if c["active"]==0),
            defaultdict(int, {c["mode"]:1 for c in cards})
        ))
        # count modes properly
        modes = defaultdict(int)
        for c in cards:
            modes[c["mode"]] += 1
        print("  modes:", dict(modes))
        print("  field uids:", sorted([c["uid"] for c in cards if c["slot"] and c["slot"]!="-1"], key=lambda x:x or 0))

# All Despawn in first 500 index
print("\n=== Despawn first 500 indices ===")
for e in evs:
    if e["index"]<=500 and e.get("kind")=="Despawn":
        print("idx=%s beatId=%s tMs=%s uid=%s" % (e["index"], e["beatId"], e["tMs"], e.get("uid")))

# VisChange all sites for beatId<=3
print("\n=== VisChange beatId<=3 ===")
for e in evs:
    if e.get("kind")=="VisChange" and e.get("beatId",99)<=3:
        print("idx=%s beatId=%s uid=%s site=%s active=%s" % (e["index"], e["beatId"], e.get("uid"), e.get("site"), (e.get("payload") or {}).get("active")))

# MotionEnd for uids at beat 1 close without matching end?
print("\n=== MissingMotionEnd uids at beat 1 ===")
for e in evs:
    if e.get("kind")=="Anomaly" and e.get("beatId")==1:
        print(e.get("uid"), e["payload"].get("detail"))

# Next beat after 1
bc1 = [e for e in evs if e.get("kind")=="BeatClose" and e.get("beatId")==1][0]
bo2 = [e for e in evs if e.get("kind")=="BeatOpen" and e["index"]>bc1["index"]][:3]
print("\nAfter beat1 close idx=%s, next BeatOpens:" % bc1["index"])
for e in bo2:
    print("  idx=%s beatId=%s kind=%s tMs=%s" % (e["index"], e["beatId"], beat_kind(e), e["tMs"]))

# BoardSnap between beat1 close and beat2 open
if bo2:
    end_idx = bo2[0]["index"]
    print("\nBoardSnap between idx %d and %d" % (bc1["index"], end_idx))
    for e in evs:
        if bc1["index"] < e["index"] < end_idx and e.get("kind")=="BoardSnap":
            p = e["payload"]
            cards = parse_cards_string(p.get("cards",""))
            print("  idx=%s phase=%s cards=%d active0=%d full=%s" % (e["index"], p.get("phase"), len(cards), sum(1 for c in cards if c["active"]==0), p.get("full")))
