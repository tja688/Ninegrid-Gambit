import json

base = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs"
for sid in ["20260712-151718", "20260712-151636"]:
    perf = json.load(open(f"{base}/PerfLog/perflog-{sid}-seed1.json", encoding="utf-8-sig"))
    reg = json.load(open(f"{base}/OtherLog/RegistryLog/registrylog-{sid}-seed1.json", encoding="utf-8-sig"))
    pevs = perf["events"]
    revs = reg["events"]
    cps = [e for e in revs if e.get("kind") == "Checkpoint"]
    first_cycle = []
    for e in cps:
        first_cycle.append(e)
        if e["payload"].get("trigger") == "IdleWatch.10s":
            break
    t_open = next(e["tMs"] for e in first_cycle if e["payload"].get("trigger") == "Opening.Settled")
    t10 = next(e["tMs"] for e in first_cycle if e["payload"].get("trigger") == "IdleWatch.10s")
    print(f"=== {sid} Opening={t_open} Idle10={t10} ===")
    for e in pevs:
        if e["kind"] in ("BeatOpen", "BeatClose") and t_open <= e["tMs"] <= t10:
            print(
                f"  tMs={e['tMs']:5d} {e['kind']:9s} beat={e['beatId']:2d} "
                f"{e['payload'].get('beatKind')}"
            )
    print()
