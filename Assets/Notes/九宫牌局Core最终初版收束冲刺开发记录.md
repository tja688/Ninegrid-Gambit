---
tags:
  - Core
  - P4
  - 收束记录
created: 2026-06-19
---

# 九宫牌局 Core 最终初版收束冲刺开发记录

## 背景

质检报告指出 `P4 · 棋盘·发牌·内核状态机` 仍残留明显缺口：关卡完成后只停在 `RewardItemChoice`，没有真正的 pending choice、奖励选择/跳过命令、房间二选一、房间事件、节点推进语义，也没有多节点 replay 覆盖。

本次收束目标是优先打磨 core，确保后续能承接大规模真实游戏模拟测试。

## 本次补齐

### 1. PendingChoiceModel

新增 `PendingChoiceModel`，作为表现层与模拟测试读取选择状态的单一入口：

- `PendingChoiceKind.None / Reward / Room`
- 奖励候选：`PoolId + RewardOptions`
- 房间候选：`RoomOptions + SelectedRoom`
- 版本号：便于后续表现层订阅刷新

`OfferRewardChoiceAction` 从“只写 EventLog”升级为“写 PendingChoiceModel + 写 EventLog”，避免奖励三选一只有日志、没有内核状态。

### 2. 关卡尾段 Command 网关

新增并接通：

- `SelectRewardCommand`
- `SkipHelpChoiceCommand`
- `SelectRoomCommand`
- `EnterRoomCommand`

`PhaseSystem` 现在按阶段声明合法命令：

- `RewardItemChoice`：可选奖励、跳过奖励、拾取格上道具、使用道具槽道具
- `RoomChoice`：可选房间、拾取格上道具、使用道具槽道具
- `RoomEvent`：可进入并结算已选房间
- `NodeCompleted`：可开始下一节点

非法命令仍统一走 `Evt_ActionRejected` 与 `ActionRejected` 事件。

### 3. FLOW_005 时机修正

额外发现：`EconomySystem` 原先在 `NodeCompletedAction` 后通过 `OnNodeEnd` 自动结算未用帮助卡金币，这早于 FLOW_005 的“点击房间按钮时结算”。

本次改为：

- 移除 `OnNodeEnd` 自动结算未用帮助卡
- 新增 `IEconomySystem.SettleUnusedHelpCards()`
- 在 `SelectRoomCommand` 成功后结算未用帮助卡

这样通关后到选房间前，玩家仍有窗口拾取格上道具或使用道具槽道具。

### 4. 节点推进语义

新增 Action：

- `OfferRoomChoicesAction`
- `SelectRoomChoiceAction`
- `AdvanceNodeAction`
- `ClearPendingChoicesAction`
- `ClearPendingRewardChoiceAction`

完整尾段现在为：

```text
ClearCheck
-> NodeCompleted
-> RewardItemChoice / OfferRewardChoice("help.choice")
-> SelectReward 或 SkipHelpChoice
-> RoomChoice / OfferRoomChoices(2)
-> SelectRoom
-> RoomEvent
-> EnterRoom / ResolveRoom
-> NodeIndex++
-> NodeCompleted，可 StartNode
```

### 5. 覆盖测试

新增/调整 `P4NodeFlowTests`：

- 更新完整节点 replay 的终态断言：`RewardItemChoice` 不再允许直接 `StartNode`，而是允许奖励选择/跳过与通关后拾取/用道具。
- 新增 `NodeTailSkipRewardSelectRoomAndEnterAdvancesNode`：覆盖“通关 -> help.choice pending -> 跳过奖励 +10 -> 房间二选一 -> 选择房间 -> 进入房间 -> `NodeIndex++` -> 回到可 `StartNode`”。
- 新增 `RewardChoiceOverlayStillAllowsBoardPickupWithoutRepeatingCompletion`：覆盖通关覆盖层期间拾取格上帮助卡，并断言不会重复发放通关奖励或重复完成节点。

## 验证结果

- Unity MCP 脚本刷新与编译：通过。
- Unity Console：无本次代码错误；仅剩 Easy Save 3 / Test Framework 既有 warning。
- Unity EditMode：`NineGrid.Core.Tests` 共 114 条，114 passed，0 failed，0 skipped。

## 暂留边界

以下仍按规划留给后续房间/表现层收束：

- 商店/酒馆的完整内部交互仍是后续 P4+ / 表现联调范围；当前 core 已能进入房间并触发 `ResolveRoom`，商店/宝箱类 offer 先以事件形式落地。
- 选择奖励后的“长期牌池/局外持有”语义仍需随后续内容与表现契约继续细化；本次保持与现有 `GrantRewardFromPoolAction` 一致的发放路径。

---

## P7 Core 表现契约与本地测试网补齐

### 背景

