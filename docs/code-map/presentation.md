# Presentation Code Map（现状）

程序集：`NineGrid.Presentation`  
根目录：`Assets/Scripts/NineGrid.Presentation/`

> 这是 **#28 落地后的真实布局**：单一程序集 + QF Controllers/Commands/Queries/Systems，同时保留历史目录 `Flow/`、`Cards/` 与命名空间 `NineGrid.Flow*` / `NineGrid.Cards*`。  
> **不要**假设已存在顶层 `Architecture/`、`Views/`、`Events/`、`Models/`、`Diagnostics/` 平铺目录——那些是 Spec 目标树，尚未落地。

## 顶层目录（事实）

| 目录 | 约 `.cs` | 命名空间（主） | 放什么 |
|------|----------|----------------|--------|
| `Setup/` | 3 | `NineGrid.Presentation.Setup` | `PresentationSceneRoot`、`PresentationCompositionRoot`、`PresentationSceneBindings` |
| `Controllers/` | 18 | `NineGrid.Presentation.Controllers` | QF `PresentationController` 场景入口 |
| `Commands/` | 25 | `NineGrid.Presentation.Commands` | 写意图 |
| `Queries/` | 10 | `NineGrid.Presentation.Queries` | 读裁决（合法性等） |
| `Systems/` | 18 | `NineGrid.Presentation.Systems` | QF System / 窄能力接口 |
| `Flow/` | ~118 | `NineGrid.Flow*` | 导演/时间线/Channel/Scheduler、局内会话、流程壳、诊断、部分 Presenter |
| `Cards/` | ~136 | `NineGrid.Cards*` | 卡视图、场地/手牌/牌库、收敛、特效 SO、静态 Hook |
| `Editor/` | ~20 | `NineGrid.Presentation.Editor` | 编辑器工具 |
| `Tests/` | ~97 | `NineGrid.Presentation.Tests*` | EditMode |

### `Flow/` 子树

| 子目录 | 内容 |
|--------|------|
| `Presentation/` | `PresentationDirector`、`BattleTimeline`、`IPresentChannel` 实现、Intent Script Factory、`BattleBeatScheduler` / `CardFaceStatHandler`、Scheduler 等编排深模块；部分 struct Event |
| `BattleSession/` | 局内会话相关类型 / 接口 |
| `GameFlow/` | 流程壳运行选项等 |
| `Diagnostics/` | Battle/Flow/Perf/Registry Trace Recorder 与 Sink |
| （根下） | `BattleSessionController`、`GameFlowController`、若干 `*ManagerSingleton`（Presenter 壳名） |

### `Cards/` 子树

| 子目录 | 内容 |
|--------|------|
| `Ground/` | 场地视图与运动执行 |
| `Battle/` | 交战表现默认资产与策略 |
| `Convergence/` | 场地收敛算法 |
| `Effects/` | 卡面 DOTween / Timeline 特效 SO |
| `Slots/` | 卡面槽表 |
| `Presentation/` | 卡面描述合成等 |
| （根下） | `FieldBattleView`、手牌/牌库管理器、静态 `*Hook` |

## 装配

- **场景根**：`Setup/PresentationSceneRoot`（`IController` → `NineGridArchitecture.Interface`）
- **组合根**：`Setup/PresentationCompositionRoot.Install(bindings)` —— 唯一生产装配入口
- **绑定表**：`Setup/PresentationSceneBindings`（场景 Host 引用）

主线 busy 真相：`PresentationDirector.IsMainlineBusy`（经 `IPresentationRuntimeSystem` / InputState 只读投影）。`BattleBusy` / `FieldBusy` 不作独立输入门禁；`OccupancyDesyncLatched` 仅为诊断断言。

## Controllers（18）

`PresentationController` · `ExploreInputController` · `AttackInputController` · `PickupInputController` · `UseItemInputController` · `GroundFieldGeometryController` · `FieldBattlePresentationController` · `CardEntityLifecycleController` · `ZoneOwnershipQueryController` · `DescriptionOutputController`（**已退役**：动态 HUD 描述 TMP 不再接线） · `DamageNumberOutputController` · `RelicHudController` · `RoomChoiceInputController` · `RewardChoiceInputController` · `GameFlowShellController` · `TriggerPulseOutputController` · `DiagnosticOutputController` · `BattleSessionPresentationController`

