# -*- coding: utf-8 -*-
"""Restore monster cards from pre-#86, keep sequence/slot names, move to deck.transition."""
from __future__ import annotations

import json
import subprocess
from pathlib import Path

ROOT = Path(r"C:/Users/jinji/Documents/GitHub/Ninegrid Gambit")
ARTS = ROOT / "Assets/Arts/ContentVisual/cards"
STREAM = ROOT / "Assets/StreamingAssets/ContentVisual/cards"
RESTORE_REV = "b717b15c6^"
TRANSITION = "deck.transition"

# Fields restored from pre-#86 (presentation / combat content).
RESTORE_KEYS = [
    "displayName",
    "description",
    "faceIntro",
    "attackPattern",
    "gold",
    "stats",
    "sprites",
    "mainVisual",
    "animations",
    "extraSlots",
    "rarity",
    "tags",
    "effectAssemblies",
    "effectIds",
    "skillIds",
    "isElite",
    "isBoss",
    "isReserve",
    "role",
]


def git_show(rev: str, path: Path) -> dict | None:
    rel = path.relative_to(ROOT).as_posix()
    try:
        raw = subprocess.check_output(
            ["git", "show", f"{rev}:{rel}"],
            cwd=ROOT,
            stderr=subprocess.DEVNULL,
        )
    except subprocess.CalledProcessError:
        return None
    return json.loads(raw.decode("utf-8"))


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def save(path: Path, data: dict) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def merge_monster(current: dict, restored: dict | None) -> dict:
    out = dict(current)
    slot = (current.get("displayName") or "").strip()
    seq = int(current.get("sequence") or 0)
    level = current.get("level")

    if restored:
        for key in RESTORE_KEYS:
            if key in restored:
                out[key] = restored[key]

    # Keep #86 loadout fields for designers.
    out["designSlotName"] = slot
    if seq > 0:
        out["sequence"] = seq
    if isinstance(level, str) and level.strip():
        out["level"] = level.strip()
    elif seq == 5:
        out["level"] = "层主"
    elif not isinstance(out.get("level"), str) or not str(out.get("level") or "").strip():
        out["level"] = "普通"

    # Align boss flags with level when sequence model is present.
    if out.get("level") == "层主" or seq == 5:
        out["isBoss"] = True
        out["isElite"] = False
        out["level"] = "层主"
        if seq <= 0:
            out["sequence"] = 5
    elif seq > 0:
        # Keep restored elite flag unless sequence model cleared it intentionally.
        pass

    out["deckId"] = TRANSITION
    out["schemaVersion"] = 2
    out["kind"] = "Monster"
    return out


def main() -> None:
    arts_monsters = sorted(ARTS.glob("monster_*.json"))
    changed = 0
    missing_restore = []
    for arts_path in arts_monsters:
        current = load(arts_path)
        if (current.get("kind") or "").lower() != "monster":
            continue
        restored = git_show(RESTORE_REV, arts_path)
        if restored is None:
            missing_restore.append(arts_path.name)
        merged = merge_monster(current, restored)
        save(arts_path, merged)
        stream_path = STREAM / arts_path.name
        if stream_path.exists() or True:
            save(stream_path, merged)
        changed += 1
        print(
            f"{arts_path.name}: display={merged.get('displayName')!r} "
            f"slot={merged.get('designSlotName')!r} seq={merged.get('sequence')} "
            f"deck={merged.get('deckId')}"
        )

    print(f"\nupdated {changed} monsters")
    if missing_restore:
        print("no pre-#86 blob (kept current body):", ", ".join(missing_restore))


if __name__ == "__main__":
    main()