质检报告指出 `Assets/Notes/九宫牌局Core分段落地规划.md` 的 P7 仍有明显缺口。由于当前策略是“先打磨 core，再做表现层”，本次优先补齐高 ROI 的 core 契约、批次握手、只读视图、ActionLog 数据与本地 replay fixture，不进入 Unity UI/动画表现实现。

### 本次补齐

#### 1. EventLog → 表现适配器映射表

新增 `PresentationEventMap`，将所有 `CoreEventType` 映射为：

- `PresentationInstructionKind`
- `PresentationEventCategory`
- 是否需要播放
- 是否需要锁输入等待整批握手
- 面向 ActionLog/调试面板的显示标签

首批重点覆盖并测试了质检报告点名的 `Damage / Move / Rotate / Kill / Phase / RewardOffered`，同时要求所有现有 `CoreEventType` 都必须有映射，避免新增事件后表现契约静默缺项。

#### 2. 批次播放 + `PresentationFinishedCommand`

新增 `PresentationBatch / PresentationInstruction / PresentationBatchFactory`，支持从 `EventLog` 的指定起点生成一批待播放表现指令。

新增 `IPresentationSyncSystem / PresentationSyncSystem`：

- `OpenBatch(batch)`：当 batch 内存在锁输入事件时进入 `IsInputLocked`
- `PresentationFinishedCommand(batchId)`：表现层整批播放完成后解锁
- batch id 不匹配时拒绝，避免旧动画回包误解锁新批次

`PhaseSystem.CanExecute()` 现在会在表现锁期间只允许 `PresentationFinished`，但未主动打开 batch 时不影响现有纯 core 模拟与测试。

#### 3. 最小只读 View Model

新增 `CoreViewSnapshotFactory.Capture()` 与 `CoreViewSnapshot`：

- 9 格棋盘只读槽位视图：uid / defId / kind / hp / armor / attack / blessed
- 化身 uid/slot
- phase / nodeIndex / coins / interactionCount
- pending reward / room choice 状态

这为后续最小棋盘/卡面读 Model 提供验证型契约，不需要表现层直接理解 Core 内部 Model 写接口。

#### 4. ActionLog 可视化数据底座

新增 `ActionLogProjector` 与 `ActionLogRow`，可把 `EventLog` 投影成面板友好的逐行数据：

- sequence / actionId / actionName
- event type / instruction kind / category
- actor / target / card / fromSlot / toSlot / amount / delta
- summary 文本

本次不做 EditorWindow/UI Toolkit 面板，只先把“技能为什么没生效”的核心数据行固定下来，后续 UI 只消费该投影结果。

#### 5. Stub 表现适配器

新增 `IPresentationAdapter` 与 `RecordingPresentationAdapter`：

- 逐条记录 batch 与 instruction 日志
- 对 `MoveCard / DealCard / SpawnCard / RemoveCard / KillCard / PickItem` 维护一个简单 uid→slot 镜像

它用于 core 测试和后续表现联调前的假播放通道，先保证 EventLog → 指令 → adapter 的管道可跑。

#### 6. 本地 replay fixture 执行器

新增 `P7ReplayFixtureRunner`（位于 `NineGrid.Core.Tests`）：

- seed + bootstrap + replay steps
- 当前支持 StartNode / AttackFirstMonster / SkipHelpChoice / SelectRoom / EnterRoom
- 执行后返回 `CoreViewSnapshot + ActionLogRows`

这为后续“大规模真实游戏模拟测试”提供可扩展的本地夹具雏形，不依赖 Presentation。

### 覆盖测试

新增 `P7PresentationContractTests`：

- `EveryCoreEventHasPresentationMapping`
- `BatchPlaybackLocksInputUntilPresentationFinishedCommand`
- `MinimalViewSnapshotExposesBoardAndPendingChoice`
- `ActionLogAndStubAdapterCanInspectRealReplayEvents`
- `LocalReplayFixtureRunnerExecutesNodeTailInCoreTests`

### 验证结果

- Unity MCP 全量刷新与脚本编译：通过。
- Unity Console：0 error；仅剩 Easy Save 3 / Test Framework 既有 warning。
- Unity EditMode：`NineGrid.Core.Tests` 共 119 条，119 passed，0 failed，0 skipped。
- 架构守卫：`Assets/Notes/CI/check-core-guards.ps1` 通过。

### 后续交接

本次已一次性落完 P7 高优先级 core 缺口；暂不进入表现 UI/动画实现。后续表现层可以直接接：

- 用 `PresentationBatchFactory.FromEventLog()` 做 EventLog 批次化
- 用 `IPresentationSyncSystem.OpenBatch()` 锁输入
- 动画播完发 `PresentationFinishedCommand(batchId)` 解锁
- 棋盘/卡面读取 `CoreViewSnapshot`
- 调试面板读取 `ActionLogProjector.FromEventLog()`

后续可选项仍保留在表现层阶段：ActionKit 复杂串并行时间线、拖拽/选目标/多子状态表现 FSM、Checkpoint 中途握手、运行时热重载 UI。
