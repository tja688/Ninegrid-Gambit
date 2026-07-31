# 表演诊断关联层重建 · chainId + choreoSeqId Spec（可切 ticket）

> **用途**：把"骷髅连锁 log 断联"根治为**两级关联键**，产出一条 `grep chainId` 即可拉全、`choreoSeqId` 即可展开的表演追踪线。给并行改代码的 AI / 人对齐。
> **权威源**：[ADR-0003](../../docs/adr/0003-diagnostic-correlation-layer-chainid-choreoseqid.md) · 父决策 [ADR-0001](../../docs/adr/0001-battle-presentation-unified-timeline-batch-ack.md)
> **性质**：只重建诊断/关联层。**不碰游戏黑盒**（手牌拖拽/hover、卡组布局/tween、融合动画、场地几何）。运行时行为零改动，可回滚。
> **靶子**：骷髅卡组连锁（补牌→发牌→融合→发牌）= 编排复杂度天花板，打样即覆盖后续所有多步表演。

---

## 1. 一句话目标

把表演诊断从「三套互不相认的 ID（batchId 扁平 / choreoSeqId 栈 / CurrentBatchTag 全局 string）+ 全局标签并行污染 + `.Forget()` 无 settle 端」收敛为：

**`chainId`（连锁根，绑 intent 生命周期）+ `choreoSeqId`（动作内展开，复用现有栈）双键，全埋点强制携带，废全局 tag，发牌补 settle。**

---

## 2. 锁定模型（改代码前先背）

| 关联键 | 绑定 | 职责 | 分配/清除 |
|--------|------|------|-----------|
| **chainId** | 一次 `InputIntent` 脚本生命周期（一条 `BattleTimeline` 从 `BuildScript` 到跑空且无缓冲 intent） | 罩住这次操作引发的**所有独立 batch**（融合/补牌/发牌/入组）——连锁根 | `PresentationDirector` 受理 intent 时分配；`HardClearIntents` 与主线 idle 且无缓冲 intent 时清除 |
| **choreoSeqId** | 单个动作（现有 `ChoreoTraceContext` 栈） | 动作内父子层级、begin/end 配对、耗时、settle 端 | `BeginChoreo`/`EndChoreo`，已存在 |

**硬约束**：Cards 层 + Flow 层所有表演诊断 payload **必须**同带 `chainId` 与 `choreoSeqId`。
**禁止**：`FlowFieldTraceSink.CurrentBatchTag` 全局可变 string（本轮废除）；任何新的第四套关联键。

### 为什么不是单键（关键事实）

- 补牌被 `refillDeferredToDirector` 推迟成**独立 batch**（测试 `FusionRefillIsSeparateBatchAfterRotatePresent` 坐实）→ `batchId` 单用在连锁每环断链。
- `choreoSeqId` 是**栈**，只表达单动作内嵌套；跨 Director 兄弟 batch 得到互不相识的 #1/#2/#3 → 单用拼不出"同一次连锁"。
- 唯有绑 intent 生命周期的 `chainId` 是语义正确的连锁边界。

---

## 3. 现状断点证据（改前必读）

| 断点 | 位置 | 症状 |
|------|------|------|
| 融合零 choreo 作用域 | `SkeletonDeckPresentationManager.PresentFusionAsync` | 全程无 `BeginChoreo`，链内所有 `choreoSeqId=0` |
| 补牌是延迟兄弟 | `BoardPresentationPlayer:1172` `onFusionStarted: null` + `refillDeferredToDirector` | 补牌不在融合 await 栈内，是 Director 独立 batch |
| 全局 tag 并行污染 | `CardHandManagerSingleton` pickup 路径 13 处 `SetBatchTag/ClearBatchTag` | pickup 撞 deckReturn 互相覆盖，log 串味 |
| 发牌无 settle 端 | `CardDeckManagerSingleton.TryDealCard` `MoveRippleAsync(...).Forget()` | 只有"发起" log，无"落位/被打断" |
| 忙态 6 源聚合 | `BattleSessionExecutor.WaitPresentationIdleAsync` | 需注释"别聚合会假忙"，无单一权威 |

---

## 4. 数据流（目标）

```text
玩家操作 → InputIntent → PresentationDirector.BuildScript
    → 分配 chainId（活到脚本跑空）
    → BattleTimeline: 融合 batch（choreo#1 begin..end，带 chainId）
    → defer 补牌 batch（choreo#2 begin..end，带 同一 chainId）
    → 发牌 batch（choreo#3；发起 + settle 配对，带 同一 chainId）
    → 主线 idle 且无缓冲 intent → 清除 chainId
```

验收：`grep chainId=X` → 整条连锁全部兄弟动作；每动作 `choreoSeqId` 展开 begin/end + 耗时 + settle。

---

## 5. Ticket 拆分（建议 6 张，含依赖）

