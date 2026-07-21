# Effects — 效果 DSL 与执行

源码根：`Effects/`。内容效果以 JSON 描述，解析为 `EffectDefinition`，经原子注册表实例化，由 `EffectSystem` 激活并挂到 `TriggerSystem` 或 Modifier 管线。

## 1. 执行入口

```text
IContentSystem.Activate*(json from Catalog)
  → IEffectSystem.ParseJson / Validate / Activate(definition, EffectOwner)
       ├─ Triggered → 注册 ITriggerSystem 或 OnActivate 立即 Enqueue
       ├─ Modifier  → 解析 Target，写入卡 StatModifier
       └─ RuleModifier → 写入 RuleModifierRegistry

ActionPipeline ResolveAction
  → ITriggerSystem.Dispatch
       → EffectTriggerReaction → CanTrigger → BuildTriggeredActions
            → ExecuteEffectAction 或直接 FollowUp GameAction
```

公开接口：`IEffectSystem`（`ParseJson`, `Validate`, `Activate`, `Deactivate*`, `BuildTriggeredActions`, `Clear`, `AtomRegistry`, `Validator`, `Instances`）。

## 2. 类型总览

| 相对路径 | 类型名 | 一句话职责 | 关键依赖 |
| --- | --- | --- | --- |
| `Effects/EffectEnums.cs` | `EffectKind` | Triggered / Modifier / RuleModifier | Definition |
| 同上 | `EffectContainerType` | Relic / MonsterSkill / PlayerSkill / HelpCard | typeTag 校验 |
| 同上 | `EffectAtomKind` | Trigger / Condition / Target / Action | 注册表 |
| 同上 | `EffectAtomAttribute` | 标记原子类 `(id, kind)` | 反射发现 |
| `Effects/EffectDslNode.cs` | `EffectDslNode` | 轻量 JSON 树节点 | Parser |
| 同上 | `EffectJson` | 内置 JSON Parser（无外部库） | — |
| `Effects/EffectDefinition.cs` | `EffectDefinition` | id/kind/container/trigger/target/action/modifier… | Parser |
| 同上 | `EffectOwner` | 容器类型 + sourceDefId + ownerUid | Instance |
| 同上 | `EffectDefinitionParser` | JSON → Definition | — |
| 同上 | `EffectValidationIssue` / `EffectValidationResult` / `EffectValidator` | 结构与动词校验 + GoldenSnapshot | AtomSchemas |
| `Effects/EffectAtoms.cs` | `IEffectAtom` / `ITrigger` / `ICondition` / `ITarget` / `IAction` | 原子接口 | Runtime |
| 同上 | `EffectBuildContext` | 构建期上下文 | Condition→StatCondition |
| 同上 | `EffectAtomRegistry` | 反射 Discover + Create* | AppDomain |
| `Effects/EffectAtomSchemas.cs` | `EffectAtomSchemas` (internal) | 原子字段/范围/嵌套图校验 | Validator |
| `Effects/EffectRuntimeContext.cs` | `EffectRuntimeContext` | 触发期访问 Model/事件/Owner | Systems |
| `Effects/EffectSystem.cs` | `IEffectSystem` / `EffectSystem` / `EffectInstance` | 生命周期与门禁 | Trigger/Stat/Pipeline |
| 同上 | `AppliedStatModifier` (internal) | 失活时移除卡修饰 | — |
| `Effects/EffectOwnerScopeGate.cs` | `EffectOwnerScopeGate` (internal) | 卡牌挂载效果本卡 scope 二验 | CanTrigger |
| `Effects/FragmentRecombineDedup.cs` | `FragmentRecombineDedup` (internal) | 无头/骷髅头合并批次去重 | TriggerContext |
| `Effects/EffectAtomLibrary.cs` | 全部具体原子类 | 见下节原子表 | GameAction / Stats |

## 3. Definition JSON 形状（代码解析字段）

根对象字段：`id`, `typeTag`, `verb`, `kind`, `containerType`, `trigger`, `target`, `action`, `modifier`, `ruleModifier`, `conditions[]` 或单数 `condition`。

`typeTag` 期望值（与 container 匹配）：

| ContainerType | typeTag |
| --- | --- |
| Relic | `【类型遗物】` |
| MonsterSkill | `【类型怪物技能】` |
| PlayerSkill | `【类型玩家技能】` |
| HelpCard | `【类型帮助卡】` |

`EffectValidator` 另校验：Triggered 需 trigger+action；Modifier 需 target+modifier；RuleModifier 需 ruleModifier；互斥字段；Relic/PlayerSkill 禁止 verb=`Use`；HelpCard 禁止 verb=`Equip`。

## 4. 原子 / 组合方式

### 4.1 注册

`EffectAtomRegistry.DiscoverLoadedAssemblies()`：扫描已加载程序集中带 `[EffectAtom]` 的非抽象类型，按 Kind 落入 Triggers/Conditions/Targets/Actions 字典。

