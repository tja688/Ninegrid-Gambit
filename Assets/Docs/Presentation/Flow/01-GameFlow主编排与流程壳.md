# GameFlow 主编排与流程壳 —— 节点循环 · 开局模式 · Run 存档 · QuickTest · 过场

> 权威代码：`Flow/GameFlow/`（4 + RunSave 2）、根下 `GameFlowController.cs` / `GameFlowShellHook.cs`、`Flow/Presentation/GameFlowShellState*.cs`、QuickTest 三件（`QuickTestDeckCatalog` / `QuickTestRunOptions` / `QuickTestRunPlanner`）、`Flow/Transitions/`（3），共 16 个文件。
> 关联 ADR：[ADR-0021 跑图进度归 Core](../../../../docs/adr/0021-run-progression-in-core.md)、[ADR-0041 跑图存档](../../../../docs/adr/0041-run-save-battle-start-checkpoint.md)、[ADR-0042 教学](../../../../docs/adr/0042-tutorial-level-module.md)、ADR-0020（场地图标选房）、ADR-0025/0026（清关与道具卡格）

## 职责综述

这里是表现层「一整局游戏」的总编排：从主菜单点「开始游戏」到通关/战败回主菜单的全部相位推进。分工三层：

- **`GameFlowShellSystem`**（在 `Systems/`，非本目录）是流程壳相位**权威**：持有 `BindableProperty<GameFlowShellState>`、节点序号壳投影、QuickTest 字段；相位写入只经 `SetGameFlowShellStateCommand`。
- **`GameFlowOrchestrator`**（本目录，由 Shell 持有的普通 C# 类）是**编排深模块**：节点循环、每种节点/房间的 await 流程、胜负收口、读档恢复、教学分派。
- **`GameFlowController`**（MonoBehaviour，场景 View）只做场景绑定、主菜单输入轮询、Notice/Panel 投影（实现 `IGameFlowView`）。

**跑图进度真相在 Core `RunModel`（ADR-0021）**：壳层 `NodeIndex` 只是投影；`Orchestrator.RunNodeCycleAsync` 每迭代读 Core `RunModel.NodeIndex` 决定分支。

