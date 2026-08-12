# Systems 核心系统一览（不含音频 / VFX / 输入门禁）

> 覆盖范围：`Systems/` 下 15 个文件（输入门禁五件套见《02》，音频十件套见《04》，VFX 见《05》）。
> 这些是表现层的 QF System 骨架：**接口窄、状态权威、场景 View 只当宿主**。

## 职责综述

表现层把"权威状态 + 编排"从 MonoBehaviour 宿主抽进 QF System，场景对象只提供锚点/Adapter/View。每个 System 遵循同一套模式：

- `EnsureRegistered(architecture)` 幂等注册（组合根与懒加载共用）；
- `Bind(view)` / `Unbind()` / `UnbindIfView(view)` 三段式视图绑定（防误解绑他人视图）；
- 读取走 Query、写入走 Command、下行走 struct Event（见《06》）。

## 关键类型表

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `IPresentationRuntimeSystem` | `Systems/IPresentationRuntimeSystem.cs` | 表现运行时接口：意图窄接口（`IPresentationIntentRuntime`）+ Start/Stop/HardClear |
| `PresentationRuntimeSystem` | `Systems/PresentationRuntimeSystem.cs` | 持有 `PresentationDirector` 的唯一壳：TrySubmitIntent / Tick / ExternalHold / MutateMainline，全部操作后 `PublishBusy` 刷新 `MainlineBusy` BindableProperty |
| `IBattleSessionSystem` | `Systems/IBattleSessionSystem.cs` | 局内会话单一所有者接口：节点启动 / Opening / 结算门 / 批次投影回调 / 盘面 Present / 战败收口（约 40 个方法） |
| `BattleSessionSystem` | `Systems/BattleSessionSystem.cs` | 纯转发壳：全部委托 `BattleSessionExecutor`（Flow/BattleSession/，兄弟文档）；`RequestSyncBoardFromCore` 已改为断言禁用（`AssertOccupancySyncForbidden`） |
| `IBoardSelectionSystem` / `BoardSelectionSystem` | `Systems/BoardSelectionSystem.cs` | BoardSelect / 手牌拖放校验门面：四个方法全部转发 `IBattleSessionSystem`（同文件定义接口+实现） |
| `IChoicePresentationSystem` / `ChoicePresentationSystem` | `Systems/ChoicePresentationSystem.cs` | 奖励 / 残留帮助卡结算 Present 门面：两个方法转发会话（同文件定义接口+实现） |
| `IGroundFieldGeometrySystem` | `Systems/IGroundFieldGeometrySystem.cs` | 场地几何注册与收敛的单一所有者接口：占格几何镜像 / 运动执行 / 格位认领（Claims）/ 飞牌 |
| `GroundFieldGeometrySystem` | `Systems/GroundFieldGeometrySystem.cs` | 实现：内持 `GroundOccupancyIndex` + `GroundMotionExecutor`（Cards/ 深模块），Bind 时接线 View 回调（FieldMaybeClear / EmptySlotClicked）并造 `GroundPresentation` |
| `GroundPresentation` | `Systems/GroundPresentation.cs` | internal 封闭 API：导演/Scheduler 经此执行 Rotate/Hop/Deal/Remove/Fusion，不暴露 View 类型 |
| `IFieldBattlePresentationSystem` | `Systems/IFieldBattlePresentationSystem.cs` | 交战表现单一所有者接口：busy/CTS/Hit·Counter Present/Lethal 与 Avatar 战败演出 |
| `FieldBattlePresentationSystem` | `Systems/FieldBattlePresentationSystem.cs` | 实现：委托 `FieldBattlePresentationExecutor`（Cards/，兄弟文档）；Bind 时 `AttachBattleOwner` 双向挂接 |
| `ICardEntityLifecycleSystem` | `Systems/ICardEntityLifecycleSystem.cs` | 卡实体注册查找 + 手牌/牌库/战斗净土域 Evict/Admit 交接接口 |
| `CardEntityLifecycleSystem` | `Systems/CardEntityLifecycleSystem.cs` | 实现：持 Cards/Hand/Deck 三 Manager 显式引用；Battle 端交接为位置快照（速度恒 0） |
| `IGameFlowShellSystem` | `Systems/IGameFlowShellSystem.cs` | 流程壳只读投影接口：State/NodeIndex/IsBusy/IsQuickTestMode/Generation |
| `GameFlowShellSystem` | `Systems/GameFlowShellSystem.cs` | 流程唯一权威：相位 BindableProperty、run mode（QuickTest/Tutorial）、节点序号、内部持 `GameFlowOrchestrator`；**BGM 期望状态唯一提交方** |

## 核心流程与数据流

