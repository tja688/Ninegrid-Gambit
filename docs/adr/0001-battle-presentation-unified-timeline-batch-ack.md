---
status: accepted
---

# 战斗表演编排走"统一时间线 + 批次锁步"

## 决策

战斗内表演编排收敛为**唯一一条串行 `BattleTimeline`**，由 `PresentationDirector` 独占所有权与出口。玩家输入转成可缓冲的 `InputIntent`（不直接改状态）；Core 解算不再 `RunToCompletion` 一口气算完整局，而是被拆成一拍一 `Batch`，每批经 `PresentationSyncSystem.OpenBatch/FinishBatch` 由表演的**就位回执（ack）**驱动前进——即"请 Core 解算下一批"与"播一段表演"在同一条时间线上交替。特效/音效退化为**脉冲 Trigger**（发即完成、可降级）；逻辑占格真相唯一归 Core，Cards 不再维护独立占格镜像。

## 为什么

对标商业级参考项目（Lost For Swords，Godot + Entitas）后确认：其表现层稳健的根源是"一条队列即时间线、逻辑解算与表演交织锁步、核永不先行"（`ActionExecutor.Exec` 逐步驻留、`DelayAction` 在真局是真步骤而在 SimContext 塌缩）。TableNine 现状的根病灶正相反——Core 用 `RunToCompletion` 先算完终局（`PhaseSystem.ResolveInteractiveRotation` 一次 Enqueue fill+rotate），表现事后投影追赶，被迫维护第二套占格真相并靠 `SyncBoardOccupancyFromCore(force)` 硬对账，这是"内核与表现层打架"、复杂演出破坏场景态（含写坏收敛塔）的来源。批次锁步在分体内核下忠实还原参考项目的交织语义。

## 考虑过的替代

- **只统一表现侧、不碰 Core（B 方案）**：不改 `RunToCompletion`，仅把三套编排机制收成一个导演。风险最小，但"核先行"残留、双真相仍在，未根治打架。被否决为"治标"，仅作为若批次锁步受阻时的回退门。
- **bigbang 大爆改**：一次性切换。被否决——本轮硬性目标含"还原现有表演效果"，复杂演出是回归高发区，改走**按流程垂直切片渐进 + PerfLog/人眼逐条验收**。

## 后果

- 需触碰 Core 边缘（`ActionPipelineSystem`/`PhaseSystem` 的解算切批，非改规则/黑盒内部），风险高于纯表现层重构；用已有但闲置的 `PresentationSyncSystem`/`CoreCommandDispatcher` 缝接线以降低侵入。
- `_uidBySlot` 占格镜像与 `SyncBoardOccupancyFromCore(force)` 硬对账退场；后者降级为"永不应触发"的断言。
- 位置收敛基元全域统一（手牌/卡组迁到收敛）明确**不在本轮**，净土域保留 DOTween 黑盒、边界走 Evict/Admit。
- tick 加速（忙时连点整线提速）留作未来门，本轮输入缓冲不变速。
