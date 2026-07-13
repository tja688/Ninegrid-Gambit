import json, re

path = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\PerfLog\perflog-20260712-141249-seed1.json"
with open(path, encoding="utf-8-sig") as f:
    evs = json.load(f)["events"]

def parse_card(seg):
    seg = seg.strip()
    if not seg:
        return None
    m = re.match(r"~?(\d+)", seg)
    uid = int(m.group(1)) if m else None
    slot_m = re.search(r"slot=([^,;\s]+)", seg)
    slot_raw = slot_m.group(1) if slot_m else ""
    # slot may be -1->2 or -1→2 or just 2
    slot = slot_raw
    if "->" in slot_raw or "\u2192" in slot_raw or "\uff1e" in slot_raw:
        parts = re.split(r"->|\u2192|\uff1e", slot_raw)
        slot = parts[-1].strip()
    xy_m = re.search(r"xy=([^,;]+)", seg)
    xy_raw = xy_m.group(1) if xy_m else ""
    xy = xy_raw
    if "->" in xy_raw or "\u2192" in xy_raw:
        parts = re.split(r"->|\u2192", xy_raw)
        xy = parts[-1].strip()
    elif "," in xy_raw and xy_raw.count(",") >= 3:
        # might be from->to as x1,y1->x2,y2 embedded
        pass
    # try xy=-1.46,-2.28->0,2.5 or arrow between pairs
    xy2 = re.search(r"xy=([-\d.]+),([-\d.]+)(?:->|\u2192)([-\d.]+),([-\d.]+)", seg)
    if xy2:
        xy = "%s,%s" % (xy2.group(3), xy2.group(4))
    elif re.search(r"xy=([-\d.]+),([-\d.]+)", seg):
        m2 = re.search(r"xy=([-\d.]+),([-\d.]+)", seg)
        xy = "%s,%s" % (m2.group(1), m2.group(2))
    active_m = re.search(r"active=(\d+)", seg)
    tween_m = re.search(r"tween=(\d+)", seg)
    mode_m = re.search(r"mode=([^,;\s]+)", seg)
    return {
        "uid": uid,
        "slot": slot,
        "slot_raw": slot_raw,
        "xy": xy,
        "active": int(active_m.group(1)) if active_m else None,
        "tween": int(tween_m.group(1)) if tween_m else None,
        "mode": mode_m.group(1) if mode_m else None,
    }

# 1) OpeningDeal beatClose
snap = next(e for e in evs if e.get("kind")=="BoardSnap" and e.get("beatId")==1 and e["payload"].get("phase")=="beatClose")
cards_str = snap["payload"]["cards"]
segments = [s for s in cards_str.split(";") if s.strip()]
field_cards = []
for seg in segments:
    c = parse_card(seg)
    if not c:
        continue
    if c["mode"] != "GroundCardMode":
        continue
    try:
        slot_n = int(c["slot"])
    except:
        continue
    if slot_n <= 0:
        continue
    field_cards.append(c)

print("OPENING_CLOSE idx=%s tMs=%s" % (snap["index"], snap["tMs"]))
print("FIELD_GROUND slot>0 count=%d" % len(field_cards))
for c in sorted(field_cards, key=lambda x: int(x["slot"])):
    print("uid=%s slot=%s xy=%s active=%s tween=%s" % (c["uid"], c["slot"], c["xy"], c["active"], c["tween"]))

# anchor positions by slot (from first full ground snap - use close positions as anchor baseline)
anchors = {int(c["slot"]): c["xy"] for c in field_cards}

# 2) SnapSet HardSnap/SoftSnap 3439-15000
snaps = []
for e in evs:
    if e.get("kind") != "SnapSet":
        continue
    site = e.get("site") or ""
    if "FinalStateGuard.HardSnap" not in site and "FinalStateGuard.SoftSnap" not in site:
        continue
    t = e.get("tMs", 0)
    if t < 3439 or t > 15000:
        continue
    p = e.get("payload") or {}
    snaps.append({
        "uid": e.get("uid"),
        "slot": p.get("slot"),
        "x": p.get("x"),
        "y": p.get("y"),
        "xy": "%s,%s" % (p.get("x"), p.get("y")),
        "reason": p.get("reason"),
        "beatId": e.get("beatId"),
        "tMs": t,
        "site": site.split(".")[-1] if "." in site else site,
        "idx": e.get("index"),
    })