### PresentationRuntimeSystem：主线 busy 的发布源

所有会改变 Director 状态的操作（Start/Stop/TrySubmitIntent/Tick/ExternalHold 三件套/MutateMainline/HardClearIntents）末尾都调用 `PublishBusy()` 把 `mDirector.IsMainlineBusy` 刷进 `MainlineBusy` BindableProperty——这是轴一互斥真相的唯一发布点。`MutateMainline(Action<BattleTimeline>)` 是教学关卡等"往主线里挂受控批次"的口子（`TutorialBattleDirector` 用）。`Stop`/`HardClearIntents` 先 `ForceEndExternalHold` 再 `HardClearIntents`，防租约悬挂。

### BattleSessionSystem：接口即会话地图

接口方法按职责分组读（实现全在 `BattleSessionExecutor`，Flow 兄弟文档负责细节）：

- **生命周期**：`BootstrapRun`（开局种子/存档恢复）、`StartBattleNodeAsync`、`TryEnterNodeSettlement`、`TeardownPresentationRuntime`、`CancelPresentationWork`；
- **Present 通道**：`BindPresentChannels` / `ClearPresentChannels` / `EnsurePresentationToken`（组合根装配，见《01》）；
- **批次投影回调**：`OnExploreBatchProjected` / `OnAttackHit|Board|CounterBatchProjected` / `OnUseItem*`——剧本工厂解算一批后把 EventLog 投影结果交回会话；
- **盘面 Present**：`DrainPostKillBoardAsync`（命中批盘面 delta 的唯一冲刷口，含 `occupancyPendingVacateUids` 豁免）、`AssertHitPresentOccupancySync`、`FlushPendingShuffleIntoPresentationAsync`、`PresentShuffleIntoDeckFromEventLog`；
- **非锁步 Present**：`PresentRewardChoiceFromCoreAsync`、`PresentUnusedHelpCardSettlementFromEventLogAsync`；
- **战败/胜负**：`RaiseBattleEnded`、`EnsureBattleEndedIfAvatarDefeated`（幂等，ADR-0039）；
- **BoardSelect**：`ValidateHandDragApplyAsync` / `AbortBoardSelectIfActive` / `OnBoardSelectionCompleted|AbortedAsync`。

`BoardSelectionSystem` 与 `ChoicePresentationSystem` 是同一会话的**窄门面**——只为让调用方依赖更小的接口，不自持状态。

### GroundFieldGeometrySystem：几何镜像 ≠ 占格权威

逻辑占格唯一归 Core `BoardModel`；本 System 持有的是**表现几何镜像**（`GroundOccupancyIndex`）+ 运动执行（`GroundMotionExecutor`）。要点：

- 写入走 Command（`PlaceGroundOccupancyCommand` 等四条，见《06》），读取走 Query（`GroundFieldSnapshotQuery` 等）；
- **格位认领**（ADR-0023）：`TryClaimSlot / ReleaseSlotClaim / ReleaseAllClaimsForOwner / TryGetSlotClaimant` 委托 `mIndex.Claims`（`SlotClaimRegistry`）——一格一认领者，悬停文案与点击同源；
- `RefreshSlotHitColliders` 语义已是 Ensure（九框恒开），方法名保留供旧调用方；
- 飞牌：`LaunchDrainDealFlight` / `WaitAllActiveDealFlightsAsync` / `CancelDealFlightForUid`（移除前取消该 uid 补牌飞牌，同步卸 ActiveCount + EndChoreo）；
- `IsBusy`（含交战外全部运动）与 `IsFieldBusy`（场地自身）区分——都**不是**输入门禁（ADR-0004）；
- `HasOccupancyConflictSinceClear` / `ConsumeOccupancyConflictFlag` 为诊断旗标。

`GroundPresentation`（internal）把 Motion/View 再包一层给导演用：`RotateOuterRingAsync`、`ApplyBoardMovesAndHopAsync`、`RevealAvatarAsync`、`LaunchDrainDealFlight`、`PresentSkeletonFusionAsync` 等，避免导演直接拿 View。

### FieldBattlePresentationSystem：交战表演入口

`PlayDirectorAttackHitPresentAsync` / `PlayDirectorCounterPresentAsync` 是组合根 Present 通道的回调终点（见《01》）；`TryBeginAvatarDefeatPresentation` / `TryBeginLethalVictimPresentation` / `PresentRemovedFieldCardAsync` 服务致死与移除演出；`ArmNextLethalAttack` 预装 Lethal Profile。`CancelBattleWork` 供清场。

### CardEntityLifecycleSystem：净土域交接

