---
name: table-nine-interaction-crafting
description: >-
  Build TableNine/NineGrid interaction FSMs that drive LocalFeedbackPerformance
  on pointer input, emit Commands only on Confirm, and enter Watching when
  InputLockGate is active. Use when the user asks to 做交互/hover/拖拽/选中/长按/
  本地反馈/交互FSM/输入锁观演, or to wire pointer gestures to performances without
  touching kernel playback adapters.
---

# TableNine 交互 FSM + 本地反馈

**心智模型：泳道 B（交互 FSM）直驱本地反馈；Confirm 才发 Command；输入锁升起时整泳道进 Watching，只留只读 Hover。**

表现层三泳道（见 [`Assets/Notes/表现层方法论.md`](Assets/Notes/表现层方法论.md) 与表现层 canvas V0.2）：

| 泳道 | 职责 | 本 skill |
|------|------|----------|
| **A** Flow Shell | `GamePhase` 粗粒度投影（主菜单/节点进行中/奖励屏…） | 只读上下文，不在这里写 hover |
| **B** Interaction FSM | 指针手势 → 本地反馈；Confirm → Command | **本 skill 主场** |
| **C** Playback Pipeline | 内核 Batch → 适配器 → 回放表演 | 交给 `table-nine-adapter-crafting` |

