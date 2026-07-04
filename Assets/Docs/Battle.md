# 战斗环节（Battle）

独立状态机：`NineGrid.GameFlow.BattleController`，挂在 MainScene `GameFlow` 上。  
由主流程 `GameFlowController.BeginBattlePhase()` 拉起（序章演出结束 / `Battle0` 就位 / 其他战斗节点占位）。

> 当前为 jam 骨架：权限、面板、锻造入口、抛锚试射、调试退场已通。胜负、互撞、主流程衔接等见文末缺口。

---

## 流程总览

```
主流程 BeginBattlePhase
        │
        ▼
   EnterBattle()
        │
        ▼
 ┌── Entering ──────────────────────────────┐
 │  UnlockPermissions                       │
 │  Enemy Info Panel 缓动入场 + 默认介绍文字 │
 └──────────────────┬───────────────────────┘
                    ▼
 ┌── Active ────────────────────────────────┐
 │  点玩家 → 锻造（液压子场景）             │
 │  点敌人 → 抛锚准备（播完后取消，占位）   │
 │  Hover 船 → outline 高亮                 │
 │  小键盘 4 → ExitBattle                   │
 └──────────────────┬───────────────────────┘
                    ▼
 ┌── Exiting ───────────────────────────────┐
 │  LockPermissions                         │
 │  若锻造开着 → 液压常规退场               │
 │  Enemy Info Panel 缓动退场 + 藏文字      │
 │  玩家缓动到 player exit                  │
 │  （不 Advance 主流程）                   │
 └──────────────────┬───────────────────────┘
                    ▼
                  Idle
```

状态枚举：`BattlePhaseState` = `Idle | Entering | Active | Exiting`。

---

## 触发与主流程关系

| 时机 | 行为 |
|------|------|
| `Prologue` 演出 `Completed` | `BeginBattlePhase()` → `EnterBattle()` |
| `Battle0`（已历序章） | 双方就位后 `BeginBattlePhase()` |
| 其他 `IsBattleState` 节点 | 同上（遭遇配置 **缺口**） |
| 战斗结束 | **不**调用 `GameFlow.Advance()`（**缺口**：接岛屿 / 下一节点） |

后续若主流程给出更精确触发点，只改 `GameFlowController.BeginBattlePhase` 的调用时机即可，不必动战斗内部状态机。

---

## 战斗开始（Entering）

| 步骤 | 实现 | 备注 |
|------|------|------|
| 解锁玩家 hover / 点击 | `SelectableSceneElement.enabled = true` | 序章期间默认 disabled |
| 解锁敌人 hover / 点击 | 同上 | 点击进抛锚，非锻造 |
| `CanEnterForgeMode` | `true` | |
| `CanEnterAnchorMode` | `true` | |
| Enemy Info Panel 入场 | `EnemyIntroducePanelController.RequestShow()` | stay + TextAnimator 介绍文字 |

---

## 战斗中（Active）

### 锻造模式（液压子场景）

专文：`HydraulicForge.md`（出料、桌面 10 槽、三台拖拽堆叠、遮罩与调参）。

- 入口：点击玩家 / 小键盘 **1**（调试）→ `HydraulicSceneController.Enter()`
- 宿主 `液压场景` **默认可失活**；`Enter()` 先激活再跑协程，退场结束后再失活
- **开启瞬间**：`EnterStarted` → `EnemyInfo.SuspendImmediate()`（面板 + 文字瞬间消失）
- **退出瞬间**：`ExitCompleted` / `HydraulicCompleted` → `ResumeImmediate()`（瞬间回到 stay + 文字）
- 战斗已进入 `Exiting` 时不 `Resume`，避免退场闪一下再藏

液压自身：入场 / 常规退场（小键盘 1）/ 锤击完成退场（小键盘 2）/ 出料（小键盘 5）。锤击读台结算与战斗数值挂钩仍为 **缺口**。

### 抛锚（占位）

- 入口：点击敌人 → `TryEnterAnchorMode()`
- 现况：`AnchorChainLauncher.Fire()`，等到 `Attached` 后 `ResetToIdle()` 取消
- **缺口**：互撞子状态机（拉近、结算、失败/成功分支等）

### 敌人信息面板

组件：`EnemyIntroducePanelController`（`UiSystem.EnemyInfo`）。

| 对象 | 作用 |
|------|------|
| `Enemy Info Panel` | 面板 stay |
| `Enemy Info Panel in` / `Enemy Introduce Panel in` | 入/出场待命点 |
| `Enemy Info Text` / `Enemy Introduce Text` | 介绍文案 + TextAnimator |

| API | 用途 |
|-----|------|
| `RequestShow` / `RequestHide` | 战斗开始入场 / 结束退场（缓动） |
| `HideImmediate` | 立即清掉（开战前、强制重置） |
| `SuspendImmediate` / `ResumeImmediate` | 锻造开关时瞬间藏/恢复 |

战斗中面板默认保持显示，**不**随 hover 显隐。

---

## 战斗结束（Exiting）

| 步骤 | 现况 |
|------|------|
| 胜负条件 | **缺口**：`NotifyCombatantDefeated(bool playerDied)` 仅占位 |
| 收权限 | `LockPermissions()` |
| 关锻造 | 若液压 `Active` 则 `Exit()` |
| 退 Enemy Info | `RequestHide()` |
| 玩家出场 | DOTween → `player exit` |
| 接主流程 | **缺口**：不 `Advance()` |

调试：小键盘 **4** → `ExitBattle()`（仅 `Active`）。

---

## 调试热键

监听挂在始终激活的 `BattleController`（`DebugHotkeyInput`：新 Input System 优先，旧 Input 兜底）。  
液压宿主失活时自身 `Update` 不跑，故小键盘不依赖液压对象。

| 键 | 行为 |
|----|------|
| 小键盘 **1** | 液压入场 / 常规退场 |
| 小键盘 **2** | 液压完成退场 |
| 小键盘 **4** | 结束战斗 |
| 小键盘 **5** | 液压 Active 时管道出一份材料 |

---

## 场景挂载

| 对象 | 组件 |
|------|------|
| `GameFlow` | `GameFlowController`、`SceneElementPointerSelector`、`BattleController` |
| `player` / `enemy` | `SelectableSceneElement`（默认 disabled） |
| `液压场景` | `HydraulicSceneController`、`HydraulicMaterialLane`、`HydraulicMaterialBoard`（默认可失活） |
| `AnchorChainSystem` | `AnchorChainLauncher` |
| `UI` | `UiSystem`、`EnemyIntroducePanelController` |
| `player exit` | 出场点位 |

---

## 后续缺口（按优先级随手补）

1. **胜负**：HP / 死亡 → `NotifyCombatantDefeated`，胜负演出分支  
2. **抛锚互撞子状态机**：Fire 之后不 Cancel，接拉近、结算、回到 Active  
3. **锻造结算**：锤击时读取三台材料堆叠 → 战斗数值 / 卡牌（布置与拖拽见 `HydraulicForge.md`）  
4. **主流程衔接**：`ExitCompleted` 后按胜负 `Advance()` 或回菜单  
5. **遭遇配置**：按 `GameFlowState` 换敌人、文案、船锚目标  
6. **多敌 / 多面板**：当前单敌人、单 Info Panel  
7. **输入**：正式操作键位（非小键盘）、手柄  
8. **音效 / 镜头**：入场、锻造、抛锚、退场  

相关笔记：`GameFlow.md`、`UiDialogueNotice.md`、`SceneElementSelection.md`。
