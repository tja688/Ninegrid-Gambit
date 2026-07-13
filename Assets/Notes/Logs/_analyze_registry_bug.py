import json
import sys

perf_path = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\PerfLog\perflog-20260712-144238-seed1.json"
core_path = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\CoreLog\corelog-20260712-144238-seed1.json"

perf = json.load(open(perf_path, encoding="utf-8-sig"))
core = json.load(open(core_path, encoding="utf-8-sig"))
evs = perf["events"]
cevs = core["events"]

print("sessionId:", perf.get("sessionId"), "seed:", perf.get("seed"))
print("perf events:", len(evs), "core events:", len(cevs))

print("\n=== UserMark ===")
um = [e for e in evs if e["kind"] == "UserMark"]
print("count:", len(um))
for e in um:
    print(json.dumps(e, ensure_ascii=False))

print("\n=== RegistryDelta remove (all) ===")
removes = [e for e in evs if e["kind"] == "RegistryDelta" and e.get("payload", {}).get("op") == "remove"]
print("count:", len(removes))
for e in removes:
    p = e["payload"]
    print(
        "idx=%s beat=%s tMs=%s uid=%s reason=%s caller=%s count=%s->%s defId=%s"
        % (
            e["index"],
            e["beatId"],
            e["tMs"],
            e["uid"],
            p.get("reason"),
            p.get("caller"),
            p.get("countBefore"),
            p.get("countAfter"),
            p.get("defId"),
        )
    )

print("\n=== Despawn (all) ===")
despawns = [e for e in evs if e["kind"] == "Despawn"]
print("count:", len(despawns))
for e in despawns:
    p = e["payload"]
    print(
        "idx=%s beat=%s tMs=%s uid=%s reason=%s caller=%s site=%s"
        % (e["index"], e["beatId"], e["tMs"], e["uid"], p.get("reason"), p.get("caller"), e.get("site"))
    )

print("\n=== RegistryAudit ===")
for e in evs:
    if e["kind"] == "RegistryAudit":
        p = e["payload"]
        print(
            "idx=%s beat=%s tMs=%s trigger=%s reg=%s field=%s ghosts=%s orphans=%s"
            % (
                e["index"],
                e["beatId"],
                e["tMs"],
                p.get("trigger"),
                p.get("registryCount"),
                p.get("fieldCount"),
                p.get("ghosts"),
                p.get("orphans"),
            )
        )

# OpeningDeal beat close -> first CombatHit/SyncBoard
print("\n=== Beat timeline (OpeningDeal -> first combat/sync) ===")
beat_kinds = {}
for e in evs:
    if e["kind"] in ("BeatOpen", "BeatClose"):
        p = e["payload"]
        bk = p.get("beatKind", "?")
        phase = e["kind"]
        bid = e["beatId"]
        if bid not in beat_kinds:
            beat_kinds[bid] = {}
        beat_kinds[bid][phase] = bk

for bid in sorted(beat_kinds.keys())[:15]:
    print("beatId=%s open=%s close=%s" % (bid, beat_kinds[bid].get("BeatOpen"), beat_kinds[bid].get("BeatClose")))

# Find OpeningDeal close beat id
opening_close_beat = None
first_combat_beat = None
for e in evs:
    if e["kind"] == "BeatClose" and e.get("payload", {}).get("beatKind") == "OpeningDeal":
        opening_close_beat = e["beatId"]
    if first_combat_beat is None and e["kind"] == "BeatOpen" and e.get("payload", {}).get("beatKind") in ("CombatHit", "SyncBoard"):
        first_combat_beat = e["beatId"]

print("\nopening_close_beat:", opening_close_beat, "first_combat/sync_beat:", first_combat_beat)

if opening_close_beat is not None:
    end_beat = first_combat_beat if first_combat_beat else opening_close_beat + 5
    print("\n=== Removes between OpeningDeal close (beat %s) and first combat/sync (beat %s) ===" % (opening_close_beat, end_beat))
    idle_removes = [e for e in removes if opening_close_beat <= e["beatId"] < (end_beat or 999)]
    idle_despawns = [e for e in despawns if opening_close_beat <= e["beatId"] < (end_beat or 999)]
    print("RegistryDelta remove in window:", len(idle_removes))
    for e in idle_removes:
        p = e["payload"]
        print("  idx=%s beat=%s tMs=%s uid=%s reason=%s caller=%s" % (e["index"], e["beatId"], e["tMs"], e["uid"], p.get("reason"), p.get("caller")))
    print("Despawn in window:", len(idle_despawns))
    for e in idle_despawns:
        p = e["payload"]
        print("  idx=%s beat=%s tMs=%s uid=%s reason=%s caller=%s" % (e["index"], e["beatId"], e["tMs"], e["uid"], p.get("reason"), p.get("caller")))

# Anomalies
print("\n=== Anomaly ===")
for e in evs:
    if e["kind"] == "Anomaly":
        print(json.dumps(e, ensure_ascii=False))

# BoardSnap at OpeningDeal close vs UserMark (if any)
print("\n=== OpeningDeal BoardSnap close ===")
for e in evs:
    if e["kind"] == "BoardSnap" and e.get("beatId") == opening_close_beat:
        p = e["payload"]
        cards = p.get("cards", "")
        n = len([x for x in cards.split(";") if x.strip()])
        print("idx=%s phase=%s full=%s n_cards=%s" % (e["index"], p.get("phase"), p.get("full"), n))
        if p.get("phase") == "close" or p.get("full") == "0":
            print("cards:", cards[:500], "..." if len(cards) > 500 else "")

if um:
    um_idx = um[-1]["index"]
    um_beat = um[-1]["beatId"]
    print("\n=== BoardSnap near UserMark (idx %s beat %s) ===" % (um_idx, um_beat))
    for e in evs:
        if e["kind"] == "BoardSnap" and abs(e["index"] - um_idx) <= 5:
            p = e["payload"]
            cards = p.get("cards", "")
            n = len([x for x in cards.split(";") if x.strip()])
            print("idx=%s beat=%s phase=%s full=%s n=%s" % (e["index"], e["beatId"], p.get("phase"), p.get("full"), n))
            print("cards:", cards[:800])

# Core events in idle window
if opening_close_beat is not None:
    print("\n=== Core events beat %s to %s (Occupancy/Sync/Vacate) ===" % (opening_close_beat, end_beat))
    for e in cevs:
        if opening_close_beat <= e.get("beatId", -1) < (end_beat or 999):
            name = e.get("name", e.get("category", "?"))
            if any(k in str(name) for k in ("Occupancy", "Sync", "Vacate", "Hop", "Drain", "Registry")):
                print(json.dumps(e, ensure_ascii=False))
