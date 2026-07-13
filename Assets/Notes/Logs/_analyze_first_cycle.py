import json
import os
import glob
import re

base = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs"
reg_path = os.path.join(base, "OtherLog", "RegistryLog", "registrylog-20260712-151718-seed1.json")
core_path = os.path.join(base, "CoreLog", "corelog-20260712-151718-seed1.json")

reg = json.load(open(reg_path, encoding="utf-8-sig"))
core = json.load(open(core_path, encoding="utf-8-sig"))
evs = reg["events"]
cevs = core["events"]


def parse_cards(cards_str):
    entries = []
    for part in cards_str.split(";"):
        if not part.strip():
            continue
        entries.append(part.strip())
    return entries


def card_summary(entries):
    field = []
    other = []
    for e in entries:
        m_uid = re.match(r"^[~+]?(\d+)", e)
        uid = int(m_uid.group(1)) if m_uid else -1
        slot_m = re.search(r"slot=(-?\d+)", e)
        slot = int(slot_m.group(1)) if slot_m else -99
        active_m = re.search(r"active=(\d+)", e)
        active = int(active_m.group(1)) if active_m else -1
        mode_m = re.search(r"mode=([^,;]+)", e)
        mode = mode_m.group(1) if mode_m else "?"
        item = {"uid": uid, "slot": slot, "active": active, "mode": mode, "raw": e[:120]}
        if slot >= 0:
            field.append(item)
        else:
            other.append(item)
    return field, other


# First opening cycle checkpoints only (before second Opening.Settled)
cps = [e for e in evs if e.get("kind") == "Checkpoint"]
first_cycle_cps = []
for e in cps:
    first_cycle_cps.append(e)
    if e["payload"].get("trigger") == "IdleWatch.10s":
        break

print("=== FIRST OPENING CYCLE CHECKPOINTS ===")
for e in first_cycle_cps:
    p = e["payload"]
    print(f"  {p.get('trigger'):25s} tMs={e['tMs']} reg={p.get('registryCount')} field={p.get('fieldCount')}")

# BoardSnap diff across first cycle
print("\n=== BOARDSNAP DIFF (first cycle) ===")
prev_field = None
prev_all_uids = None
for i, e in enumerate(evs):
    if e.get("kind") != "Checkpoint":
        continue
    trig = e["payload"].get("trigger")
    if trig not in [c["payload"].get("trigger") for c in first_cycle_cps]:
        continue
    cards_str = ""
    for j in range(max(0, i - 2), min(len(evs), i + 3)):
        if evs[j].get("kind") == "BoardSnap":
            cards_str = evs[j]["payload"].get("cards", "")
    entries = parse_cards(cards_str)
    field, other = card_summary(entries)
    all_uids = {x["uid"] for x in field + other}
    field_uids = {x["uid"] for x in field}
    inactive_field = [x for x in field if x["active"] == 0]
    print(f"\n--- {trig} tMs={e['tMs']} total={len(entries)} field={len(field)} other={len(other)} ---")
    print(f"  field uids: {sorted(field_uids)}")
    if inactive_field:
        print(f"  INACTIVE on field: {inactive_field}")
    if prev_field is not None:
        lost_field = prev_field - field_uids
        gained_field = field_uids - prev_field
        lost_all = prev_all_uids - all_uids
        if lost_field or gained_field or lost_all:
            print(f"  DELTA field lost={sorted(lost_field)} gained={sorted(gained_field)}")
            print(f"  DELTA all registry lost={sorted(lost_all)}")
    prev_field = field_uids
    prev_all_uids = all_uids

# Removes in first idle window: after IdleWatch.5s, before IdleWatch.10s
t5 = next(e["tMs"] for e in first_cycle_cps if e["payload"].get("trigger") == "IdleWatch.5s")
t10 = next(e["tMs"] for e in first_cycle_cps if e["payload"].get("trigger") == "IdleWatch.10s")
t_open = next(e["tMs"] for e in first_cycle_cps if e["payload"].get("trigger") == "Opening.Settled")

