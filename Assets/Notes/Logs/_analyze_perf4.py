import json

def load(path):
    with open(path, "r", encoding="utf-8-sig") as f:
        return json.load(f)

evs = load(r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\PerfLog\perflog-20260712-141249-seed1.json")["events"]

print("=== RegistryMiss ===")
for e in evs:
    if e.get("kind")=="RegistryMiss":
        print(e)

print("\n=== Vacate beatId<=8 ===")
for e in evs:
    if e.get("kind")=="Vacate" and e.get("beatId",99)<=8:
        print("idx=%s beat=%s uid=%s site=%s payload=%s" % (e["index"], e["beatId"], e.get("uid"), e.get("site"), e.get("payload")))

print("\n=== SyncBoard beat 8 window 580-610 ===")
for e in evs:
    if 580 <= e["index"] <= 610:
        k=e["kind"]
        if k in ("BeatOpen","BeatClose","BoardSnap","VisChange","SnapSet","Spawn","Despawn","MotionBegin","MotionEnd","RegistryAudit"):
            extra=""
            if k=="BoardSnap":
                p=e["payload"]
                n=len([x for x in p.get("cards","").split(";") if x.strip()])
                extra=" phase=%s n=%d full=%s" % (p.get("phase"), n, p.get("full"))
            print("idx=%s beat=%s kind=%s tMs=%s%s" % (e["index"], e["beatId"], k, e["tMs"], extra))

# Core beat 8 SyncBoard?
cevs = load(r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\CoreLog\corelog-20260712-141249-seed1.json")["events"]
print("\n=== Core beat 8 events ===")
for e in cevs:
    if e.get("beatId")==8:
        print(json.dumps(e, ensure_ascii=False))

# Count MotionEnd for ground uids at beat 1
ground_uids = [19,12,5,15,8,10,16,2]
print("\n=== MotionBegin/End for player field uids during beat 1 ===")
for uid in ground_uids:
    begins=[e["index"] for e in evs if e.get("kind")=="MotionBegin" and e.get("uid")==uid and e.get("beatId")==1]
    ends=[e["index"] for e in evs if e.get("kind")=="MotionEnd" and e.get("uid")==uid and e.get("beatId")==1]
    print("uid", uid, "begins", begins, "ends", ends)
