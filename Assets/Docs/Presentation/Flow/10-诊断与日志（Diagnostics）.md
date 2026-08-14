# 诊断与日志（Diagnostics）—— 五轨 Trace · 关联键 · 手动 Bug 快照

> 权威代码：`Flow/Diagnostics/`（24 个文件）。
> 关联 ADR：ADR-0003（chainId + choreoSeqId 诊断关联）、ADR-0039（判死谓词对齐 Core）；相关票 #142（floor/nodeIndex 统一关联字段）、#51（占格分叉降级为断言）

## 职责综述

五条互相独立又共享会话身份的旁路日志轨，Play 结束自动落盘（配套技能 `table-nine-battlelog-analysis`）。

> 本篇是**给 AI 深挖的原生流水**：全量、带程序噪音、用内部 defId/uid。给人当场看的提炼版是另一套东西，见 [11-人读战斗日志（BattleLog）](11-人读战斗日志（BattleLog）.md)——两者代码与数据均不相干，只是同读 `EventLog`。
**落盘根目录**：Editor → `Assets/Notes/Logs/`；**Development Player → exe 旁 `GameLogs/Logs/`**（2026-08-13 起，此前为 persistentDataPath；试玩者可直接找到打包发给开发者）。

| 轨 | 记录器 | 内容 | 落盘子目录 |
|----|--------|------|---------|
| **Battle** | `BattleTraceRecorder` | 每次结算门（CombatHit / PostKillBoard / StartNode）的 EventLog 切片 + 表现 verdict | `Logs/OtherLog/BattleLog/` |
| **Flow（CoreLog）** | `FlowTraceRecorder` | 全流程语义事件（节点、奖励、占格、编排、Pickup、Lease 等 50+ 具名事件） | `Logs/CoreLog/` |
| **Perf** | `PerfTraceRecorder` | 表现层时间线（Motion / BoardSnap / Director 剧本 / 音频 / VFX / Anomaly） | `Logs/PerfLog/` |
| **Registry** | `RegistryTraceRecorder` | CardManager 注册表生命周期（Delta / Release / Audit / FieldVisualGap / 拾取门禁） | `Logs/OtherLog/RegistryLog/` |
| **Console** | `ConsoleTraceRecorder` | Debug Console 抓取（Warning / Error / Exception / Assert，主抓报错；普通 Log 不进轨；环形上限 2000 条） | `Logs/OtherLog/ConsoleLog/` |

**总纪律**：打点失败一律吞掉，绝不影响结算/表演路径；Editor/Development 默认启用（四轨经 `DiagTraceExportPreferences` 可关；Console 轨只受自动落盘总开关约束）。

