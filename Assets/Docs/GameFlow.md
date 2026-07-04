# 主流程状态机（GameFlow）

跨场景全局唯一，入口：`NineGrid.GameFlow.GameFlowController`（`PersistentMonoSingleton`，挂在 MainScene，`DontDestroyOnLoad`）。

`GameFlowController` 本身**只管状态**（`Advance()` 只改 `CurrentState`）。真实的**场景加载 + 过渡蒙版 + 节点入场**由常驻编导 `SceneFlowDirector` 监听 `StateChanged` 驱动（自举，无需手动挂）。详见 `全流程接线交接笔记.md`。

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

战斗结束触发 `BattleController.BattleFinished`，由 `SceneFlowDirector` 接住并自动 `Advance()`（当前不分胜负，真实胜负判定见 `全流程接线交接笔记.md`）。
