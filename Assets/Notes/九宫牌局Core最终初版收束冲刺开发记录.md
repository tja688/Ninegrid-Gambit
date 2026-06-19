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
