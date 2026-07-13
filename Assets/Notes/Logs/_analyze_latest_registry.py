import json
import os
import glob

base = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs"
reg_dir = os.path.join(base, "OtherLog", "RegistryLog")
core_dir = os.path.join(base, "CoreLog")

reg_files = sorted(
    glob.glob(os.path.join(reg_dir, "registrylog-*.json")),
    key=os.path.getmtime,
    reverse=True,
)
print("Latest registry logs:")
for f in reg_files[:5]:
    print(" ", os.path.basename(f), os.path.getsize(f))


def parse_field_uids(cards_str):
    uids = set()
    for part in cards_str.split(";"):
        if not part.strip():
            continue
        head = part.split(",")[0]
        if head.startswith("~") or head.startswith("+"):
            try:
                uids.add(int(head.lstrip("~+")))
            except ValueError:
                pass
    return uids


for reg_path in reg_files[:2]:
    sid = os.path.basename(reg_path).replace("registrylog-", "").replace("-seed1.json", "")
    core_path = os.path.join(core_dir, f"corelog-{sid}-seed1.json")
    print("\n" + "=" * 80)
    print("SESSION", sid)
    print("registry:", reg_path)
    print("core exists:", os.path.exists(core_path))

    reg = json.load(open(reg_path, encoding="utf-8-sig"))
    evs = reg.get("events", [])
    print("events:", len(evs), "sessionId:", reg.get("sessionId"), "seed:", reg.get("seed"))

    cps = [e for e in evs if e.get("kind") == "Checkpoint"]
    audits = [e for e in evs if e.get("kind") == "RegistryAudit"]
    removes = [
        e
        for e in evs
        if e.get("kind") == "RegistryDelta" and e.get("payload", {}).get("op") == "remove"
    ]
    despawns = [e for e in evs if e.get("kind") == "Despawn"]
    anomalies = [e for e in evs if e.get("kind") == "Anomaly"]

    print("\n--- Checkpoints ---")
    prev_rc = None
    for e in cps:
        p = e["payload"]
        trig = p.get("trigger", "")
        rc = int(p.get("registryCount") or 0)
        fc = int(p.get("fieldCount") or 0)
        ghosts = p.get("ghosts", "")
        orphans = p.get("orphans", "")
        delta = "" if prev_rc is None else f" ({rc - prev_rc:+d})"
        print(
            f"  tMs={e['tMs']:6d} beat={e['beatId']:2d} trigger={trig:25s} "
            f"reg={rc}{delta} field={fc} ghosts={ghosts!r} orphans={orphans!r}"
        )
        prev_rc = rc

    print("\n--- BoardSnap at Checkpoint triggers ---")
    prev_field_uids = None
    for i, e in enumerate(evs):
        if e.get("kind") != "Checkpoint":
            continue
        trig = e["payload"].get("trigger")
        cards_str = ""
        for j in range(max(0, i - 2), min(len(evs), i + 3)):
            if evs[j].get("kind") == "BoardSnap":
                cards_str = evs[j]["payload"].get("cards", "")
        n = len([x for x in cards_str.split(";") if x.strip()])
        field_uids = parse_field_uids(cards_str)
        missing = ""
        if prev_field_uids is not None:
            lost = prev_field_uids - field_uids
            gained = field_uids - prev_field_uids
            if lost or gained:
                missing = f" lost={sorted(lost)} gained={sorted(gained)}"
        print(
            f"  {trig:25s} tMs={e['tMs']:6d} boardSnapCards={n} "
            f"fieldUids={sorted(field_uids)}{missing}"
        )
        prev_field_uids = field_uids

    print(f"\n--- RegistryDelta remove: {len(removes)} ---")
    for e in removes:
        p = e["payload"]
        print(
            f"  idx={e['index']} tMs={e['tMs']} beat={e['beatId']} uid={e.get('uid')} "
            f"reason={p.get('reason')} caller={p.get('caller')} "
            f"count={p.get('countBefore')}->{p.get('countAfter')}"
        )

    print("\n--- IdleWatch / Opening audits ---")
    for e in audits:
        p = e["payload"]
        trig = p.get("trigger", "")
        if any(x in trig for x in ("Opening", "Idle", "Interaction")):
            print(
                f"  tMs={e['tMs']:6d} trigger={trig} reg={p.get('registryCount')} "
                f"field={p.get('fieldCount')} ghosts={p.get('ghosts')!r} "
                f"orphans={p.get('orphans')!r}"
            )

    print(f"\n--- Anomaly count: {len(anomalies)} ---")
    for e in anomalies[:20]:
        p = e["payload"]
        print(
            f"  tMs={e['tMs']} uid={e.get('uid')} code={p.get('code')} "
            f"detail={p.get('detail')}"
        )

    if not os.path.exists(core_path):
        continue

    core = json.load(open(core_path, encoding="utf-8-sig"))
    cevs = core.get("events", [])

    def is_combat_or_sync(ev):
        nm = str(ev.get("name", ""))
        return "CombatHit" in nm or "SyncBoard" in nm or nm in ("CombatHit", "SyncBoard")

    first_hit = next((e for e in cevs if is_combat_or_sync(e)), None)
    idle10 = next((e for e in cps if e["payload"].get("trigger") == "IdleWatch.10s"), None)
    opening_cp = next((e for e in cps if e["payload"].get("trigger") == "Opening.Settled"), None)

    if idle10 and first_hit:
        t0 = idle10["tMs"]
        t1 = first_hit.get("tMs", 999999)
        print(f"\n--- Idle window: IdleWatch.10s tMs={t0} -> first CombatHit/Sync tMs={t1} ---")
        idle_rem = [e for e in removes if t0 <= e["tMs"] < t1]
        idle_des = [e for e in despawns if t0 <= e["tMs"] < t1]
        print(f"  removes in window: {len(idle_rem)}")
        for e in idle_rem:
            p = e["payload"]
            print(
                f"    uid={e.get('uid')} reason={p.get('reason')} "
                f"caller={p.get('caller')} tMs={e['tMs']}"
            )
        print(f"  despawns in window: {len(idle_des)}")

    if opening_cp and first_hit:
        ob = opening_cp["beatId"]
        fb = first_hit["beatId"]
        ot = opening_cp["tMs"]
        ft = first_hit["tMs"]
        print(f"\n--- Pre-interaction window: Opening.Settled tMs={ot} -> first hit tMs={ft} ---")
        pre_rem = [e for e in removes if ot <= e["tMs"] < ft]
        print(f"  removes before first interaction: {len(pre_rem)}")
        for e in pre_rem:
            p = e["payload"]
            print(
                f"    uid={e.get('uid')} reason={p.get('reason')} "
                f"caller={p.get('caller')} tMs={e['tMs']} beat={e['beatId']}"
            )

    print("\n--- CoreLog Presentation/RegistryAudit ---")
    for e in cevs:
        nm = str(e.get("name", ""))
        if "RegistryAudit" in nm:
            p = e.get("payload") or {}
            trig = p.get("trigger", nm)
            if any(x in str(trig) for x in ("Opening", "Idle", "Interaction")):
                print(
                    f"  tMs={e.get('tMs')} trigger={trig} accepted={e.get('accepted')} "
                    f"ghosts={p.get('ghosts', '')} orphans={p.get('orphans', '')}"
                )

    if opening_cp and first_hit:
        ob = opening_cp["beatId"]
        fb = first_hit["beatId"]
        print(f"\n--- Core Occupancy/Sync/Vacate beat {ob}-{fb} ---")
        for e in cevs:
            if ob <= e.get("beatId", -1) <= fb:
                nm = e.get("name", "")
                if any(k in nm for k in ("Occupancy", "SyncDiff", "Vacate", "RegistryAudit", "HopPlan")):
                    slim = {
                        k: e.get(k)
                        for k in ("index", "beatId", "tMs", "name", "category", "accepted", "payload")
                    }
                    print(json.dumps(slim, ensure_ascii=False)[:400])