## 关键类型表

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `BattleTraceRecorder`（静态类） | `Diagnostics/BattleTraceRecorder.cs` | Battle 轨记录器：RecordOp 补齐 floor/nodeIndex/opIndex；Play 退出串联四轨导出 |
| `BattleTraceModels`（多类型） | `Diagnostics/BattleTraceModels.cs` | Battle 轨数据模型（Session/Op/CardSnap/EventRow/Presentation/VerdictHints） |
| `BattleTraceJson`（静态类） | `Diagnostics/BattleTraceJson.cs` | Battle 轨手写 JSON 序列化（零第三方依赖，规避 JsonUtility 限制） |
| `FlowTraceRecorder`（静态类） | `Diagnostics/FlowTraceRecorder.cs` | Flow 轨记录器：自动填 beatId/floor/nodeIndex/refBattleOpIndex |
| `FlowTraceModels`（多类型） | `Diagnostics/FlowTraceModels.cs` | Flow 轨模型：Category / 50+ 稳定事件名常量（含交战反打裁决 `CounterVerdict`：firstStrike/counterScheduled/skipBanned〔附禁反击来源〕/skipAvatarDefeated/skipInvalid/skipNoChannel/rejected；遗物挂载轨 `RelicGranted`〔route=grant/reactivate〕/ `RelicMountAudit`〔declared/implemented/mounted/modifiers 四数 + `mods=[…]` 每条修饰符形态〕/ `ConditionalModifierAudit`〔条件修饰符激活态：route=initial/flip，detail 带 hp/有效上限快照〕；管线熔断 `PipelineFault`〔深度/总量/Apply 异常，ADR-0047〕——均由 `RhythmFaceFlowTraceBinder` 从 EventLog 翻译）/ sceneTag 可选标记 |
| `FlowTraceJson`（静态类） | `Diagnostics/FlowTraceJson.cs` | Flow 轨手写 JSON 序列化 |
| `PerfTraceRecorder`（静态类） | `Diagnostics/PerfTraceRecorder.cs` | Perf 轨记录器：Beat 管理 + BoardSnap + 实时异常检测 + 镜像 Registry |
| `PerfTraceModels`（多类型） | `Diagnostics/PerfTraceModels.cs` | Perf 轨模型：60+ Kind 常量、打点站点、异常码 |
| `PerfTraceJson`（静态类） | `Diagnostics/PerfTraceJson.cs` | Perf 轨手写 JSON 序列化 |
| `RegistryTraceRecorder`（静态类） | `Diagnostics/RegistryTraceRecorder.cs` | Registry 轨记录器：镜像 Perf + IdleWatch 自动审计 + FieldVisualGap 检测 |
| `RegistryTraceModels`（多类型） | `Diagnostics/RegistryTraceModels.cs` | Registry 轨模型（独立于 PerfLog，便于缺卡类 bug 单线分析） |
| `RegistryTraceJson`（静态类） | `Diagnostics/RegistryTraceJson.cs` | Registry 轨手写 JSON 序列化 |
| `DiagTraceShared`（静态类） | `Diagnostics/DiagTraceShared.cs` | 四轨共享会话身份（sessionId/seed/runTag）、落盘基础设施、Play 退出去重 |
| `DiagTraceExportPreferences` + `DiagTraceTrack` | `Diagnostics/DiagTraceExportPreferences.cs` | 自动落盘与内存记录偏好（EditorPrefs 持久化；手动导出不受限） |
| `DiagTraceManualSnapshot`（静态类） | `Diagnostics/DiagTraceManualSnapshot.cs` | 试玩者手动 Bug 快照：五轨物理写盘 + AI 必读说明 md |
| `ConsoleTraceRecorder`（静态类） | `Diagnostics/ConsoleTraceRecorder.cs` | Console 抓取轨：`logMessageReceivedThreaded` 环形缓冲 Warning/Error/Exception/Assert；随四轨自动/手动落盘；Player 下另挂 `Application.quitting` 退出兜底导出 |
| `DiagBeatClock` + `DiagBeatKinds` | `Diagnostics/DiagBeatClock.cs` | 共用 beatId 单调时钟（Core/Perf/Registry 节奏对齐） |
| `DiagBoardSnapCapture` + `CardSnap` | `Diagnostics/DiagBoardSnapCapture.cs` | 共享 BoardSnap 采集（uid/slot/xy/active/mode/tween） |
| `DiagFieldVisualCapture` + `FieldVisualReport` | `Diagnostics/DiagFieldVisualCapture.cs` | 场地「肉眼可见满场」审计：Core 占格 vs 可见 Ground 卡逐槽对比 |
| `DirectorTrace`（静态类） | `Diagnostics/DirectorTrace.cs` | 导演剧本层打点薄封装：Batch 门/Present 步/Intent 流/Timeline 换步 → PerfLog |
| `ChoreoTraceContext`（静态类） | `Diagnostics/ChoreoTraceContext.cs` | 场地编排批次关联键（choreoSeqId 单调递增）+ busy payload 汇总 |
| `FieldTraceHelper`（静态类） | `Diagnostics/FieldTraceHelper.cs` | FlowTrace V2 占格/表现诊断门面（Sink 处理器注册 + 自动 enrich） |
| `BoardIntentGateDiagnostics`（静态类） | `Diagnostics/BoardIntentGateDiagnostics.cs` | 输入门禁拒绝快照采集（phase/pending/gates/avatar/legal commands） |
| `CombatHitTraceContext`（静态类） | `Diagnostics/CombatHitTraceContext.cs` | 交战命中 reason 跨层传递（Field 写入 → BattleTrace 消费并清空） |

## 核心流程

### 1. 会话生命周期与四轨联动

