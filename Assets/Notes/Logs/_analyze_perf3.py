import json, re

def load_doc(path):
    with open(path, "r", encoding="utf-8-sig") as f:
        return json.load(f)

path = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\PerfLog\perflog-20260712-141249-seed1.json"
evs = load_doc(path)["events"]

# BoardSnap beatClose with nCards=0 or full=0
print("=== BoardSnap beatClose with empty cards ===")
for e in evs:
    if e.get("kind") != "BoardSnap":
        continue
    p = e["payload"]
    if p.get("phase") != "beatClose":
        continue
    cards = p.get("cards") or ""
    if not cards or cards.strip()=="":
        print("idx=%s beatId=%s tMs=%s full=%s" % (e["index"], e["beatId"], e["tMs"], p.get("full")))

# Count how many beatClose snaps are empty vs full
empty = full = 0
for e in evs:
    if e.get("kind")=="BoardSnap" and e["payload"].get("phase")=="beatClose":
        if e["payload"].get("cards"):
            full += 1
        else:
            empty += 1
print("\nbeatClose BoardSnap: empty=%d full=%d" % (empty, full))

# First SyncBoard (user interaction?)
print("\n=== SyncBoard beats ===")
for e in evs:
    if e.get("kind")=="BeatOpen" and (e.get("payload") or {}).get("beatKind")=="SyncBoard":
        idx = e["index"]
        # find matching close snap
        close_snap = next((x for x in evs if x["index"]>idx and x.get("kind")=="BoardSnap" and x["beatId"]==e["beatId"] and x["payload"].get("phase")=="beatClose"), None)
        open_snap = next((x for x in evs if x["index"]>=idx and x.get("kind")=="BoardSnap" and x["beatId"]==e["beatId"] and x["payload"].get("phase")=="beatOpen"), None)
        def n(s):
            if not s: return 0
            return len([p for p in s["payload"].get("cards","").split(";") if p.strip()])
        print("Open idx=%s beatId=%s tMs=%s openCards=%s closeCards=%s" % (idx, e["beatId"], e["tMs"], n(open_snap), n(close_snap)))

# Gap between opening deal close (355) and beat 2 open (366) - any motion still open?
print("\n=== MotionBegin without MotionEnd for deal uids around tMs 3439-4163 ===")
open_motions = {}
for e in evs:
    if e["index"] < 355:
        continue
    if e["index"] > 366:
        break
    if e.get("kind")=="MotionEnd":
        open_motions.pop(e.get("uid"), None)
# actually track from beat 1
open_motions = {}
for e in evs:
    if e["index"] > 366:
        break
    if e.get("kind")=="MotionBegin":
        open_motions[e.get("uid")] = e["index"]
    elif e.get("kind")=="MotionEnd" and e.get("uid") in open_motions:
        del open_motions[e["uid"]]
print("Still open at idx 366:", open_motions)

# Check renderOn / alpha VisChanges after deal
print("\n=== VisChange renderOn/alpha/mode suspicious after idx 345 ===")
for e in evs:
    if e["index"] < 345 or e["index"] > 450:
        continue
    if e.get("kind") != "VisChange":
        continue
    p = e.get("payload") or {}
    if p.get("renderOn")=="0" or p.get("alpha")=="0" or p.get("mode") in ("RemovedMode","HiddenMode") or p.get("active") in ("0",0):
        print("idx=%s uid=%s site=%s payload=%s" % (e["index"], e.get("uid"), e.get("site"), p))
