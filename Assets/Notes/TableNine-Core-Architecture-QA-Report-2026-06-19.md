# TableNine Core 架构落地深度质检报告（2026-06-19）

## 结论

**可以正式进入表现层第一阶段搭建，但不建议直接进入完整战斗演出大规模铺量。**

当前 Core 已经具备表现层开工所需的主要地基：程序集隔离、QFramework Architecture 根、核心 Model/System、Command 入口、GameAction 流水线、Trigger/Effect/Stat 三态、Luban 内容目录、事件日志、表现事件映射和 EditMode 回归测试都已经落地，并且 Unity MCP 下 `NineGrid.Core.Tests` 全量通过。

但如果按 `Assets/Notes/九宫牌局权威顶层架构设计.md` 的“正式生产级契约”要求，仍有两个 P1 级口子需要在表现层复杂化前补齐：

1. **表现批次生成/输入锁还没有自动接到 Command 执行链路。** 现在已有 `PresentationBatchFactory`、`PresentationSyncSystem`、`PresentationFinishedCommand`，但生产路径不会在每次 Command 后自动截取 EventLog、创建 batch、打开输入锁；P7 测试目前是手动调用 `sync.OpenBatch(batch)`。
2. **效果生命周期没有强制按容器离场注销。** `ContentSystem.ClearRuntimeEffects()` 已实现但未被生产路径调用；`RemoveCardAction`、`KillAction`、`SetupNodeDeckAction` 没有自动按 owner 注销效果实例。很多效果因 uid 不复用、条件失活而暂时安全，但这低于文档要求的“容器离开/被移除即失活”强度。

因此建议采用：

- **Go：** 可以开始表现层骨架、事件适配器、棋盘/卡牌视图、输入 Command 转发、批次回放序列器、调试面板。
- **Hold：** 在复杂动画、跳过/加速/中断、长链特效、跨节点表现状态缓存大规模铺开前，先补齐上面两个 P1 契约。

## 审计范围与验证方式

- 对照文档：`Assets/Notes/九宫牌局权威顶层架构设计.md`
- 项目规则：`rules.md`
- 代码范围：`Assets/Scripts/NineGrid.Core`、`Assets/Scripts/NineGrid.Content`、`Assets/Scripts/NineGrid.Presentation`、`Assets/Scripts/NineGrid.Core.Tests`
- 内容范围：`Assets/StreamingAssets/TableNine/LubanData`
- 工具：CodeGraph、`rg` 精确检索、Unity MCP 刷新/编译/测试/Console
- Unity 实例：`Ninegrid Gambit@e64551e9580a8a7a`，Unity `6000.3.10f1`

### Unity 验证结果

- `refresh_unity(mode=if_dirty, scope=all, compile=request)`：成功，刷新后 `ready_for_tools=true`
- Console 刷新后：0 error / 0 warning
- `run_tests(EditMode, assembly_names=["NineGrid.Core.Tests"])`：**119 / 119 passed，0 failed，0 skipped，耗时 13.76s**
- 测试后 Console：Unity Test Framework 有保存结果的噪声日志 `Saving results to ... TestResults.xml`，测试结果本身为 Passed

## 一页总评

