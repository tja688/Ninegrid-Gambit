import json
import os
import re

base = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs"
sid = "20260712-151636"
reg = json.load(open(os.path.join(base, "OtherLog", "RegistryLog", f"registrylog-{sid}-seed1.json"), encoding="utf-8-sig"))
core = json.load(open(os.path.join(base, "CoreLog", f"corelog-{sid}-seed1.json"), encoding="utf-8-sig"))
evs = reg["events"]
cevs = core["events"]


def field_uids_from_snap(cards_str):
    uids = []
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
            uids.append((uid, slot, active))
    return uids


print("=== 151636 CHECKPOINT + FIELDS ===")
for i, e in enumerate(evs):
    if e.get("kind") != "Checkpoint":
        continue
    trig = e["payload"].get("trigger")
    if "Idle" not in trig and "Opening" not in trig and "Interaction" not in trig:
        continue
    cards_str = ""
    for j in range(max(0, i - 2), min(len(evs), i + 3)):
        if evs[j].get("kind") == "BoardSnap":
            cards_str = evs[j]["payload"].get("cards", "")
    fu = field_uids_from_snap(cards_str)
    p = e["payload"]
    print(
        f"{trig:25s} tMs={e['tMs']:5d} reg={p.get('registryCount')} "
        f"field={p.get('fieldCount')} snapField={len(fu)} uids={[u[0] for u in fu]}"
    )

t_open = 3545
t10 = 20540
print(f"\n=== REMOVES {t_open}-{t10} ===")
for e in evs:
    if e.get("kind") != "RegistryDelta" or e.get("payload", {}).get("op") != "remove":
        continue
    if not (t_open <= e["tMs"] <= t10):
        continue
    p = e["payload"]
    print(
        f"tMs={e['tMs']:5d} beat={e['beatId']:2d} uid={e.get('uid'):2d} "
        f"reason={p.get('reason'):30s} defId={p.get('defId')}"
    )

print("\n=== CORE: all events with tMs 3500-21000 ===")
for e in cevs:
    t = e.get("tMs")
    if t is None:
        continue
    try:
        t = int(t)
    except (TypeError, ValueError):
        continue
    if 3500 <= t <= 21000:
        print(
            f"tMs={t:5d} beat={e.get('beatId')} name={e.get('name')} "
            f"cat={e.get('category')} accepted={e.get('accepted')}"
        )

print("\n=== CORE: OccupancySnapshot / SyncDiff / Vacate (any tMs) ===")
for e in cevs:
    nm = e.get("name", "")
    if any(k in nm for k in ("Occupancy", "SyncDiff", "Vacate", "RegistryAudit")):
        p = e.get("payload") or {}
        print(json.dumps({k: e.get(k) for k in ("index", "beatId", "tMs", "name", "accepted")}, ensure_ascii=False))
        if p:
            keys = ["trigger", "registryCount", "fieldCount", "ghosts", "orphans", "hasDiff", "slots"]
            print("  payload:", {k: p.get(k) for k in keys if k in p or p.get(k) is not None})

print("\n=== CORE: first 50 events ===")
for e in cevs[:50]:
    print(f"idx={e.get('index')} beat={e.get('beatId')} name={e.get('name')} cat={e.get('category')}")

print("\n=== REGISTRY AUDIT MISMATCH triggers ===")
for e in evs:
    if e.get("kind") == "Checkpoint" and "AuditMismatch" in e.get("payload", {}).get("trigger", ""):
        print(e)

print("\n=== VisChange / active=0 in registry? (should be in perf) ===")
# check if any Despawn without prior field remove
despawns = [e for e in evs if e.get("kind") == "Despawn" and t_open <= e["tMs"] <= t10]
print(f"despawns in idle window: {len(despawns)}")