创建：读节点 `atom` 或 `type` 字段 → `Activator` + `Configure(node)`。Target 缺省为 `{ atom: "Self" }`。

### 4.2 组合 Action 原子

| 原子 ID | 类 | 组合语义 |
| --- | --- | --- |
| `Sequence` | `SequenceEffectAction` | 顺序执行子 action 列表 |
| `WeightedRandom` | `WeightedRandomEffectAction` | 加权随机一支 |
| `Repeat` | `RepeatEffectAction` | 重复 |
| `Conditional` | `ConditionalEffectAction` | 条件分支 |

叶子 Action 映射到 Domain `GameAction`（`DealDamage`, `Heal`, `Move`, `Spawn`…）。

### 4.3 已注册原子清单（`EffectAtomLibrary.cs`）

**Trigger（19）**  
`OnBattle`, `OnKill`, `OnDeal`, `OnEvent`, `OnRemove`, `OnUseHelpCard`, `OnActivate`, `OnNodeStart`, `OnNodeEnd`, `OnRotate`, `OnInteract`, `OnSelfMove`, `OnMoveToSlot`, `OnMoveToBoardMark`, `OnEnter`, `OnArmorBreak`, `OnDamageTaken`, `OnFatalDamage`, `OnCumulative`

**Target（13）**  
`Self`, `Player`, `EventCard`, `EventTarget`, `BoardMarkEventCard`, `RandomMonster`, `FilteredCards`, `AllMonsters`, `SelectedCards`, `OrthoAdjacent`, `SlotCard`, `Column`, `AdjacentCard`

**Condition（15）**  
`AdjacentHasCard`, `AtSlot`, `Adjacent`, `CardZone`, `HpBelow`, `StatAtLeast`, `HasCard`, `CardCounter`, `TargetCount`, `BoardMarkCount`, `SelectedOption`, `EventFilter`, `ActionSource`, `OwnsRelicSet`, `LevelParity`

**Action（28）**  
`Sequence`, `WeightedRandom`, `Repeat`, `Conditional`, `DealDamage`, `Heal`, `GainArmor`, `TransferArmor`, `ModifyGold`, `ModifyBaseStat`, `OfferRewardChoice`, `GrantRewardFromPool`, `GrantRelic`, `GrantPlayerSkillContent`, `Move`, `Swap`, `Rotate`, `ShuffleInto`, `MoveToDrawPile`, `Spawn`, `ShuffleRandomContent`, `ExchangeWithDrawPile`, `ForceBattle`, `AddModifier`, `AddRuleModifier`, `ReplayHelpCardEffects`, `SetBoardMark`, `GrantSkill`, `RemoveCard`, `DeactivateSelfEffect`

另有抽象基类 `TriggerAtomBase`（无 Atom 属性）。

## 5. `CanTrigger` 门禁链（代码顺序）

1. Trigger/Action 非空  
2. `IsCardOwnedEffectInTriggerableZone` — MonsterSkill/HelpCard：坟场/移除/抽牌堆一般禁止（本卡 OnRemove 本帧例外）；Board 允许；ItemSlots 仅帮助卡特定触发  
3. `EffectOwnerScopeGate.Passes` — 本卡 scope / 显式全局声明  
4. `Trigger.Matches`  
5. 全部 `Condition.IsMet`  
6. `FragmentRecombineDedup.Passes` — 仅重组头/体效果  

失活：`Deactivate` 注销 Trigger、移除 Stat/Rule Modifier、从 `CardInstance.EffectIds` 去掉定义 ID。

## 6. 公开 / 内部 / 未完成

| 类别 | 内容 |
| --- | --- |
| 公开 | `IEffectSystem`, `EffectInstance`, `EffectDefinition*`, `EffectDslNode`, 原子接口与全部 Atom 类, `EffectRuntimeContext`, 枚举 |
| 内部 | `EffectOwnerScopeGate`, `FragmentRecombineDedup`, `EffectAtomSchemas`, `AppliedStatModifier`, `EffectTriggerReaction`（EffectSystem 内） |
| 未完成痕迹 | 源码无 TODO；内容侧用 `ContentImplementationState.PendingAtom` / `RawDesignOnly` 跳过激活；`MonsterSkill`+`OnBattle` 校验要求 conditions 含 EventFilter/AtSlot |

## 7. 文件清单

```
Effects/EffectAtomLibrary.cs
Effects/EffectAtoms.cs
Effects/EffectAtomSchemas.cs
Effects/EffectDefinition.cs
Effects/EffectDslNode.cs
Effects/EffectEnums.cs
Effects/EffectOwnerScopeGate.cs
Effects/EffectRuntimeContext.cs
Effects/EffectSystem.cs
Effects/FragmentRecombineDedup.cs
```
