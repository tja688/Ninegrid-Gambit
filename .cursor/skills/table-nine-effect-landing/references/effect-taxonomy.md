# Effect Taxonomy

Classify every effect before coding or analyzing. Goal: one correct lane, minimal new surface, reusable atoms.

## Three-State Decision

Classify every effect into **exactly one** primary state:

| State | Use when | Runtime carrier |
|:--|:--|:--|
| `Modifier` | Changes a numeric stat while active | `StatModifier` via `StatSystem` |
| `RuleModifier` | Changes a rule or decision point, not a card stat panel | `RuleModifierRegistry` + explicit query at decision site |
| `Triggered` | Fires on an event and performs actions | `TriggerSystem` → enqueue `GameAction`s |

Examples:

- 攻击+1 → `Modifier`
- 处于格6攻击+2 → conditional `Modifier` (`AtSlot` condition)
- 恢复翻倍 → `RuleModifier`
- 每移动3次洗入一张卡 → `Triggered`
- 下一次伤害为0 → `RuleModifier` or scoped prevention + cleanup; never fake as negative damage

## Classification Template

Write this before editing code (also use in compose-analysis mode):

```text
Effect id:
Source doc:
Container: HelpCard | Relic | PlayerSkill | MonsterSkill
Lifecycle: on use | while equipped | while on board | node start/end | once | permanent
Primary state: Modifier | RuleModifier | Triggered
Trigger:
Conditions:
Target selection:
Value expression:
Actions:
Needs new capability: yes/no — if yes, which cluster?
Can reuse current atoms: list
Similar implemented reference:
Test case name:
```

## Compose-First Principles

Apply before proposing a new atom:

| Need | Prefer | Avoid |
|:--|:--|:--|
| Fixed damage/heal/armor | `amount` on `DealDamage`/`Heal`/`GainArmor` | New `DealNDamageAtom` |
| Dynamic amount | `value` expression (`Player.Attack`, etc.) | Content-named action |
| Filtered/random target | `FilteredCards` + `kind`/`zone`/`exclude`/`random` | New target per effect |
| Player-selected target | `SelectedCards` (test via programmatic selection) | Hardcoded defId in Core |
| Rule rewrite | `AddRuleModifier` + `RuleId` query in system | Hidden stat hack |
| Branching | `Conditional` action | Custom trigger logic in content |
| Random choice of actions | `WeightedRandom` | Multiple duplicate effects |
| Multi-step | `Sequence` | One mega-action |
| Multi-slot same behavior | Multiple effect rows (`skill.air_strike.slot1`…) | One trigger with slot array |
| Until battle/node ends | `Scope` on Modifier or `DeactivateSelfEffect` | Manual subtract later |

## Capability Clusters (remaining gaps)

Use when composition fails. Prefer extending an existing cluster over one-off code.

### 1. Dynamic value expressions

Effects needing damage/heal/stat = f(player, target, event, lost resources):

- `help.brutality_card.pending` — attack multiplier until battle end
- `skill.hard.pending`, `skill.bloodthirst.pending`, `skill.throw_stone.pending`
- `skill.even_hatred.pending` — parity-conditioned multiplier

Shape: extend `EffectValueExpression` + action `value` field; add cleanup scope for temporary multipliers.

### 2. Target filters and selection

- `help.kidnapping.pending` — non-elite, non-boss filter
- `help.watchtower.pending` — zone branch (board vs item slots)
- `help.armor_breaking_hammer.pending` — selected target + armor reduce
- `skill.sacrifice.pending`, `skill.flame_breath.pending` — defId/elite batch remove

Shape: extend `FilteredCards` filters (`tier`, `defId`, `excludeElite`) before new Target atoms.

### 3. Lifecycle, prevention, scoped cleanup

- `help.brutality_card.pending` — revert after battle
- `skill.orc_tactics.pending`, `skill.rascality.pending` — buff while adjacent, remove on leave

Shape: conditional `Modifier` with adjacency condition, or scoped temp Modifier + cleanup on `OnRemove`/node end.

### 4. Board movement, marks, movement metadata

- `help.rolling_stone.pending` — move-to-slot trigger on help card
- `skill.hot_observation.pending` — distinguish movement source
- `skill.relentless_chase.pending`, `skill.fight_me.pending` — force battle

Shape: movement `sourceAction`/`reason` on triggers; possible new `ForceBattleAction` GameAction.

### 5. Armor read / transfer / global listeners

- `relic.heavy_armor.pending` — base armor at node start
- `skill.absorb_stone.pending`, `skill.swallow_stone.pending` — armor transfer
- `skill.stone_lover.pending` — global armor-loss listener
- `skill.stone_shelter.pending` — damage reduction when others hurt

Shape: `OnCumulative`/`OnEvent` for armor metrics; transfer as `Sequence` of read + `GainArmor`/`ModifyBaseStat` once expression supports it.

### 6. Counters, cause tags, recursion guards

- `skill.stocking.pending` — cumulative adjacent player count
- `skill.find_weakness.pending` — blessed count + one-shot damage
- `skill.smart.pending` — `EventFilter.targetIs` for recursion stop
- `skill.fire_power.pending` — count cards modifier

Shape: `CardCounter`/`OnCumulative` + `EventFilter`; extend event payloads before new triggers.

### 7. Cross-zone content actions

- `skill.delivery.pending` — board ↔ deck exchange
- `skill.fracture_fall_apart.pending`, `skill.mixed_bones.pending`, `skill.otherworld_help.pending` — random deck/level shuffle-in

Shape: compose `Move`/`ShuffleInto`/`Spawn` with filters; may need deck-level random source.

## Container-Specific Reminders

From `Assets/Docs/九宫牌局/01-机制规则/效果类型隔离规范.md`:

| Container | Key rules |
|:--|:--|
| HelpCard | `[使用时]` vs `[场上]` vs `[道具牌格]` — different triggers and lifecycles |
| MonsterSkill | Counters bind to owning card uid; all effects gone on remove |
| PlayerSkill | Passive only; affects player/global rules, not monster stat panels |
| Relic | No cost/uses; event-triggered or always-on |

## Accuracy Rules

- Do not classify "damage = X" as a new action if only a value expression is missing.
- Do not create a new target atom if `FilteredCards`/`SelectedCards` + filters suffice.
- Do not implement rule changes as hidden stats — use `RuleModifier` + `RuleId`.
- Do not implement "until" effects by subtracting later — use scope, conditions, deactivation.
- Do not leak content-specific names into Core atoms unless the concept is universal.
- Do not clear `PendingAtom` by weakening `EffectAtomSchemas` or skipping activation tests.

## Golden Implemented References

| Pattern | Reference effect ids |
|:--|:--|
| On use help card damage | `help.fireball.use`, `help.throwing_knife.use` |
| Value = player attack | `help.fireball.use` (`value`) |
| Selected target move | `help.swap_card.use`, `help.teleport_card.use` |
| Rule modifier | `skill.range_expand.rule`, `relic.gold_armor.rule`, `skill.taunt.rule` |
| OnSelfMove + filter + random buff | `skill.gear_delivery.move` |
| Multi-slot move trigger | `skill.air_strike.slot*`, `skill.stone_growth.slot*` |
| OnRemove shuffle | `skill.fall_apart.remove` |
| Weighted random | `relic.junk_slot_machine.use` |
| Board mark | `skill.guide.create_blessed`, `help.bear_trap.use` |
| Reward choice | `help.common_chest_card.use`, `skill.easy_road.node_end` |
