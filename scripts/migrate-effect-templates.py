#!/usr/bin/env python3
"""Migrate one-off effects → parameterized templates + card assembly refs (#70 / ADR-0009)."""
from __future__ import annotations

import json
from collections import defaultdict
from copy import deepcopy
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ARTS_TABLES = ROOT / "Assets/Arts/ContentVisual/tables"
STREAM_TABLES = ROOT / "Assets/StreamingAssets/ContentVisual/tables"
ARTS_CARDS = ROOT / "Assets/Arts/ContentVisual/cards"
STREAM_CARDS = ROOT / "Assets/StreamingAssets/ContentVisual/cards"

PARAM_KEYS = {
    "amount",
    "delta",
    "value",
    "count",
    "weight",
    "reason",
    "cause",
    "source",
    "poolId",
    "sourceAction",
    "excludeSourcePrefix",
    "option",
}

# Human template ids for known merge clusters (param-normalized fingerprint → id).
SHARED_TEMPLATE_NAMES = {
    # heal player on OnUseHelpCard
    "heal_player_on_use_help_card": "tpl.heal_player_on_use_help_card",
    "gain_armor_on_use_help_card": "tpl.gain_armor_on_use_help_card",
}


def load_effects():
    rows = json.loads((ARTS_TABLES / "effects.json").read_text(encoding="utf-8"))
    out = []
    for row in rows:
        raw = row["json"]
        # Fix known trailing brace
        if row["id"] == "help.stat_boost_card.use" and raw.endswith("}"):
            try:
                json.loads(raw)
            except json.JSONDecodeError:
                raw = raw[:-1]
                json.loads(raw)
                row = dict(row)
                row["json"] = raw
        obj = json.loads(row["json"])
        out.append((row, obj))
    return out


def walk_replace_params(node, args: dict, path: str = ""):
    """Replace PARAM_KEYS leaves with {{key}} placeholders; fill args."""
    if isinstance(node, dict):
        out = {}
        for k, v in node.items():
            if k in ("id", "typeTag", "verb", "containerType", "requires"):
                continue
            child_path = f"{path}.{k}" if path else k
            if k in PARAM_KEYS and not isinstance(v, (dict, list)):
                key = k
                if key in args and args[key] != v:
                    key = child_path.replace(".", "_").replace("[", "_").replace("]", "")
                args[key] = v
                out[k] = f"{{{{{key}}}}}"
            else:
                out[k] = walk_replace_params(v, args, child_path)
        return out
    if isinstance(node, list):
        return [walk_replace_params(x, args, f"{path}[{i}]") for i, x in enumerate(node)]
    return node


def normalize_for_cluster(obj):
    def walk(n):
        if isinstance(n, dict):
            out = {}
            for k, v in n.items():
                if k in ("id", "typeTag", "verb", "containerType"):
                    continue
                if k in PARAM_KEYS and not isinstance(v, (dict, list)):
                    out[k] = f"<{k}>"
                else:
                    out[k] = walk(v)
            return out
        if isinstance(n, list):
            return [walk(x) for x in n]
        return n

    return json.dumps(walk(obj), sort_keys=True, ensure_ascii=False)


def infer_requires(obj, container: str) -> list[str]:
    reqs = []
    trigger = ((obj.get("trigger") or {}).get("atom") or "")
    if container == "HelpCard":
        reqs.append("HasOwnerEntity")
        if trigger == "OnUseHelpCard":
            reqs.append("ActivatedByUse")
        reqs.append("CardZoneTriggerable")
    elif container == "MonsterSkill":
        reqs.append("HasOwnerEntity")
        reqs.append("CardZoneTriggerable")
    elif container == "Relic":
        # Relics have no board entity; empty or explicit NoneOwner
        reqs.append("NoOwnerEntity")

    for c in obj.get("conditions") or []:
        if isinstance(c, dict) and c.get("atom") == "CardZone":
            zone = c.get("zone") or c.get("zones") or ""
            if zone:
                reqs.append(f"CardZone:{zone}")
    # stable unique
    seen = set()
    ordered = []
    for r in reqs:
        if r not in seen:
            seen.add(r)
            ordered.append(r)
    return ordered


