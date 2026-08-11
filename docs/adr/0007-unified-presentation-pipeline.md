---
status: accepted
---

# 统一表现管线：多处理器唯一分发出口

## 决策

把 [ADR-0005](0005-card-face-beat-commit.md) 已落地的「排期器 → 卡面数值处理器」升格为输出侧**统一表现管线**：

1. **`BattleBeatScheduler` 是一批结算指令的唯一分发出口。** 可注入多个 `IBattleBeatHandler`；报点时对归属该锚点的每条 pending 指令交给**第一个 `TryApply` 为 true 的处理器**并移出队列。
2. **`PresentationBeat` 表示「表演消费归属」，不是「仅卡面」。** `Impact` / `Settled` 由排期器装载并在报点消费；`None` 表示无表演消费（不装载），理由字段说明为何不进管线。卡面数值、旁路装饰（飘字 / FX 脉冲 / 金币等）共用同一张 `PresentationEventMap` 锚点列，**不另建第二张锚点表**。
3. **装饰处理器不占主线就位回执。** 对齐领域词汇「旁路装饰道」与 ADR-0005「提交瞬时、跳动是装饰」：handler 可发脉冲 / 飘字 / 广播，但不得把装饰 await 进编排主线 ack。
4. **无人认领的已装载指令在 Settled 后仍只报诊断与断言，不做强制对账。** 沿用 ADR-0005 无兜底政策；措辞从「card-face」扩为「presentation instruction」。
5. **后续消费者只注册 handler + 改映射表 Beat，不改排期器骨架。** 卡面路径由既有 `CardFaceStatHandler` 在 Impact / Settled 消费，观感与行为不变。

本票落地多处理器基础设施与文档；#60 已将伤害飘字 / FX 脉冲收口到 Impact 并删除 EventLog 旁路；#61 已将金币 / PlayerInfo HUD 收口到 Settled / Impact 指令并统一 FlushBeats；#62 已将 Bounce 候选项收口到 `RewardOffered` 投影。仍遗留：Relic HUD 直读等其它消费者。

## 为什么

ADR-0005 消灭了卡面直读 Core，但伤害飘字、FX 脉冲、金币、PlayerInfo HUD 等仍在投影瞬间扫 EventLog 或 `SyncFromCore`，与「抬手就涨」同构的双轨再次出现。若为每类消费者另建排期或第二张锚点表，穷尽性与失配面会加倍。

将排期器泛化为「一批指令 × 多处理器 × 同一报点」，输出侧保持**唯一出口**结构（与 ADR-0004 输入唯一收口同源），后续迁移只需加 handler 与改 `Beat` 列。

## 考虑过的替代

- **表现层另建「指令→锚点→处理器」表**：否决——延续 ADR-0005 D3；多一张表即多一处失配。
- **每类装饰各自订阅 EventLog / 投影瞬间扫描**：否决——正是本管线要消灭的双轨。
- **装饰进主线 ack**：否决——与旁路装饰道教义冲突，会拖慢输入互斥。
- **本票一并重填 Damage/Gold Beat 并删旁路**：否决为范围——基础设施与消费者迁移分票，避免大爆破。

## 后果

- 新增 `IBattleBeatHandler`；`CardFaceStatHandler` 实现 `TryApply`；`BattleBeatScheduler` 构造注入 `params IBattleBeatHandler[]`。
- `PresentationBeat` / `NoneReason` 文档措辞改为表演消费归属；组合根注册卡面 + 飘字/FX/金币/Avatar HUD 处理器（#60/#61）；Bounce OfferReward 由 `CardFaceStatHandler` 消费（#62）。
- #60：`PresentEffectTriggersFromEventLog` / `SpawnDamagePopups` 生产旁路删除，`DamageDealt`/`EffectTriggered` → Impact。
- #61：`PresentGoldGainsFromEventLog` 生产旁路删除，`GoldModified` → Settled；排期器 `SyncFromCore` 衔接补丁删除；非锁步路径经 `BattleBeatFlush.PresentEventLogSlice`。
- #198：金币装饰锚点由 `Settled` 改至 `Impact`——尸体 Vacate 前消费 `UpdateGold` 保出生点（对齐 ADR-0018 飘字保坐标精神）；`ModifyGoldAction` 可选来源卡并把 `CardUid` 写入 `GoldModified`，击杀/移除赏金与拾取加金传递正确来源 uid；非战斗 EventLog slice 仍走 FlushBeats 的 Impact，行为不回归。
- #62：`RewardOffered` → Settled；`RewardEntry` 投影绝对值；Bounce 删除 `ApplyVisualsByDefId(..., clearCombatStats: true)` 数值旁路。仍遗留：Relic HUD 直读。

## 相关

- [ADR-0001](0001-battle-presentation-unified-timeline-batch-ack.md) — 统一时间线与批次就位回执
- [ADR-0005](0005-card-face-beat-commit.md) — 卡面数值锚点提交（本决策将其泛化为统一管线）
- [ADR-0004](0004-input-intake-two-axis-gating.md) — 输入唯一收口；输出侧结构同源
- Issue #60 — 伤害飘字与 FX 脉冲收口到 Impact
- Issue #61 — 金币与 PlayerInfo HUD 收口到指令 + FlushBeats
- Issue #62 — Bounce 候选项走结算指令投影