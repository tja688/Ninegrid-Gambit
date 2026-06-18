# File Touch Map

Where to land changes by capability type. Respect `Presentation → Core ← Content`.

## Quick Reference

| Goal | Primary files | Tests |
|:--|:--|:--|
| Convert pending → DSL | `NineGrid.Content/Catalog/TableNineContentCatalog.cs` | `P6ContentLandingTests` |
| New trigger/condition/target/action atom | `NineGrid.Core/Effects/EffectAtomLibrary.cs` | `P5EffectSystemTests` |
| Atom schema / validation | `NineGrid.Core/Effects/EffectAtomSchemas.cs` | `P5EffectSystemTests` |
| Value expression | `EffectValueExpression` (Core/Effects) | `P5EffectSystemTests` |
| Rule modifier | Rule registry + consumer query site | `P2StatPipelineTests` or `P3ActionPipelineTests` |
| New GameAction | `NineGrid.Core/Domain/Actions/*.cs` | `P3ActionPipelineTests` |
| Pipeline trigger point | `ActionPipelineSystem`, `TriggerSystem` | `P3ActionPipelineTests` |
| EventLog event | `NineGrid.Core/Domain/Events/` | pipeline/replay tests |
| Luban sync | export → `Assets/Tools/Luban/Datas/` | `P6LubanContentTests` |
| Content load/bootstrap | `ContentSystem`, `TableNineLubanCatalogFactory` | `P6LubanContentTests`, `P6R3LubanIntegrationTests` |

## Assembly Boundaries

| Assembly | May contain | Must not |
|:--|:--|:--|
| `NineGrid.Core` | Models, Systems, GameActions, effect interfaces, atom registry | UnityEngine.UI, direct content ids |
| `NineGrid.Content` | Atom implementations, catalog, Luban generated code | Presentation, scene refs |
| `NineGrid.Presentation` | View, Command senders, animation | Direct model mutation, GameAction apply |
| `NineGrid.Core.Tests` | EditMode tests | Production gameplay code |

## Catalog Changes (`TableNineContentCatalog.cs`)

Sections:

- `AddEffects` — effect DSL rows (`Impl`, `Pending`)
- `AddHelpCards` / `AddRelics` / `AddPlayerSkills` / `AddMonsterSkills`
- `AddMonsterCards` — monster → skill binding
- Helpers: `Triggered`, `Modifier`, `RuleModifier`, `Impl`, `Pending`, `PendingSkill`

Pending → implemented:

1. Add `Impl("effect.id", ...)` with full JSON
2. Point container `.AddEffect("effect.id")` at implemented id
3. Remove `Pending(...)` row or leave orphaned only if intentionally deferred

## New Atom

1. `EffectAtomLibrary.cs` — class with `[EffectAtom("Name", EffectAtomKind.*)]`
   - Triggers extend `TriggerAtomBase`
   - Actions implement action factory / `IEffectAction`
2. `EffectAtomSchemas.cs` — `ValidateRequiredFields`, `ValidateRanges`, nested graph if composite
3. `EffectAtomRegistry` — usually auto-collected; verify registration
4. `P5EffectSystemTests` — unknown atom rejection, valid DSL execution, edge cases

Do **not** embed content-specific defIds in Core atoms.

## New RuleModifier

1. Define `RuleId` / rule type in stats/rules area
2. Register via `AddRuleModifier` action or relic/skill activation path
3. Add **query** at decision point:
   - Damage: `DealDamageAction`
   - Heal multiplier: `HealAction`
   - Interaction range: `CanInteractQuery`
   - Effective stats: `StatSystem` / `EffectiveStatQuery`
4. Test: rule on/off, stacking, cleanup on deactivate

## New GameAction

1. `NineGrid.Core/Domain/Actions/{Name}Action.cs`
   - `Apply(IArchitecture)` — mutate via models only here
   - `EmitEvents()` — append to EventLog
2. Register factory if dispatched by name from atoms
3. `ActionPipelineSystem` — ensure trigger points fire PRE/POST correctly
4. `P3ActionPipelineTests` — ordering, reactions, determinism

**Never** mutate models outside `Apply()` or approved Model methods called from Actions.

## Target Selection (Presentation gap)

Core path today:

- `SelectedCards` target atom
- Tests inject selection via architecture/test harness before pipeline step
- Presentation will later send `Command` with selected uids — do not block Core landing on UI

## Luban Pipeline

```
TableNineContentCatalog.cs
    → TableNineLubanDataExporter (Editor)
    → Assets/Tools/Luban/Datas/*.json
    → gen_table_nine.ps1
    → StreamingAssets/TableNine/LubanData
    → TableNineLubanCatalogFactory → GameContentCatalog
```

Edit order: **always catalog first**, then export, then gen.

## Files to Avoid

- `*.unity` — use Unity MCP only
- Hand-editing `Generated/Luban` — regenerate instead
- Weakening `P6ContentLandingTests` pending gates without user intent

## CodeGraph Entry Points

Useful symbols for impact analysis:

- `EffectAtomLibrary`, `EffectValidator`, `EffectSystem`
- `TriggerSystem`, `ActionPipelineSystem`, `ContentSystem`
- `TableNineContentCatalog`, `P5CatalogTestSupport`
- `DealDamageAction`, `MoveCardAction`, `RuleModifierRegistry`