`EvictFromHand/Deck` → `HandoffState`（位置+速度）→ `AdmitToHand/Deck/Battle`；Battle 端 C 阶段速度恒 0（Evict 取 localPosition 快照、Admit 直接写 localPosition）。跨域交接统一走 `HandoffCardBetweenZonesCommand`（见《06》），不碰 `Manager.Instance` 静态。

### GameFlowShellSystem：流程权威 + BGM 唯一提交方

- 相位写入仅经 `SetGameFlowShellStateCommand`（`ApplyState` 注释明示）；相位变更同步 `SubmitMusicForShellState`——MainMenu→`MainMenu`、BattleStub→`Battle`/`BossBattle`（按 `RunModel.Room==Boss` 或节点序 %8==0 判层主）、Victory/DefeatNotice→`Victory`/`Defeat`、Reward/Room 相位→`RunExploration`。**Presenter/View/卡牌不得播 Music 轨**（ADR-0036）。
- `BeginRun(options)` / `ReturnToMainMenu()` / `Signal(signal)` 委托内部 `GameFlowOrchestrator`（Flow/GameFlow/，兄弟文档）。
- QuickTest：`TryBeginQuickTestFromPickerCode`（主菜单 `\0`–`\9`）→ `QuickTestDeckCatalog.TryResolvePickerCode` → `GameFlowRunOptions.CreateQuickTest`；`PrepareQuickTest` 装载 skillIds/trapContentIds/节点队列；`TryConsumePinnedFirstBattle`、`ResolveBattleContentNodeIndex` 供发牌侧消费。
- 教学模式：`IsTutorialMode` / `ApplyTutorialMode`（ADR-0042）。
- 存档恢复：`SetNodeProgressBeforeRestoredNode`（把壳层节点序设为目标前一格，节点循环首次 `IncrementNodeIndex` 后正好对齐快照序号，保证发牌复现，ADR-0041）；作弊跨层 `SyncShellNodeIndexForCheat`。
- `ForceMainMenuAuthority`：DisableDomainReload 下 Play 退出后把权威拉回主菜单（不碰场景 View——销毁期可能已失效）。

## 对外通信面

| 方向 | 机制 |
|------|------|
| 上行写入 | Command（门禁四条、占格四条、Shell 相位/信号/开局、遗物 HUD 同步等，见《06》） |
| 只读查询 | Query（几何快照、slot↔uid、FieldBusy、ManagedCard 查找等，见《06》） |
| 下行事件 | `BattleSessionSettlementReadyEvent` / `BattleSessionEndedEvent`（GameFlowShellController 订阅转 Signal）、`GameFlowShellStateChangedEvent`、`TeardownPresentationDirectorRequested`、`Evt_PresentationBatchOpened` 等 |
| 事件（C# event） | `IBattleSessionSystem.OnNodeSettlementReady` |

## 关联 ADR

ADR-0001（Batch-ack：会话与通道结构）、ADR-0004（busy 真相唯一）、ADR-0005/0007（指令消费经排期器，System 不旁路写卡面）、ADR-0021（跑图进度真相在 Core，壳层 NodeIndex 只是镜像）、ADR-0023（格位认领）、ADR-0034（补牌裁决在 Core，`BoardStabilizationScheduler` 只按 ack 推进）、ADR-0036（BGM 归属）、ADR-0039（战败收口）、ADR-0041/0042（存档恢复 / 教学模式）。

## 不变量与坑

- **禁 force sync 对账**：`BattleSessionSystem.RequestSyncBoardFromCore` 已是断言（`AssertOccupancySyncForbidden`），谁调谁报——真 desync 修根因，不许拉齐。
- **壳层 `NodeIndex` 不是进度真相**（真相在 Core `RunModel`）；它只服务 QuickTest 内容节点队列与存档对齐。
- `GameFlowShellSystem.EnsureRegistered` 在 Architecture 为 null 时会返回一个**未注册**的临时实例（EditMode 容错）——生产别依赖这条路径。
- View 绑定要用 `UnbindIfView` 而不是 `Unbind`，防止后绑者被先解绑者误清。
- `CardEntityLifecycleSystem.IsBound` 要求三 Manager 齐活；`HandoffCardBetweenZonesCommand` 对 Battle 端不查 IsBound（Battle 交接只动 Transform）。
- Geometry 的 `skipBusyGuard` 参数遍布写入口——导演在已知安全的编排点内跳过忙保护；外部调用别随手传 true。

## 相关文档

- `BattleSessionExecutor` / `PresentationDirector` / `BattleTimeline` / `GameFlowOrchestrator` / `BoardStabilizationScheduler` 本体 → `Flow/` 文档区块
- `GroundMotionExecutor` / `GroundOccupancyIndex` / `FieldBattlePresentationExecutor` / 收敛 → `Cards/` 文档区块