典型路径：场景 Host / Hook → Controller → `IntentIntake.Submit`（所有权 × MainlineBusy）→ Director / Core Command。

## Systems

| 类型 | 用途 |
|------|------|
| `IPresentationRuntimeSystem` / `PresentationRuntimeSystem` | Director / Timeline 生命周期；意图提交 |
| `IBattleSessionSystem` / `BattleSessionSystem` | 局内会话与 Present Channel 绑定 |
| `IGroundFieldGeometrySystem` / `GroundFieldGeometrySystem` | 场地几何锚点（不暴露具体 View 类型） |
| `IFieldBattlePresentationSystem` / `FieldBattlePresentationSystem` | 交战表现锚点 |
| `ICardEntityLifecycleSystem` / `CardEntityLifecycleSystem` | 卡实体生命周期 |
| `IGameFlowShellSystem` / `GameFlowShellSystem` | 流程壳相位权威 |
| `IPresentationInputStateSystem` / `PresentationInputStateSystem` | 输入所有权轴只读投影（`CurrentOwner`）+ MainlineBusy |
| `IIntentIntake` / `IntentIntakeSystem` | 唯一意图收口：两轴门禁 + 合法性 + Director 缓冲；忙时 `IAccelerationSink` |
| `BoardSelectionSystem` | 棋盘选择模式 |
| `ChoicePresentationSystem` | 房间/奖励选择表现 |
| `GroundPresentation` | 场地表现辅助 |

## 静态 Hook（17）——装配缝，不是业务 Sink

`CombatHitSink` 已删除。现存 `*Hook` 是 **Cards/Flow 目录代码 ↔ Presentation Controllers** 的窄接线（避免历史环依赖），由 Controller 在 `RuntimeInitializeOnLoad` / `OnBind` 注册委托；卡面锚点报点桥由组合根注入。

| 位置 | 示例 |
|------|------|
| `Cards/` | `AttackInputHook`、`ExploreInputHook`、`PickupInputHook`、`UseItemInputHook`、`FieldBattlePresentationHook`、`GroundFieldGeometryHook`、`CardEntityLifecycleHook`、`CardZoneOwnershipHook`、输出类 Hook… |
| `Flow/` | `GameFlowShellHook`、`RelicHudHook`、`RoomChoiceCoreHook`、`RewardChoiceCoreHook`、`BattleBeatHook`（排期器报点） |

**禁止**新增业务静态 Sink（跨层读写规则状态）。新交互优先走 Command / Query / Event / System。

## 仍名 `*ManagerSingleton` 的壳（8）

这些是 **Presenter / 管理器壳**，不是旧四大巨型宿主（已改名为 `BattleSessionController` / `GroundFieldView` / `FieldBattleView` / `GameFlowController`）：

- Cards：`CardManagerSingleton`、`CardHandManagerSingleton`、`CardDeckManagerSingleton`
- Flow：`DescriptionManagerSingleton`（**已退役**：不再写 Card InfoText / NoticeText；卡面静态描述权威在 `Basic_Description` Commit）、`DamageNumberManagerSingleton`、`GoldGainFxManagerSingleton`、`RelicManagerSingleton`、`SelectorManagerSingleton`

`DescriptionDisplayHook` 现为 no-op；Hover/Drag/BoardSelect 动态描述 TMP 管道已砍。卡牌运行时持续呈现的描述只走卡面槽 `Basic_Description`。

结构护栏见 `Tests/HostContractStructuralTests`（禁回流四大旧名与 `CombatHitSink`）与 `Tests/IntentIntakeStructuralTests`（禁绕过 IntentIntake、门禁/收口禁壁钟）。

## 读写约定

