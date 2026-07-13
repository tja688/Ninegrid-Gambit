import json
import os

base = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs"
sid = "20260712-151636"
perf = json.load(open(os.path.join(base, "PerfLog", f"perflog-{sid}-seed1.json"), encoding="utf-8-sig"))
evs = perf["events"]

t_open, t10 = 3545, 20540

print("=== BEATS in idle window ===")
for e in evs:
    if e["kind"] in ("BeatOpen", "BeatClose") and t_open <= e["tMs"] <= t10:
        p = e["payload"]
        print(f"tMs={e['tMs']:5d} {e['kind']:9s} beat={e['beatId']:2d} kind={p.get('beatKind')}")

print("\n=== VisChange active=0 in idle window ===")
for e in evs:
    if t_open <= e["tMs"] <= t10 and e["kind"] == "VisChange":
        p = e["payload"]
        if p.get("active") in (0, "0"):
            print(f"tMs={e['tMs']} uid={e.get('uid')} site={e.get('site')} active={p.get('active')}")

print("\n=== RegistryDelta remove in perf (idle) ===")
for e in evs:
    if t_open <= e["tMs"] <= t10 and e["kind"] == "RegistryDelta" and e.get("payload", {}).get("op") == "remove":
        p = e["payload"]
        print(f"tMs={e['tMs']} uid={e.get('uid')} reason={p.get('reason')} caller={p.get('caller')}")

print("\n=== BoardSnap at checkpoints (from perf) ===")
for e in evs:
    if e["kind"] == "Checkpoint":
        trig = e["payload"].get("trigger", "")
        if trig in ("Opening.Settled", "IdleWatch.2s", "IdleWatch.5s", "IdleWatch.10s"):
            # find adjacent boardsnap
            pass
for e in evs:
    if e["kind"] == "RegistryAudit" and e["payload"].get("trigger", "").startswith("IdleWatch"):
        p = e["payload"]
        print(f"tMs={e['tMs']} {p.get('trigger')} reg={p.get('registryCount')} field={p.get('fieldCount')} ghosts={p.get('ghosts')!r} orphans={p.get('orphans')!r}")

print("\n=== Anomaly in idle ===")
for e in evs:
    if t_open <= e["tMs"] <= t10 and e["kind"] == "Anomaly":
        print(json.dumps(e, ensure_ascii=False)[:250])

# Compare Opening.Settled vs IdleWatch.10s BoardSnap field slots
def get_snap(trigger_substr):
    for i, e in enumerate(evs):
        if e.get("kind") == "Checkpoint" and trigger_substr in e.get("payload", {}).get("trigger", ""):
            for j in range(i, min(i + 3, len(evs))):
                if evs[j]["kind"] == "BoardSnap":
                    return evs[j]["payload"].get("cards", ""), e["tMs"]
    return "", 0

open_cards, _ = get_snap("Opening.Settled")
idle10_cards, _ = get_snap("IdleWatch.10s")

import re
def field_detail(cards_str):
    rows = []
    for part in cards_str.split(";"):
        if not part.strip():
            continue
        m = re.match(r"^[~+]?(\d+)", part)
        uid = int(m.group(1)) if m else -1
        slot_m = re.search(r"slot=(-?\d+)", part)
        slot = int(slot_m.group(1)) if slot_m else -99
        active_m = re.search(r"active=(\d+)", part)
        active = int(active_m.group(1)) if active_m else -1
        if slot >= 0:
            rows.append((slot, uid, active))
    return sorted(rows)

print("\n=== FIELD SLOT COMPARISON Opening vs IdleWatch.10s ===")
o = field_detail(open_cards)
i10 = field_detail(idle10_cards)
print("Opening:", o)
print("Idle10:", i10)
slots_o = {s: (u, a) for s, u, a in o}
slots_i = {s: (u, a) for s, u, a in i10}
for s in range(9):
    if s in slots_o and s not in slots_i:
        print(f"  slot {s}: LOST uid={slots_o[s]}")
    elif s not in slots_o and s in slots_i:
        print(f"  slot {s}: GAINED uid={slots_i[s]}")
    elif s in slots_o and s in slots_i and slots_o[s] != slots_i[s]:
        print(f"  slot {s}: CHANGED {slots_o[s]} -> {slots_i[s]}")
