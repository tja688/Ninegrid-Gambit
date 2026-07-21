# NineGrid.Core 内核总览

> 源码根：`Assets/Scripts/NineGrid.Foundation/NineGrid.Core/`  
> 本文档仅依据源码事实整理，覆盖程序集内全部 66 个 `.cs` 文件。

## 1. 模块职责边界

`NineGrid.Core` 是无 Unity 引擎依赖的纯逻辑内核（`noEngineReferences: true`），基于 **QFramework** 的 `Architecture` / `ISystem` / `IModel` / `ICommand` / `IQuery` / `IUtility` 组装。

| 边界 | 内核负责 | 内核不负责 |
| --- | --- | --- |
| 对局规则 | 相位门禁、攻击/拾取/用牌、清场、房间与奖励流转 | UI、动画、输入设备 |
| 状态 | Model 上的牌/盘/牌库/玩家/Run/待选 | 场景物体、Sprite、Prefab |
| 效果 | JSON DSL 解析、原子注册、触发与 Action 产出 | 内容表的外部装载实现（由外部写入 `IConfigUtility`） |
| 表现契约 | `EventLog` → `PresentationBatch` + 快照；输入锁 | 实际播放与 Ack 时机的导演逻辑（消费方在 Core 外） |

**黑盒对外表面（建议消费方只碰这些）：**

1. `NineGridArchitecture` — 挂接与测试重置  
2. `InitialGameFactory` / `ProfessionCatalog` — 开局装配  
3. `Commands/*` + `CoreCommandDispatcher` — 玩家/导演指令入口  
4. `IPhaseSystem` 公开命令语义（也可经 Command 间接调用）  
5. `Queries/EffectiveStatQuery`、`CoreViewSnapshotFactory` — 读模型  
6. `IPresentationSyncSystem` + `PresentationFinishedCommand` — 表现批次回执  
7. `Content/GameContentCatalog` 经 `IConfigUtility` 注入后由 `IContentSystem` 消费  

内部实现（Action 管线、各 System、Effect 原子、Domain Actions）对表现层应视为实现细节。

## 2. 程序集依赖（`.asmdef`）

文件：`NineGrid.Core.asmdef`

| 字段 | 值 |
| --- | --- |
| `name` | `NineGrid.Core` |
| `rootNamespace` | `NineGrid.Core` |
| `references` | 仅 `QFramework` |
| `noEngineReferences` | `true` |
| `autoReferenced` | `true` |
| 平台限制 | 无 |

结论：内核不引用 UnityEngine、也不引用项目内其他程序集；内容目录与表现层通过接口/`IConfigUtility` 注入。

## 3. 顶层数据流（代码事实）

```text
外部 → CoreCommandDispatcher.Send(ICommand)
         → Architecture.SendCommand
              → IPhaseSystem / IPresentationSyncSystem
                   → IActionPipelineSystem（Enqueue / RunToCompletion）
                        → GameAction.Apply → EventLog
                        → ITriggerSystem.Dispatch → 反应 Action（反应栈）
                   → PresentationBatchFactory.FromEventLog + CoreViewSnapshot
                   → IPresentationSyncSystem.OpenBatch（Accepted 时）
外部 → PresentationFinishedCommand → FinishBatch(batchId)
```

效果路径：`IContentSystem.Activate*` → `IEffectSystem.Activate` → 注册 `ITriggerSystem` 或写入 Modifier → 触发时 `ExecuteEffectAction` / 直接 FollowUp `GameAction`。

## 4. 目录路由表