def infer_condition_declarations(obj) -> list[str]:
    """Lightweight string declarations mirroring DSL condition atoms (for #72)."""
    decls = []
    for c in obj.get("conditions") or []:
        if not isinstance(c, dict):
            continue
        atom = c.get("atom") or "Condition"
        parts = [atom]
        for k in ("eventType", "zone", "stat", "op", "left", "right"):
            if k in c and not isinstance(c[k], (dict, list)):
                parts.append(f"{k}={c[k]}")
        decls.append(":".join(str(p) for p in parts))
    return decls


def semantic_shared_id(obj) -> str | None:
    """Detect well-known shared shapes."""
    kind = obj.get("kind")
    trigger = (obj.get("trigger") or {}).get("atom")
    target = (obj.get("target") or {}).get("atom")
    action = obj.get("action") or {}
    atom = action.get("atom")
    if (
        kind == "Triggered"
        and trigger == "OnUseHelpCard"
        and target == "Player"
        and atom == "Heal"
        and action.get("actor") == "Player"
        and set(action.keys()) <= {"atom", "amount", "actor"}
        and not obj.get("conditions")
    ):
        return "tpl.heal_player_on_use_help_card"
    if (
        kind == "Triggered"
        and trigger == "OnUseHelpCard"
        and target == "Player"
        and atom == "GainArmor"
        and set(action.keys()) <= {"atom", "amount"}
        and not obj.get("conditions")
    ):
        return "tpl.gain_armor_on_use_help_card"
    return None


def build_template_body_and_args(obj):
    args = {}
    body = walk_replace_params(deepcopy(obj), args)
    # Drop identity if any remained
    for k in ("id", "typeTag", "verb", "containerType", "requires"):
        body.pop(k, None)
    return body, args


def index_card_effect_refs():
    """effectId -> list of (card_path, kind, contentId)"""
    refs = defaultdict(list)
    for path in sorted(ARTS_CARDS.glob("*.json")):
        dto = json.loads(path.read_text(encoding="utf-8"))
        kind = dto.get("kind") or ""
        cid = dto.get("contentId") or ""
        for eid in dto.get("effectIds") or []:
            refs[eid].append((path, kind, cid))
    return refs


