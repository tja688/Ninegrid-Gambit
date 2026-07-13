import json

cevs = json.load(open(r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\CoreLog\corelog-20260712-141249-seed1.json", encoding="utf-8-sig"))["events"]

# Any event with presentationLocked, fieldBusy, drainInFlight before beat 5
for e in cevs:
    if e["index"] > 32 and e.get("beatId",0) < 5:
        print(json.dumps(e, ensure_ascii=False))

# refBattleOpIndex progression first 10
print("\n=== refBattleOpIndex markers ===")
seen=set()
for e in cevs:
    op=e.get("refBattleOpIndex",-1)
    if op not in seen and op>=0:
        seen.add(op)
        print("op=%s idx=%s beat=%s name=%s" % (op, e["index"], e["beatId"], e.get("name")))

# Summarize occupancy-related event counts
from collections import Counter
names=Counter(e.get("name") for e in cevs)
for n in ["OccupancySnapshot","HopPlan","SyncDiff","OccupancyConflict","RegistryAudit","OccupancyVacate"]:
    print(n, names.get(n,0))

has_diff = sum(1 for e in cevs if e.get("name")=="OccupancySnapshot" and (e.get("payload") or {}).get("hasDiff")=="true")
print("OccupancySnapshot hasDiff=true:", has_diff)

# opening batch only - any pres count drop?
for e in cevs:
    p=e.get("payload") or {}
    if p.get("batchTag")=="opening" and e.get("name")=="OccupancySnapshot":
        print("opening snap", e["index"], p.get("phase"), "core", p.get("coreOccupantCount"), "pres", p.get("presOccupantCount"), "hasDiff", p.get("hasDiff"))
