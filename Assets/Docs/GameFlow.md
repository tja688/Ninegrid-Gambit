# 主流程状态机（GameFlow）

跨场景全局唯一，入口：`NineGrid.GameFlow.GameFlowController`（`PersistentMonoSingleton`，挂在 MainScene，`DontDestroyOnLoad`）。

**不自动加载场景**；状态推进只改 `CurrentState`，场景跳转由外部在需要时申请。

## 开局

Inspector 字段 **Enter Prologue On Start**（`enterPrologueOnStart`，默认勾选）：

| 选项 | 行为 |
|------|------|
| 勾选 | 开局进入 `Prologue`，播 `prologue_intro` |
| 取消勾选 | 开局进入 `Battle0`，并标记已历序章通道 |

## 完整战役链

```
序章教学(Prologue / 战斗0)
  → 岛屿 → 路线选择 → 事件1 → 战斗1
  → … → BOSS战 → 胜利结算
```

对应枚举：`GameFlowState`。

## 对外 API

- `StartNewRun()` / `Advance()` / `ForceState(state)`
- `NotifyPlayerDefeated()` / `NotifyVictorySettled()` / `GoToMainMenu()`
- `StateChanged` — `(previous, next)`，**不含**场景加载

场景映射约定仍见 `GameFlowScenes`（供后续手动跳转使用）。UI/对话见 `UiDialogueNotice.md`。

## 战斗环节

序章演出结束 / `Battle0` 就位后，`BeginBattlePhase()` 转交 `BattleController`（独立状态机，挂在 MainScene `GameFlow`）。详见 `Battle.md`。

战斗结束暂不 `Advance()`。
