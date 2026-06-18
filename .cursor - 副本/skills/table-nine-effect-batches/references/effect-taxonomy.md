# Effect Taxonomy

Use this reference to classify effects before coding. The goal is to avoid one-off implementations and grow reusable atoms.

## Three-State Decision

Classify every effect into exactly one primary state:

| State | Use when | Runtime carrier |
|:--|:--|:--|
| `Modifier` | It changes a numeric stat while active. | `StatModifier` through `StatSystem` |
| `RuleModifier` | It changes a rule or decision point, not a card stat. | `RuleModifierRegistry` plus an explicit system query/use site |
| `Triggered` | It happens at an event/time and performs actions. | `TriggerSystem` reaction that enqueues `GameAction`s |

Examples:

- "攻击+1" -> `Modifier`.
- "处于格6攻击+2" -> conditional `Modifier`.
- "恢复翻倍" -> `RuleModifier`.
- "每移动3次洗入一张卡" -> `Triggered`.
- "下一次伤害为0" -> often `RuleModifier` or prevention state plus `Triggered` cleanup; do not fake it as negative damage.

## Classification Template

For each content item, write this before editing code:

```text
Effect:
Source doc:
Container: HelpCard | Relic | PlayerSkill | MonsterSkill
Lifecycle: on use | while equipped | while on board | node start/end | once | permanent
Primary state: Modifier | RuleModifier | Triggered
Trigger:
Conditions:
Target selection:
Value expression:
Actions:
Needs new capability:
Can reuse current atoms:
Test case:
```

## Capability Clusters

Prefer implementing one cluster at a time.

### 1. Dynamic Value Expressions

Needed by effects such as:

- target damage equals player attack,
- target damage equals player current HP/current armor,
- damage equals target attack,
- damage/heal equals lost HP or lost armor,
- heal to full,
- attack/armor multiplier until scope ends.

Likely shape:

- Add a value-expression reader used by action atoms.
- Keep constant `amount` as the simple case.
- Support named sources such as `Player.Attack`, `Player.Hp`, `Player.Armor`, `Target.Attack`, `Event.Amount`, `Owner.BaseArmor`, and simple `multiply/add/min/max`.
- Extend `EffectAtomSchemas` so bad expressions fail at load time.

Representative content:

- `help.fireball.pending`
- `help.impact_tutorial.pending`
- `help.shield_bash_tutorial.pending`
- `skill.thorn_skin.pending`
- `skill.hard.pending`

### 2. Target Filters and Selection

Needed by effects such as:

- selected target,
- random other monster,
- random help card,
- adjacent help/monster only,
- non-player card,
- non-elite non-boss monster,
- card in item slots or board,
- card by defId/kind/zone.

Likely shape:

- Add filter fields to target atoms or introduce `Filtered`/`Selected`/`RandomCard`.
- Make all random selection use `IRngUtility`.
- Avoid hardcoding one effect name into a selector.

Representative content:

- `help.swap_card.pending`
- `help.teleport_card.pending`
- `skill.thief_claims.pending`
- `skill.unstable.pending`
- `skill.random_walk.pending`
- `help.kidnapping.pending`

### 3. Lifecycle, Prevention, and Once-Only State

Needed by effects such as:

- next damage becomes 0,
- first strike,
- no stacking,
- once per node,
- remove this relic after triggering,
- temporary bonus until battle/enemy/node changes,
- non-recursive extra trigger.

Likely shape:

- Prefer `RuleModifier` or scoped runtime marker instead of ad hoc booleans.
- Give every temporary source a `ModifierSource`/effect instance id.
- Add cleanup tests: owner removed, node ended, enemy changed, effect deactivated.

Representative content:

- `help.ward_magic_card.pending`
- `skill.stray_cub.first_strike`
- `skill.first_strike.pending`
- `skill.blessing.pending`
- `relic.gold_armor.pending`
- `skill.taunt.pending`
- `help.doubling_tower.pending`

### 4. Board Movement and Marks

Needed by effects such as:

- reverse rotation,
- extra rotation,
- chosen swap,
- random swap,
- board traps,
- blessed slots,
- all monsters count as adjacent,
- movement lock.

Likely shape:

- Keep movement as `GameAction`s.
- Add source/reason metadata to movement actions when triggers must distinguish system rotation, help-card movement, and monster-skill movement.
- Store board marks in `BoardModel` only if they are true runtime state; otherwise use `RuleModifier`.

Representative content:

- `help.rotation_wheel.pending`
- `relic.rotation_engine` design doc effect,
- `help.bear_trap.pending`
- blessing slot monster skills in the docs,
- `skill.range_expand.pending`
- movement-lock boss skill.

### 5. Reward, Choice, and Content Actions

Needed by effects such as:

- chest cards grant relic choice,
- blood conversion random reward,
- arsenal/easy road choices,
- lucky coin elite/boss reward,
- add help card to player deck or item slot,
- grant relic/player skill.

Likely shape:

- Reuse `RewardSystem` and existing content actions (`OfferRewardChoiceAction`, `GrantRelicAction`, etc.) instead of making effect atoms own UI choices.
- For player choice, emit a deterministic offered-choice event and let presentation choose later if needed.

Representative content:

- `help.common_chest_card.pending`
- `help.blue_chest_card.pending`
- `help.golden_chest_card.pending`
- `help.blood_conversion.pending`
- `skill.arsenal.node_end`
- `skill.easy_road.pending`
- `relic.lucky_coin.pending`

### 6. Counters and Source-Sensitive Events

Needed by effects such as:

- every N kills/removes/uses,
- every N damage/lost armor/lost HP,
- only when caused by a specific effect,
- do not recurse when the effect itself adds a card/stat.

Likely shape:

- Counter keys must include owner/effect/source when needed.
- Event payloads may need cause/source tags.
- Add tests for recursion prevention.

Representative content:

- `relic.junk_slot_machine.use` already proves `WeightedRandom`; extend carefully.
- `relic.junk_cycler`/kill-count effects from docs.
- `skill.learning_growth.pending`
- `skill.flame_boiling.pending`
- `skill.violence_maniac.pending`

## Accuracy Rules

- Do not classify "does damage equal to X" as a new action if it only needs a value expression.
- Do not create a new target atom if an existing target plus a reusable filter can express it.
- Do not implement a rule-changing effect as a hidden stat. Add a `RuleId` and make the affected system ask the registry.
- Do not implement "until" effects by remembering to subtract later. Use scopes, conditions, cleanup, or explicit deactivation.
- Do not let content-specific names leak into Core atoms unless the concept is truly universal.
