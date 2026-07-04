# 战斗环节（Battle）

独立状态机：`NineGrid.GameFlow.BattleController`，挂在 MainScene 的 `GameFlow` 上，由主流程 `GameFlowController.BeginBattlePhase()` 拉起。

当前不完善，大量子玩法留位；先保证序章入场后能进战斗、权限开关、敌人信息面板与调试退场。

## 触发

| 时机 | 行为 |
|------|------|
| 序章 `ProloguePerformance.Completed` | `GameFlowController` → `BeginBattlePhase()` → `BattleController.EnterBattle()` |
| `Battle0`（已历序章） | 双方就位后同样 `BeginBattlePhase()` |
| 其他战斗节点 | 同上（遭遇配置仍占位） |
| 后续细化 | 等主流程给出更精确的触发时机再接线 |

**不**在战斗结束后调用 `GameFlow.Advance()`；完善战斗后再接下一节点。

## 状态

```
Idle → Entering → Active → Exiting → Idle
```

- **Entering**：解锁权限 + 敌人信息面板入场（介绍文字默认显示）
- **Active**：点玩家锻造 / 点敌人抛锚准备 / hover 仅高亮
- **Exiting**：收权限、退面板与文字、玩家走到 `player exit`

## 战斗开始解锁

| 项 | 状态 |
|----|------|
| 玩家船 hover + outline，可点击 | 启用 `player` 上 `SelectableSceneElement`；点击进锻造 |
| 敌方船 hover + outline，可点击 | 启用 `enemy` 上 `SelectableSceneElement`；点击抛锚准备 |
| 可进入锻造模式 | `CanEnterForgeMode` → `HydraulicSceneController.Enter()`（宿主可默认失活，Enter 时拉起） |
| 可进入抛锚互撞 | `CanEnterAnchorMode` → `TryEnterAnchorMode()`：`Fire()` 后立刻 `ResetToIdle()`（测试占位） |
| 演出：Enemy Info Panel 入场 | `EnemyIntroducePanelController.RequestShow()`，战斗中保持显示 |

序章演出期间双方 `SelectableSceneElement` 保持 **disabled**，避免提前高亮。

## 战斗中

1. **点击玩家** → `TryEnterForgeMode()` → 液压锻造子状态机
2. **Hover 敌人** → 仅 outline 高亮（面板不随 hover 显隐）
3. **点击敌人** → `AnchorChainLauncher.Fire()` 后立刻取消（抛锚互撞子状态机 TODO）

## 敌人信息面板（UI）

`NineGrid.UI.EnemyIntroducePanelController`，挂在 UI 根，由 `UiSystem.EnemyInfo` 暴露。

只保留 **Enemy Info Panel** 一套（不再区分 Info / Introduce 两套行为）：

| 对象 | 作用 |
|------|------|
| `Enemy Info Panel` | 面板本体（stay 位） |
| `Enemy Info Panel in` / `Enemy Introduce Panel in` | 入场前待命 / 退场终点 |
| `Enemy Info Text` / `Enemy Introduce Text` | TextAnimator 文案，面板就位后播放 |

- 战斗开始：缓动入场 + 默认显示介绍文字
- 战斗结束：收起文字与面板

API：`RequestShow(text?)` / `RequestHide()` / `HideImmediate()`。

## 战斗结束

| 项 | 状态 |
|----|------|
| 胜负条件（敌/我死亡） | `NotifyCombatantDefeated(bool playerDied)` 占位 |
| 退场 Enemy Info Panel + 文字 | `RequestHide()` |
| 收回开始时解锁的权限 | `LockPermissions()` |
| 玩家缓动到 `player exit` | DOTween |
| 接主流程下一节点 | **不做** |

### 调试（`DebugHotkeyInput`：新 Input System 优先，旧 Input 兜底；挂在始终激活的 `BattleController`）

| 键 | 行为 |
|----|------|
| 小键盘 **1** | 液压入场 / 常规退场（宿主可失活，由此拉起） |
| 小键盘 **2** | 液压完成退场 |
| 小键盘 **4** | 结束战斗（仅 `Active`） |

液压宿主默认失活时自身 `Update` 不跑，因此小键盘统一由 `BattleController` 监听。仅走旧 `Input.GetKeyDown(Keypad*)` 在本项目里经常收不到键。

## 场景挂载

- `GameFlow`：`GameFlowController` + `SceneElementPointerSelector` + `BattleController`
- `player` / `enemy`：`SelectableSceneElement`（默认 disabled，战斗时启用）
- `液压场景`：默认 **失活**，`HydraulicSceneController.Enter()` 外部拉起，退场后再失活
- `AnchorChainSystem`：`AnchorChainLauncher`
- `UI`：`UiSystem` + `EnemyIntroducePanelController`
