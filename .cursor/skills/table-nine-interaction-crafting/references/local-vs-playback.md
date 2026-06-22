# 本地反馈 vs 回放 — 判定与示例

## 判定流程（canvas 图例原文）

1. **它改变权威状态吗？** → 回放（泳道 C）
2. **它只是输入反馈吗？** → 本地（泳道 B 直驱 Performance）
3. **它需要别的表演"配合"吗？** → 不存在；靠输入锁 + 快照

## 对照表

| 用户动作 | 类型 | 谁演 | 是否 Command |
|----------|------|------|--------------|
| 鼠标悬停卡牌 | 本地反馈 | `CardHoverPerformance` | 否 |
| 点击选中 | 本地反馈 | `CardSelectedPerformance` | 否 |
| 长按 | 本地反馈 | `CardHoldPerformance` | 否 |
| 拖拽道具预览 | 本地反馈 | `ItemCardInteractPerformance` | 否 |
| 确认攻击/拾取/用道具 | 意图 | FSM Confirm 节点 | **是** |
| 内核拒绝意图 | 轻反馈 | `ShowRejectedIntent`（抖动/提示） | 否（消费 `ActionRejected` 事件） |
| 伤害/击杀/旋转/发牌 | 回放 | 适配器 + Playback Performance | 否（消费 Batch） |
| 批末对齐 | 回放 | `CoreViewSnapshot` snap | `PresentationFinishedCommand` |

## 时序（解锁态一次完整交互）

```text
用户 Hover        → FSM: Idle→Hover        → LocalFeedback.Play
用户 Click        → FSM: Hover→Selected    → LocalFeedback.Play
用户 Confirm 攻击 → FSM: Selected→Cmd      → SendCommand(Attack)
内核单帧结算      → Batch 推送             → InputLockGate: locked
FSM              → Watching               → 吞操作，Hover 仍可用
PresentationBatchPlayer 播完             → PresentationFinishedCommand
InputLockGate    → unlocked               → FSM: Watching→Idle
```

## 与 adapter-crafting 的衔接点

- 交互 FSM **从不**订阅 `PresentationBatch` 来播攻击/发牌动画
- Confirm 发 Command 后，FSM 职责结束；后续全是泳道 C
- 若 Confirm 后需要即时视觉（如卡牌脱手），用**本地**短反馈（如 0.1s 缩放），且必须在 Watching 前 `StopAndRestore`，避免与回放 tween 打架

## 与 performance-crafting 的衔接点

- LocalFeedback 与 Playback 共用 `*Performance` 形态与预览契约
- 区别仅在于**调用方**：FSM 直驱 vs 适配器直驱
- 同一张卡的 Hover 与 Deal 必须是不同 Performance（或同一 Performance 的不同模式），不要混在一个类里同时处理指针与 Batch
