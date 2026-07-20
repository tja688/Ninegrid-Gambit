# 战斗表演编排 · PresentationDirector Spec（AI 速览）

> **用途**：给并行改代码的 AI / 人快速对齐 #2 设计，避免复活已删旧编排。  
> **权威源**：[#2 Spec](https://github.com/tja688/Ninegrid-Gambit/issues/2) · [ADR-0001](../../docs/adr/0001-battle-presentation-unified-timeline-batch-ack.md) · [`CONTEXT.md`「战斗表演编排」](../../CONTEXT.md) · [落地方案](../../.cursor/plans/表演编排统一时间线重构_f1ba353d.plan.md)  
> **状态快照**：2026-07-20 — #2 Spec 与子树 #3–#11 **CLOSED**；导演为唯一编排出口；反击锁步入主线；输入互斥认 `DirectorMainlineBusy`。  
> **本轮性质**：勿再复活 `_boardPresentationQueue` / `legacyPostPresent` / `RequestBasicAttackInternal` / `RequestBasicCounterAttack*`。

---

## 1. 一句话目标

把战斗内表演从「三套编排 + Core `RunToCompletion` 核先行 + Cards 占格双真相」收敛为：

**唯一 `PresentationDirector` 编排时间线 + 一拍一 Batch + 表演 ack 驱动前进 + 输入意图缓冲 + Core 单一占格真相。**

对标商业参考：逻辑改写与表演步骤**同流交织**，核永不大幅领先屏幕。

---

## 2. 锁定架构（改代码前先背）

| 决策 | 必须遵守 | 禁止 |
|------|----------|------|
| 唯一出口 | 局内表演只走 Director / Timeline | 再开并行编排器、散落 async 链抢时间轴 |
| 批次锁步 | `ResolveBatch` ↔ `Present` + `OpenBatch`/`FinishBatch`；未 ack 不得下一批 | 恢复整局 `RunToCompletion` 核先行 |
| 并发 | **一条串行主线**（输入互斥只认它）+ Step 内可 fork 并行子流 + 极少旁路装饰道 | 用飘字/闪白锁死主交互；旁路占主线 busy |
| 输入 | `InputIntent`；忙时缓冲**最早一条** + `uiPick`；Phase/战败/换层**硬清空** | 忙时静默吞点击；输入回调直接写 Core/Transform |
| 命名 | 表现侧原子叫 **Step** | 用 Action 指代表演原子（Action 专属 Core） |
| 分层 | Director/Timeline/Step/Intent/Trigger 在 **Flow**；Cards **零 Core 引用** | Cards 直接裁决合法性 |
| 占格终态 | Core=`BoardModel` 唯一逻辑真相；Cards=几何注册；合法性 Flow idle 裁决 | 强化 `_uidBySlot` 权威；依赖 `SyncBoardOccupancyFromCore(force)` 静默修 |
| 位置 | 编排收编时间线 + 全局租约；净土域（手牌/卡组）保留 DOTween，边界 `Evict`/`Admit` | 本轮强行统一收敛基元 |
| FX | 脉冲 `Trigger`（发即完成、可降级）；卡时序用显式 `Delay` | `await` FX 当主线完成条件 |

**busy 唯一真相** = `PresentationDirector.IsMainlineBusy`（经 `CombatHitSink.DirectorMainlineBusy` 暴露给 Cards）。`PresentationLocked` 仅作 Pickup/Drain 适配器防重入，并经 `BeginDirectorExternalHold` 挂主线租约。

---

## 3. Issue 树（历史）

| 阶段 | Issue | 内容 | 状态 |
|------|-------|------|------|
| Spec | #2 | 总纲 | **CLOSED** |
| 0 | #3 | 导演骨架 | CLOSED |
| 1–3 | #4–#9 | 垂直切片 | CLOSED |
| 4 | #10 | 占格退场 | CLOSED |
| 5 | #11 | 硬切收尾 | CLOSED |

---

## 4. 数据流

```text
点击/输入 → InputIntent 入队（忙则缓冲+uiPick）
    → PresentationDirector / BattleTimeline
    → Step: ResolveBatch（请 Core 解算一批 + OpenBatch）
    → EventLog 投影为 Present / Delay / Trigger …
    → Step: Present（播表演；主线串行，可 fork 并行子流）
    → ack / FinishBatch → 时间线取下一步
```

攻击未击杀：主线续写 `AttackCounter` Resolve → `CounterHit` Present（非 Forget 旁路）。

---

## 5. 关键落点

| 区域 | 路径（相对 `Assets/Scripts/`） |
|------|-------------------------------|
| 导演 | `Flow/Presentation/PresentationDirector.cs` |
| 攻击/反击剧本 | `Flow/Presentation/AttackIntentScriptFactory.cs` |
| 反击 Present | `Flow/Presentation/CombatCounterPresentChannel.cs` |
| 外部主线租约 | `Flow/Presentation/ExternalMainlineHoldStep.cs` |
| 装配 | `Flow/InBattleManagerSingleton.cs`（EnsurePresentationDirector） |

---

## 6. 诊断

- PerfLog：`path=director`、`DirectorStep*`、`slice=AttackCounter`、`channel=CounterHit`
- `path=presentAdapter`：导演 Present 薄适配内补牌等（非第二编排出口）
- 分析 skill：`table-nine-battlelog-analysis`

---

## 7. 范围外（保持）

收敛基元全域统一、tick 加速、灵动 UI 具体接入、纪律 C/DAG/固定逻辑帧、表现侧 Action 命名回潮。