## 关键类型表

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `GameFlowOrchestrator` | `GameFlow/GameFlowOrchestrator.cs` | 主流程编排：Start（四模式分派）→ 节点循环 → 战斗/非战斗/选房/房内会话 → 胜负收口回主菜单 |
| `GameFlowRunOptions` | `GameFlow/GameFlowRunOptions.cs` | 开局选项；**载荷即模式**：QuickTest / RestoreSnapshot / Tutorial 三载荷，非空即该模式；`CreateFormal/CreateQuickTest/CreateRestore/CreateTutorial` 四工厂 |
| `GameFlowSignal`（struct）+ `GameFlowSignalKind` | `GameFlow/GameFlowSignal.cs` | 编排信号：`SettlementReady`（节点结算就绪）/ `BattleEnded(victory)`（整局胜负） |
| `IGameFlowView` | `GameFlow/IGameFlowView.cs` | 流程场景视图接口：Notice / Panel 显隐 / 时序参数 / QuitGame |
| `RunSaveService`（静态类） | `GameFlow/RunSave/RunSaveService.cs` | 存档服务：检查点捕获→自动槽、手动槽写入、读档收口重开 |
| `IRunSaveStore` + `RunSaveStoreHook` | `GameFlow/RunSave/RunSaveStore.cs` | 落盘后端接口 + 装配缝（ES3 桥在 `NineGrid.SaveBridge` 注册） |
| `GameFlowController` | `GameFlowController.cs` | 主流程场景 View：主菜单四按钮轮询命中 + 悬停缩放 + 菜单音效；实现 `IGameFlowView` |
| `GameFlowShellHook`（静态类） | `GameFlowShellHook.cs` | 流程壳 Controller 接线入口；`PublishState` 镜像路径**已停用**（no-op，防旧路径偷写第二份相位） |
| `GameFlowShellState`（enum） | `Presentation/GameFlowShellState.cs` | 壳相位七态：MainMenu / BattleStub / RewardChoice / RoomChoice / RoomEvent / VictoryNotice / DefeatNotice |
| `GameFlowShellStateChangedEvent`（struct） | `Presentation/GameFlowShellStateChangedEvent.cs` | 相位变更一次性广播（From/To） |
| `QuickTestRunOptions` + `QuickTestNodeOrderMode` | `QuickTestRunOptions.cs` | QuickTest 载荷：节点序（Shuffled/Sequential）、钉死首关牌组、skillIds、trapContentIds |
| `QuickTestDeckCatalog`（静态类） | `QuickTestDeckCatalog.cs` | `\0`–`\9` 十条通道预设真源 + 选码菜单文本 + QuickTest 卡面短描述（<16 字，非正式文案权威） |
| `QuickTestRunPlanner`（静态类） | `QuickTestRunPlanner.cs` | 整局内容节点队列规划：每配置出现 floor 次，Sequential 原序 / Shuffled 全序洗牌 |
| `RunSceneTransitionService` | `Transitions/RunSceneTransitionService.cs` | 局内节点过场：同层 Directional（四向袋洗牌偏置）/ 跨层 Round（洞心追 Avatar）；支持 Cover 挂起 |
| `DirectionalBiasPicker` | `Transitions/DirectionalBiasPicker.cs` | 四向袋洗牌抽取：抽完再洗，倾向尚未出现的方向 |
| `RunSceneTransitionSettingsSO` + `TransitionTweenCurve` | `Transitions/RunSceneTransitionSettingsSO.cs` | 过场参数 SO（Resources `Transitions/RunSceneTransition`）；FaderId 常量 100/101 |

## 核心流程

### 1. 开局分派（Orchestrator.Start）

入口链：主菜单点击 → `GameFlowController.BeginFormalRun`（无教学标记时改派教学，见 [教学篇](06-教学关卡模块（Tutorial）.md)）→ `SendCommand(BeginGameFlowRunCommand(options))` → `GameFlowShellSystem.BeginRun` → `Orchestrator.Start(options)`：

1. `CancelBattleEndWork()`——必须先取消胜负 Notice 的延迟回菜单，否则其 Delay 结束后 `EnterMainMenu` 会把已重开的 Opening 清成空场（代码注释明示）。
2. Busy 且非 MainMenu 时忽略 BeginRun（防重入）。
3. 复位：`PresentationInputGates.Reset`、`CancelBattleWork`、`ClearCardPresentationSurface`、`ApplyRunMode/ApplyTutorialMode/ResetNodeProgress/BumpGeneration`；QuickTest 模式再 `PrepareQuickTest`（须在 ResetNodeProgress 之后，否则被清掉）+ 设 RunTag 诊断标。
4. `BattleTraceRecorder.RotateSessionForNewRun()` + FlowTrace `StartRun` 记录。
5. **恢复模式**：`TryBootstrapRestoredRun(snapshot)`——`session.BootstrapRun(new InitialGameOptions{Seed=snapshot.SeedValue})` → `RunSaveGame.RestoreAfterCreate`（Core 覆盖恢复含 RNG）→ `mShell.SetNodeProgressBeforeRestoredNode`（壳序号对齐到目标节点**前一格**，首迭代 Increment 后正好落在快照节点）→ 置 `mRestoredBootstrapPending`（首个战斗节点跳过重复 BootstrapRun）→ 刷遗物栏/玩家 HUD。失败则 `EnterMainMenuImmediate`。
6. **教学模式**：`RunTutorialAsync`，不进节点循环。
7. 其余：`RunNodeCycleAsync(ct)`。

### 2. 节点循环（RunNodeCycleAsync）

