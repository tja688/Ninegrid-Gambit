import json

def load_doc(path):
    with open(path, "r", encoding="utf-8-sig") as f:
        return json.load(f)

path = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\CoreLog\corelog-20260712-141249-seed1.json"
doc = load_doc(path)
evs = doc.get("events") or []
print("CoreLog total events:", len(evs))

kinds = {}
for e in evs:
    k = e.get("kind") or e.get("event") or "?"
    kinds[k] = kinds.get(k,0)+1
print("Kind counts:", dict(sorted(kinds.items(), key=lambda x:-x[1])))

# sample first 5 events keys
if evs:
    print("First event keys:", sorted(evs[0].keys()))
    print("First 3 events:")
    for e in evs[:3]:
        print(json.dumps(e, ensure_ascii=False)[:500])

keywords = ("OccupancySnapshot", "HopPlan", "SyncDiff", "OccupancyConflict", "hasDiff")
print("\n=== Events matching occupancy/sync keywords ===")
matched = []
for e in evs:
    s = json.dumps(e, ensure_ascii=False)
    if any(k in s for k in keywords):
        matched.append(e)
print("Count:", len(matched))
for e in matched[:40]:
    print(json.dumps(e, ensure_ascii=False)[:700])
if len(matched)>40:
    print("...", len(matched)-40, "more")

print("\n=== First 25 beats timeline ===")
for e in evs[:80]:
    k = e.get("kind")
    if k in ("BeatOpen","BeatClose") or "beat" in json.dumps(e).lower()[:200]:
        print(json.dumps(e, ensure_ascii=False)[:600])