权威 canvas：[`九宫牌局表现层.canvas`](file:///C:/Users/jinji/Desktop/文档/MyNote/游戏开发项目/引擎工作区/九宫牌局架构/九宫牌局表现层.canvas)（V0.2 三泳道 + 输入锁脊柱）。

## Required Context

动手前读：

- 四盒子总览 — [`Assets/Notes/表现层方法论.md`](Assets/Notes/表现层方法论.md)
- 内核↔表现契约 — [`Assets/Notes/九宫牌局权威顶层架构设计.md`](Assets/Notes/九宫牌局权威顶层架构设计.md) §9
- Command/Event 清单 — [`Assets/Notes/Core表现层Command与Event消费清单-2026-06-20.md`](Assets/Notes/Core表现层Command与Event消费清单-2026-06-20.md)
- 本地反馈黑盒制作 — [`.cursor/skills/table-nine-performance-crafting/SKILL.md`](../table-nine-performance-crafting/SKILL.md)（颜色 **④ LocalFeedback**）
- 回放接入 — [`.cursor/skills/table-nine-adapter-crafting/SKILL.md`](../table-nine-adapter-crafting/SKILL.md)（颜色 **② Playback**）

落地前读 `rules.md`；不手改 `.unity`；代码落地后触发 Unity 刷新并读 Console。

## 黄金法则

1. **本地 vs 回放三分法**（canvas 图例）：
   - 改变权威状态？→ 泳道 C 回放，**不在交互 FSM 里演**。
   - 只是输入反馈？→ 泳道 B 直驱 `*Performance`，**不发 Command**。
   - 需要别的表演"配合"？→ **不存在**；靠输入锁 + 批末快照对齐，不靠 FSM 互等。
2. **盲目乐观 ≠ 输入锁。** 解锁态：用户 Confirm → 先发 Command，内核裁决；`ActionRejected` → 轻量 `ShowRejectedIntent` 本地反馈。**不要**在发 Command 前用 Query 挡操作。
3. **锁起进 Watching。** `IPresentationSyncSystem.IsInputLocked == true` 时，交互 FSM 进入观演子态：吞掉一切可改状态的交互（点击攻击、拖拽道具、Confirm），**仅保留只读 Hover**（介绍/高亮）。
4. **状态驱动反馈，Confirm 驱动意图。** `Idle→Hover→Selected→Hold/Drag` 每个进入/退出调对应 `LocalFeedbackPerformance`；只有落到 `*Command` 节点时才 `SendCommand`。
5. **演员按 `CardUid`、锚点按 `SlotId`。** 本地反馈只移动/高亮已有演员，不 reparent、不改 Model。位置是数据，不是父子关系（见表现层方法论 §三）。
6. **多了吃掉、少了去补、对不上就问。** 交互所需只读信息缺了 → 补 Core Query/Command 字段；**不在 FSM 造假**；映射歧义用 AskQuestion。

## 通用工作流（6 步）

```
Task Progress:
- [ ] 1. 听描述：哪种交互（场地卡/道具卡/覆盖层选择）、手势链、基准手感在哪
- [ ] 2. 画 FSM：列状态、转移条件、哪些态直驱本地反馈、哪个态发 Command
- [ ] 3. 对齐 Command：查 Core 已有 Command；Confirm 发什么、带哪些参数
- [ ] 4. 本地反馈：已有 *Performance 则挂载调用；没有则走 performance-crafting 先烘焙
- [ ] 5. 写 FSM + InputLockGate 订阅：解锁/锁起、Watching 分支、Rejected 反馈
- [ ] 6. 验证：解锁态 Confirm→Command→锁起→Watching；批末 Finished→解锁；Hover 全程可用
```

### Step 1 — 听描述

收集：交互对象（棋盘格/道具槽/覆盖层选项）、手势（hover/click/hold/drag）、是否需要选目标（飞刀/帮助卡）、现有视觉基准（DOTween/录屏/已有脚本）。

### Step 2 — 画 FSM

从 canvas 已有规划出发，按域选模板（详见 [`references/fsm-templates.md`](references/fsm-templates.md)）：

| FSM | 典型链 | Confirm 发 Command |
|-----|--------|-------------------|
| `BoardInteractionFsm` | Idle→Hover→Selected→(Hold / KnifeTargeting)→Cmd | Attack / Pickup / ClickEmpty / UseItem(带目标) |
| `ItemCardInteractionFsm` | Idle→Hover→Drag→Confirm→Cmd | UseItem |
| `SelectionOverlayMode` | 由 `PendingChoiceKind` 拉起；覆盖奖励/房间/路线/帮助卡 | SelectReward / SelectRoom / EnterRoom / SkipHelpChoice |

**进入/退出态**必须成对调用本地反馈的 `Play` / `StopAndRestore`（或等价），避免 tween 泄漏。

### Step 3 — 对齐 Command

查：

- `GameCommandKind` — [`Assets/Scripts/NineGrid.Core/Domain/CoreEnums.cs`](Assets/Scripts/NineGrid.Core/Domain/CoreEnums.cs)
- 具体 Command 类 — [`Assets/Scripts/NineGrid.Core/Commands/CoreCommands.cs`](Assets/Scripts/NineGrid.Core/Commands/CoreCommands.cs)
- 只读合法性提示（可选，不用于挡操作）— `CanInteractQuery` 等

**禁止**为 hover/drag/selected 动画发 Command。`UseItem` 若缺 `selectedCardUids` / `option` 参数，先补 Core Command 再写 Confirm 节点。

### Step 4 — 本地反馈

本地反馈是盒子③里的 **LocalFeedback** 子类（canvas 颜色 ④），命名如 `CardHoverPerformance`、`CardSelectedPerformance`、`ItemCardInteractPerformance`。

- 已有黑盒 → FSM 状态 `OnEnter/OnExit` 里 `Play(actor, …)` / `StopAndRestore()`
- 没有 → 先走 `table-nine-performance-crafting` 烘焙，**再**回到本 skill 接线

本地反馈契约与 Playback 相同（可打断、双通道预览），但**禁止**消费 `PresentationBatch`、**禁止**发 `PresentationFinishedCommand`。

### Step 5 — 写 FSM + 输入锁

**落盘约定（本项目）**

- FSM 脚本：`Assets/Scripts/NineGrid.Presentation/FSM/`
- 命名空间：`NineGrid.Presentation.FSM`
- 类名以 `Fsm` 或 `InteractionFsm` 结尾（如 `BoardInteractionFsm`）
- 输入锁闸门：`InputLockGate`（或等价）订阅 `IPresentationSyncSystem`，向各交互 FSM 广播 `EnterWatching` / `ExitWatching`

**Watching 行为（硬约束）**

```text
IsInputLocked == true:
  - 忽略：click / drag / confirm / 发 Command
  - 允许：Hover（只读介绍/高亮）
  - 本地反馈：仅 Hover 类；Selected/Drag/Hold 若已激活则 OnExit 还原
IsInputLocked == false:
  - 恢复正常 FSM 转移
```

**Command 发送**

- 经 `CoreCommandDispatcher` / QFramework `SendCommand`，不直写 Model
- Confirm 后立即本地清理交互态（退出 Selected/Drag），**不等**回放结束
- 回放由泳道 C 接管；FSM 不监听 Batch 逐条指令

### Step 6 — 验证

- 解锁：Hover 有反馈；Selected/Hold/Drag 跟手；Confirm 发出正确 Command
- 锁起：Confirm/Click 无效；Hover 仍可用；无 Command 泄漏
- Reject：`ActionRejected` 事件 → `ShowRejectedIntent`（轻提示/抖动），FSM 回 Idle 或保持 Selected（按设计）
- 批末：`PresentationFinishedCommand` → 解锁 → FSM 退出 Watching

## 与相邻 skill 的边界

| Skill | 盒子/泳道 | 分工 |
|-------|-----------|------|
| **本 skill** | 泳道 B · 交互 FSM | 指针状态机、Watching、Confirm→Command、直驱 LocalFeedback |
| `table-nine-performance-crafting` | ③ LocalFeedback / Playback | 烘焙 `*Performance` 黑盒（手感/预览） |
| `table-nine-adapter-crafting` | 泳道 C · ④ Adapter | 内核 Event→回放表演；批末快照对齐 |
| `表现层方法论.md` | ①②③④ 总览 | 锚点/演员/表演/导演四盒子 |

**本 skill 不做的事**：写适配器、消费 `PresentationBatch`、编排同 `ActionId` 并行、替内核判断 Command 合法性。

## 脊柱依赖（只读/订阅，不在 FSM 里重写）

| 组件 | 职责 |
|------|------|
| `IPresentationSyncSystem` | `IsInputLocked` / `ActiveBatchId` |
| `InputLockGate` | 镜像锁状态 → 通知各 FSM Watching |
| `ViewRegistry` | `CardUid→演员` · `SlotId→锚点` |
| `CoreViewSnapshot` | 批末对齐用；**交互 FSM 不直接写快照** |
| `CanInteractQuery` | 可选 UI 提示（灰显/文案），不用于挡 Confirm |

## 反模式

- 在 Hover/Drag 里发 Command 或改 Model
- 用 `CanInteractQuery` 结果阻止用户点击（违背盲目乐观）
- 在本地反馈里等待 `PresentationFinished` 才退出 Selected
- 为动画 reparent 演员到槽位子节点
- 把内核 Batch 路由逻辑写进交互 FSM（那是 adapter 的事）
- 每种选择各开一个 FSM（应用 `SelectionOverlayMode` 参数化）
- 锁起时仍允许 Drag/Confirm

## References

| 文件 | 内容 |
|------|------|
| [`references/fsm-templates.md`](references/fsm-templates.md) | 场地/道具/覆盖层 FSM 状态图与转移表 |
| [`references/local-vs-playback.md`](references/local-vs-playback.md) | 本地反馈 vs 回放判定与示例 |