| 子目录 | 文档 | 职责一句话 |
| --- | --- | --- |
| `Architecture/` | [架构与QFramework入口](Architecture/架构与QFramework入口.md) | `NineGridArchitecture.Init` 注册 Utility/Model/System |
| `Domain/` | [领域模型](Domain/领域模型.md) | 枚举、SlotId、CardDraft、Actions、Events、Stats、CommandResult |
| `Systems/` | [系统一览](Systems/系统一览.md) | 管线、相位、盘面、牌库、内容、经济、奖励、属性、触发、交战域 |
| `Effects/` | [效果DSL与执行](Effects/效果DSL与执行.md) | DSL 节点、原子库、注册、门禁、执行 |
| `Models/` | [运行时模型](Models/运行时模型.md) | 卡实例注册表、盘、牌库、玩家、Run、待选、交战上下文 |
| `Presentation/` | [内核表现桥](Presentation/内核表现桥.md) | 事件映射、批次、快照、命令分发器、同步锁 |
| `Commands/` + `Queries/` | [命令与查询](Commands-Queries/命令与查询.md) | QFramework Command/Query 入口 |
| `Setup/` | [初始化工厂](Setup/初始化工厂.md) | `InitialGameFactory`、职业表 |
| `Content/` | （本 README §5） | 内容目录 POCO / 配置键；无独立 System |
| `Utilities/` | （本 README §6） | RNG / Log / Config 三个 `IUtility` |

## 5. Content/（无独立文档）

仅含 `ContentDefinitions.cs`：定义 `GameContentCatalog` 及卡/技能/遗物/奖励池/房间/经济/节点规则等数据结构。运行时由外部 `IConfigUtility.Set(ContentConfigKeys.DefaultCatalog, catalog)` 注入，`IContentSystem` / `IRewardSystem` / `IEconomySystem` 读取。

### 文件路由

| 相对路径 | 类型名 | 一句话职责 | 关键依赖 |
| --- | --- | --- | --- |
| `Content/ContentDefinitions.cs` | `ContentConfigKeys` | 配置键常量 `tableNine.defaultCatalog` | `IConfigUtility` |
| 同上 | `ContentRarity` / `ContentImplementationState` / `MonsterDeckKind` | 稀有度、实现状态、怪物牌组种类枚举 | — |
| 同上 | `ContentStatLine` | 内容侧基础属性行 | — |
| 同上 | `ContentEffectDefinition` | 效果 JSON + 实现状态 + 设计原文 | `EffectContainerType` |
| 同上 | `CardContentDefinition` | 卡牌内容定义（属性、效果/技能引用） | `CardKind` |
| 同上 | `SkillContentDefinition` | 技能内容定义 | `EffectContainerType` |
| 同上 | `RelicContentDefinition` | 遗物内容定义 | `ContentRarity` |
| 同上 | `RewardEntry` / `RewardPoolDefinition` | 奖励池条目与池 | `CardKind` |
| 同上 | `MonsterDeckDefinition` / `NodeDeckRule` | 怪物牌组与节点生成规则 | `MonsterDeckKind` |
| 同上 | `RoomDefinition` | 房间结算参数 | `RoomKind` |
| 同上 | `EconomyConfig` | 经济常量默认值 | `EconomySystem` |
| 同上 | `RewardConfig` | 池/房间/节点规则容器 | `RewardSystem` |
| 同上 | `GameContentCatalog` | 总目录：Cards/Skills/Relics/Effects/MonsterDecks/Rewards/Economy | `ContentSystem` |

## 6. Utilities/（无独立文档）

| 相对路径 | 类型名 | 一句话职责 | 关键依赖 |
| --- | --- | --- | --- |
| `Utilities/RngUtility.cs` | `IRngUtility` / `RngState` / `DeterministicRngUtility` | 可复现 xorshift 风格 RNG | `NineGridArchitecture` 注册 |
| `Utilities/LogUtility.cs` | `ILogUtility` / `InMemoryLogUtility` / `CoreLogLevel` / `CoreLogEntry` | 内存日志（内核内几乎未被业务调用） | Architecture 注册 |
| `Utilities/ConfigUtility.cs` | `IConfigUtility` / `InMemoryConfigUtility` | 类型化键值配置仓 | `ContentSystem.TryReloadFromConfig` |

## 7. 公开 API / 内部实现 / 未完成痕迹

### 公开 API（消费方）