```text
while 未取消:
    mShell.IncrementNodeIndex()
    coreNodeIndex = RunModel.NodeIndex           ← 进度真相在 Core
    if MapNodeProgression.EntersInteractionLoop(coreNodeIndex):   ← 节点 4/7 为 false
        PlayRealBattleAsync()      战斗节点（详下）
        PlayRewardChoiceAsync()    清关奖励（Bounce 扇形三选一）
    else:
        PlayNonCombatNodeAsync()   节点 4/7：仅 StartNode，无发牌无清关
    PlayRoomIconChoiceAsync()      场地图标选房/导航 → 驻留进房 → （消费/特殊房）房内场地板
    if Core phase == Victory: ShowVictoryAndReturnAsync(); return
```

### 3. 战斗节点（PlayRealBattleAsync）

1. `RequestSetState(BattleStub)`（经 `SetGameFlowShellStateCommand`，Shell 相位切换顺带提交期望音乐状态）；`HardClear` 简要解释。
2. `EnsureBattleNodeBootstrap`：节点 1 直接 `BootstrapRun()`；否则若 `StartNode` 非法先清粘连 Present 锁（`sync.Clear` + `ForceEndExternalHold`），再尝试 `TryRecoverStuckRoomTransition`（帮 Core 补交 SelectRoom/EnterRoom），末路才 `BootstrapRun(preserveRunInventory: true)`（跨关必须带回遗物/金币/道具卡，`RunInventorySnapshot` 快照恢复在 [会话篇](02-战斗会话与批次投影.md)）。恢复模式首个战斗节点经 `mRestoredBootstrapPending` 一次性豁免。
3. **存档检查点（ADR-0041）**：正式局（非 QuickTest）在 `BuildNodeDeckOptions` 消耗 RNG **之前** `RunSaveService.CaptureCheckpoint(mShell.NodeIndex)`——恢复时以同一 RNG 状态重跑发牌即可复现本场开局。
4. 内容节点解析：QuickTest 可 `TryConsumePinnedFirstBattle`（钉死首关牌组）；否则 `mShell.ResolveBattleContentNodeIndex()`。`IRewardSystem.BuildNodeDeckOptions(contentNodeIndex, monsterDeckId)` 产出发牌选项；QuickTest 再 `ApplyQuickTestTrapCardsIfNeeded`（`trapContentIds` 经 `content.CreateDraft` + `options.AddEnemyCard` 注入敌池）。
5. **战前信息预览硬阻塞**（仅正式 Run）：`BattleInfoPreviewPresenter.ShowAndWaitAsync(options, ct)`——未关闭前不 `StartBattleNodeAsync`（不入场/不发牌）。
6. `session.StartBattleNodeAsync(options, ct)`（入场发牌，见会话篇）→ QuickTest 作弊（改血 99/攻 5、挂技能 `TryAttachSkillsToBoardMonsters`、timeScale）→ `session.TryEnterNodeSettlement()`（开局即清关的罕见路径兜底）→ `await mSettlementTcs`（等 `GameFlowSignal.SettlementReady`）。

### 4. 奖励与选房