- **写** → `NineGridArchitecture.Interface.SendCommand(...)`
- **读** → `SendQuery` / `GetSystem<T>()` 只读 API
- **下→上** → struct Event（多数在 `Flow/Presentation/`）或 BindableProperty
- **编排** → `PresentationDirector` / `BattleTimeline` / `IPresentChannel`（普通 C# 深模块，由 System 持有）

## 效果扩展点

1. `Commands/` / `Queries/` / Event  
2. `ITimelineStep` / `IPresentChannel`（`Flow/Presentation/`）  
3. `BattleBeatScheduler` / `CardFaceStatHandler`（卡面数值锚点；新事件须在 `PresentationEventMap` 声明 Beat）  
4. 卡面视觉 SO（`Cards/Effects/`）  

不要接回静态业务 Sink，也不要在 View 上直接改 Core 规则状态，也不要旁路直读 Core 写卡面数值。

## ADR 不变量（摘要）

- Batch/ack：表演未就位前 Core 不推进下一批（ADR-0001）  
- 逻辑占格唯一归 Core `BoardModel`  
- 卡面可见值经投影 Commit（ADR-0002）；禁止队列外正式 Setter 通路  
- 卡面**数值**只经结算指令在表演锚点提交，不直读 Core（ADR-0005）

## Core 表演契约与卡面锚点（#54 / #55 / #56 / #57 / #58）

- `NineGrid.Core.PresentationBeat`：`Impact` / `Settled` / `None`（升级路径注释在枚举旁）
- `PresentationEventMapEntry.Beat` + `NoneReason`：每个 `CoreEventType` 显式锚点归属；不上卡面必须写理由
- 数值类指令绝对值：血甲用既有 `RemainingHp`/`RemainingArmor`；`BaseStatModified` / 生成类事件攻用 `ResultValue`（`Amount` 仍为 StatId，`Delta` 仍为增量）；`CardKilled` 携带 `RemainingHp`
- 穷尽性：`NineGrid.Core.Tests.PresentationEventMapBeatExhaustivenessTests`
- **排期器** `Flow/Presentation/BattleBeatScheduler`：批次开启装载未消费指令；`ReportBeat` 分发；Settled 后未消费只报不改；消费 **Avatar** 数值指令的同拍旁路调用 `PlayerInfoHudPresenter.SyncFromCore`（HUD 仍直读内核，非卡面第二条路径）
- **卡面数值处理器** `Flow/Presentation/CardFaceStatHandler`：只从指令赋值 → `ManagedCard.CommitPresentation`；不碰 `CardRegistry` / `IStatSystem`；`SpawnCard` / `DealCard` / `ShowAvatar` 走同一 `ApplySpawnFace`；`KillCard` 取 `RemainingHp`
- **生成绝对值** `CardFaceEventValues.WithFaceAbsolutes`：`CardSpawned` / 带 uid 的 `CardDealt` / `AvatarAppeared` 写入造卡/发牌时攻甲血
- **开局引导** `Flow/Presentation/CardFaceGenerationBootstrap`：非锁步 Opening 从事件日志重放生成类指令；BoardSelect 视图重 Spawn 用 `ApplyFaceHistoryForUid` 重放该 uid 的生成+后续数值指令（与 Settled 同一 Handler）
- **报点**：攻击/反击命中帧 → `Impact`；`PresentStep` 通道完成后先冲刷 `Impact` 再报 `Settled`，然后 `TryAcknowledge`（探索/用道具无帧级回调，收尾时一并冲刷）
- **组合根**：`PresentationCompositionRoot` 注册排期器并订阅 `Evt_PresentationBatchOpened`
- **读写约定补则**：卡面数值只经排期器/生成引导，禁止 Mapper 首次 `TryRead` 写数值；JSON `stats` 仅 Catalog 造卡用；用道具 Present 只 `RefreshVisualsPreservingCommittedStatsOnAllSpawned`；探索/用道具批次投影不写卡面数值；`MarkFieldDead` 只标死亡态不改血量；底盘数值 Setter 非公开
- **ADR**：[ADR-0005](../adr/0005-card-face-beat-commit.md)（与 0001/0002/0004 交叉引用）