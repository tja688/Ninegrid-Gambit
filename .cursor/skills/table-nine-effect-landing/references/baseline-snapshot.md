# TableNine Baseline Snapshot

Static facts from batch-8 closeout (2026-06-18). Refresh dynamic counts at the start of any landing task.

## Architecture Contract

- Project: Unity 6.3 LTS, URP, 2D card roguelike **TableNine**, QFramework.
- Source root: `Assets/Scripts`.
- Design docs: `Assets/Docs`; process notes: `Assets/Notes`.
- Protected: never hand-edit `.unity` scenes.
- Layering: `Presentation → Core ← Content`.
- Core laws:
  - user intent enters as `Command`
  - every state change is a `GameAction` on one pipeline
  - effects split into `Modifier`, `RuleModifier`, `Triggered`
  - RNG uses seeded `IRngUtility`
  - `EventLog` is the verification/replay surface

Authoritative architecture: `Assets/Notes/九宫牌局权威顶层架构设计.md`

## Content Baseline (batch 8)

| Item | Count |
|:--|--:|
| effects total | 133 |
| implemented effects | 100 |
| pending effects | 32 |
| cards | 87 |
| skills | 68 |
| relics | 19 |
| monster decks | 6 |
| node deck rules | 9 |
| reward pools | 8 |
| rooms | 5 |

Pending breakdown:

| Container | Pending |
|:--|--:|
| MonsterSkill | 24 |
| HelpCard | 6 |
| Relic | 1 |
| PlayerSkill | 1 |

Full pending list and capability clusters: `references/pending-gaps.md`  
Closeout report: `Assets/Notes/TableNine-Effect-Batch-8-Final-Landing-Report.md`

## Content Pipeline

| Stage | Location |
|:--|:--|
| Authoritative catalog | `Assets/Scripts/NineGrid.Content/Catalog/TableNineContentCatalog.cs` |
| Luban source (exported) | `Assets/Tools/Luban/Datas/*.json` |
| Luban generated code | `Assets/Scripts/NineGrid.Content/Generated/Luban` |
| Luban runtime data | `Assets/StreamingAssets/TableNine/LubanData` |
| Exporter | `TableNineLubanDataExporter` — menu `TableNine/Content/Export Hardcoded Catalog To Luban Datas` |
| Generator | `.\Assets\Tools\Luban\gen_table_nine.ps1` |
| Factory | `TableNineLubanCatalogFactory` |

Workflow: edit catalog → export → gen → parity tests must stay green.

## Effect Runtime

Atom registry: `Assets/Scripts/NineGrid.Core/Effects/EffectAtomLibrary.cs`  
Schema validation: `Assets/Scripts/NineGrid.Core/Effects/EffectAtomSchemas.cs`  
Catalog validation: `ContentSystem.ValidateCatalog()` via `EffectValidator`

Approximate atom counts (refresh with grep):

- Triggers: 17
- Conditions: 11
- Targets: 16
- Actions: 24

Full inventory: `references/atom-catalog.md`

## Core Systems Present

- `ActionPipelineSystem`, `TriggerSystem`, `StatSystem`, `EffectSystem`, `ContentSystem`
- `BoardSystem`, `DeckSystem`, `PhaseSystem`, `EconomySystem`, `RewardSystem`

## Test Surface

| File | Role |
|:--|:--|
| `P0ArchitectureGuardTests` | asmdef / layering guards |
| `P2StatPipelineTests` | Modifier / stat pipeline |
| `P3ActionPipelineTests` | GameAction pipeline |
| `P5EffectSystemTests` | atom + validator behavior |
| `P6ContentLandingTests` | catalog DSL + pending gates |
| `P6LubanContentTests` | Luban load + hardcoded parity |

Batch-8 gate: `PendingEffectIds.Count <= 32`, implemented `>= 100`.  
EditMode suite: 98+ tests (refresh after changes).

## Refresh Commands

```powershell
rg -n "Pending\(|PendingSkill\(" "Assets/Scripts/NineGrid.Content/Catalog/TableNineContentCatalog.cs"
rg -n "\[EffectAtom\(" "Assets/Scripts/NineGrid.Core/Effects/EffectAtomLibrary.cs"
rg -n "AssertImplemented|Assert\.LessOrEqual\(report\.PendingEffectIds" "Assets/Scripts/NineGrid.Core.Tests"
```

Validation:

```powershell
.\Assets\Notes\CI\check-core-guards.ps1
.\Assets\Tools\Luban\gen_table_nine.ps1
.\Assets\Notes\CI\run-core-tests.ps1
```

Prefer Unity MCP compile + EditMode tests when the editor is available.

## Interpretation Notes

- Hardcoded/Luban catalog is a **playable prototype subset**, not the full design doc corpus.
- `PendingAtom` rows are intentional gaps — do not hide them without executable DSL.
- Presentation/UI does not yet own target selection or reward UI; Core tests inject selection programmatically.