| 架构项 | 文档目标 | 当前落地 | 判定 |
|---|---|---|---|
| Core 纯逻辑隔离 | Core 禁 UnityEngine / 表现 API | `NineGrid.Core.asmdef` 设置 `noEngineReferences: true`，Core/Content 未检出 UnityEngine 引用 | 通过 |
| 依赖方向 | `Presentation -> Core <- Content` | Core 仅引用 QFramework；Content 引 Core + Luban；Presentation 引 Core/Content/QFramework | 通过 |
| QFramework 根 | Architecture 注册 Model/System/Utility | `NineGridArchitecture` 注册 RNG、Log、Config、Card/Board/Deck/Player/Run/PendingChoice、Stat/Trigger/Content/Effect/Economy/Reward/PresentationSync/Pipeline/Board/Deck/Phase | 通过 |
| Model 数据层 | CardInstance 唯一真身、zone/slot、uid、Bindable | `CardRegistry` 全局 uid，`CardInstance` 有 `Zone/Slot/Stats/Counters/EffectIds`，Board/Deck/Player/Run 均在位 | 基本通过 |
| 状态变更统一 Action | System 不直接改 Model，投递 Action | 守卫测试覆盖 Systems；实际系统入口基本投递 Action；Action 内集中改 Model | 基本通过 |
| 流水线 | 队列 + 反应栈 + Pre/Post Trigger + EventLog | `ActionPipelineSystem` 有 Queue/Stack、Pre/Post Dispatch、深度优先反应、EventLog 序列号、递归深度保护 | 通过 |
| 属性管线 | Modifier / RuleModifier / Trigger 三态分离 | `StatPipeline`、`RuleModifierRegistry`、`EffectKind` 三态、Modifier/RuleModifier DSL 均落地 | 通过 |
| 效果 DSL | Trigger/Condition/Target/Action 原子 + 校验 | 18 Trigger / 15 Condition / 13 Target / 30 Action；五防线校验、组合子、动态值表达已落地 | 通过 |
| 内容管线 | Luban + 运行时 Catalog + 校验 | StreamingAssets 数据存在；Luban 目录测试断言 87 Card / 141 Effect / 68 Skill / 19 Relic；所有 effect 为 Implemented | 通过 |
| 流程系统 | PhaseSystem 合法 Command 集合 | `PhaseSystem` 覆盖 Start/Attack/Pickup/ClickEmpty/UseItem/Reward/Room/PresentationFinished；非法命令发拒绝事件 | 通过 |
| Core -> 表现契约 | EventLog 批次回放 + 输入锁 | 事件映射 39/39，Snapshot/Batch/Adapter/LogProjector 在位；生产接线未自动化 | 有 P1 缺口 |
| 生命周期 | 容器进入激活、离场失活 | 激活有；显式 `Deactivate` 有；离场自动注销不足 | 有 P1 缺口 |

## 关键落地点详查

### 1. 程序集与分层隔离

当前 asmdef 物理隔离基本符合文档铁律一：

- `Assets/Scripts/NineGrid.Core/NineGrid.Core.asmdef`
  - `references: ["QFramework"]`
  - `noEngineReferences: true`
- `Assets/Scripts/NineGrid.Content/NineGrid.Content.asmdef`
  - `references: ["NineGrid.Core", "Luban.Runtime"]`
  - `noEngineReferences: true`
- `Assets/Scripts/NineGrid.Presentation/NineGrid.Presentation.asmdef`
  - `references: ["NineGrid.Core", "NineGrid.Content", "QFramework"]`
  - 允许 UnityEngine

代码检索未发现 `NineGrid.Core` / `NineGrid.Content` 中存在 `using UnityEngine`、`MonoBehaviour`、`Coroutine`、`ScriptableObject`、`UnityEngine.UI` 等表现/引擎 API。

**偏离点：** 文档建议 “Content 实现 Core 定义的原子接口”，当前大部分具体原子在 `NineGrid.Core/Effects/EffectAtomLibrary.cs`。这让 Core 更厚，但也让测试和反射注册简单。原型期可以接受；若后续让策划扩原子频率很高，建议把“通用内核原子”和“内容特化原子”拆层。

### 2. QFramework Architecture 与入口

`NineGridArchitecture` 已经是完整 IOC 根：

- Utility：`IRngUtility`、`ILogUtility`、`IConfigUtility`
- Model：`CardRegistry`、`BoardModel`、`DeckModel`、`PlayerModel`、`RunModel`、`PendingChoiceModel`
- System：`IStatSystem`、`ITriggerSystem`、`IContentSystem`、`IEffectSystem`、`IEconomySystem`、`IRewardSystem`、`IPresentationSyncSystem`、`IActionPipelineSystem`、`IBoardSystem`、`IDeckSystem`、`IPhaseSystem`

`CoreCommands` 以 QFramework `AbstractCommand<CoreCommandResult>` 包装玩家意图，且遵守 `this.GetSystem<T>()` 项目规则。

**差距：** `CoreCommandResult` 目前只有 `Accepted/Reason/ResolvedActions`，没有携带 EventLog 增量、batchId、snapshot 或 presentation batch。表现层要么自己记录 EventLog 起始 index，要么需要新增一层 Core facade。

### 3. Model 层

文档要求的关键模型基本在位：

