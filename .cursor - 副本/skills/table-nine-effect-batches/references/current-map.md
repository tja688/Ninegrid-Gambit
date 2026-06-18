# TableNine Current Map

Snapshot basis: project files inspected on 2026-06-18 in `C:\Users\jinji\Documents\GitHub\Ninegrid Gambit`. Refresh these facts at the start of any real batch because the project is active.

## Architecture Contract

- Project: Unity 6.3 LTS, URP, 2D pixel card roguelike, QFramework.
- Source root: `Assets/Scripts`.
- Design source: `Assets/Docs`; process notes: `Assets/Notes`.
- Protected file rule: never hand-edit `.unity` scenes.
- Core architecture: `Presentation -> Core <- Content`.
- Core laws:
  - user intent enters as Command,
  - every state change is a `GameAction`,
  - effects split into `Modifier`, `RuleModifier`, `Triggered`,
  - randomness uses seeded `IRngUtility`,
  - EventLog is the replay/verification surface.

## Code Shape

CodeGraph saw 88 files under `Assets/Scripts`:

- `NineGrid.Core`: Architecture, Commands, Domain Actions/Events/Stats, Effects, Models, Queries, Systems, Utilities.
- `NineGrid.Content`: hardcoded catalog, Luban generated code, Luban catalog factory/bootstrap.
- `NineGrid.Presentation`: currently only marker.
- `NineGrid.Core.Tests`: P0-P6 EditMode tests.

Core systems already present:

- `ActionPipelineSystem`
- `TriggerSystem`
- `StatSystem`
- `EffectSystem`
- `ContentSystem`
- `BoardSystem`, `DeckSystem`, `PhaseSystem`
- `EconomySystem`, `RewardSystem`

## Effect Runtime

Current atom library in `Assets/Scripts/NineGrid.Core/Effects/EffectAtomLibrary.cs`:

- Triggers: `OnBattle`, `OnKill`, `OnRemove`, `OnUseHelpCard`, `OnNodeStart`, `OnNodeEnd`, `OnRotate`, `OnInteract`, `OnSelfMove`, `OnMoveToSlot`, `OnEnter`, `OnArmorBreak`, `OnDamageTaken`, `OnFatalDamage`, `OnCumulative`.
- Conditions: `AdjacentHasCard`, `AtSlot`, `Adjacent`, `HpBelow`, `HasCard`, `OwnsRelicSet`, `LevelParity`.
- Targets: `Self`, `Player`, `EventCard`, `RandomMonster`, `AllMonsters`, `OrthoAdjacent`, `SlotCard`, `Column`, `AdjacentCard`.
- Actions: `Sequence`, `WeightedRandom`, `Repeat`, `Conditional`, `DealDamage`, `Heal`, `GainArmor`, `ModifyGold`, `Move`, `Swap`, `Rotate`, `ShuffleInto`, `Spawn`, `AddModifier`, `GrantSkill`, `RemoveCard`, `DeactivateSelfEffect`.

`EffectValidator` already uses `EffectAtomRegistry` and `EffectAtomSchemas` for unknown atom, missing field, nested action graph, and range checks. Treat R1 as "mostly implemented; continue hardening and golden samples", not as untouched.

## Current Content Baseline

Hardcoded catalog: `Assets/Scripts/NineGrid.Content/Catalog/TableNineContentCatalog.cs`.

Runtime effect estimate from the hardcoded catalog:

| Area | Count |
|:--|--:|
| Implemented effects | 48 |
| Explicit pending effects | 38 |
| Pending monster skill placeholders | 38 |
| Estimated runtime pending effects | 76 |
| Estimated runtime total effects | 124 |

Current hardcoded content counts:

| Area | Count |
|:--|--:|
| Help cards | 27 |
| Relics in hardcoded catalog | 19 |
| Direct skill definitions before placeholder helper expansion | 31 |
| Monster cards | 60 |
| Monster decks | 6 |
| Node deck rules | 9 |
| Reward pools | 4 |
| Rooms | 5 |

Design doc effect lines:

| Doc | Effect lines |
|:--|--:|
| `Assets/Docs/九宫牌局/07-数据/帮助卡数据.md` | 22 |
| `Assets/Docs/九宫牌局/07-数据/遗物数据.md` | 58 |
| `Assets/Docs/九宫牌局/04-技能/玩家技能.md` | 7 |
| `Assets/Docs/九宫牌局/04-技能/怪物技能.md` | 81 |

Interpretation: hardcoded content is a playable subset/prototype, not the whole design. Full planning must compare design docs, hardcoded catalog, Luban rows, and tests.

## Luban Baseline

Luban note: `Assets/Notes/Luban-Content-Pipeline-Setup.md`.

Luban source rows currently present:

| File | Rows |
|:--|--:|
| `Assets/Tools/Luban/Datas/effects.json` | 5 |
| `Assets/Tools/Luban/Datas/cards.json` | 3 |
| `Assets/Tools/Luban/Datas/skills.json` | 2 |
| `Assets/Tools/Luban/Datas/relics.json` | 1 |
| `Assets/Tools/Luban/Datas/monster_decks.json` | 1 |
| `Assets/Tools/Luban/Datas/node_deck_rules.json` | 1 |
| `Assets/Tools/Luban/Datas/reward_pools.json` | 1 |
| `Assets/Tools/Luban/Datas/reward_entries.json` | 2 |
| `Assets/Tools/Luban/Datas/rooms.json` | 3 |
| `Assets/Tools/Luban/Datas/economy.json` | 1 |

Generated data path: `Assets/StreamingAssets/TableNine/LubanData`.

R3 truth: Luban pipeline and factory are connected, but Luban is still a minimal sample. Runtime full content should keep using `Hardcoded` until a batch explicitly migrates and validates a slice.

## Test Surface

Current test method counts by file:

| Test file | Tests |
|:--|--:|
| `P0ArchitectureGuardTests.cs` | 3 |
| `P0InfrastructureTests.cs` | 4 |
| `P1ModelTests.cs` | 2 |
| `P2StatPipelineTests.cs` | 6 |
| `P3ActionPipelineTests.cs` | 2 |
| `P4NodeFlowTests.cs` | 11 |
| `P5EffectSystemTests.cs` | 12 |
| `P6ContentLandingTests.cs` | 6 |
| `P6LubanContentTests.cs` | 1 |
| `P6R3LubanIntegrationTests.cs` | 4 |

Important tests already added after the older QA report:

- unknown atom/schema/range validator rejection,
- representative catalog DSL golden snapshots,
- real catalog DSL for recombine head, falling rocks, phoenix feather, slot machine, wood set, dragon scale, craving,
- R3 bootstrap/Luban sample behavior.

## Useful Refresh Commands

Inventory:

```powershell
rg -n "Pending\(c,|PendingSkill\(" "Assets/Scripts/NineGrid.Content/Catalog/TableNineContentCatalog.cs"
rg -n "效果【类型" "Assets/Docs/九宫牌局/07-数据" "Assets/Docs/九宫牌局/04-技能"
rg -n "\[EffectAtom\]|public sealed class .*Trigger|public sealed class .*Condition|public sealed class .*Target|public sealed class .*Action" "Assets/Scripts/NineGrid.Core/Effects/EffectAtomLibrary.cs"
```

Validation:

```powershell
.\Assets\Notes\CI\check-core-guards.ps1
.\Assets\Tools\Luban\gen_table_nine.ps1
.\Assets\Notes\CI\run-core-tests.ps1
```

Use Unity MCP tests when available instead of relying only on shell scripts.
