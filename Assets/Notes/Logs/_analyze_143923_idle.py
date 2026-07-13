import json

perf_path = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\PerfLog\perflog-20260712-143923-seed1.json"
core_path = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\CoreLog\corelog-20260712-143923-seed1.json"

perf = json.load(open(perf_path, encoding="utf-8-sig"))
core = json.load(open(core_path, encoding="utf-8-sig"))
evs = perf["events"]
cevs = core["events"]

oc = next(e["index"] for e in evs if e["kind"] == "BeatClose" and e.get("beatId") == 1)
fc = next(e["index"] for e in evs if e["kind"] == "BeatOpen" and e.get("beatId") == 2)

print("=== ALL events idx %s..%s (opening idle) ===" % (oc, fc))
for e in evs:
    if oc <= e["index"] <= fc:
        k = e["kind"]
        extra = ""
        if k == "RegistryDelta":
            p = e["payload"]
            extra = " op=%s reason=%s caller=%s" % (p.get("op"), p.get("reason"), p.get("caller"))
        elif k == "BoardSnap":
            p = e["payload"]
            cards = p.get("cards", "")
            n = len([x for x in cards.split(";") if x.strip()])
            extra = " phase=%s n=%d cards=%s" % (p.get("phase"), n, cards[:300])
        elif k == "RegistryAudit":
            p = e["payload"]
            extra = " trigger=%s reg=%s field=%s ghosts=%s orphans=%s" % (
                p.get("trigger"),
                p.get("registryCount"),
                p.get("fieldCount"),
                p.get("ghosts"),
                p.get("orphans"),
            )
        elif k in ("Despawn", "Spawn", "VisChange", "Vacate"):
            p = e.get("payload", {})
            extra = " " + str(p)
        print("idx=%s beat=%s tMs=%s kind=%s uid=%s%s" % (e["index"], e["beatId"], e["tMs"], k, e.get("uid"), extra))

print("\n=== Core events idx-aligned beat 1 tail ===")
for e in cevs:
    if e.get("beatId") == 1 and e["index"] >= 25:
        print(json.dumps(e, ensure_ascii=False))

# Check if any SyncBoard beat opens during idle (beatId still 1)
print("\n=== Sync-related perf in beat 1 tMs 3400-4200 ===")
for e in evs:
    if e.get("beatId") == 1 and 3400 <= e["tMs"] <= 4200:
        if e["kind"] in ("BeatOpen", "BeatClose", "RegistryDelta", "Despawn", "Spawn", "Vacate", "SnapSet", "VisChange", "BoardSnap", "RegistryAudit", "Anomaly"):
            print("idx=%s tMs=%s %s uid=%s payload=%s" % (e["index"], e["tMs"], e["kind"], e.get("uid"), e.get("payload")))
