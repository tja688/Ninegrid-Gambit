import json

perf_path = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\PerfLog\perflog-20260712-144238-seed1.json"
core_path = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\CoreLog\corelog-20260712-144238-seed1.json"

perf = json.load(open(perf_path, encoding="utf-8-sig"))
core = json.load(open(core_path, encoding="utf-8-sig"))
evs = perf["events"]
cevs = core["events"]

# Beat 1 OpeningDeal close index
opening_close_idx = None
first_combat_idx = None
for e in evs:
    if e["kind"] == "BeatClose" and e.get("beatId") == 1:
        opening_close_idx = e["index"]
    if first_combat_idx is None and e["kind"] == "BeatOpen" and e.get("beatId") == 2:
        first_combat_idx = e["index"]

print("OpeningDeal close idx:", opening_close_idx, "first CombatHit open idx:", first_combat_idx)

# All events between opening close and combat hit open
print("\n=== Events between OpeningDeal close and CombatHit beat 2 open ===")
window = [e for e in evs if opening_close_idx <= e["index"] <= first_combat_idx]
for e in window:
    k = e["kind"]
    if k in (
        "RegistryDelta",
        "Despawn",
        "Spawn",
        "VisChange",
        "ParentChange",
        "SnapSet",
        "BoardSnap",
        "Anomaly",
        "RegistryAudit",
        "Vacate",
        "RegistryMiss",
        "BeatOpen",
        "BeatClose",
    ):
        extra = ""
        if k == "RegistryDelta":
            p = e["payload"]
            extra = " op=%s reason=%s caller=%s count=%s->%s" % (
                p.get("op"),
                p.get("reason"),
                p.get("caller"),
                p.get("countBefore"),
                p.get("countAfter"),
            )
        elif k == "VisChange":
            p = e["payload"]
            extra = " active=%s mode=%s" % (p.get("active"), p.get("mode"))
        elif k == "BoardSnap":
            p = e["payload"]
            cards = p.get("cards", "")
            n = len([x for x in cards.split(";") if x.strip()])
            extra = " phase=%s full=%s n=%d" % (p.get("phase"), p.get("full"), n)
        elif k == "RegistryAudit":
            p = e["payload"]
            extra = " trigger=%s reg=%s field=%s ghosts=%s orphans=%s" % (
                p.get("trigger"),
                p.get("registryCount"),
                p.get("fieldCount"),
                p.get("ghosts"),
                p.get("orphans"),
            )
        elif k in ("Despawn", "Spawn"):
            p = e["payload"]
            extra = " reason=%s caller=%s" % (p.get("reason"), p.get("caller"))
        print("idx=%s beat=%s tMs=%s kind=%s uid=%s%s" % (e["index"], e["beatId"], e["tMs"], k, e.get("uid"), extra))

# Parse BoardSnap open vs close at beat 1
print("\n=== Beat 1 BoardSnap full comparison ===")
snaps = [e for e in evs if e["kind"] == "BoardSnap" and e.get("beatId") == 1]
for e in snaps:
    p = e["payload"]
    print("idx=%s phase=%s full=%s" % (e["index"], p.get("phase"), p.get("full")))
    print("cards:", p.get("cards", ""))

# Ground field uids at opening settled
def parse_cards(s):
    out = {}
    for part in s.split(";"):
        part = part.strip()
        if not part:
            continue
        if part.startswith("+"):
            part = part[1:]
        uid = int(part.split(",")[0].replace("~", "").split()[0])
        out[uid] = part
    return out

open_snap = next((e for e in snaps if e["payload"].get("phase") == "beatOpen"), None)
close_snap = next((e for e in snaps if e["payload"].get("phase") == "beatClose"), None)
if open_snap and close_snap:
    o = parse_cards(open_snap["payload"].get("cards", ""))
    c = parse_cards(close_snap["payload"].get("cards", ""))
    print("\nOpen uids:", sorted(o.keys()))
    print("Close uids:", sorted(c.keys()))
    print("Missing at close:", sorted(set(o.keys()) - set(c.keys())))
    print("New at close:", sorted(set(c.keys()) - set(o.keys())))

# Core beat 1 events
print("\n=== Core beat 1 events ===")
for e in cevs:
    if e.get("beatId") == 1:
        print(json.dumps(e, ensure_ascii=False))

# VisChange for ground cards during beat 1
ground_modes = set()
for e in evs:
    if e["kind"] == "VisChange" and e.get("beatId") == 1:
        p = e["payload"]
        if p.get("mode") == "GroundCardMode" or "Ground" in str(p.get("mode", "")):
            print("VisChange idx=%s uid=%s active=%s mode=%s site=%s" % (e["index"], e["uid"], p.get("active"), p.get("mode"), e.get("site")))

# Any remove/despawn with beatId==1
print("\n=== beatId=1 removes/despawns ===")
for e in evs:
    if e.get("beatId") == 1 and e["kind"] in ("RegistryDelta", "Despawn"):
        print(json.dumps(e, ensure_ascii=False))
