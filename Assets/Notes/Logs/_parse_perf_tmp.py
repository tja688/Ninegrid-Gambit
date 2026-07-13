import json
from pathlib import Path

p = Path(r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs\PerfLog\perflog-20260712-141249-seed1.json")
with open(p, encoding="utf-8-sig") as f:
    events = json.load(f)["events"]

def pl_str(pl):
    if not pl:
        return ""
    parts = []
    for k in sorted(pl.keys()):
        parts.append(f"{k}={pl[k]}")
    return "; ".join(parts)

spawns = [e for e in events if e.get("kind")=="Spawn"]
despawns = [e for e in events if e.get("kind")=="Despawn"]
audits = [e for e in events if e.get("kind")=="RegistryAudit"]

print("="*80)
print(f"DESPAWN EVENTS (n={len(despawns)})")
print("="*80)
print(f"{'index':>6} | {'tMs':>7} | {'beatId':>6} | {'uid':>5} | {'site':<28} | payload")
print("-"*120)
for e in despawns:
    print(f"{e['index']:>6} | {e['tMs']:>7} | {e['beatId']:>6} | {e.get('uid',''):>5} | {e.get('site',''):<28} | {pl_str(e.get('payload'))}")

print()
print("="*80)
print(f"SPAWN EVENTS (n={len(spawns)})")
print("="*80)
print(f"{'index':>6} | {'tMs':>7} | {'beatId':>6} | {'uid':>5} | {'site':<28} | payload")
print("-"*120)
for e in spawns:
    print(f"{e['index']:>6} | {e['tMs']:>7} | {e['beatId']:>6} | {e.get('uid',''):>5} | {e.get('site',''):<28} | {pl_str(e.get('payload'))}")

print()
print("="*80)
print(f"REGISTRY AUDIT ALL (n={len(audits)})")
print("="*80)
print(f"{'index':>6} | {'tMs':>7} | {'beatId':>6} | {'trigger':<20} | {'registryCount':>13} | {'fieldCount':>10} | {'ghosts':>6} | {'orphans':>7}")
print("-"*100)
for e in audits:
    pl = e.get("payload") or {}
    print(f"{e['index']:>6} | {e['tMs']:>7} | {e['beatId']:>6} | {pl.get('trigger',''):<20} | {pl.get('registryCount',''):>13} | {pl.get('fieldCount',''):>10} | {pl.get('ghosts',''):>6} | {pl.get('orphans',''):>7}")

print()
print("="*80)
print("REGISTRY AUDIT tMs 0-15000 (opening period)")
print("="*80)
opening = [e for e in audits if 0 <= e["tMs"] <= 15000]
print(f"count in range: {len(opening)}")
for e in opening:
    pl = e.get("payload") or {}
    print(f"{e['index']:>6} | {e['tMs']:>7} | beatId={e['beatId']} | trigger={pl.get('trigger','')} | registry={pl.get('registryCount')} | field={pl.get('fieldCount')} | ghosts={pl.get('ghosts')} | orphans={pl.get('orphans')}")

print()
print("="*80)
print("COUNTS")
print("="*80)
print(f"Spawn total: {len(spawns)}")
print(f"Despawn total: {len(despawns)}")
print(f"Net (Spawn-Despawn): {len(spawns)-len(despawns)}")

close_t, open_t = 3439, 4163
gap = [e for e in despawns if close_t < e["tMs"] < open_t]
gap_inc = [e for e in despawns if close_t <= e["tMs"] <= open_t]
print()
print(f"Despawn strictly between OpeningDeal BeatClose ({close_t}) and first CombatHit BeatOpen ({open_t}): {len(gap)}")
for e in gap:
    print(" ", e)
print(f"Despawn inclusive [{close_t},{open_t}]: {len(gap_inc)}")

beat1_desp = [e for e in despawns if e["beatId"]==1]
print()
print(f"Despawn during beatId=1 (OpeningDeal): {len(beat1_desp)}")
for e in beat1_desp:
    print(f"  index={e['index']} tMs={e['tMs']} uid={e.get('uid')} site={e.get('site')} payload={e.get('payload')}")

print()
print("All events with tMs in (3439, 4163):")
for e in events:
    if close_t < e["tMs"] < open_t:
        print(f"  {e['index']} tMs={e['tMs']} beatId={e['beatId']} kind={e['kind']} uid={e.get('uid')} site={e.get('site')}")

