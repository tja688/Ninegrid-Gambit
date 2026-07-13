import json

def load_doc(path):
    with open(path, "r", encoding="utf-8-sig") as f:
        return json.load(f)

evs = load_doc(r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\CoreLog\corelog-20260712-141249-seed1.json")["events"]

# Find event structure - look for occupancy
for i, e in enumerate(evs[:50]):
    print(i, e.get("kind"), list((e.get("payload") or {}).keys())[:8])

# Search kinds containing Occupancy or Sync
interesting = []
for e in evs:
    k = e.get("kind","")
    p = e.get("payload") or {}
    if any(x in k for x in ("Occupancy","Hop","Sync","Conflict")):
        interesting.append(e)
    elif any(x in json.dumps(p) for x in ("OccupancySnapshot","HopPlan","SyncDiff","OccupancyConflict","hasDiff")):
        interesting.append(e)
print("\nInteresting count", len(interesting))

# First beats
print("\n=== Beat flow first 20 ===")
for e in evs:
    k = e.get("kind")
    if k not in ("BeatOpen","BeatClose","FlowBeat","Beat"):
        continue
    print(json.dumps(e, ensure_ascii=False))

# If no BeatOpen in core, look at structure
if not any(e.get("kind")=="BeatOpen" for e in evs):
    print("\nUnique kinds:", sorted(set(e.get("kind") for e in evs)))
    # first 15 events full
    for e in evs[:15]:
        print(json.dumps(e, ensure_ascii=False)[:900])
