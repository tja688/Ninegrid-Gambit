# FlowTrace V2 事件名与挂点

落盘：`Assets/Notes/FlowLog/flowlog-{sessionId}-seed{seed}.json`  
与 BattleLog 共享 `sessionId` / `seed`（`DiagTraceShared`）。  
`schemaVersion`：**2**（V1 事件仍保留）。

## category

| category | 含义 |
|----------|------|
| `Loop` | 主循环状态机 |
| `UI` | 玩家入口操作 |
| `CoreGate` | Core 门禁 / 选择结果 |
| `CombatSummary` | 战斗摘要（细节在 BattleLog） |
| `Deck` | 发牌 / 牌组空 / DealAttempt·DealResult |
| `Economy` | 金币等经济变动（表现侧消费 EventLog 时打点） |
| `Field` | 占格登记 / Vacate / Conflict / Snapshot / HopPlan |
| `Presentation` | Drain 阶段、SyncDiff、Opening 进度 |
| `Hand` | 入手 / Release 生命周期 |

## name → 挂点（V1）

| name | category | 挂点文件 |
|------|----------|----------|
| `EnterMainMenu` | Loop | `MainGameLoopManagerSingleton.EnterMainMenuImmediate` |
| `StartRun` | UI | `MainGameLoopManagerSingleton.BeginRun` |
| `ReturnMainMenu` | UI | `MainGameLoopManagerSingleton.ReturnToMainMenu` |
| `SetState` | Loop | `MainGameLoopManagerSingleton.SetState`（payload: from/to） |
| `RewardPresented` | CoreGate | `PlayRewardChoiceAsync` / 房间事件内奖励；跳过时 `skipped=true` |
| `RewardChosen` | CoreGate | `InBattleManagerSingleton.PresentRewardChoiceFromCoreAsync` |
| `RoomPresented` | CoreGate | `PlayRoomChoiceAsync`；跳过时 `skipped=true` |
| `RoomChosen` | CoreGate | `SelectRoom` 后 |
| `EnterRoom` | CoreGate | `EnterRoom` 后（含拒因） |
| `StartNode` | CoreGate | `InBattleManagerSingleton.StartBattleNodeInternalAsync`；**V2 增** `nodeIndex` / `deckCount` / `boardOccupantCount` / `presOccupantCount` |
| `CombatHitSummary` | CombatSummary | `ApplyCombatHitFromCore`；`refBattleOpIndex` → BattleLog |
| `PostKillBoard` | Deck | `ResolvePostKillBoardFromCore`；`deckEmpty`=抽牌堆空；`enemyDrawEmpty`=无怪可抽 |
| `Victory` / `Defeat` | Loop | `ShowBattleEndAndReturnAsync` |
| `GoldGained` | Economy | `InBattleManagerSingleton.PresentGoldGainsFromEventLog`；payload: `delta` / `amountAfter` / `reason` / `sourceDefId` / `action` |

## name → 挂点（V2 占格 / 表现）

每条 V2 事件 payload 尽量带 `nodeIndex` + `batchTag`；`refBattleOpIndex` 对齐同局 BattleLog。

| name | category | 挂点 | 关键 payload |
|------|----------|------|----------------|
| `DrainBegin` / `DrainEnd` | Presentation | `DrainPostKillBoardAsync` | `moves` / `deals` / `drainInFlight` / `fieldBusy` / `presentationLocked` |
| `HopPlan` | Field | `ApplyBoardMovesAndHopInternalAsync`（过滤后 hopPlans） | `plans`=`uid:from→to;…` |
| `DealAttempt` / `DealResult` | Deck | `CardDeckManagerSingleton.TryDealCard` | `uid` / `slot` / `placeable` / `ok` / `rollback` / `caller` |
| `OccupancyConflict` | Field | `TryRegisterCardAtSlot` 拒登 | `slot` / `existingUid` / `incomingUid` / `caller`；`accepted=false` |
| `OccupancySnapshot` | Field | Drain 前/后、Sync 前/后、StartNode 后 | `phase` / `coreHash` / `presHash` / `diffSlots` / `hasDiff` |
| `SyncDiff` | Presentation | `SyncBoardOccupancyFromCore` | `vacated` / `placed` / `spawned` / `swept` + uid 列表 |
| `OpeningDealProgress` | Presentation | `PresentOpeningAsync` 每格 | `uid` / `slot` / `ok` / `ringIndex` |
| `HandAcquire` / `HandRelease` | Hand | `PullFromGroundAsync` 成功 / Pickup 失败 Release | `uid` / `phase` / `handContains` / `displayMode` |

### coreHash / presHash / diffSlots

- 跳过 avatar 格 5；格式 `1:12|2:15|…`
- `diffSlots` 仅不一致格：`3:C26/P0,7:C0/P32`
- `hasDiff=false` 且 `accepted=true` → Core 与表现占格一致

### batchTag

| 值 | 场景 |
|----|------|
| `postKill` | DrainPostKill |
| `opening` | PresentOpening |
| `startNode` | StartNode 后 Snapshot |
| `sync` | 独立 Sync（非 Drain 内） |
| `pickup` / `hop` / `deal` | 预留 |

## 占格验证剧本（导出 FlowLog 判定）

1. **连杀补牌**：`moveCount=8, dealCount=1` → 无 `OccupancyConflict`；Drain 后 Snapshot `hasDiff=false`
2. **跨关**：node2+ `StartNode.nodeIndex` ≥ 2；`OpeningDealProgress` 全 `ok=true`
3. **道具入手**：`HandAcquire` 后无异常 `HandRelease`；无 MissingReference
4. **deck 空旋转**：`moveCount<8` → hop 仍无 Conflict

## 插桩

任意处：

```csharp
FlowTraceRecorder.Record(
    FlowTraceCategory.UI,
    "MyCustomEvent",
    new Dictionary<string, string> { { "key", "value" } });
```

占格门面（Flow 层）：

```csharp
FieldTraceHelper.RecordOccupancySnapshot("customPhase");
```

Cards → Flow 旁路：`FlowFieldTraceSink`（由 `FieldTraceHelper.RegisterSinkHandlers` 注册）。

## 导出

- 胜负 Notice：当场 `ExportBothNow`（落盘当前局）
- `BeginRun`：若已有上一局（StartRun/胜负/Battle ops）→ 再导出并 **轮转新 sessionId**，两边清空对齐
- Play 退出：`BattleTraceRecorder.ExportOnPlayExit` 顺带 Flow
- DevTest Keypad4：`ExportBothNow`
- Keypad5：同步开关 BattleTrace + FlowTrace `Enabled`
