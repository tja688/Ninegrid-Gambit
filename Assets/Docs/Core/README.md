# NineGrid.Core 权威代码文档（总览）

> 程序集：`NineGrid.Core`，路径 `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/`，共 **89** 个 .cs 文件。
> 本文档库以 2026-08-12 代码实际实现为准，供预发布后快速理解架构与定位问题。分篇文档见本目录 01–10。

## 职责边界

NineGrid.Core 是《九宫牌局》的**纯领域规则层**：九宫棋盘、卡牌、战斗规则、效果 DSL、跑图进程、经济与存档快照的唯一权威。

- **不依赖 Unity 表现**：asmdef `noEngineReferences`，只依赖 QFramework；无 MonoBehaviour、无资源加载、无 JSON 落盘（存档序列化在表现层）。
- **确定性**：唯一随机源 `IRngUtility`（可捕获/恢复状态）；同种子同命令序列产生完全相同的事实流——战斗日志可重放、存档可复现发牌。
- **一切变更事实化**：状态只能被 `GameAction` 修改，动作产出 `CoreGameEvent` 追加进 `EventLog`；表现层看到的一切都来自这条事实流的投影。

## 内部目录结构

```
NineGrid.Core/
├─ Architecture/     QF 架构装配（1 文件）                       → 篇 01
├─ Commands/         表现层命令白名单（1）                       → 篇 01
├─ Queries/          只读 Query（1）                              → 篇 01
├─ Utilities/        RNG / 日志 / 配置（3）                       → 篇 01
├─ Models/           9 个权威状态模型                             → 篇 02
├─ Domain/           基础类型 + 规则 + 动作 + 事件
│   ├─ (根)          SlotId/枚举/常量/攻击模式/节奏/寻路/战斗规则… → 篇 02/03/04/05/06
│   ├─ Actions/      全部 GameAction（8 文件）                    → 篇 01/04/05/07/08/09
│   ├─ Commands/     CoreCommandResult（1）                       → 篇 01
│   ├─ Events/       CoreGameEvent/EventLog/卡面旁路/Offer 编码（4）→ 篇 10
│   └─ Stats/        属性与规则修正管线（6）                      → 篇 03
├─ Effects/          效果 DSL 引擎（12）                          → 篇 09
├─ Systems/          11 个 QF System（管线/触发/相位/盘面/装填…）  → 篇 01/04/05/06/07/08
├─ Content/          内容目录与装填契约（5）                      → 篇 08
├─ Setup/            开局工厂/职业/存档快照（3）                  → 篇 08
└─ Presentation/     表现桥接：批次/映射/快照/分发器（8）          → 篇 10
```

## 对外暴露面（Presentation 如何驱动 Core）

1. **命令入口（唯一写通道）**：`CoreCommandDispatcher.Send(ICommand<CoreCommandResult>)` → 24 种命令（`Commands/CoreCommands.cs`）→ `PhaseSystem` 门禁与编排 → 动作管线解算。`Apply*` 前缀为表现层可信通道（跳相位门禁）。
2. **事实输出**：每条命令的事件增量切片 + `PresentationEventMap` 过滤 → `PresentationBatch`（结算指令 + 表演锚点 Impact/Settled + 是否锁输入）+ `CoreViewSnapshot` 全量快照。
3. **锁步回执**：含阻塞指令的批次 `OpenBatch` 锁输入；表现播完发 `PresentationFinishedCommand` ack 解锁（ADR-0001）。
4. **只读查询**：`IPhaseSystem.LegalCommands/CanExecute`、`IBoardStabilizationSystem.NeedsRefill`、`EffectiveStatQuery`、`IEffectSystem.ProbeWhyNotTriggered` 等。
5. **QF 事件**：`Evt_PresentationBatchOpened/Cleared/FinishRejected`、`Evt_ActionRejected`。
6. **内容注入**：表现层把 `GameContentCatalog` 写入 `IConfigUtility["tableNine.defaultCatalog"]`；存档 DTO（`RunSaveSnapshot`）由表现层序列化落盘。

## 领域状态模型总图