print(f"\n=== REMOVES tMs {t5} -> {t10} (5s to 10s idle) ===")
removes = [
    e
    for e in evs
    if e.get("kind") == "RegistryDelta"
    and e.get("payload", {}).get("op") == "remove"
    and t5 <= e["tMs"] <= t10
]
print(f"count: {len(removes)}")
for e in removes:
    p = e["payload"]
    print(
        f"  tMs={e['tMs']} uid={e.get('uid')} reason={p.get('reason')} "
        f"caller={p.get('caller')} count={p.get('countBefore')}->{p.get('countAfter')} defId={p.get('defId')}"
    )

print(f"\n=== REMOVES tMs {t_open} -> {t5} (Opening to 5s idle) ===")
removes2 = [
    e
    for e in evs
    if e.get("kind") == "RegistryDelta"
    and e.get("payload", {}).get("op") == "remove"
    and t_open <= e["tMs"] <= t5
]
print(f"count: {len(removes2)}")
for e in removes2:
    p = e["payload"]
    print(
        f"  tMs={e['tMs']} uid={e.get('uid')} reason={p.get('reason')} "
        f"caller={p.get('caller')} count={p.get('countBefore')}->{p.get('countAfter')} defId={p.get('defId')}"
    )

# First interaction in core
print("\n=== CORE FIRST INTERACTION EVENTS ===")
for e in cevs[:80]:
    nm = e.get("name", "")
    cat = e.get("category", "")
    if any(k in str(nm) + str(cat) for k in ("CombatHit", "SyncBoard", "CardSelect", "HelpCard", "PlayCard", "Interaction")):
        print(f"  idx={e.get('index')} beat={e.get('beatId')} name={nm} cat={cat}")

# Core events in first idle window
print(f"\n=== CORE EVENTS tMs {t_open}-{t10} ===")
for e in cevs:
    t = e.get("tMs", e.get("payload", {}).get("tMs", -1))
    if isinstance(t, str):
        try:
            t = int(t)
        except ValueError:
            t = -1
    if t_open <= t <= t10:
        nm = e.get("name", "")
        if any(
            k in nm
            for k in ("Occupancy", "Sync", "Vacate", "RegistryAudit", "Combat", "Remove", "Hop", "Drain")
        ):
            slim = {k: e.get(k) for k in ("index", "beatId", "name", "accepted")}
            p = e.get("payload", {})
            if p:
                slim["payload"] = {k: p.get(k) for k in list(p.keys())[:8]}
            print(json.dumps(slim, ensure_ascii=False)[:350])

# Session 151636 quick check
reg2_path = os.path.join(base, "OtherLog", "RegistryLog", "registrylog-20260712-151636-seed1.json")
if os.path.exists(reg2_path):
    reg2 = json.load(open(reg2_path, encoding="utf-8-sig"))
    evs2 = reg2["events"]
    print("\n\n=== SESSION 151636 (shorter?) ===")
    cps2 = [e for e in evs2 if e.get("kind") == "Checkpoint"]
    for e in cps2:
        p = e["payload"]
        print(
            f"  {p.get('trigger'):25s} tMs={e['tMs']} reg={p.get('registryCount')} "
            f"field={p.get('fieldCount')} ghosts={p.get('ghosts')!r}"
        )
    removes2_all = [
        e
        for e in evs2
        if e.get("kind") == "RegistryDelta" and e.get("payload", {}).get("op") == "remove"
    ]
    print(f"  total removes: {len(removes2_all)}")
    if cps2:
        t10_636 = next(
            (e["tMs"] for e in cps2 if e["payload"].get("trigger") == "IdleWatch.10s"),
            None,
        )
        t_open_636 = next(
            (e["tMs"] for e in cps2 if e["payload"].get("trigger") == "Opening.Settled"),
            None,
        )
        if t_open_636 and t10_636:
            pre = [
                e
                for e in removes2_all
                if t_open_636 <= e["tMs"] <= t10_636
            ]
            print(f"  removes Opening->IdleWatch.10s: {len(pre)}")
            for e in pre:
                p = e["payload"]
                print(
                    f"    tMs={e['tMs']} uid={e.get('uid')} reason={p.get('reason')} "
                    f"caller={p.get('caller')}"
                )
