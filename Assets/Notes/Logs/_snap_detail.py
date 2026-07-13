import json
import re

base = r"C:\Users\jinji\Documents\GitHub\Ninegrid-Gambit\Assets\Notes\Logs"
sid = "20260712-151636"
reg = json.load(open(f"{base}/OtherLog/RegistryLog/registrylog-{sid}-seed1.json", encoding="utf-8-sig"))
evs = reg["events"]

for label, trig in [("Opening", "Opening.Settled"), ("Idle10", "IdleWatch.10s")]:
    for i, e in enumerate(evs):
        if e.get("kind") == "Checkpoint" and e["payload"].get("trigger") == trig:
            for j in range(i, min(i + 4, len(evs))):
                if evs[j]["kind"] == "BoardSnap":
                    cards = evs[j]["payload"].get("cards", "")
                    print(f"=== {label} BoardSnap raw (field entries only) ===")
                    for part in cards.split(";"):
                        if "slot=" not in part:
                            continue
                        slot_m = re.search(r"slot=(-?\d+)", part)
                        slot = int(slot_m.group(1))
                        if slot < 0:
                            continue
                        uid_m = re.match(r"^[~+]?(\d+)", part.strip())
                        uid = uid_m.group(1) if uid_m else "?"
                        active_m = re.search(r"active=(\d+)", part)
                        active = active_m.group(1) if active_m else "?"
                        xy_m = re.search(r"xy=([^,;]+)", part)
                        xy = xy_m.group(1) if xy_m else "?"
                        mode_m = re.search(r"mode=([^,;]+)", part)
                        mode = mode_m.group(1) if mode_m else "?"
                        print(f"  slot={slot} uid={uid} active={active} xy={xy} mode={mode}")
                    break
            break