```
                    NineGridArchitecture（装配 8 Model + 13 System + 3 Utility）
                                          │
   RunModel ──层/节点/相位/种子/本层主题卡组──┐
   PlayerModel ──金币/互动/遗物/双容量/来源池──┤        BattleContextModel
   RelicRunContributionModel ──遗物run攻甲贡献─┤        （交战窗口/清关标志/开局真怪进度）
                                          │
   CardRegistry（uid→CardInstance：FaceUp/攻击模式/节奏/StatBlock/CounterBag/Zone/Slot）
        │                                  │
   BoardModel（9 格占格权威 + AvatarSlot + 祝福/机关空位标记）
   DeckModel（抽牌堆/玩家池/敌池/道具卡格 ItemSlots——唯一跨节点容器）
   PendingChoiceModel（奖励/房间/导航待决 + 商店卡店会话态）
                                          │
   StatSystem（有效值三层管线）+ RuleModifierRegistry（全局规则修正）
   EffectSystem（效果实例：Triggered/Modifier/RuleModifier）→ TriggerSystem ← ActionPipelineSystem
                                          │
   EventLog（CoreGameEvent 事实流）→ PresentationBatch/CoreViewSnapshot → 表现层
```

## 分篇导航

| 篇 | 主题 | 要点 |
|---|---|---|
| [01](01-架构与动作管线.md) | 架构装配与动作管线 | QF 装配、GameAction/管线/触发器、命令面、确定性 RNG |
| [02](02-领域状态模型.md) | 领域状态模型 | 9 Model + SlotId/枚举/常量/CardDraft；状态关系与约定 |
| [03](03-属性与规则修正管线.md) | 属性与规则修正 | StatModifier/RuleModifier 三层求值、三层护甲、作用域清理 |
| [04](04-棋盘卡组与盘面稳定化.md) | 棋盘卡组与稳定化 | 装填→发牌、补牌/旋转/换位、稳定化切片、跳格寻路、虚拟邻接 |
| [05](05-战斗规则与伤害结算.md) | 战斗与伤害 | 双桶、先手还击、标准伤害公式、移除vs击杀、朝向动作、判死闭环 |
| [06](06-节奏倒计时与敌方行动.md) | 节奏与敌方行动 | 攻击模式几何、卡级共享倒计时、开火窗口、齐射三段 |
| [07](07-相位跑图与节点流程.md) | 相位与跑图 | PhaseSystem 门禁矩阵、8 节点编排、清关收场、消费房会话 |
| [08](08-内容装填奖励经济与存档.md) | 内容装填经济存档 | Catalog、节点装填总装、奖池、房间货架、金币事实、存档快照 |
| [09](09-效果DSL与技能装配.md) | 效果 DSL | 三型效果、75 原子、requires 自陈、倒计时投影、魔免/背面惰性 |
| [10](10-表现桥接与事件流.md) | 表现桥接 | 事件→批次→ack、映射表、卡面旁路、快照、命令分发器 |

## 高频排障入口

- 玩家点击被拒 → `PhaseSystem.RefreshLegalCommands` + 各命令门禁（篇 07）；拒绝事实见 `ActionRejected` 事件。
- 伤害数字不对 → `DealDamageAction` 公式八步（篇 05）+ 相关 RuleModifier 是谁挂的（篇 03/09）。
- 效果没触发 → `IEffectSystem.ProbeWhyNotTriggered` 四类分诊（篇 09）。
- 怪不开火/开火太快 → 共享倒计时生命周期与报名逻辑（篇 06）。
- 补牌时机怪 → 稳定化 `NeedsRefill` 条件与切片推进（篇 04）。
- 表现卡死/输入锁死 → 批次开合与 ack（篇 10）；拒收命令不开批。
- 读档后不一致 → `RunSaveGame` 恢复顺序契约（篇 08）。

---

## 文件覆盖清单（89 / 89）

路径基准：`Assets/Scripts/NineGrid.Foundation/NineGrid.Core/`。

