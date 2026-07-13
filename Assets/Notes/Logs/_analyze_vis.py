import json

evs = json.load(open(r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\PerfLog\perflog-20260712-141249-seed1.json", encoding="utf-8-sig"))["events"]

# All VisChange with empty active or renderOn 0 entire log
print("VisChange renderOn=0 or active=0 or alpha=0:")
for e in evs:
    if e.get("kind")!="VisChange":
        continue
    p=e.get("payload") or {}
    if p.get("renderOn") in ("0",0) or p.get("active") in ("0",0) or p.get("alpha") in ("0",0):
        print(e)

print("\nVisChange RemovedMode or Hidden:")
for e in evs:
    if e.get("kind")!="VisChange":
        continue
    p=e.get("payload") or {}
    if "Removed" in str(p.get("mode")) or "Hidden" in str(p.get("mode")):
        if e["index"] < 600:
            print("idx=%s beat=%s uid=%s site=%s %s" % (e["index"], e["beatId"], e.get("uid"), e.get("site"), p))

# SnapSet restoreHard between opening close and beat 8
print("\nSnapSet restoreHard idx 345-590:")
for e in evs:
    if 345 <= e["index"] <= 590 and e.get("kind")=="SnapSet":
        print("idx=%s uid=%s reason=%s" % (e["index"], e.get("uid"), (e.get("payload") or {}).get("reason")))
