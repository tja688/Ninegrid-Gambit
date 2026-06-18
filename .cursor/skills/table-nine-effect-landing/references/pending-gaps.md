# Pending Gaps Snapshot

Read-only reference from `Assets/Notes/TableNine-Effect-Batch-8-Final-Landing-Report.md` (2026-06-18).  
**Not a maintenance task** — refresh manually when a landing task completes.

Total pending: **32** (100 implemented / 133 total)

## HelpCard (6)

| Effect id | Design summary | Likely gap |
|:--|:--|:--|
| `help.armor_breaking_hammer.pending` | Selected monster armor −10 | Armor reduction action or ModifyBaseStat on Armor; target selection |
| `help.brutality_card.pending` | Player total attack ×2 until one battle ends | Temporary attack multiplier + battle-end cleanup |
| `help.healing_spring.pending` | Multi-zone heal | Target/zone branching |
| `help.kidnapping.pending` | Remove non-elite non-boss monster; gain armor = its armor | Elite/boss filter + pre-remove armor snapshot |
| `help.rolling_stone.pending` | On move to slot 3: remove slot 6 normal monster + self | Position trigger + normal-monster filter + self-remove |
| `help.watchtower.pending` | Random damage on board/item-slot cards | Zone branch + random damage target |

## Relic (1)

| Effect id | Design summary | Likely gap |
|:--|:--|:--|
| `relic.heavy_armor.pending` | Gain armor = base armor at node start | Read player base armor at node start |

## PlayerSkill (1)

| Effect id | Design summary | Likely gap |
|:--|:--|:--|
| `skill.even_hatred.pending` | Double damage vs even-level monsters | Level parity condition + damage multiplier rule |

## MonsterSkill (24) — by capability cluster

### Armor transfer

- `skill.absorb_stone.pending`
- `skill.swallow_stone.pending`

### Battle-time dynamic values (lost armor / damage dealt counts)

- `skill.hard.pending`
- `skill.bloodthirst.pending`
- `skill.throw_stone.pending`

### Adjacent temporary buffs (expire on leave)

- `skill.orc_tactics.pending`
- `skill.rascality.pending`

### Force battle / battle hijack

- `skill.relentless_chase.pending`
- `skill.fight_me.pending`

### Cross-zone exchange (board ↔ deck)

- `skill.delivery.pending`

### Batch remove by defId / elite filter

- `skill.sacrifice.pending`
- `skill.flame_breath.pending`

### Card-count-based modifier

- `skill.fire_power.pending`

### Global armor-loss listener

- `skill.stone_lover.pending`

### Rule: reduce damage when others hurt

- `skill.stone_shelter.pending`

### Adjacent count condition + multi-card remove

- `skill.strong_combo.pending`

### Slot + kind condition combo

- `skill.rolling_crush.pending`

### Random deck/level shuffle-in

- `skill.fracture_fall_apart.pending`
- `skill.mixed_bones.pending`
- `skill.otherworld_help.pending`

### Movement source distinction

- `skill.hot_observation.pending`

### Cumulative adjacent player count

- `skill.stocking.pending`

### Blessed-slot count + one-shot high damage

- `skill.find_weakness.pending`

### Recursive suppression (`EventFilter.targetIs`)

- `skill.smart.pending`

## Already composable without new atoms (batch 9 reference)

These were landed by DSL composition only:

| Skill | Compose pattern |
|:--|:--|
| `skill.turn_world` | `OnEnter` + `Rotate` |
| `skill.hoodlum` | `OnMoveToSlot(1)` + `DealDamage` |
| `skill.fall_apart` | `OnRemove` + `Sequence(ShuffleInto×2)` |
| `skill.air_strike` | Four corner slots × (`OnMoveToSlot` + `DealDamage`) |
| `skill.space_mastery` | `OnBattle` + `Rotate` |
| `skill.gear_delivery` | `OnSelfMove(every:2)` + `FilteredCards` + `WeightedRandom` |
| `skill.stone_growth` | Left column slots × (`OnMoveToSlot` + `FilteredCards` + `GainArmor`) |

Use these as golden compose references before inventing new atoms.