- `CardRegistry`：全局 uid、创建/查找/移动/移除、版本号
- `CardInstance`：`Uid/DefId/Kind/Stats/Counters/Zone/Slot/EffectIds`
- `BoardModel`：9 格、Avatar、Blessed mark、版本号
- `DeckModel`：DrawPile、Player/Enemy staging pool、ItemSlots
- `PlayerModel`：金币、互动次数、遗物/技能列表、玩家 Stats
- `RunModel`：Floor、NodeIndex、Seed、Room、Phase
- `PendingChoiceModel`：奖励/房间选择态

**风险：模型写方法仍是 public。** 这对 Core Action 很方便，但 Presentation 未来引用 Core 后也能直接 `board.PlaceCard()`、`player.AddCoins()`、`card.Stats.SetBase()`。目前 `CoreArchitectureGuard` 只扫描 `Systems` 是否绕过 Action，尚不能约束 Presentation。建议表现层开工同时补一个守卫测试：Presentation asmdef 下禁止调用 Model 写方法；中期可考虑把模型写 API 改为 `internal` 并用 Core assembly 内部访问。

### 4. GameAction 流水线

`ActionPipelineSystem` 已落地文档脊柱：

- `Queue<GameAction>` 承载主动作
- `Stack<GameAction>` 承载反应动作
- `RunToCompletion()` 单帧跑完
- `ResolveAction()` 顺序为：
  1. append `ActionStarted`
  2. dispatch Pre trigger
  3. `action.Apply()`
  4. append result events
  5. dispatch Post trigger
  6. resolve follow-up actions
  7. append `ActionFinished`
- `ResolveTriggeredActions()` 反向压栈后立即深度优先解析
- `EventLog` 为每条事件分配递增 `Sequence`
- 深度超过 64 抛异常，避免无限连锁

动作覆盖度已经超过初版清单：伤害、治疗、护甲、金币、击杀、移除、移动、旋转、交换、补牌、发牌、生成、洗入、奖励、房间、标记、规则/属性修正、效果执行等均在 Action 层。

**观察：** EventLog 是长日志，不是自动按 Command 清批次；这适合调试和回放，但表现层必须明确“从哪个 index 开始截取本次 Command 的事件”。

### 5. Trigger / Effect / DSL

效果系统是当前 Core 最强的一块。

已落地：

- `EffectKind`: `Triggered` / `Modifier` / `RuleModifier`
- `EffectAtomRegistry`: 通过 `[EffectAtom]` + 反射自动注册
- `EffectValidator`: 类型标签、必填、互斥、动词、防线、schema/range 校验
- `EffectDslNode` + 内置 JSON parser
- `EffectSystem.Activate/Deactivate/BuildTriggeredActions`
- 原子数量：
  - Trigger：18
  - Condition：15
  - Target：13
  - Action：30
- 组合子：
  - `Sequence`
  - `WeightedRandom`
  - `Repeat`
  - `Conditional`
- 代表效果测试覆盖：
  - 重新组合头/身
  - 落石累计护甲损失
  - 凤凰羽毛致命伤
  - 废物老虎机 WeightedRandom
  - 条件光环离格失效
  - 龙鳞甲、渴望、金币盔甲、嘲讽、范围扩大
  - 祝福/福地/发现弱点
  - 双倍塔重放帮助卡
  - 多批怪物复杂技能

**P1 风险：生命周期自动注销不足。**

文档 7.4 要求“容器进入生效区域激活，离开/被移除失活”。当前：

- `ContentSystem.ActivateCardEffects` 会激活卡效果，并记录 `mRuntimeEffectInstanceIds`
- `EffectSystem.Deactivate` 能撤销 Trigger 注册、StatModifier、RuleModifier
- 但生产路径没有调用 `ContentSystem.ClearRuntimeEffects`
- `RemoveCardAction/KillAction` 没有按 `OwnerUid` 自动 Deactivate
- `SetupNodeDeckAction` 清旧卡时只 `RemoveNonAvatarCards(registry)`，没有先注销旧卡效果

这会导致旧实例可能继续留在 `TriggerSystem` / `RuleModifierRegistry`。目前很多效果因为条件指向旧 uid、slot 为 None、uid 不复用而不会马上表现为错误，但它不是结构性保证。表现层开始消费事件后，这类残留最容易变成“幽灵特效/幽灵光环/调试日志混入”。

建议表现层复杂化前补：