1. **会话身份**：首次 Record / `BeginSessionIfNeeded` 时 `DiagTraceShared.EnsureSessionIdentity` 统一 sessionId（时间戳）与 seed（RunModel）；同一次 Play 四轨 sessionId/seed **必须一致**。QuickTest 等经 `SetRunTag` 打标（进文件名前缀）。
2. **重开轮转**：`BattleTraceRecorder.RotateSessionForNewRun`（由 Orchestrator 开局调）——先 `ExportBothNow` 四轨落盘，再清空 + `ForceNewSessionIdentity` + `DiagBeatClock.Reset` + `ChoreoTraceContext.ForceCloseOpenChoreos`。
3. **Play 退出**：`BattleTraceRecorder.ExportOnPlayExit`（去重，`AlreadyExportedThisPlayExit`）串联 Flow/Perf/Registry/Console 导出；导出前各轨写入 `SessionChoreoSummary` 汇总事件。受 `DiagTraceExportPreferences.AutoExport*` 总开关约束；胜负/DevKeys 可 `ExportBothNow` 立即落盘（不受去重影响）。
4. **每局自动落盘（Player 也生效）**：整局胜/负回主菜单时 `GameFlowOrchestrator.ShowBattleEndAndReturnAsync` 调 `ExportBothNow`；失败重开/再点开始经 `RotateSessionForNewRun` 先导出上一局再轮转；Development Player 另有 `Application.quitting` 兜底（`ConsoleTraceRecorder` 挂接）。同一会话同名文件覆盖写，最终文件即该局全量。

### 2. 关联键体系（跨轨对齐的钥匙）

| 键 | 来源 | 作用 |
|----|------|------|
| `sessionId` + `seed` + `runTag` | `DiagTraceShared` | 四轨同 Play 会话对齐 |
| `floor` / `nodeIndex`（#142） | shell NodeIndex 优先、RunModel 回退 | Run-Floor-Node 定位，逐事件/op 补齐 |
| `opIndex`（Battle）↔ `refBattleOpIndex`（Flow） | `BattleTraceRecorder.RecordOp` | 结算门与流程事件互查 |
| `beatId` | `DiagBeatClock`（Open/Close 单调递增） | Core/Perf/Registry 节奏切片（beatKind：OpeningDeal / PostKillDrain / CombatHit / BoardChoreo 等） |
| `chainId` + `choreoSeqId`（ADR-0003） | `DirectorTrace.BeginChain` + `ChoreoTraceContext.BeginChoreo` | 意图剧本 ↔ 场地编排批次串联；Cards/Flow 编排 payload 必须同带两键 |

### 3. Perf 轨的实时异常检测

`PerfTraceRecorder` 不只记录还主动检测：`TrackAfterRecord` 跟踪 open motions/plans，实时判 `MotionOverlap`、`SnapDuringMotion`、战斗中卡牌 offscreen 等异常码；Beat Close 时 capture BoardSnap 并对比检测；DealInFlight 时跳过 `SlotWorldMismatch` 防假阳；击杀尸体不算 Orphan（单独记 `DeadCorpseAtVacatedSlot`）。每条事件同时 `RegistryTraceRecorder.MirrorFromPerf` 镜像注册表相关子集。

### 4. Registry 轨的缺卡专项

- `BeginIdleWatch`：InteractionLoop 阶段按 1/2/3/5/7/10/15s 序列自动 Audit + 0.5s 轮询 FieldVisual。
- `CaptureFieldVisualState` → `DiagFieldVisualCapture.Capture`：逐槽对比 Core 占格 vs 可见 Ground 卡（DisplayMode/activeInHierarchy/renderer enabled 三重判定，Avatar 保留槽跳过），与 Opening 基线对比得 `visualMissing`，触发 `FieldVisualGap`（同 missing 值去重）；追踪 lastRelease/lastVacate 供 gap 关联。
- `RecordPickupEligibility` / `RecordSuspectGroundRelease`：拾取门禁与可疑 Release 专项。

### 5. 手动 Bug 快照（DiagTraceManualSnapshot）

试玩者填 `userTag` → `Save`：先 `PerfTraceRecorder.StampUserObservation`（UserMark + 全量 BoardSnap + RegistryAudit）→ 五轨（四轨 + Console 抓取）`CurrentSession` 直接序列化（**不受自动落盘开关限制**）写入独立目录 —— Editor：`Assets/Notes/Logs/ManualBugSnapshots/!!!AI-BUG-REPORT!!!-{时间}_{tag}/`；Player：exe 旁 `ManualBugSnapshots/`。同目录生成 `!!!AI_READ_THIS_FIRST!!!.md`（USER_PROBLEM_TAG、sessionId/seed/runTag、按 floor/node → opIndex 排查指引）。少于 2 个文件（仅 readme）视为失败并 Warning。

### 6. 打点门面与 Sink 接线

