## Parent

Part of #114

## What to build

Ship nine brand-new content ids for the design names that currently sit on wrong legacy relics (除甲刀、腐朽顺劈斧、荆棘甲、肌肉反击、金色宝箱、废物增幅器、铁盾、身体潜力、超越维度), each with correct effect assemblies, rarity/role, and editor-editable presentation fields. Map 腐朽顺劈斧 to Red rarity (unique / not chest-rolled). Align Profession: initial relic is the new cleave axe; Warrior base armor is 0.

## Acceptance criteria

- [ ] Nine new `relic.*` ids exist with design-accurate effects and filled name/description/assemblies
- [ ] 腐朽顺劈斧 is Red and is not offered by White/Blue/Gold chest pools
- [ ] New runs grant 腐朽顺劈斧 and start with base armor 0
- [ ] EditMode coverage for Profession grant + at least sample effect contracts; recompile console clean
- [ ] Missing main icons may be empty (human fills later) without blocking merge

## Blocked by

- #115
