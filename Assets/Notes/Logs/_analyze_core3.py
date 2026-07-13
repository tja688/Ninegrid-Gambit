import json

def load(path):
    with open(path, "r", encoding="utf-8-sig") as f:
        return json.load(f)

evs = load(r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\CoreLog\corelog-20260712-141249-seed1.json")["events"]

def show(name_filter=None, beat=None, idx_range=None):
    for e in evs:
        if name_filter and e.get("name") != name_filter:
            continue
        if beat is not None and e.get("beatId") != beat:
            continue
        if idx_range and not (idx_range[0] <= e["index"] <= idx_range[1]):
            continue
        print(json.dumps(e, ensure_ascii=False))

print("=== beat 1: all OccupancySnapshot, SyncDiff, RegistryAudit, Opening*, Interaction* ===")
for e in evs:
    if e.get("beatId") != 1:
        continue
    n = e.get("name","")
    if n in ("OccupancySnapshot","SyncDiff","HopPlan","OccupancyConflict","RegistryAudit","OpeningDealComplete","DealComplete","InteractionGate","PresentationLock","FieldBusy") or "Opening" in n or "Deal" in n and e["index"]>=25:
        print(json.dumps(e, ensure_ascii=False))

print("\n=== beat 1 tail index 25-45 ===")
for e in evs:
    if 25 <= e["index"] <= 45:
        print("idx=%s beat=%s cat=%s name=%s acc=%s payload=%s" % (e["index"], e["beatId"], e.get("category"), e.get("name"), e.get("accepted"), json.dumps(e.get("payload"), ensure_ascii=False)[:200]))

print("\n=== beats 2-4 all events ===")
for e in evs:
    if e.get("beatId") in (2,3,4):
        print("idx=%s beat=%s name=%s payload=%s" % (e["index"], e["beatId"], e.get("name"), json.dumps(e.get("payload"), ensure_ascii=False)[:250]))

print("\n=== OccupancyConflict all ===")
for e in evs:
    if e.get("name")=="OccupancyConflict":
        print(json.dumps(e, ensure_ascii=False))

print("\n=== hasDiff=true snapshots (first 15) ===")
c=0
for e in evs:
    if e.get("name")!="OccupancySnapshot":
        continue
    p=e.get("payload") or {}
    if p.get("hasDiff")=="true":
        print(json.dumps(e, ensure_ascii=False)[:500])
        c+=1
        if c>=15: break

print("\n=== coreOccupantCount drops (compare consecutive core snapshots) ===")
last=None
for e in evs:
    if e.get("name")!="OccupancySnapshot":
        continue
    p=e.get("payload") or {}
    if p.get("phase") not in ("syncAfter","startNodeAfter","drainAfter"):
        continue
    cc=int(p.get("coreOccupantCount","0"))
    if last and cc < last[1]:
        print("DROP beat %s->%s idx %s->%s count %s->%s" % (last[0], e["beatId"], last[2], e["index"], last[1], cc))
    last=(e["beatId"], cc, e["index"])

print("\n=== First user-facing op (refBattleOpIndex>0 accepted) ===")
for e in evs:
    if e.get("refBattleOpIndex", -1) > 0 and e.get("accepted"):
        print("idx=%s beat=%s name=%s refOp=%s" % (e["index"], e["beatId"], e.get("name"), e.get("refBattleOpIndex")))
        break
