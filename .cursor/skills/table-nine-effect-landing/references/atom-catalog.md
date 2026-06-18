# Atom Catalog

Registered atoms in `EffectAtomLibrary.cs` (batch-8 baseline). Refresh:

```powershell
rg -n '\[EffectAtom\("' Assets/Scripts/NineGrid.Core/Effects/EffectAtomLibrary.cs
```

Schema rules: `EffectAtomSchemas.cs` — unknown atom, missing field, nested graph, ranges.

## DSL Shape (Triggered)

```jsonc
{
  "id": "skill.example.effect",
  "typeTag": "怪物技能",
  "containerType": "MonsterSkill",
  "kind": "Triggered",
  "trigger": { "atom": "OnSelfMove", "every": 2 },
  "conditions": [{ "atom": "AtSlot", "slot": 6 }],
  "target": { "atom": "Player" },
  "action": { "atom": "DealDamage", "amount": 2, "actor": "Self" }
}
```

`Modifier` and `RuleModifier` kinds use `modifier` / `ruleModifier` blocks instead of trigger/action.

Catalog helpers in `TableNineContentCatalog.cs`: `Triggered()`, `Modifier()`, `RuleModifier()`, `Impl()`, `Pending()`.

## Triggers (17)

| Atom | Required / notable fields | Typical use |
|:--|:--|:--|
| `OnBattle` | `sourceAction`, `targetKind`, `maxActionDepth` | After fighting a card |
| `OnKill` | — | Kill enemy |
| `OnDeal` | — | Deal damage event |
| `OnEvent` | `eventType` | Generic event hook |
| `OnRemove` | — | Card removed |
| `OnUseHelpCard` | — | Help card used from item slot |
| `OnNodeStart` | — | Node begins |
| `OnNodeEnd` | — | Node ends |
| `OnRotate` | — | Board rotated |
| `OnInteract` | — | Player interaction |
| `OnSelfMove` | `every` (≥1) | Owner moves N times |
| `OnMoveToSlot` | `slot`, optional `target` | Move to grid slot |
| `OnMoveToBoardMark` | `mark`, `targetKind` | Move onto blessed/trap mark |
| `OnEnter` | — | Card enters board |
| `OnArmorBreak` | — | Armor breaks |
| `OnDamageTaken` | — | Damage taken |
| `OnFatalDamage` | — | Lethal damage |
| `OnCumulative` | `metric`, `threshold` | Cumulative resource threshold |

## Conditions (11)

| Atom | Required / notable fields | Typical use |
|:--|:--|:--|
| `AdjacentHasCard` | `defId` | Adjacent card of defId |
| `AtSlot` | `slot` | Owner at slot |
| `Adjacent` | `to` (e.g. Player) | Adjacent to entity |
| `CardZone` | `zone` | Card in zone |
| `HpBelow` | `pct` | HP threshold |
| `HasCard` | `defId` and/or `kind` and/or `zone` | Board/deck presence |
| `CardCounter` | `key`, `op`, `value` | Per-card counter |
| `SelectedOption` | `option` | Reward/choice option |
| `EventFilter` | `targetIs`, `sourceAction`, … | Event payload filter |
| `ActionSource` | `action` | Caused by action name |
| `OwnsRelicSet` | `defIds` or `defId` | Relic set owned |
| `LevelParity` | `parity` (`Even`/`Odd`) | Monster level parity |

## Targets (16)

| Atom | Required / notable fields | Typical use |
|:--|:--|:--|
| `Self` | — | Effect owner |
| `Player` | — | Player avatar |
| `EventCard` | — | Card from event context |
| `EventTarget` | — | Target from event |
| `BoardMarkEventCard` | `mark` | Card on board mark |
| `RandomMonster` | — | Random board monster |
| `FilteredCards` | `kind`, `zone`, `exclude`, `random`, `count`, `defId` | Filtered set / random pick |
| `AllMonsters` | — | All board monsters |
| `SelectedCards` | — | Player/program selected set |
| `OrthoAdjacent` | — | Orthogonal neighbors |
| `SlotCard` | `slot` | Card at slot |
| `Column` | `column` | Column cards |
| `AdjacentCard` | `defId` | Adjacent matching card |

## Actions (24)