def main():
    effects = load_effects()
    refs = index_card_effect_refs()

    # Cluster by normalized shape
    clusters = defaultdict(list)
    for row, obj in effects:
        clusters[normalize_for_cluster(obj)].append((row, obj))

    templates = {}  # id -> template dict
    mount_plan = {}  # effect_id -> {templateId, args, containerType, requires...}

    shared_counts = defaultdict(int)

    for shape, members in clusters.items():
        # Prefer semantic shared id when all members match
        shared = None
        if len(members) > 1:
            ids = {semantic_shared_id(obj) for _, obj in members}
            if len(ids) == 1 and next(iter(ids)):
                shared = next(iter(ids))

        if shared:
            # Build template from first member with params
            row0, obj0 = members[0]
            body, _ = build_template_body_and_args(obj0)
            # Re-extract args per member separately for mounts
            requires = infer_requires(obj0, row0.get("container_type") or obj0.get("containerType") or "")
            # For shared heal/armor, requires should allow both containers — use ActivatedByUse only
            if shared.startswith("tpl.heal_") or shared.startswith("tpl.gain_armor_"):
                requires = ["ActivatedByUse"]
            conditions = infer_condition_declarations(obj0)
            templates[shared] = {
                "id": shared,
                "state": "Implemented",
                "design_text": row0.get("design_text") or shared,
                "requires_json": json.dumps(requires, ensure_ascii=False),
                "conditions_json": json.dumps(conditions, ensure_ascii=False),
                "body": json.dumps(body, ensure_ascii=False, separators=(",", ":")),
            }
            for row, obj in members:
                _, args = build_template_body_and_args(obj)
                container = row.get("container_type") or obj.get("containerType")
                mount_plan[row["id"]] = {
                    "templateId": shared,
                    "args": args,
                    "containerType": container,
                }
                shared_counts[shared] += 1
            continue

        if len(members) > 1:
            # Generic merge: one template, per-mount args
            row0, obj0 = members[0]
            # Find all differing param leaves across members by building union of args keys
            bodies_args = [build_template_body_and_args(obj) for _, obj in members]
            # Use first body (placeholders); args per mount
            body = bodies_args[0][0]
            member_ids = sorted(r["id"] for r, _ in members)
            slug = member_ids[0].replace(".", "_")
            tpl_id = f"tpl.shared.{len(members)}.{slug}"
            if tpl_id in templates:
                tpl_id = f"tpl.shared.{len(members)}." + "_".join(member_ids[:2]).replace(".", "_")
            requires = infer_requires(obj0, row0.get("container_type") or "")
            conditions = infer_condition_declarations(obj0)
            templates[tpl_id] = {
                "id": tpl_id,
                "state": row0.get("state") or "Implemented",
                "design_text": row0.get("design_text") or tpl_id,
                "requires_json": json.dumps(requires, ensure_ascii=False),
                "conditions_json": json.dumps(conditions, ensure_ascii=False),
                "body": json.dumps(body, ensure_ascii=False, separators=(",", ":")),
            }
            for (row, obj), (_, args) in zip(members, bodies_args):
                mount_plan[row["id"]] = {
                    "templateId": tpl_id,
                    "args": args,
                    "containerType": row.get("container_type") or obj.get("containerType"),
                }
                shared_counts[tpl_id] += 1
            continue

        # 1:1
        row, obj = members[0]
        body, args = build_template_body_and_args(obj)
        tpl_id = "tpl." + row["id"]
        container = row.get("container_type") or obj.get("containerType") or ""
        requires = infer_requires(obj, container)
        conditions = infer_condition_declarations(obj)
        templates[tpl_id] = {
            "id": tpl_id,
            "state": row.get("state") or "Implemented",
            "design_text": row.get("design_text") or tpl_id,
            "requires_json": json.dumps(requires, ensure_ascii=False),
            "conditions_json": json.dumps(conditions, ensure_ascii=False),
            "body": json.dumps(body, ensure_ascii=False, separators=(",", ":")),
        }
        mount_plan[row["id"]] = {
            "templateId": tpl_id,
            "args": args,
            "containerType": container,
        }

    # Write templates
    template_rows = [templates[k] for k in sorted(templates.keys())]
    for dest in (ARTS_TABLES, STREAM_TABLES):
        dest.mkdir(parents=True, exist_ok=True)
        (dest / "effect_templates.json").write_text(
            json.dumps(template_rows, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

    # Update cards: replace effectIds with effectAssemblies; keep effectIds empty or mirrored
    updated_cards = 0
    for path in sorted(ARTS_CARDS.glob("*.json")):
        dto = json.loads(path.read_text(encoding="utf-8"))
        effect_ids = list(dto.get("effectIds") or [])
        if not effect_ids:
            # still ensure field present
            dto["effectAssemblies"] = dto.get("effectAssemblies") or []
            text = json.dumps(dto, ensure_ascii=False, indent=4) + "\n"
            path.write_text(text, encoding="utf-8")
            stream = STREAM_CARDS / path.name
            if stream.exists() or True:
                STREAM_CARDS.mkdir(parents=True, exist_ok=True)
                stream.write_text(text, encoding="utf-8")
            continue

        assemblies = []
        for eid in effect_ids:
            plan = mount_plan.get(eid)
            if not plan:
                raise SystemExit(f"No mount plan for effect {eid} on {path.name}")
            assemblies.append(
                {
                    "id": eid,
                    "templateId": plan["templateId"],
                    "containerType": plan["containerType"],
                    "argsJson": json.dumps(plan["args"], ensure_ascii=False, separators=(",", ":")),
                }
            )
        dto["effectAssemblies"] = assemblies
        dto["effectIds"] = []  # authority moved to assemblies
        text = json.dumps(dto, ensure_ascii=False, indent=4) + "\n"
        path.write_text(text, encoding="utf-8")
        STREAM_CARDS.mkdir(parents=True, exist_ok=True)
        (STREAM_CARDS / path.name).write_text(text, encoding="utf-8")
        updated_cards += 1

    # Orphan effects (no card ref): still have templates via 1:1 above; no assembly
    orphans = [eid for eid in mount_plan if eid not in refs]
    # Keep effects.json as empty array marker? Leave a stub pointing readers to templates.
    stub = []
    for dest in (ARTS_TABLES, STREAM_TABLES):
        (dest / "effects.json").write_text(
            json.dumps(stub, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

    print("templates", len(template_rows))
    print("mounts", len(mount_plan))
    print("updated_cards", updated_cards)
    print("shared_templates", {k: v for k, v in shared_counts.items() if v > 1})
    print("orphans", orphans)
    print(
        "cross_container_heal_armor",
        shared_counts.get("tpl.heal_player_on_use_help_card"),
        shared_counts.get("tpl.gain_armor_on_use_help_card"),
    )


if __name__ == "__main__":
    main()
