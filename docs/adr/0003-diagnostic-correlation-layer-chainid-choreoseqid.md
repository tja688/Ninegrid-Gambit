---
status: proposed
---

# 表演诊断走"两级关联键：chainId 连锁根 + choreoSeqId 动作内展开"

## 决策

表演/编排诊断的关联机制收敛为**两级关联键**，取代当前三套互不相认的 ID 与一个全局可变批标签：

- **`chainId`（连锁根）**：绑一次 `InputIntent` 脚本的生命周期（一条 `BattleTimeline` 从 `BuildScript` 到跑空且无缓冲 intent）。它罩住这次操作引发的**所有独立 batch**——融合、盘面稳定化补牌、发牌、入组。分配点在 `PresentationDirector` 受理 intent 时；清除点在 `HardClearIntents` 与主线 idle 且无缓冲 intent 时。
- **`choreoSeqId`（动作内展开，复用现有）**：`ChoreoTraceContext` 的栈式序号，管**单个动作内部**的父子层级、begin/end 配对、耗时、settle 端。

约束（硬性）：Cards 层与 Flow 层所有表演诊断埋点**必须**在 payload 同时带 `chainId` 与 `choreoSeqId`。废除 `FlowFieldTraceSink.CurrentBatchTag` 全局可变字符串——它是并行动作 log 污染的根源。九套 recorder（Perf/Flow/Battle/Registry/Director/Choreo…）作为输出汇保留，但**关联键统一为这一套**。

**边界**：本轮只重建诊断/关联层，不碰游戏黑盒——手牌拖拽/hover、卡组布局/tween、融合动画数学、场地几何一行不动。游戏运行时行为零改动，可随时回滚。

## 为什么

核心诉求是"一条绝对清晰、无断联的 log 线，能按内核投影看出每步做了什么"。排查后确认，log 拼不起来的机械根因**不在架构纯不纯，而在关联键**：

- **三套 ID 各说各话**：`DirectorTrace.batchId`（扁平 int）、`ChoreoTraceContext.choreoSeqId`（栈式 int）、`FlowFieldTraceSink.CurrentBatchTag`（全局可变 string）。融合那步记 `batchId=42`，发牌那步记 `CurrentBatchTag="opening"`，两者无共同键可 join。
- **连锁是"延迟兄弟"而非"嵌套"**：融合产生的盘面稳定化补牌由 `BoardStabilizationScheduler` 推迟为 Director 主线上的**独立 batch**。因此栈式 `choreoSeqId` 单独用会在每个兄弟 batch 断开，`batchId` 单用也罩不住整条连锁——只有绑 intent 生命周期的 `chainId` 是语义正确的连锁边界。
- **全局 string tag 并行必污染**：`SetBatchTag("pickup")` / `ClearBatchTag()` 是全局 set/clear，pickup 撞 deckReturn 时互相覆盖，该段 log 直接串味。
- **`.Forget()` 无 settle 端**：发牌 `MoveRippleAsync(...).Forget()` 只有"发起" log，无"落位/被打断" log，异步空档不可追。

骷髅卡组连锁（补牌→发牌→融合→发牌）是全游戏编排复杂度天花板；两级关联键在此打样，即覆盖后续所有多步表演。此决策是 ADR-0001（统一时间线 + 批次锁步）在**可观测性**维度的延续——0001 统一了执行，0003 统一了对这条执行线的追踪。

## 考虑过的替代

- **推倒重建整个表现层（含手牌/卡组/场地黑盒）**：被否决。log 断联与黑盒是否 QF 化无因果关系；重写会烧掉已验证的正确行为（场地已是 View+GeometrySystem 样板、融合/发牌有测试），并把同一道"融合与补牌是两 batch，拿什么缝"的设计题推迟数月，期间零可观测性收益。
- **只复用 Director `batchId` 当唯一关联键**：被否决。补牌是独立 batch，batchId 会在连锁每一环断开。
- **只给每段套 `choreoSeqId`、不引 chainId**：被否决。栈式序号只表达单动作内嵌套，跨 Director 兄弟 batch 得到互不相识的 #1/#2/#3，仍拼不出"同一次连锁"。
- **接缝抽取（手牌/卡组权威搬进 QF System，视图 tween 留壳）**：不在本轮，作为后续可选项。它改善架构整洁度，但不是 log 断联的必要条件；本轮聚焦最小可验证的可观测性重建。

## 后果

- 新增 `chainId` 需在 `PresentationDirector` 分配/清除，并在 `BoardStabilizationScheduler` 的 defer 补牌跨 batch 传递不丢——这是本轮唯一需触碰调度层的改动，属加法。
- `FlowFieldTraceSink.CurrentBatchTag` / `SetBatchTag` / `ClearBatchTag` 退场；调用点（`CardHandManagerSingleton` pickup 路径、`BoardPresentationPlayer`、`BattleSessionExecutor.Opening`）改为不再写全局 tag，关联键由当前 `chainId`+`choreoSeqId` 上下文提供。
- 骷髅融合 `PresentFusionAsync` 需套根 choreo 作用域（`finally` 兜底 `ForceCloseOpenChoreos`），连锁各段（补牌/入组/发牌）各 push 子 choreo。
- 发牌 `.Forget()` 补 settle 端埋点（飞牌 settle 回调 `RecordExploreTrace(uid,"deal.settled")`，带 choreoSeqId），实现"发起→落位"配对。
- 验收指标：随机跑一次骷髅连锁，`grep chainId=<X>` 拉出全部兄弟动作；每动作 `choreoSeqId` 展开有成对 begin/end + 耗时；无 `choreoSeqId=0` 断点；并行 pickup/deckReturn 无 tag 串味。
- 手牌/卡组/场地黑盒的权威抽取（迁 QF System）明确**不在本轮**。