> 依赖：T1 → T2 → (T3 ∥ T4 ∥ T5) → T6。T1 是地基，必须先落。

### T1 · 新增 chainId 载体 + Director 分配/清除
- **文件**：`PresentationDirector.cs`、`DirectorTrace.cs`
- **做**：`PresentationDirector` 在 `TrySubmitIntent`/`BuildScript` 分配单调 `chainId`；`HardClearIntents` 与 `Tick` 主线 idle 且无缓冲 intent 时清除。`DirectorTrace` 暴露 `CurrentChainId` 并在 `BasePayload`/`AppendBusyFields` 写入。
- **验收**：intent 受理→跑空，`DirectorTrace` 全程带同一 chainId；HardClear 后归零。
- **风险**：低（加法，不改行为）。

### T2 · chainId 跨 defer 补牌传递不丢
- **文件**：`FusionRefillScheduler.cs`、`UseItemIntentScriptFactory.cs`、`BoardPresentationPlayer.cs`
- **做**：`AppendAfterRotatePresent` / `EnqueueRefillBatches` 追加的补牌 Step 继承当前 chainId（不因新 batch 重分配）。
- **验收**：融合 batch 与其 defer 补牌 batch 的 log 带**同一** chainId。
- **依赖**：T1。**风险**：中（触碰调度追加路径，属加法）。

### T3 · 骷髅融合套根 choreo + 连锁子 choreo
- **文件**：`SkeletonDeckPresentationManager.PresentFusionAsync`
- **做**：开头 `BeginChoreo("SkeletonFusion", {resultUid, participantCount, chainId})`，`finally` 兜底 `ForceCloseOpenChoreos`。`ExitResultToDeckAsync`（入组）push 子 choreo；补牌段（Director 侧）push `Refill` 子 choreo。
- **验收**：融合链无 `choreoSeqId=0`；`GetOpenChoreoSummary` 输出父子链。
- **依赖**：T1。**风险**：低（纯埋点）。

### T4 · 废 CurrentBatchTag，改读双键
- **文件**：`FlowFieldTraceSink.cs`、`FieldTraceHelper.cs`、`CardHandManagerSingleton.cs`（13 处）、`BoardPresentationPlayer.cs`、`BattleSessionExecutor.Opening.cs`
- **做**：删 `CurrentBatchTag`/`SetBatchTag`/`ClearBatchTag`；调用点改为不写全局 tag，payload 由当前 chainId+choreoSeqId 上下文注入。
- **验收**：并行 pickup + deckReturn，两者 log 各带自身 chainId/choreoSeqId，无串味。
- **依赖**：T1。**风险**：中（调用点多，但均为删除+替换）。

### T5 · 发牌补 settle 端埋点
- **文件**：`CardDeckManagerSingleton.TryDealCard`、飞牌 settle 回调（`GroundMotionExecutor`/`DealFlightCoordinator`）
- **做**：`MoveRippleAsync(...).Forget()` 之后，飞牌 settle 回调补 `RecordExploreTrace(uid, "deal.settled", ...)` 带当前 choreoSeqId + chainId。
- **验收**：每次发牌"发起→落位/中止"在同一 choreoSeqId 下成对。
- **依赖**：T1。**风险**：低（纯埋点）。

### T6 · 骷髅连锁验收 + 指标固化
- **做**：跑既有骷髅融合/连锁测试（`FusionVerticalSliceTests`、`ResolveFusionRefillBatchCommandTests` 等）；补一条"连锁 log 完整性"断言（chainId 一致、无 choreoSeqId=0、settle 配对）。
- **验收**：见 §6。
- **依赖**：T2–T5。**风险**：低。

---

## 6. 验收指标（Definition of Done）

1. **链完整**：随机跑一次骷髅连锁，`grep chainId=<X>` 拉出融合+补牌+发牌+入组全部兄弟动作，无遗漏。
2. **内部可展开**：每动作 `choreoSeqId` 有成对 begin/end + `durationMs`。
3. **无断点**：连锁链内 `choreoSeqId=0` 计数为 0。
4. **无污染**：并行 pickup/deckReturn，log 无 tag 串味（各带独立 chainId/choreoSeqId）。
5. **settle 配对**：每次发牌"发起"必有对应"落位/中止"log。
6. **行为零改**：既有骷髅/融合/发牌测试全绿；无游戏运行时行为变更。

---

## 7. 明确不做（边界护栏）

- 不动手牌拖拽/hover 命中/ScreenToWorld、卡组布局/tween、融合动画数学、场地几何。
- 不把手牌/卡组权威搬进 QF System（接缝抽取属后续可选轮次）。
- 不新增第四套关联键；不保留任何全局可变标签。
- 不改 ADR-0001 的时间线/批次锁步语义（本轮是其可观测性延续，非重定义）。