print("\nSNAPSET 3439-15000 count=%d" % len(snaps))
for s in snaps:
    print("tMs=%s beat=%s uid=%s slot=%s xy=%s reason=%s site=%s idx=%s" % (
        s["tMs"], s["beatId"], s["uid"], s["slot"], s["xy"], s["reason"], s["site"], s["idx"]))

first_hard = next((s for s in snaps if "Hard" in s["site"] or s["reason"]=="restoreHard"), None)
print("\nFIRST_HARDSNAP:", first_hard)

# 3) CombatHit beat 2 or 3 - combatants
# Look for BattleBind, CombatHitFrame, or payload with attacker/target
for bid in (2, 3):
    hits = [e for e in evs if e.get("beatId")==bid and e.get("kind")=="CombatHitFrame"]
    binds = [e for e in evs if e.get("beatId")==bid and e.get("kind")=="BattleBind"]
    print("\nCOMBATHIT beatId=%s CombatHitFrame=%d BattleBind=%d" % (bid, len(hits), len(binds)))
    for e in hits[:5]:
        print("  frame", e.get("payload"), "uid", e.get("uid"))
    for e in binds:
        print("  bind", json.dumps(e, ensure_ascii=False))
    # MotionPlan with combat?
    for e in evs:
        if e.get("beatId")!=bid:
            continue
        if e.get("kind") in ("CombatHitFrame", "BattleBind", "BeatOpen"):
            if e.get("kind")=="BeatOpen":
                print("  BeatOpen", e.get("payload"))

# Search all events beat 2 for uid roles
uids_combat = set()
for e in evs:
    if e.get("beatId") not in (2, 3):
        continue
    p = e.get("payload") or {}
    for k in ("attackerUid", "targetUid", "sourceUid", "defenderUid"):
        if k in p:
            uids_combat.add((k, p[k]))
    if e.get("kind")=="CombatHitFrame":
        uids_combat.add(("frame_uid", str(e.get("uid"))))
        if "targetUid" in p or "attackerUid" in p:
            print("hitframe beat%d idx=%s %s" % (e["beatId"], e["index"], p))

# read CombatHitFrame payloads for beat 2
print("\nAll CombatHitFrame beat 2-3:")
for e in evs:
    if e.get("kind")=="CombatHitFrame" and e.get("beatId") in (2,3):
        print("beat=%s idx=%s tMs=%s uid=%s payload=%s" % (e["beatId"], e["index"], e["tMs"], e.get("uid"), e.get("payload")))

# 4) Mismatch at first HardSnap
if first_hard and anchors:
    print("\nMISMATCH vs opening close xy (anchor=close snap end position):")
    fh_uid = int(first_hard["uid"])
    fh_slot = first_hard["slot"]
    fh_xy = first_hard["xy"]
    # compare all field uids that get first hard snap batch (same tMs)
    t0 = first_hard["tMs"]
    batch = [s for s in snaps if s["tMs"]==t0 and ("Hard" in s["site"] or s["reason"]=="restoreHard")]
    opening_by_uid = {c["uid"]: c for c in field_cards}
    for s in batch:
        uid = int(s["uid"])
        oc = opening_by_uid.get(uid)
        anchor_xy = oc["xy"] if oc else anchors.get(int(s["slot"]), "?")
        match = (s["xy"] == anchor_xy) if oc else "n/a"
        print("uid=%s slot_open=%s xy_open=%s snap_slot=%s xy_snap=%s match=%s" % (
            uid, oc["slot"] if oc else "-", anchor_xy, s["slot"], s["xy"], match))
