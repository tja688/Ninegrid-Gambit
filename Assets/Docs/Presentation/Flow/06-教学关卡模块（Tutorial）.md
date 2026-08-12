# Flow/Tutorial/ —— 教学关卡模块

> 权威代码：`Assets/Scripts/NineGrid.Presentation/Flow/Tutorial/`（命名空间 `NineGrid.Flow.Tutorial`，共 5 个文件）
> 关联 ADR：[ADR-0042 教学关卡模块](../../../../docs/adr/0042-tutorial-level-module.md)，交叉：ADR-0022（关卡装填）、ADR-0026（离开机关唯一清关）、ADR-0034（盘面稳定化）、ADR-0041（跑图存档）

## 职责综述

教学是一场**独立于节点循环**的一次性受控教学战斗：8 张教学机关卡（`trap.tutorial.*`，统一 `deck.tutorial`、稀有度 None）按「阅读序」铺满九宫格，卡面描述互相拼接成讲解文本；玩家按四波剧本逐步体验互动 → 怪物机制 → 道具使用 → 清关变卖，最后击破离开机关走 Core **正常清关链**结束。完成标记写独立存档槽 `tutorial_profile`；首次点「开始游戏」自动先进教学（通关后转正式开局），主菜单「教学」按钮任何时候可重进。

本目录只含**表现层导演与数据计划**；`PreserveDealOrder` 保序发牌的 Core 缝在 `NodeDeckOptions`（Core 侧），编排入口 `RunTutorialAsync` 在 [`GameFlowOrchestrator`](01-GameFlow主编排与流程壳.md)。

## 关键类型表

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `TutorialRunOptions` | `Tutorial/TutorialRunOptions.cs` | 教学开局载荷（载荷即模式）；唯一字段 `ContinueToFormalRun`：通关后转正式开局（首次「开始游戏」路径）还是回主菜单（独立教学入口） |
| `TutorialProgressStore`（静态类） | `Tutorial/TutorialProgressStore.cs` | 完成标记持久化：经 `RunSaveStoreHook`（ES3 桥）读写独立槽 `tutorial_profile`；后端缺失按「未完成」处理且不落盘；`ResetCompleted()` 供 Dev 复位 |
| `TutorialDeckPlan`（静态类） | `Tutorial/TutorialDeckPlan.cs` | 四波卡表真源 + 「阅读序 → 抽牌堆序」换算 + 第一波开局 `NodeDeckOptions` 构造（`PreserveDealOrder=true`，0 直摆 + 9 张保序抽牌堆） |
| `TutorialClearWaveCommand` | `Tutorial/TutorialWaveCommands.cs` | 换波清场：移除场上全部机关与怪物（Avatar 与道具卡不动），`reason=clearResidualBoard` 不派发 OnRemove 连锁 |
| `TutorialInjectWaveCommand` | `Tutorial/TutorialWaveCommands.cs` | 换波补发：下一波卡按抽牌堆序**逆序顶插**（`ShuffleIntoDrawPileAction top:true, cause:"tutorialWave"`），保证最终顺序 |
| `TutorialBattleDirector` | `Tutorial/TutorialBattleDirector.cs` | 教学导演：每帧巡检 Core 状态推进四波；换波脚本经 `MutateMainline` 挂真时间线（清场批 → 补发批 → 盘面稳定化）；换波条件满足即挂 Opening 输入门，新波扫描完成才解锁 |

## 核心流程与数据流

### 入口与收口（在 GameFlowOrchestrator 内，为完整性列出）

1. 主菜单入口两条：`GameFlowController.BeginFormalRun` 检查 `TutorialProgressStore.IsCompleted()`——无标记时改派 `GameFlowRunOptions.CreateTutorial(continueToFormalRun: true)`；`BeginTutorialRun`（TutorialRun 按钮）派 `CreateTutorial(false)`。
2. `GameFlowOrchestrator.Start` 见 `TutorialMode` 走 `RunTutorialAsync`，**不进** `RunNodeCycleAsync`：`BootstrapRun` → `StartBattleNodeAsync(TutorialDeckPlan.BuildOpeningOptions(content))` → `TutorialBattleDirector.StartNew(ct)` → 等 `mSettlementTcs`（结算就绪信号）。
3. 教学局**不**捕获存档检查点（`PlayRealBattleAsync` 才捕获，教学不走它）、不展示战前信息预览、不叠 QuickTest 作弊。
4. 通关（第四波击破离开机关 → Core 正常清关 → `SettlementReady` 信号）后 `TutorialProgressStore.MarkCompleted()` → 短暂「教学完成」Notice → `EnterMainMenuImmediate` 收干净 →（`ContinueToFormalRun` 时）下一帧 `BeginRun(CreateFormal())`。
5. **战败**走常规 `BattleEnded` 收口：不标记完成；且 `ShowBattleEndAndReturnAsync` 对教学局跳过 `RunSaveService.HandleRunEnded()`——不得误删玩家早前正式局的自动存档。