- **PlayRewardChoiceAsync**：校验 `phase==RewardItemChoice && pending.Kind==Reward`（不满足则记 FlowTrace 跳过）→ `ShowRewardOverlay` + `SetChoiceOverlay(true)` → `session.PresentRewardChoiceFromCoreAsync(hoverOnNotice:true)`（Bounce 扇形，见会话篇）→ finally 收 overlay。
- **PlayNonCombatNodeAsync**（节点 4/7）：`RequestSetState(RoomChoice)` → `EnsureBattleNodeBootstrap` → `phase.StartNode(CreateDefaultBattle())`（Core 对 4/7 不进 InteractionLoop，直接放出导航/层主图标）。
- **PlayRoomIconChoiceAsync**：校验 `phase==RoomChoice && pending∈{Room,Navigation}` → 启用 `AvatarWalkSystem` → `RoomIconBoardPresenter.TrySpawnFromPending`（图标落格；失败即 LogError，旧浮层已退役无回退）→ `WaitUntil`（驻留提交已 Select+Enter：导航/战斗房 → NodeCompleted/CanExecute(StartNode)；消费/特殊房 → `IsAwaitingInRoomBoard`）→ finally 关走格、`DespawnAll` → 若进房会话则 `PresentInRoomSessionAfterEnterAsync`。
- **PresentInRoomSessionAfterEnterAsync**：`RequestSetState(RoomEvent)`；按 `PendingChoiceModel.PoolId` 分派 `PresentShopBoardAsync` / `PresentTavernBoardAsync` / `PresentRewardBoardAsync` / `PresentAttributeBoardAsync`（属性房路径遗留，ADR-0031 已废止正式不触发）。四个 Present*Board 同构：启用走格 → `Presenter.Bind + TrySpawnFromPending` → `RoomIconBoardPresenter.HardCutAfterEnter`（Avatar 硬切格 5）→ `RevealHeldTransitionIfAnyAsync`（揭开挂起的过场 Cover）→ `WaitUntil`（NodeCompleted / Victory / Defeat / CanExecute(StartNode)）→ finally 关走格 + `DespawnAll`。**关键约定（ADR-0020，代码注释）**：房内场地板 = 受保护场地，**勿整段持有 ChoiceOverlay**——否则 BoardWalk 以 ProtectedField 提交会 ownerMismatch，离开/空格全点不动。
- 结尾兜底：若过场 Cover 仍挂起（Spawn 失败等）`CompleteRevealAsync` 揭开，防全屏黑死。

### 5. 胜负收口（ShowBattleEndAndReturnAsync）

触发：`Signal(BattleEnded)`（战败/局内通关，来自 `BattleSessionEndedEvent` 经 Shell 转发）或节点循环见 `Victory` 相位。流程：

1. 非教学局 `RunSaveService.HandleRunEnded()`（清内存检查点 + 删自动存档，手动槽保留）。
2. `CancelLoopWork` → 清卡表面、刷持久 HUD、`CancelBattleWork`。
3. `RequestSetState(VictoryNotice/DefeatNotice)`（音乐切 Victory/Defeat）+ 胜负 SFX（`FlowRoomEconomyAudioCues`）+ 胜负 VFX（`FlowBattleEndVfxCues`）+ `BattleTraceRecorder.ExportBothNow`。
4. **结算面板**：`RunSummaryPanel.TryShowAndWaitAsync(victory, ct)`（只读展示 `ClearRunSession` 前快照，等玩家点返回）；场景缺预置回退旧 Notice + 延时。
5. `EnterMainMenuImmediate`：`RunSummaryPanel.CloseIfOpen` 兜底（强退路径可能未走面板 finally）→ HideNotice → `PresentationInputGates.Reset` → `ClearPresentationSurface` → `ShowMainMenuPanels` → 回菜单 SFX → `RequestSetState(MainMenu)`（音乐回主菜单）→ `mShell.ClearRunSession` → timeScale 复位。

**取消令牌纪律**（代码注释）：`ShowVictoryAndReturnAsync` 不可把节点循环 ct 传给胜负回菜单——`ShowBattleEnd` 开头 `CancelLoopWork` 会立刻取消该 ct，Delay 抛取消后 `EnterMainMenuImmediate` 走不到，DisableDomainReload 下残留非 MainMenu。胜负收口用独立 `mBattleEndCts`。

### 6. Run 存档（RunSaveService，ADR-0041）