- `EffectSystem.DeactivateByOwner(ownerUid)` 或 `ContentSystem.DeactivateCardEffects(cardUid)`
- `RemoveCardAction` / `KillAction` 对卡牌容器统一追加注销 follow-up 或在 Apply 中注销
- `SetupNodeDeckAction` 重置节点前清理非跨节点运行时效果；玩家技能/遗物保留，怪物/帮助卡清理
- 新增测试：
  - 怪物移除后，OnBattle/OnEvent 类怪物技能不再触发
  - 开新节点后，上节点怪物/帮助卡 RuleModifier/TriggerReaction 不残留
  - 道具槽帮助卡使用并移除后，其 ownerOnly 效果不再响应后续帮助卡

### 6. Stat / Rule 管线

已符合文档第 6 节：

- `StatBlock` 只存 base + modifier list
- `StatPipeline` 按 `Persistent -> Conditional -> Temporary` 求值
- `StatModifier` 带 `Stat/Op/Value/Layer/Source/Scope/Condition/ValueProvider`
- `RuleModifierRegistry` 支持 Add/Remove/RemoveBySource/ClearByScope/Consume/Evaluate
- `RuleId` 覆盖恢复倍率、敌攻修正、金币护甲、伤害倍率/平增、先攻、攻击目标限制、虚拟相邻等关键规则

测试覆盖：

- 持久 modifier
- 条件 modifier
- HP 阈值/相邻条件
- 临时 scope 清理
- 按 source 移除
- RuleModifier 增删求值
- 一次性 RuleModifier 消耗与效果反注册

**注意：** `CoreViewSnapshot` 目前展示板上卡的 HP/Armor/Attack 是 base 值，不是 effective stat。表现层如果要显示光环/条件加成后的攻击，需要新增 `EffectiveStat` 快照字段或在表现层用 Query 读有效值。

### 7. Phase / 流程系统

`PhaseSystem` 当前是 enum + switch，不是文档里建议的 FSMKit，但功能契约已覆盖：

- `StartNode`：清选择、建池、开局发牌、补格、NodeStarted、进入 InteractionLoop
- `Attack`：阶段合法性、槽位/怪物/相邻/嘲讽规则校验、先攻、攻击结算、击杀后互动旋转、通关检查
- `PickupItem`：拾取非怪物、互动期旋转
- `ClickEmpty`：空格点击触发互动旋转
- `UseItem`：道具槽 uid、卡类型、触发 UseItemAction、通关检查
- Reward/Room 尾流程：选择/跳过奖励、房间选择、进入房间、节点推进
- 输入锁：若 `PresentationSyncSystem.IsInputLocked`，只允许 `PresentationFinished`
- 非法命令：`ActionRejected` 事件 + QFramework event

核心流程测试 `P4NodeFlowTests` 覆盖了开局发牌、节点清空、金币、先攻、非法命令、道具使用、完整节点脚本、奖励尾流程、清关后拾取不重复完成等。

**偏离点：** 未使用 FSMKit，不影响当前表现层开工，但如果后续流程分支增多，enum/switch 会变厚。

### 8. Content / Luban

内容管线已经能支撑表现层读取定义与展示：

- `TableNineContentCatalog.CreateDefault()`
- `TableNineLubanCatalogFactory.CreateFromDirectory()`
- `ContentCatalogBootstrap.Load(Auto/Hardcoded/Luban)`
- StreamingAssets 中存在 Luban JSON：
  - `tablenine_tbcard.json`
  - `tablenine_tbeffect.json`
  - `tablenine_tbskill.json`
  - `tablenine_tbrelic.json`
  - 奖励、房间、节点牌组、经济等表

测试断言：

- Luban catalog：87 cards / 141 effects / 68 skills / 19 relics / 9 node deck rules
- `report.IsValid == true`
- implemented effects >= 141
- pending effects <= 0
- hardcoded catalog 与 Luban catalog key 和字段相等
- Auto source 在 StreamingAssets 可用时使用 Luban production catalog

表现层可以依赖 `defId/displayName/kind/rarity/stats/effectIds/skillIds/reward/room` 做基础 UI。

### 9. Core -> Presentation 契约

已具备：

