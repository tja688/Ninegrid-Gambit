import json
from collections import defaultdict

def load_doc(path):
    with open(path, "r", encoding="utf-8-sig") as f:
        return json.load(f)

def payload_get(e, *keys):
    p = e.get("payload") or {}
    for k in keys:
        if k in e:
            return e[k]
        if k in p:
            return p[k]
    return None

def diff_interesting(e):
    p = e.get("payload") or {}
    diff = p.get("diff") or p.get("cardDiff") or p.get("cardsDiff") or ""
    if isinstance(diff, list):
        diff_s = json.dumps(diff, ensure_ascii=False)
        for item in diff:
            if isinstance(item, dict):
                if item.get("active") in (0, "0", False) or str(item.get("uid", "")).startswith("-"):
                    return True, diff_s[:500]
            elif isinstance(item, str) and item.startswith("-"):
                return True, diff_s[:500]
    else:
        diff_s = str(diff)
    has_minus_uid = "-" in diff_s and "uid" in diff_s.lower()
    if "active" in diff_s and ('"0"' in diff_s or ":0" in diff_s):
        return True, diff_s[:500]
    return has_minus_uid, (diff_s[:500] if has_minus_uid else None)

def card_count_from_snap(e):
    p = e.get("payload") or {}
    cards = p.get("cards")
    if isinstance(cards, list):
        return len(cards)
    if isinstance(cards, dict):
        return len(cards)
    for k in ("cardCount", "count", "boardCardCount", "nCards"):
        if k in p:
            return p[k]
    return None

