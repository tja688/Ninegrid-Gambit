import json, re

def load(path):
    with open(path, "r", encoding="utf-8-sig") as f:
        return json.load(f)

for label, path in [
    ("141249", r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\PerfLog\perflog-20260712-141249-seed1.json"),
    ("141203", r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\PerfLog\perflog-20260712-141203-seed1.json"),
]:
    evs = load(path)["events"]
    # Motion state at OpeningDeal beatClose BoardSnap idx ~345
    snap_close = next(e for e in evs if e.get("kind")=="BoardSnap" and e.get("beatId")==1 and e["payload"].get("phase")=="beatClose")
    # tween=1 cards at close
    cards = snap_close["payload"]["cards"].split(";")
    tween1 = [c for c in cards if "tween=1" in c]
    deckmode = [c for c in cards if "CardDeckMode" in c]
    ground = [c for c in cards if "GroundCardMode" in c]
    print("\n%s OpeningDeal close: total=%d tween1=%d deckMode=%d ground=%d" % (label, len(cards), len(tween1), len(deckmode), len(ground)))
    for c in tween1[:5]:
        print("  tween1:", c[:100])
    # RegistryAudit beat 1
    ra = [e for e in evs if e.get("kind")=="RegistryAudit" and e.get("beatId")==1]
    print("RegistryAudit beat1 count", len(ra))
    for e in ra[:3]:
        print(" ", e.get("payload"))