- `CoreEventType`：39 个事件
- `PresentationEventMap`：39/39 全映射
- `PresentationInstructionKind`：用于表现适配器分发
- `PresentationBatch`：包含 batch id、sequence 范围、instructions、snapshot、是否需要 ack
- `PresentationBatchFactory.FromEventLog`
- `CoreViewSnapshotFactory.Capture`
- `ActionLogProjector`
- `RecordingPresentationAdapter`
- `PresentationSyncSystem`
- `PresentationFinishedCommand`

P7 测试证明：

- 每个 CoreEvent 都有表现映射
- batch 可锁输入，错误 batch id 不解锁，正确 `PresentationFinishedCommand` 解锁
- snapshot 暴露棋盘和 PendingChoice
- 真实 replay events 可投影为 action log 并由 stub adapter 播放
- 本地 replay fixture 能跑节点尾流程

**P1 缺口：缺生产级 “Command -> Batch -> OpenBatch” 接线。**

建议新增一个面向表现层的入口，例如：

- `CoreCommandDispatcher.Send(command)`：
  - 记录 `startIndex = pipeline.EventLog.Entries.Count`
  - `architecture.SendCommand(command)`
  - 捕获 snapshot
  - `PresentationBatchFactory.FromEventLog(eventLog, startIndex, nextBatchId, snapshot)`
  - `PresentationSyncSystem.OpenBatch(batch)`
  - 返回 `{ CoreCommandResult, PresentationBatch }`

或者扩展 `CoreCommandResult`，但前者更少侵入 QFramework Command。

## 是否能开工：分级建议

### 可以立即开始

1. `NineGrid.Presentation` 目录骨架
2. Core bootstrap / Content bootstrap
3. 输入层：点击格子、道具槽、奖励按钮、房间按钮 -> 发送 Core Command
4. 只读棋盘视图：基于 `CoreViewSnapshot`
5. 事件适配器表：`PresentationInstructionKind -> handler`
6. 简单批次回放：按 EventLog sequence 串行处理
7. Debug ActionLog 面板
8. 回放/加速/跳过按钮的第一版

### 开始前最好先补

1. 生产级 Core command dispatch facade
2. 表现层禁止直接写 Model 的守卫测试
3. Snapshot 增加 effective stats 或明确 UI 暂时显示 base stats

### 复杂演出铺量前必须补

1. 卡牌/帮助卡/怪物技能 owner 离场自动注销
2. 节点重置时 runtime effects 清理策略
3. EventLog batch 边界策略
4. 反应链异常/超深度时表现层错误恢复策略

## 风险清单

| 等级 | 风险 | 影响 | 建议 |
|---|---|---|---|
| P1 | Command 后不自动生成/打开 PresentationBatch | 表现层容易漏事件、重复播、输入锁不一致 | 先做 Core command facade |
| P1 | 效果实例不随 owner 离场自动注销 | 可能出现幽灵触发、旧 RuleModifier 残留、跨节点污染 | 增加 DeactivateByOwner + 节点清理测试 |
| P2 | Model 写 API public | Presentation 未来可能绕过 Command 改状态 | 加 Presentation guard；中期 internal 化写 API |
| P2 | Snapshot 使用 base stats | UI 可能显示与实际结算不一致的攻击/护甲加成 | 增加 effective stat 字段 |
| P2 | Content 特化原子在 Core | Core 越来越厚，后续原子扩展会触碰内核程序集 | 原型期可接受；后续分拆通用/特化原子 |
| P3 | PhaseSystem 未用 FSMKit | 状态分支增加后 switch 变厚 | 先保留；流程复杂后再迁 FSM |
| P3 | EventLog 长日志不自动分段 | 调试好用，但表现层需自己管理 startIndex | facade 内封装 batch cursor |

## 最终建议

我建议把当前节点定义为：

**Core MVP 已通过，进入 Presentation Phase 1。**

Phase 1 的验收目标不是“漂亮动画”，而是：

1. 表现层只通过 Command 改状态
2. 每次 Command 都产出一个 PresentationBatch
3. 批次播放期间 Core 输入锁生效
4. 播放完成后 `PresentationFinishedCommand` 解锁
5. UI 完全由 snapshot + EventLog 驱动
6. 至少跑通 StartNode -> Attack/Pickup/UseItem -> Reward -> Room -> EnterRoom

在 Phase 1 同步补掉 P1 两项后，就可以进入正式动画/特效/牌面资产铺量。