- `FieldTraceHelper.RegisterSinkHandlers`：把 `FlowFieldTraceSink` / `ChoreoTraceSink`（Cards 区声明的静态缝）绑到本类与 `ChoreoTraceContext`——Field/Presentation 层打点经 Sink 间接进 CoreLog，避免程序集反向引用。自动 enrich nodeIndex/chainId/choreoSeqId + `refBattleOpIndex`。
- `DirectorTrace`：被 `PresentationDirector` 直调（Diagnostics 不反向引用 Director）；缓存 busy 状态供 `ChoreoTraceContext.BuildBusyPayload`；`TimelineDiagnosticAdapter` 实现 `ITimelineDiagnosticSink` 接给 BattleTimeline。降噪：BatchOpenRejected 同 reason 只记首条；PresentStall 阈值 1s、重复间隔 1s。
- `BoardIntentGateDiagnostics.Collect/AppendPhaseReject`：门禁拒绝时采集 phase/pending/inputLocked/gates/avatar/legal commands 快照拼进 rejectReason（不独立导出，嵌入 Flow/Perf 事件 payload）。ADR-0039：判死谓词与 Core 对齐（uid 缺失/未注册/HP≤0，不看 Zone）。
- `CombatHitTraceContext.PendingReason`：Field 层写入（PlayerAttack/CounterAttack）、`BattleTraceRecorder.ConsumePendingReason` 读取并清空的单向极简跨层通道。
- `ChoreoTraceContext.LatchOccupancyDesync`：Core↔表现占格分叉旗标（#51 起不再作输入硬拒，改触发 Assert）。

## 对外通信面

- **被谁打点**：统一门禁 / `GameFlowOrchestrator`（Battle/Flow）；`PresentationDirector`（DirectorTrace）；Field/Board 编排（FieldTraceHelper Sink）；`CardPresentationProbe`（Perf Sink）；DevTest/编辑器窗口（手动导出与快照）。
- **读什么**：`NineGridArchitecture`、`RunModel`、`IGameFlowShellSystem`（floor/node）、`CardRegistry`、`IStatSystem`、`CardEntityLifecycleHook`、`GroundFieldGeometryHook`、`PresentationEventMap`。
- **产物消费**：`Assets/Notes/Logs/` 四目录 + ManualBugSnapshots；分析技能 `table-nine-battlelog-analysis`。

## 关联 ADR

ADR-0003（chainId/choreoSeqId 必须成对出现在编排 payload；sceneTag 仅单条 payload 非全局键）、ADR-0039（判死谓词、StartNode 留存 avatar zone/HP 取证）；#142（floor/nodeIndex 最低日志字段，Flow schemaVersion 4）、#51（占格分叉降级断言）。

## 不变量与坑

- **打点永不拆主线**：所有 Record 外层 try-catch 吞异常；新增打点遵守同一纪律。
- **手动导出豁免**：`ShouldWriteFile(track, automatic:false)` 始终允许——自动落盘关掉不影响编辑器窗口/DevTest 手动导出与 Bug 快照。
- `Enabled` setter 与偏好系统联动但须防递归（`SetEnabledFromPreferences` 单向写回）。
- `DiagBeatClock.Open` 时已有打开 Beat 会先 Close；Close 后 `CurrentBeatId=0` 但 `LastBeatId` 仍可供事件回落。
- 四轨手写 JSON（零第三方依赖）是刻意选择——勿引入 Newtonsoft 或改 JsonUtility。
- 空会话（仅 schema 壳）手动快照仍可能写出，读日志时先看 ops/events 数。
- `rejectReason` 在 accepted=true 时为空；`ChaseSample` Kind 已废弃仅兼容保留。

## 本篇文件清单（24）

`BattleTraceRecorder.cs`、`BattleTraceModels.cs`、`BattleTraceJson.cs`、`FlowTraceRecorder.cs`、`FlowTraceModels.cs`、`FlowTraceJson.cs`、`PerfTraceRecorder.cs`、`PerfTraceModels.cs`、`PerfTraceJson.cs`、`RegistryTraceRecorder.cs`、`RegistryTraceModels.cs`、`RegistryTraceJson.cs`、`ConsoleTraceRecorder.cs`、`DiagTraceShared.cs`、`DiagTraceExportPreferences.cs`、`DiagTraceManualSnapshot.cs`、`DiagBeatClock.cs`、`DiagBoardSnapCapture.cs`、`DiagFieldVisualCapture.cs`、`DirectorTrace.cs`、`ChoreoTraceContext.cs`、`FieldTraceHelper.cs`、`BoardIntentGateDiagnostics.cs`、`CombatHitTraceContext.cs`（均在 `Flow/Diagnostics/`）。