- **槽位**：`auto` + `manual_0..2`（`ManualSlotCount=3`，读档列表 = 自动档 + 3 手动 = 4 行，对齐 UI 槽位面板）。
- **CaptureCheckpoint**：`RunSaveGame.Capture(arch, shellGlobalNodeIndex)` → 内存检查点 + 写自动槽。失败只告警不阻断开局。
- **SaveCheckpointToSlot**：把**当前内存检查点**写手动槽——局中任意时刻手动存档，回滚点都是本场战斗开始（颗粒度决策，消费房购买会被回滚，见 ADR-0041 后果）。
- **RequestLoadSlot → LoadAsync**：读槽（`JsonUtility.FromJson<RunSaveSnapshot>`，版本不符忽略）→ 若在局内先 `shell.ReturnToMainMenu()` 并轮询收口（4 秒超时）→ 收口后再让一帧（避免与 EnterMainMenu 同帧表现清理竞争）→ `shell.BeginRun(CreateRestore(snapshot))`。
- **落盘**：全部经 `RunSaveStoreHook.StoreOrNull()`（ES3 桥缺失时 LogError 一次并不可用）；写入前 `snapshot.StampSavedAtNow()`。
- UI 入口在 `Ui/RunSaveLoadPanel`（局内功能菜单存档/读档模块，非本目录）。

### 7. QuickTest 通道

