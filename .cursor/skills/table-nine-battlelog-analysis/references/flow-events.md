# FlowTrace V1 事件名与挂点

落盘：`Assets/Notes/FlowLog/flowlog-{sessionId}-seed{seed}.json`  
与 BattleLog 共享 `sessionId` / `seed`（`DiagTraceShared`）。

## category

| category | 含义 |
|----------|------|
| `Loop` | 主循环状态机 |
| `UI` | 玩家入口操作 |
| `CoreGate` | Core 门禁 / 选择结果 |
| `CombatSummary` | 战斗摘要（细节在 BattleLog） |
| `Deck` | 发牌 / 牌组空 |

## name → 挂点

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
| `StartNode` | CoreGate | `InBattleManagerSingleton.StartBattleNodeInternalAsync` |
| `CombatHitSummary` | CombatSummary | `ApplyCombatHitFromCore`；`refBattleOpIndex` → BattleLog |
| `PostKillBoard` | Deck | `ResolvePostKillBoardFromCore`；`deckEmpty`=抽牌堆空；`enemyDrawEmpty`=无怪可抽 |
| `Victory` / `Defeat` | Loop | `ShowBattleEndAndReturnAsync` |

## 插桩

任意处：

```csharp
FlowTraceRecorder.Record(
    FlowTraceCategory.UI,
    "MyCustomEvent",
    new Dictionary<string, string> { { "key", "value" } });
```

## 导出

- 胜负 Notice：当场 `ExportBothNow`（落盘当前局）
- `BeginRun`：若已有上一局（StartRun/胜负/Battle ops）→ 再导出并 **轮转新 sessionId**，两边清空对齐
- Play 退出：`BattleTraceRecorder.ExportOnPlayExit` 顺带 Flow
- DevTest Keypad4：`ExportBothNow`
- Keypad5：同步开关 BattleTrace + FlowTrace `Enabled`