### 保序发牌的数学（TutorialDeckPlan）

- Core `FillEmptySlotsAction` 固定补格序为 `1,2,3,6,9,8,7,4`（跳过 Avatar 格 5）；玩家阅读习惯为格 `1,2,3 / 4,6 / 7,8,9` 逐行。
- 因此「阅读序第 i 张」须按 `sReadingIndexForPile = {0,1,2,4,7,6,5,3}` 重排后入抽牌堆：`pile[i] = reading[sReadingIndexForPile[i]]`。
- 第一波开局：`BuildOpeningOptions` 造 `NodeDeckOptions{ PlayerOpeningCount=0, EnemyOpeningCount=0, RequireElite=false, PreserveDealOrder=true }`，把第一波 8 张 + 1 张补位卡 `trap.tutorial.spare` 全部 `AddEnemyCard`，经既有 OpeningDeal → FillEmptySlots 常规发牌链路铺满场地。`PreserveDealOrder=true` 让 `OpeningDealAction` 跳过洗牌与离开机关落点重排（正式流程该开关恒为 false）。

### 四波剧本与换波判定（TutorialBattleDirector.Tick）

导演每帧 `UniTask.Yield` 巡检（`phase==InteractionLoop`、无换波在途）。**换波条件按 Core 真相先行判定**——击杀批一解算即为真，无需等表演收尾；条件满足立即挂 **Opening 输入门**（`PresentationInputGates.SetOpening(true)`，幂等重挂），再等主线不忙（`runtime.MainlineBusy==false`）且无打开批次（`sync.ActiveBatchId==0`）时把换波脚本挂上主线。这堵住「击杀表演收尾 → 换波脚本入队」之间主线短暂空闲的输入窗口。

新波稳定化完成后（`onStable`），下一次空闲 Tick 先 `ScanWave()` 登记本波 uid（扫盘面 + 抽牌堆，识别教学机关前缀 / 离开机关 / 教学怪 `monster.melee_3` / 飞刀 `help.throwing_knife` / 药水 `help.healing_potion`），**登记完毕才释放输入门**；相位切换（清关 / 战败）与导演 `Stop()` 兜底释放，不粘门。波内维护（机关补位 / 飞刀练习靶）只在空闲且无换波挂起时执行。

| 波 | 内容（阅读序尾项为特殊卡） | 换波条件 | 波内维护 |
|----|---------------------------|----------|----------|
| 1 基础互动 | 8 张教学机关（welcome/deal/rotate/attack/hp/no_counter/refill/advance） | 累计击破 ≥2 张（`CountDeadWaveCards()`） | — |
| 2 怪物机制 | 7 机关 + 教学怪（monster_intro/countdown/tick/fire/inspect/reposition/hunt + 怪） | 教学怪死亡（`IsDeadOrGone(mMonsterUid)`） | 机关无限补位 `EnsureFillerStock`：抽牌堆空即非锁步顶插 `trap.tutorial.filler` |
| 3 道具使用 | 5 机关 + 飞刀 + 药水 + 练习靶怪（item_intro/pickup/use_item/aim/practice + 三特殊卡） | 飞刀与药水都已离场 | 机关补位 + `EnsureKnifeTargetAvailable`：飞刀未用且全场无活怪时补练习靶（防卡死） |
| 4 清关变卖 | 7 机关 + 离开机关 `trap.leave`（door/sell_rule/sell_gain/early_leave/greed/retreat/boss_door + 离开机关） | 击破离开机关 → Core 正常清关收口，导演无事可做 | — |

### 换波脚本挂主线（EnqueueWaveTransition）

换波是**真时间线脚本**，经 `IPresentationRuntimeSystem.MutateMainline` 追加三段，复用既有 Resolve/Present/ack 锁步，切换期间输入互斥自然生效：

1. **清场批**：`PresentationSyncBatchGate.FromSync(sync, () => ResolveAndProject(TutorialClearWaveCommand), slice:"TutorialWaveClear")` → `ResolveBatchStep` + `PresentStep`（棋盘通道消费移除表演）。清场用 `RemoveCardAction(uid, ZoneId.Removed, "clearResidualBoard")` + `RunToCompletion`，与清关收场同款 reason——**不派发 OnRemove/OnCumulative**，避免遗物/效果在换波拍连锁。
2. **补发批**：同构 gate 包 `TutorialInjectWaveCommand(pileOrder)`——逆序 `ShuffleIntoDrawPileAction(defId, Trap, 1, top:true, cause:"tutorialWave")` 顶插。
3. **盘面稳定化**：`BoardStabilizationScheduler.Append(...)`（共享实例）逐轮 Resolve → Present → ack 补满场地（真发牌表演）；`onStable` 回调清 `mTransitionQueued`。

