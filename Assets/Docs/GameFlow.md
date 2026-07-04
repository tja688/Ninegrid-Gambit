# 主流程状态机（GameFlow）

跨场景全局唯一，入口：`NineGrid.GameFlow.GameFlowController`（`PersistentMonoSingleton`，`DontDestroyOnLoad`）。启动时 `RuntimeInitializeOnLoadMethod` 自动创建，无需手动挂场景。

## 完整战役链

```
序章教学(Prologue / 战斗0)
  → 岛屿 → 路线选择 → 事件1 → 战斗1
  → 岛屿 → 路线选择 → 事件2 → 战斗2
  → 岛屿 → 路线选择 → 事件3 → 战斗3(精英)
  → 岛屿(精英奖励) → 战斗4
  → 岛屿 → 路线选择 → 事件4 → 战斗5
  → 岛屿 → 路线选择 → 事件5 → 战斗6
  → 岛屿 → 路线选择 → 事件6 → BOSS战
  → 胜利结算
```

对应枚举：`GameFlowState`（`Assets/Scripts/GameFlow/GameFlowState.cs`）。

## 分流规则

| 情况 | 行为 |
|------|------|
| 首次进入游戏（未完成序章） | 无主菜单，直接 `Prologue` |
| 序章 `Advance()` | 标记序章完成，进入 `Island1` |
| 主菜单开新局（已完成序章） | 进入 `Battle0`（无教学） |
| 中途暴毙 | `NotifyPlayerDefeated()` → `MainMenu` |
| 胜利结算结束 | `NotifyVictorySettled()` / `Advance()` → `MainMenu` |

序章完成标记：`GameFlowProgress.HasCompletedPrologue`（PlayerPrefs，可换存档）。

## 场景映射

| 状态类型 | 场景 |
|----------|------|
| 序章 / 全部战斗 / BOSS | `MainScene` |
| 主菜单 | `MainPanelScene` |
| 岛屿（含精英奖励） | `IslandScene` |
| 路线选择 | `RouteScene` |
| 事件 / 胜利结算 | `TransitionalScene` |

## 对外 API（占位可接）

- `StartNewRun()` — 主菜单开始
- `Advance()` — 当前节点完成，进下一节点
- `NotifyPlayerDefeated()` / `NotifyVictorySettled()` / `GoToMainMenu()`
- `ForceState(state)` — 调试跳转
- `StateChanged` — `(previous, next)` 事件

`Prologue` 进入时播放 `DialoguePool` 的 `prologue_intro`，对话结束后自动 `Advance()`。其余状态 `OnEnter` 仍为占位，战斗初始化、岛屿 UI、事件表等后续按状态补。详见 `UiDialogueNotice.md`。
