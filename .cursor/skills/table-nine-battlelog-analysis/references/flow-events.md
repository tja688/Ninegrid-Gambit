# CoreLog（FlowTrace）事件名与挂点

落盘：`Assets/Notes/Logs/CoreLog/corelog-{sessionId}-seed{seed}.json`  
（过渡兼容：旧文件可能仍叫 `flowlog-*`，或位于已废弃的 `Assets/Notes/FlowLog/`。）

与 BattleLog / PerfLog 共享 `sessionId` / `seed`（`DiagTraceShared`）。  
共通节奏：`beatId`（`DiagBeatClock`；与 PerfLog 对齐）。  
`schemaVersion`：**3**（含 `beatId`；V1/V2 事件名仍保留）。

> **命名**：文档与 skill 称 **CoreLog**；代码类名仍为 `FlowTrace*`。  
> **OccupancySnapshot ≠ 世界坐标**：逻辑占格在此；画面位置见 PerfLog（[`perf-events.md`](perf-events.md)）。

## category

| category | 含义 |
|----------|------|
| `Loop` | 主循环状态机 |
| `UI` | 玩家入口操作 |
| `CoreGate` | Core 门禁 / 选择结果 |
| `CombatSummary` | 战斗摘要（细节在 BattleLog） |
| `Deck` | 发牌 / 牌组空 / DealAttempt·DealResult |
| `Economy` | 金币等经济变动（可抽成 OtherLog/EconomyLog） |
| `Field` | 占格登记 / Vacate / Conflict / Snapshot / HopPlan |
| `Presentation` | Drain 阶段、SyncDiff、Opening 进度（逻辑侧忙闲；视觉细节在 Perf） |
| `Hand` | 入手 / Release 生命周期 |

## name → 挂点（V1）

| name | category | 挂点文件 |
|------|----------|----------|
| `EnterMainMenu` | Loop | `MainGameLoopManagerSingleton.EnterMainMenuImmediate` |
| `StartRun` | UI | `MainGameLoopManagerSingleton.BeginRun` |
| `ReturnMainMenu` | UI | `MainGameLoopManagerSingleton.ReturnToMainMenu` |
| `SetState` | Loop | `MainGameLoopManagerSingleton.SetState`（payload: from/to） |
| `RewardPresented` | CoreGate | `PlayRewardChoiceAsync`；跳过时 `skipped=true` |
| `RewardChosen` | CoreGate | `InBattleManagerSingleton.PresentRewardChoiceFromCoreAsync` |
| `RoomPresented` | CoreGate | `PlayRoomChoiceAsync`；跳过时 `skipped=true` |
| `RoomChosen` | CoreGate | `SelectRoom` 后 |
| `EnterRoom` | CoreGate | `EnterRoom` 后（含拒因） |
| `StartNode` | CoreGate | `InBattleManagerSingleton.StartBattleNodeInternalAsync`；含 `nodeIndex` / deck/board 计数 |
| `CombatHitSummary` | CombatSummary | `ApplyCombatHitFromCore`；`refBattleOpIndex` → BattleLog |
| `PostKillBoard` | Deck | `ResolvePostKillBoardFromCore` |
| `Victory` / `Defeat` | Loop | `ShowBattleEndAndReturnAsync` |
| `GoldGained` / `GoldSpent` | Economy | `PresentGoldGainsFromEventLog` |

## name → 挂点（V2 占格 / 表现逻辑）

每条尽量带 `nodeIndex` + `batchTag`；另有顶层 **`beatId`**。`refBattleOpIndex` 对齐同局 BattleLog。

| name | category | 挂点 | 关键 payload |
|------|----------|------|----------------|
| `DrainBegin` / `DrainEnd` | Presentation | `DrainPostKillBoardAsync`（Beat=`PostKillDrain`） | moves/deals/busy 标志 |
| `HopPlan` | Field | `ApplyBoardMovesAndHopInternalAsync` | `plans`=`uid:from→to;…` |
| `DealAttempt` / `DealResult` | Deck | `CardDeckManagerSingleton.TryDealCard` | uid/slot/ok… |
| `OccupancyConflict` | Field | `TryRegisterCardAtSlot` 拒登 | `accepted=false` |
| `OccupancySnapshot` | Field | Drain/Sync/StartNode 前后 | `coreHash`/`presHash`/`diffSlots`/`hasDiff` |
| `SyncDiff` | Presentation | `SyncBoardOccupancyFromCore` | vacated/placed/spawned/swept |
| `OpeningDealProgress` | Presentation | `PresentOpeningAsync`（Beat=`OpeningDeal`） | uid/slot/ok |
| `HandAcquire` / `HandRelease` | Hand | Pickup 生命周期 | uid/displayMode |

### coreHash / presHash / diffSlots

- 跳过 avatar 格 5；格式 `1:12|2:15|…`
- `diffSlots` 仅不一致格：`3:C26/P0,7:C0/P32`
- `hasDiff=false` 且 `accepted=true` → Core 与表现**登记表**一致（仍可能画面错 → 查 PerfLog）

### batchTag（冗余；优先用 beatId / beatKind）

| 值 | 场景 |
|----|------|
| `postKill` | DrainPostKill → beatKind `PostKillDrain` |
| `opening` | PresentOpening → `OpeningDeal` |
| `startNode` | StartNode 后 Snapshot |
| `sync` | 独立 Sync → `SyncBoard` |

## 占格验证剧本

1. **连杀补牌**：Drain 后 Snapshot `hasDiff=false`；无 `OccupancyConflict`
2. **画面仍错**：同 `beatId` 打开 PerfLog（模式 C）
3. **跨关 / 入手 / deck 空**：同前

## 插桩

```csharp
FlowTraceRecorder.Record(
    FlowTraceCategory.UI,
    "MyCustomEvent",
    new Dictionary<string, string> { { "key", "value" } });
// beatId 由 DiagBeatClock 自动写入
```

占格门面：`FieldTraceHelper`；Cards 旁路：`FlowFieldTraceSink`。  
表现坐标：`CardPresentationProbe` / `PerfTraceRecorder`（见 perf-events）。

## 导出

- 胜负 / `BeginRun` 轮转 / Play 退出 / Keypad4：`ExportBothNow` → Battle + CoreLog + PerfLog
- 目录：`Logs/CoreLog`、`Logs/PerfLog`、`Logs/OtherLog/BattleLog`