| # | 文件 | 一句话说明 | 归属 |
|---|---|---|---|
| 1 | `Architecture/NineGridArchitecture.cs` | QF 架构装配唯一入口，注册全部 Model/System/Utility | 01 |
| 2 | `Commands/CoreCommands.cs` | 表现层 24 种命令白名单，逐条转发 PhaseSystem/PresentationSync | 01 |
| 3 | `Domain/Commands/CoreCommandResult.cs` | 命令结果类型（Accepted/Reason/ResolvedActions） | 01 |
| 4 | `Domain/Actions/GameAction.cs` | 动作抽象基类与上下文/结果类型（事件 + follow-up） | 01 |
| 5 | `Systems/ActionPipelineSystem.cs` | 动作串行解算管线：队列+反应栈、EventLog、深度上限、拒绝事实化 | 01 |
| 6 | `Systems/TriggerSystem.cs` | 触发点注册/派发；TriggerContext 含碎片重组单批去重 | 01 |
| 7 | `Queries/EffectiveStatQuery.cs` | 只读 Query：按 uid+StatId 读有效属性 | 01 |
| 8 | `Utilities/RngUtility.cs` | xorshift128 确定性随机源，状态可捕获/恢复 | 01 |
| 9 | `Utilities/LogUtility.cs` | 内存结构化日志 | 01 |
| 10 | `Utilities/ConfigUtility.cs` | 类型+键内存配置表（Catalog 注入通道） | 01 |
| 11 | `Domain/SlotId.cs` | 格位值类型：正交/对角邻接几何 | 02 |
| 12 | `Domain/CoreEnums.cs` | 全部核心枚举（CardKind/ZoneId/StatId/RuleId/TriggerPoint/CoreEventType/GamePhase 等） | 02 |
| 13 | `Domain/CoreConstants.cs` | 计数器键约定与借甲光环基线键 | 02 |
| 14 | `Domain/ModifierSource.cs` | 修正器来源身份值类型 | 02 |
| 15 | `Domain/CardDraft.cs` | 造卡草稿与节点装填选项（NodeDeckOptions） | 02 |
| 16 | `Models/CardInstance.cs` | 卡牌运行时实例 + CounterBag 通用计数容器 | 02 |
| 17 | `Models/CardRegistry.cs` | uid→卡实例注册表 | 02 |
| 18 | `Models/BoardModel.cs` | 九宫占格权威 + Avatar 格 + 祝福/机关空位标记 | 02 |
| 19 | `Models/DeckModel.cs` | 抽牌堆/双暂存池/道具卡格四容器 | 02 |
| 20 | `Models/PlayerModel.cs` | 跑图持久玩家态（金币/遗物/双容量/来源池/决斗标记） | 02 |
| 21 | `Models/RunModel.cs` | 跑图进度权威（层/节点/相位/种子/主题卡组） | 02 |
| 22 | `Models/BattleContextModel.cs` | 战斗节点上下文（交战窗口/清关标志/开局真怪进度） | 02 |
| 23 | `Models/PendingChoiceModel.cs` | 待决选择与消费房会话态（含挂起/恢复） | 02 |
| 24 | `Models/RelicRunContributionModel.cs` | 遗物 run 内攻/甲贡献（#120） | 02 |
| 25 | `Domain/Stats/StatBlock.cs` | 单卡属性容器（base + 修正器列表） | 03 |
| 26 | `Domain/Stats/StatModifier.cs` | StatModifier 与 RuleModifier 定义 | 03 |
| 27 | `Domain/Stats/StatPipeline.cs` | 三层（Persistent/Conditional/Temporary）求值管线 | 03 |
| 28 | `Domain/Stats/StatConditions.cs` | IStatCondition 14 实现与 StatEvaluationContext | 03 |
| 29 | `Domain/Stats/RuleModifierRegistry.cs` | 全局规则修正表（Evaluate/Consume） | 03 |
| 30 | `Domain/Stats/StatArmorUtility.cs` | 三层护甲读写工具 | 03 |
| 31 | `Systems/StatSystem.cs` | 属性/规则求值门面 | 03 |
| 32 | `Systems/BoardSystem.cs` | 邻接裁决门面（正交+虚拟邻接）与格位可用性 | 04 |
| 33 | `Systems/DeckSystem.cs` | 节点装填入口、清关判定、层主房离开机关洗入反应 | 04 |
| 34 | `Systems/BoardStabilizationSystem.cs` | 盘面稳定化权威：欠补位判定 + 逐切片 Fill + 延后抽牌 | 04 |
| 35 | `Domain/Actions/BoardDeckActions.cs` | SetupNodeDeck/OpeningDeal/FillEmptySlots/Rotate/Swap + 离开机关落点契约 | 04 |
| 36 | `Domain/AvatarWalkPathfinder.cs` | 非战斗跳格两阶段 BFS 寻路 | 04 |
| 37 | `Domain/MonsterBoardRules.cs` | 怪-怪虚拟邻接与天涯若比邻规则查询 | 04 |
| 38 | `Domain/CardCombatRules.cs` | 双桶集中入口（可交战/真怪物/选牌过滤） | 05 |
| 39 | `Domain/CombatEngagementOrder.cs` | 先手还击裁决 | 05 |
| 40 | `Domain/AvatarDefeatFollowUp.cs` | Avatar 判死唯一谓词与 Defeat follow-up | 05 |
| 41 | `Systems/BattleScopeSystem.cs` | 交战窗口开关与作用域修正器清理 | 05 |
| 42 | `Domain/Actions/BattleScopeActions.cs` | 交战作用域开/关动作 | 05 |
| 43 | `Domain/Actions/CoreActions.cs` | DealDamage（标准公式）/Heal/GainArmor/TransferArmor/ModifyGold/RemoveCard/Kill | 05 |
| 44 | `Domain/Actions/FaceOrientationActions.cs` | 翻面 toggle/盖面/翻开三动作（CardFaceChanged + OnFlip） | 05 |
| 45 | `Domain/FaceDownTickCounters.cs` | 背面专用回合计数（faceDownTick.*） | 05 |
| 46 | `Domain/AttackPattern.cs` | 攻击模式枚举与几何裁决（ADR-0011） | 06 |
| 47 | `Domain/ActionCountdown.cs` | "每 N 次"倒计时数学原语（跨阈值多发） | 06 |
| 48 | `Domain/CardRhythm.cs` | 卡级节奏源枚举与裁决族（ADR-0038） | 06 |
| 49 | `Domain/CardRhythmMoveTicks.cs` | 移动计数通道推进（盘面换格 −1 共享倒计时） | 06 |
| 50 | `Systems/PhaseSystem.cs` | 命令门禁 + 节点流程 + 敌方行动阶段 + 消费房会话（Core 最大流程枢纽） | 07 |
| 51 | `Domain/Actions/PhaseActions.cs` | 相位切换/节点事件/判死收束/选择容器/互动计数/倒计时写入等轻量动作 | 07 |
| 52 | `Domain/MapNodeProgression.cs` | 每层 8 节点编排只读表（ADR-0021） | 07 |
| 53 | `Content/ContentDefinitions.cs` | GameContentCatalog 与全部内容定义类型、卡组归档约定、奖池查询展开器 | 08 |
| 54 | `Content/ItemUseEligibility.cs` | 非战斗可用裁决（ADR-0032） | 08 |
| 55 | `Content/MonsterDeckFloorPool.cs` | 主题卡组难度档→层号映射 | 08 |
| 56 | `Content/MonsterFloorStatScaling.cs` | 层数数值叠加（攻+1/血+2 每层） | 08 |
| 57 | `Content/RegularTrapPool.cs` | 常规机关池契约（White 无放回抽 3；trap.leave 常量） | 08 |
| 58 | `Systems/ContentSystem.cs` | Catalog 门面：CreateDraft/效果激活/Trap 静默禁反击/五类卫生校验 | 08 |
| 59 | `Systems/RewardSystem.cs` | 节点装填总装、奖池抽取、选房池、四类房间货架、击杀掉落 | 08 |
| 60 | `Systems/EconomySystem.cs` | 金币事实：回收/丢弃/跳过兑金、清关结算与清残留、移除赏金反应 | 08 |
| 61 | `Domain/Actions/ContentActions.cs` | ModifyBaseStat/遗物授予丢弃/run 贡献/道具直发/各类会话 Offer/ResolveRoom | 08 |
| 62 | `Setup/InitialGameFactory.cs` | 开局工厂：清理重建 + 种子 + Avatar + 职业（含 Clear 后重绑纪律） | 08 |
| 63 | `Setup/ProfessionCatalog.cs` | 固定职业（战士）属性/初始遗物/来源池播种 | 08 |
| 64 | `Setup/RunSaveGame.cs` | 跑图存档快照 DTO 与捕获/恢复（ADR-0041） | 08 |
| 65 | `Effects/EffectEnums.cs` | 效果三型/容器/原子类枚举与注册特性 | 09 |
| 66 | `Effects/EffectDefinition.cs` | 效果定义 DTO、JSON 解析器、结构校验器 | 09 |
| 67 | `Effects/EffectDslNode.cs` | 手写 JSON 解析与动态节点访问 | 09 |
| 68 | `Effects/EffectAtoms.cs` | 原子四接口、反射注册表、倒计时投影契约与作用域策略 | 09 |
| 69 | `Effects/EffectAtomLibrary.cs` | 全部约 75 个内建原子 + TargetResolver + 数值表达式 | 09 |
| 70 | `Effects/EffectAtomSchemas.cs` | 逐原子参数 schema 校验 | 09 |
| 71 | `Effects/EffectRequires.cs` | requires 词表、装配期校验、运行时区域门闩、禁上下文开关参数 | 09 |
| 72 | `Effects/EffectSystem.cs` | 效果引擎本体：激活/卸载/触发链/魔免过滤/背面压制 | 09 |
| 73 | `Effects/EffectRuntimeContext.cs` | 原子求值上下文 | 09 |
| 74 | `Effects/EffectNonTriggerProbe.cs` | 未触发探查分诊（ADR-0010） | 09 |
| 75 | `Effects/FragmentRecombineDedup.cs` | 骷髅重组同批去重 | 09 |
| 76 | `Effects/HelpCardStatBonusUtility.cs` | 卡店道具数值强化运行时加成 | 09 |
| 77 | `Domain/Actions/EffectActions.cs` | 效果触发包装、倒计时投影三动作、KillIfDead/ForceBattle/Spawn/ShuffleInto/修正器动作等效果专用动作全集 | 09 |
| 78 | `Domain/Events/CoreGameEvent.cs` | 事实条目（链式构建，含伤害拆分/绝对值字段） | 10 |
| 79 | `Domain/Events/EventLog.cs` | 顺序事实日志（全局 Sequence） | 10 |
| 80 | `Domain/Events/CardFaceEventValues.cs` | 卡面绝对值旁路（有效攻/当前甲/投影战斗攻提交） | 10 |
| 81 | `Domain/Events/RewardOfferFaceEncoding.cs` | 奖励 Offer Message 编解码 | 10 |
| 82 | `Presentation/PresentationBeat.cs` | 表演锚点枚举（None/Impact/Settled） | 10 |
| 83 | `Presentation/PresentationEventMap.cs` | 事件→指令穷尽映射表（锚点/锁输入/穷尽性自检） | 10 |
| 84 | `Presentation/PresentationBatch.cs` | 结算指令与批次（含建批工厂） | 10 |
| 85 | `Presentation/PresentationSyncSystem.cs` | 批次开合与 ack 权威 | 10 |
| 86 | `Presentation/PresentationSyncEvents.cs` | 批次生命周期 QF 事件 | 10 |
| 87 | `Presentation/CoreCommandDispatcher.cs` | 表现层唯一命令入口（切片建批 + 条件开批） | 10 |
| 88 | `Presentation/CoreViewSnapshot.cs` | 全量只读快照与捕获工厂 | 10 |
| 89 | `Presentation/ActionLogProjector.cs` | EventLog→可读日志行投影 | 10 |