- **载荷**：`QuickTestRunOptions{ NodeOrder, PinnedFirstBattleDeckId, SkillIds, TrapContentIds }`；主菜单长按 `\` 选码 0–9 释放确认（入口在 `NineGrid.DevTest` 的 `QuickTestEntryInputHandler`，经 `GameFlowController.TryBeginQuickTestFromPickerCode` → Shell）。
- **通道表**（`QuickTestDeckCatalog.sPresets`）：`\0` 流程测试（空技能/空机关 + Sequential，正式开局内容镜像 + 作弊）；`\1`–`\9` 效果体验通道，各配 1–3 个 skillId（一怪一技按格号升序挂载，经 `BattleSessionCheat.TryAttachSkillsToBoardMonsters`）+ 1 张定向注入机关。
- **节点队列**（`QuickTestRunPlanner`）：从 `node_deck_rules` 收集去重节点索引，每配置复制 `FinalFloor` 次；Sequential 原序 / Shuffled `UnityEngine.Random` 洗牌（表现 RNG，不消费 Core RNG）。
- **作弊注入点**都在 Orchestrator：`ApplyQuickTestTrapCardsIfNeeded`（发牌选项阶段）、`ApplyQuickTestAvatarCheatsIfNeeded`（HP99/ATK5 每关重置）、`ApplyQuickTestSkillMountsIfNeeded`、`ApplyQuickTestTimeScale`（当前 QuickTestTimeScale=1）。
- QuickTest 卡面短描述（`BuildCardFaceDescription`，<16 字硬截断）仅供动态挂技能的白板怪临时卡面，非正式文案权威。

### 8. 局内过场（Transitions/）

- **两种 fader**（Feel 插件）：同层进房 `MMFaderDirectional`（方向由 `DirectionalBiasPicker` 四向袋洗牌——抽完再洗，避免连续同向）；跨层 `MMFaderRound`（iris 洞心定位 Avatar 世界位：优先活卡 Transform → 格锚 → 屏幕中心）。
- **两段式 API**：`PlayCoverRevealAsync(crossFloor, midAction, ct)` 完整盖-切-揭；或 `BeginCoverAsync`（盖住后 `IsCoverHeld=true` 挂起）+ `CompleteRevealAsync`（进消费房：图标驻留处先盖，等 `Present*Board` 刷完货架再揭，避免揭开后跳切；`ShouldDeferRevealForInRoomBoard` 判定）。
- `WillCrossFloor`：`RunModel.NodeIndex+1 >= NodesPerFloor && Floor < FinalFloor`。
- `ForceClearFaders`：异常/取消路径强制收遮罩，防全屏黑死；`ForceRoundHidden` 是因为「Feel 用 float== 判断是否 DisableFader，不可靠」的收尾兜底。
- Round 层级卫生：stencil 要求 Mask 的 sibling index 必须小于 Background（`EnsureRoundHierarchy`）。
- 过场音效唯一发射点 `BeginCoverAsync`（`FlowRoomEconomyAudioCues.PulseTransition`）。
- 参数 SO：`RunSceneTransitionSettingsSO`（Resources `Transitions/RunSceneTransition`），表现层配置器有「过场」foldout；`TransitionTweenCurve` 枚举值对齐 Feel `MMTween.MMTweenCurve` 序号。

## 对外通信面

- **写**：`SetGameFlowShellStateCommand`（唯一相位写入）、`BeginGameFlowRunCommand` / `ReturnToMainMenuCommand`（Controller 发）；Core 侧 `phase.StartNode` / `SelectRoom` / `EnterRoom`（后两者优先走 `RoomChoiceCoreHook` 委托）。
- **读**：`RunModel`（进度真相）、`IPhaseSystem.CurrentPhase/CanExecute`、`PendingChoiceModel`、`IRewardSystem.BuildNodeDeckOptions`、`IContentSystem`。
- **信号**：`Orchestrator.Signal(GameFlowSignal)` 由 Shell 转发（`SettlementReady` ← `BattleSessionSettlementReadyEvent`；`BattleEnded` ← `BattleSessionEndedEvent`）。
- **驱动的兄弟模块**：`BattleSessionSystem`（入场/结算/奖励）、`RoomIconBoardPresenter` 及四个房内 Presenter（[房间篇](08-房间流程与场地板.md)）、`BattleInfoPreviewPresenter`、`RunSceneTransitionService`、`AvatarWalkSystem`、`BoardBriefTipPresenter`（Notice 出口）、`RunSummaryPanel` / `CharacterSelectPanel` / `PlayerAudioSettingsPanel`（Ui/）、`BattleSessionCheat`（QuickTest 作弊）。
- **音视效**：主菜单按钮 Hover/按下/拒绝/取消音（`GameFlowController` 是主菜单 Start/Quit 等 cue 的唯一权威发射者，`AudioCue` 声明属性就写在类上）；胜负 SFX/VFX、回主菜单 SFX、过场 SFX。

## 关联 ADR

ADR-0021（节点编排归 Core、4/7 非战斗、通关判定）、ADR-0041（检查点/恢复/自动存档生命周期）、ADR-0042（教学分派）、ADR-0020（选房与房内板走场地交互面）、ADR-0004（`PresentationInputGates` 覆盖层/外部租约语义）。

## 不变量与坑

- **相位只经 Command 写**：`GameFlowShellHook.PublishState` 已 Obsolete no-op，专防旧「MainGameLoop→Shell 镜像」路径偷偷写第二份相位。
- `EnsureBattleNodeBootstrap` 的末路 `BootstrapRun(preserveRunInventory:true)` 会 `player.Reset`——跨关必须带回遗物/金币/道具卡（否则中途遗物全丢）；每次决策记 FlowTrace `BootstrapRun`（decision/relicsBefore/After）。
- `GameFlowController.Awake` 对 DisableDomainReload 做防御：上一局 Shell 相位/IsBusy 残留会静默吞掉 StartRun，故非 MainMenu 或 Busy 时先 `ReturnToMainMenu()`；`OnDestroy` 先 `ForceMainMenuAuthority` 再 Unbind。
- 主菜单输入在 `PlayerAudioSettingsPanel.IsOpen` 或 `BattleUiDimmerOverlay.IsActive`（半黑屏盖住主菜单）时不响应。
- 场景引用只在 Awake / 显式 `EnsureViewBindings` 时解析（`FindDeep` 走 `Resources.FindObjectsOfTypeAll` 兜底），Update 仅轮询缓存。
- QuickTest 的 `PrepareQuickTest` 必须在 `ResetNodeProgress` 之后调用（后者清 QuickTest 字段）。
- 教学局跳过 `HandleRunEnded`（防误删正式局自动存档）与检查点捕获。
- `RunSaveService.LoadAsync` 的 4 秒收口轮询用 `Time.realtimeSinceStartup`——这是流程层收口等待，不属于 IntentIntake 门禁（门禁禁壁钟约束不适用于此处）。