- `NineGridArchitecture`、`InitialGameFactory`、`ProfessionCatalog`
- `NineGrid.Core.Commands.*`、`CoreCommandDispatcher`、`CoreCommandResult`
- `EffectiveStatQuery`、`CoreViewSnapshot` / `CoreViewSnapshotFactory`
- `IPresentationSyncSystem` 与 `Evt_PresentationBatch*`
- 各 `I*System` 接口（测试与高级编排可用）

### 内部实现

- `Domain/Actions/*`、`EffectAtomLibrary` 中全部原子、`ActionPipelineSystem` 反应栈细节
- `EffectOwnerScopeGate`、`FragmentRecombineDedup`（`internal`）
- `HelpCardGrantRouting` / `RewardGrantActionSupport`（`internal`）

### 代码中可见的未完成 / 局限痕迹

| 位置 | 事实 |
| --- | --- |
| `PhaseSystem` 注释 | `StatId.InteractionRange` / 职业范围「尚未接入」；邻接仍用 `IBoardSystem.AreAdjacent` |
| `EconomyConfig.DiscardRelicGold` / `SkipRelicChoiceGold` | 字段存在；`IEconomySystem.AwardSkipRelicChoice` 有实现，但 `PhaseSystem` 无对应命令入口 |
| `NodeDeckOptions.CreateDefaultBattle` | 硬编码占位 draft（无 Catalog 时的默认战斗牌） |
| `ProfessionCatalog` | 注释写明「首版唯一职业（小丑）」 |
| 效果原子 | 无 `TODO`/`NotImplemented` 标记；未实现内容靠 `ContentImplementationState.PendingAtom` / `RawDesignOnly` 跳过激活 |

## 8. 文件清单（全部 .cs）

```
Architecture/NineGridArchitecture.cs
Commands/CoreCommands.cs
Content/ContentDefinitions.cs
Domain/Actions/BattleScopeActions.cs
Domain/Actions/BoardDeckActions.cs
Domain/Actions/ContentActions.cs
Domain/Actions/CoreActions.cs
Domain/Actions/EffectActions.cs
Domain/Actions/GameAction.cs
Domain/Actions/PhaseActions.cs
Domain/CardDraft.cs
Domain/Commands/CoreCommandResult.cs
Domain/CoreConstants.cs
Domain/CoreEnums.cs
Domain/Events/CoreGameEvent.cs
Domain/Events/EventLog.cs
Domain/ModifierSource.cs
Domain/SlotId.cs
Domain/Stats/RuleModifierRegistry.cs
Domain/Stats/StatArmorUtility.cs
Domain/Stats/StatBlock.cs
Domain/Stats/StatConditions.cs
Domain/Stats/StatModifier.cs
Domain/Stats/StatPipeline.cs
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
Models/BattleContextModel.cs
Models/BoardModel.cs
Models/CardInstance.cs
Models/CardRegistry.cs
Models/DeckModel.cs
Models/HelpCardStackEntry.cs
Models/PendingChoiceModel.cs
Models/PlayerModel.cs
Models/RunModel.cs
Presentation/ActionLogProjector.cs
Presentation/CoreCommandDispatcher.cs
Presentation/CoreViewSnapshot.cs
Presentation/PresentationBatch.cs
Presentation/PresentationEventMap.cs
Presentation/PresentationSyncEvents.cs
Presentation/PresentationSyncSystem.cs
Queries/EffectiveStatQuery.cs
Setup/InitialGameFactory.cs
Setup/ProfessionCatalog.cs
Systems/ActionPipelineSystem.cs
Systems/BattleScopeSystem.cs
Systems/BoardSystem.cs
Systems/ContentSystem.cs
Systems/DeckSystem.cs
Systems/EconomySystem.cs
Systems/PhaseSystem.cs
Systems/RewardSystem.cs
Systems/StatSystem.cs
Systems/TriggerSystem.cs
Utilities/ConfigUtility.cs
Utilities/LogUtility.cs
Utilities/RngUtility.cs
NineGrid.Core.asmdef
```

共 **66** 个 C# 源文件 + 1 个 asmdef。