def analyze_perflog(path, label):
    doc = load_doc(path)
    evs = doc.get("events") or []
    print("\n" + "=" * 72)
    print("PERFLOG", label, path.split("\\")[-1])
    print("sessionId=%s seed=%s" % (doc.get("sessionId"), doc.get("seed")))
    print("Total events:", len(evs))

    kinds = defaultdict(int)
    for e in evs:
        kinds[e.get("kind", "?")] += 1
    print("Kind counts:", dict(sorted(kinds.items(), key=lambda x: -x[1])))

    anomalies = [e for e in evs if e.get("kind") == "Anomaly"]
    print("\n--- Anomaly events (%d) ---" % len(anomalies))
    for e in anomalies:
        p = e.get("payload") or {}
        row = {
            "index": e.get("index"),
            "beatId": e.get("beatId"),
            "tMs": e.get("tMs"),
            "uid": e.get("uid"),
            "code": p.get("code") or e.get("code"),
            "detail": p.get("detail") or e.get("detail"),
        }
        for k, v in p.items():
            if k not in ("code", "detail"):
                row[k] = v
        print(json.dumps(row, ensure_ascii=False))

    vis0 = []
    for e in evs:
        if e.get("kind") != "VisChange":
            continue
        active = payload_get(e, "active")
        if active in (0, "0", False, "false"):
            vis0.append(e)
    print("\n--- VisChange active=0 (%d) ---" % len(vis0))
    for e in vis0:
        print(
            "  idx=%s beatId=%s tMs=%s uid=%s site=%s payload=%s"
            % (e.get("index"), e.get("beatId"), e.get("tMs"), e.get("uid"), e.get("site"), e.get("payload"))
        )

    beat_close_bad = []
    for e in evs:
        if e.get("kind") != "BeatClose":
            continue
        ac = payload_get(e, "anomalyCount")
        try:
            acn = int(ac) if ac is not None else 0
        except Exception:
            acn = 0
        if acn > 0:
            beat_close_bad.append((e, acn))
    print("\n--- BeatClose anomalyCount>0 (%d) ---" % len(beat_close_bad))
    for e, acn in beat_close_bad:
        print(
            "  idx=%s beatId=%s tMs=%s anomalyCount=%s payload=%s"
            % (e.get("index"), e.get("beatId"), e.get("tMs"), acn, e.get("payload"))
        )

    board_snaps = [e for e in evs if e.get("kind") == "BoardSnap"]
    close_interesting = 0
    print("\n--- BoardSnap Close minus/active=0 ---")
    for e in board_snaps:
        p = e.get("payload") or {}
        phase = str(p.get("phase") or p.get("openClose") or "").lower()
        if phase != "close":
            continue
        ok, snippet = diff_interesting(e)
        if ok:
            close_interesting += 1
            print(
                "  idx=%s beatId=%s tMs=%s diff=%s"
                % (e.get("index"), e.get("beatId"), e.get("tMs"), snippet)
            )
    print("  count:", close_interesting, "(of", len(board_snaps), "BoardSnap total)")

    reg_bad = []
    for e in evs:
        if e.get("kind") != "RegistryAudit":
            continue
        p = e.get("payload") or {}
        ghosts = p.get("ghosts") or p.get("ghostCount") or 0
        orphans = p.get("orphans") or p.get("orphanCount") or 0
        try:
            if int(ghosts) > 0 or int(orphans) > 0:
                reg_bad.append(e)
        except Exception:
            pass
    print("\n--- RegistryAudit ghosts/orphans>0 (%d) ---" % len(reg_bad))
    for e in reg_bad:
        print("  idx=%s beatId=%s tMs=%s payload=%s" % (e.get("index"), e.get("beatId"), e.get("tMs"), e.get("payload")))

    beat_names = {}
    for e in evs:
        if e.get("kind") in ("BeatOpen", "BeatClose"):
            p = e.get("payload") or {}
            name = p.get("beatName") or p.get("name") or p.get("beatType")
            if name:
                beat_names[e.get("beatId")] = name

    opening_beat_ids = [bid for bid, n in beat_names.items() if "OpeningDeal" in str(n)]
    print("\n--- Beat timeline (first 30 beatIds) ---")
    for bid in sorted(beat_names.keys())[:30]:
        print("  beatId=%s: %s" % (bid, beat_names[bid]))
    print("OpeningDeal beatIds:", opening_beat_ids)

    print("\n--- BoardSnap around OpeningDeal ---")
    target_ids = set(opening_beat_ids) if opening_beat_ids else None
    if not target_ids:
        for e in evs:
            if e.get("kind") == "BeatOpen":
                p = e.get("payload") or {}
                if "OpeningDeal" in json.dumps(p):
                    target_ids = set([e.get("beatId")])
                    break
    for e in board_snaps:
        bid = e.get("beatId")
        if target_ids and bid not in target_ids:
            continue
        p = e.get("payload") or {}
        phase = p.get("phase") or p.get("openClose") or "?"
        print(
            "  idx=%s beatId=%s name=%s phase=%s cards=%s"
            % (e.get("index"), bid, beat_names.get(bid), phase, card_count_from_snap(e))
        )
        if card_count_from_snap(e) is None:
            print("    payload=", json.dumps(p, ensure_ascii=False)[:600])

    opening_close_tms = None
    opening_close_idx = None
    for e in evs:
        if e.get("kind") == "BeatClose" and (not opening_beat_ids or e.get("beatId") in opening_beat_ids):
            opening_close_tms = e.get("tMs")
            opening_close_idx = e.get("index")

    interaction_keywords = ("PlayCard", "User", "Click", "Select", "Drag", "Input", "HandPlay", "Deploy", "Interact", "CardPlay")
    first_interact = None
    for e in sorted(evs, key=lambda x: (x.get("index", 0))):
        if e.get("kind") != "BeatOpen":
            continue
        if opening_close_idx is not None and e.get("index", 0) <= opening_close_idx:
            continue
        p = e.get("payload") or {}
        name = str(p.get("beatName") or p.get("name") or "")
        if any(k in name for k in interaction_keywords):
            first_interact = (e.get("beatId"), name, e.get("tMs"), e.get("index"))
            break
    if first_interact is None:
        for e in sorted(evs, key=lambda x: (x.get("index", 0))):
            if e.get("kind") != "BeatOpen":
                continue
            if opening_close_idx is not None and e.get("index", 0) <= opening_close_idx:
                continue
            p = e.get("payload") or {}
            name = str(p.get("beatName") or p.get("name") or "")
            first_interact = (e.get("beatId"), name, e.get("tMs"), e.get("index"))
            break

    print("\n--- OpeningDeal close: idx=%s tMs=%s ---" % (opening_close_idx, opening_close_tms))
    print("First BeatOpen after OpeningDeal:", first_interact)

    if opening_close_idx is not None:
        gap = []
        for e in evs:
            idx = e.get("index", 0)
            if idx <= opening_close_idx:
                continue
            if first_interact and idx >= first_interact[3]:
                break
            k = e.get("kind")
            suspicious = False
            if k == "VisChange" and payload_get(e, "active") in (0, "0", False):
                suspicious = True
            elif k == "Anomaly":
                suspicious = True
            elif k == "BoardSnap":
                ok, _ = diff_interesting(e)
                if ok:
                    suspicious = True
            elif k in ("Despawn", "Unregister"):
                suspicious = True
            elif k == "RegistryAudit":
                p = e.get("payload") or {}
                try:
                    if int(p.get("ghosts") or 0) > 0 or int(p.get("orphans") or 0) > 0:
                        suspicious = True
                except Exception:
                    pass
            if suspicious:
                gap.append(e)
        print("\n--- Gap suspicious events (%d) ---" % len(gap))
        for e in gap:
            print(
                "  idx=%s kind=%s beatId=%s tMs=%s uid=%s site=%s payload=%s"
                % (e.get("index"), e.get("kind"), e.get("beatId"), e.get("tMs"), e.get("uid"), e.get("site"), e.get("payload"))
            )

    return len(evs)

c249 = analyze_perflog(r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\PerfLog\perflog-20260712-141249-seed1.json", "141249")
c203 = analyze_perflog(r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\PerfLog\perflog-20260712-141203-seed1.json", "141203")
print("\nSUMMARY: 141249 events=%d, 141203 events=%d" % (c249, c203))