导演自持独立设施，不借用生产会话的：`CoreCommandDispatcher(architecture, 700_000)`（独立批号段，避免与生产 Dispatcher 撞号——sync 同时只开一批，纯保险）与私有 `QueuedBoardPresentChannel`（Present 回调仍走 `mSession.DrainPostKillBoardAsync`）。每个解算批经 `ResolveAndProject` 把 EventLog 切片投影（`IntentBatchProjection.Build`）后 `OnTutorialBatchProjected` **投递导演私有通道**（`mBoardChannel.Enqueue` + `mSession.PresentShuffleIntoDeckFromEventLog` 播洗入卡组飞行）。**不得**交 `mSession.OnExploreBatchProjected`——那会投进生产 Explore 通道，无人消费 → 换波在 Core 落地但零表演（旧卡影子留场可被点击、新卡「虚空发牌」、盘面全格 Core/Pres 脱轨）。

### 非锁步小补给（InjectTopUnlockstep）

机关无限补位、飞刀练习靶走**作弊面板同款**范式：`pipeline.Enqueue(ShuffleIntoDrawPileAction top:true)` + `RunToCompletion` + `BattleBeatFlush.PresentEventLogSlice(arch, start)` 事件切片冲刷——不开锁步批、不占主线。

## 对外通信面

- **写 Core**：`TutorialClearWaveCommand` / `TutorialInjectWaveCommand`（QFramework `AbstractCommand<CoreCommandResult>`，直接操作 `IActionPipelineSystem`）；非锁步路径直接 `pipeline.Enqueue + RunToCompletion`。
- **读 Core**：`IPhaseSystem.CurrentPhase`、`BoardModel` / `DeckModel` / `CardRegistry`（巡检波次状态）。
- **编排**：`IPresentationRuntimeSystem.MutateMainline`（挂主线）、`IPresentationSyncSystem`（批次门）、`BoardStabilizationScheduler`、`QueuedBoardPresentChannel`、`BattleBeatFlush.PresentEventLogSlice`、`IntentBatchProjection`。
- **存档**：`RunSaveStoreHook.StoreOrNull()`（读写 `tutorial_profile` 槽，JSON `{schemaVersion, completed}`）。
- **被谁调用**：`GameFlowOrchestrator.RunTutorialAsync`（StartNew/Stop）；`GameFlowController.BeginFormalRun`（IsCompleted 判定）。

## 关联 ADR

ADR-0042（本模块）、ADR-0026（第四波清关走离开机关唯一清关链）、ADR-0034（换波补场复用盘面稳定化锁步）、ADR-0041（存档后端复用；完成标记独立槽）。

## 不变量与坑（ADR-0042「禁止」节 + 代码注释提炼）

- **正式流程禁止**引用 `deck.tutorial` 内容或置 `PreserveDealOrder`；教学机关卡**不得**改成 White 稀有度（会流入常规机关池 `RegularTrapPool`）。
- 教学战败 / 中途退出**不写**完成标记；教学局**不得**删除玩家自动存档（`ShowBattleEndAndReturnAsync` 里 `IsTutorialMode` 分支）。
- 换波逻辑**不得绕过主线**（直接改盘面视图、或在锁步批打开时直跑管线）——导演 Tick 的空闲检查就是这条纪律的执行点。
- 换波批次投影**必须进导演私有通道**（`OnTutorialBatchProjected` → `mBoardChannel.Enqueue`）；投 `mSession.OnExploreBatchProjected` 会进生产 Explore 通道无人消费，换波零表演（虚空发牌）。
- 换波期输入门（Opening）从「条件满足」持到「新波 ScanWave 完成」；相位切换与 `Stop()` 兜底释放，不得粘门。
- 清场 `RemoveCardAction` 的 reason 必须是 `clearResidualBoard`，否则会触发 OnRemove 效果连锁。
- 补发顶插必须**逆序**遍历 pileOrder（`top:true` 语义是「插到最顶」，逆序插完后顺序才正确）。
- `ScanWave` 在第一波时才清飞刀/药水 uid 记录（`mWave == WaveOne`），第二三波扫描不重置——飞刀可能已被拾进手牌（不在盘面/抽牌堆扫描范围内），重置会误判「已用掉」。