| Atom | Required / notable fields | Typical use |
|:--|:--|:--|
| `Sequence` | `actions[]` (non-empty) | Multi-step |
| `WeightedRandom` | `choices[]` with `weight`, `action` | Random branch |
| `Repeat` | `action`, `count` | Repeat nested action |
| `Conditional` | `condition`, `then`, optional `else` | Branch |
| `DealDamage` | `amount` or `value`; `actor` | Damage |
| `Heal` | `amount` or `value` | Heal |
| `GainArmor` | `amount` or `value` | Armor |
| `ModifyGold` | `delta` | Gold change |
| `ModifyBaseStat` | `stat`, `delta` or `value`; `reason` | Card base stat |
| `OfferRewardChoice` | `poolId` | Offer reward pick |
| `GrantRewardFromPool` | `poolId` | Grant from pool |
| `GrantRelic` | `relicDefId` | Add relic |
| `GrantPlayerSkillContent` | `skillDefId` | Grant player skill |
| `Move` | `toSlot` | Move card |
| `Swap` | — | Swap cards |
| `Rotate` | `count`, optional `reverse` | Rotate board |
| `ShuffleInto` | `defId`, `kind`, `count`, `top` | Shuffle into deck |
| `MoveToDrawPile` | — | Move to draw pile |
| `Spawn` | `defId`, `kind` | Spawn card |
| `AddModifier` | modifier block | Add stat modifier |
| `AddRuleModifier` | `rule`, `value` | Add rule modifier |
| `ReplayHelpCardEffects` | optional `targetKind` | Replay help effects |
| `SetBoardMark` | `mark`, `slot` or `random` | Place board mark |
| `GrantSkill` | `skillDefId` | Grant monster skill |
| `RemoveCard` | — | Remove card |
| `DeactivateSelfEffect` | — | Turn off owning effect |

## Value Expressions

Used in `amount`/`value` on `DealDamage`, `Heal`, `GainArmor`, `ModifyBaseStat`.

Constant: `"amount": 6`  
Dynamic: `"value": { "source": "Player.Attack" }` (see `EffectValueExpression` for supported sources)

Schema validates expressions at load time.

## Compose Patterns (proven in catalog)

### Move to slot → damage player

```json
"trigger": { "atom": "OnMoveToSlot", "slot": 1, "target": "Self" },
"target": { "atom": "Player" },
"action": { "atom": "DealDamage", "amount": 2, "actor": "Self" }
```

Ref: `skill.hoodlum.slot1`

### On enter → rotate

```json
"trigger": { "atom": "OnEnter" },
"action": { "atom": "Rotate", "count": 1 }
```

Ref: `skill.turn_world.enter`

### Every N moves → random buff other monsters

```json
"trigger": { "atom": "OnSelfMove", "every": 2 },
"target": { "atom": "FilteredCards", "kind": "Monster", "zone": "Board", "exclude": ["Self"], "random": true, "count": 1 },
"action": { "atom": "WeightedRandom", "choices": [ ... ] }
```

Ref: `skill.gear_delivery.move`

### On remove → shuffle multiple defs

```json
"trigger": { "atom": "OnRemove" },
"action": { "atom": "Sequence", "actions": [
  { "atom": "ShuffleInto", "defId": "...", "kind": "Monster", "count": 1 },
  { "atom": "ShuffleInto", "defId": "...", "kind": "Monster", "count": 1 }
]}
```

Ref: `skill.fall_apart.remove`

### Rule modifier (range expand)

Use `kind: "RuleModifier"` with `AddRuleModifier` or dedicated rule block — ref `skill.range_expand.rule`.

## Nested Action Validation

`EffectAtomSchemas` recursively validates:

- `Sequence.actions[]`
- `WeightedRandom.choices[].action`
- `Repeat.action`
- `Conditional.then` / `else`

Empty nested arrays fail validation.

## Adding a New Atom Checklist

1. Implement class in `EffectAtomLibrary.cs` with `[EffectAtom("Name", EffectAtomKind.*)]`
2. Wire factory in `EffectAtomRegistry` if not auto-discovered
3. Add required/range rules to `EffectAtomSchemas.cs`
4. Add P5 validator/execution test
5. Document typical JSON in this file
6. Convert at least one catalog effect using it
