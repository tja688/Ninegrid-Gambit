# PerfLog / PerfTrace 事件与约定

落盘：`Assets/Notes/Logs/PerfLog/perflog[-{runTag}]-{sessionId}-seed{seed}.json`  
与 CoreLog / BattleLog / RegistryLog 共享 `sessionId` / `seed` / `runTag`（见 skill「日志发现」）。  
共通键：`beatId`（与 CoreLog 事件、`DiagBeatClock` 对齐）。  
`schemaVersion`：**2**（编排批次 choreoSeqId / ExploreTrace / BusySnapshot；v1 字段兼容）。

## 设计原则

- 只记意图与突变，**不逐帧**采样。
- 主键 **uid**；排查先按 uid 过滤。
- 每次改位/改显隐带稳定 **site**（调用点短名）。
- Beat 边界 **BoardSnap**（Open 常 full，Close 常 diff）+ Close 时轻量 **Anomaly**。

## Event 字段

| 字段 | 含义 |
|------|------|
| `index` | 会话内序号 |
| `beatId` | 共通 Beat；0 表示无 |
| `tMs` | 相对会话起点毫秒 |
| `kind` | 见下表 |
| `uid` | 卡牌；Beat/BoardSnap 可为 -1 |
| `site` | 调用点 |
| `payload` | 扁平 string 字典 |

## kind

| kind | 何时 | 关键 payload |
|------|------|----------------|
| `BeatOpen` / `BeatClose` | 与流程同拍 | `beatKind`, `nodeIndex`；Close 含 `anomalyCount` |
| `Spawn` / `Despawn` | 创建/回收视图 | `defId`, `slot`, `parent` / `reason`, `caller` |
| `RegistryMiss` | CardManager TryGet 失败 | `detail`（含 slot/caller） |
| `Vacate` | 场地占格注销 | `slot`, `caller` |
| `RingShift` | 外圈旋转计划/阶段 | `phase`=`plan\|vacated\|registered\|animate`, `plan`, `choreoSeqId`, `clockwise`, `skipBusyGuard`, `ringOccupied`, `animateTasks` |
| `ChoreoBegin` / `ChoreoEnd` | 编排批次开闭 | `choreoSeqId`, `kind`, `outcome`, `plannedAnim`, `actualAnim`, `durationMs` + Busy 摘要 |
| `ExploreTrace` | 空槽探求生命周期 | `phase`=`start\|withdraw\|chaseStart\|chaseEnd\|placeOk\|placeFail\|ringShift\|rollback`, `birthSlot`, `trackedSlot` |
| `BusySnapshot` | 各 busy 位快照 | 旧 busy 位 + `directorMainlineBusy` / `directorBypassBusy` / `bufferedIntent` / `activeBatchId`（见下） |
| `SessionChoreoSummary` | Play 退出前摘要 | `choreoOpenAtExit`, `choreoPartialAnimateCount`, `pickupGateFailCount`, `lastPickupGate`, `lastChoreoSeqId` |
| `DirectorBatchOpen` | 批次门成功 OpenBatch | `batchId`, `path=director`, `lane=mainline`, 可选 `slice` |
| `DirectorBatchOpenRejected` | 拒绝打开下一批（同 reason 去重） | `reason`=`hasOpen\|dispatchReject\|noBatch`；`dispatchReject` 可带 `detail`（Core 拒因） |
| `DirectorScriptAborted` | Resolve 终态失败中止剧本（主线应 idle） | `reason`（如 `batchOpenFailed`） |
| `DirectorPresentBegin` | PresentStep 开始播批 | `batchId`, `channel` |
| `DirectorPresentAck` | Present 就位回执成功 | `batchId` |
| `DirectorPresentAckRejected` | FinishBatch 被拒（同批去重） | `batchId`, `expected` |
| `DirectorPresentStall` | Present 连续 Continue ≥1s | `waitMs`, `phase`=`!complete\|!ack` |
| `DirectorIntentAccepted` | 意图立即开主线剧本 | `intentKind`, `intentSlot` |
| `DirectorIntentBuffered` | 忙时缓冲 + uiPick | `intentKind`, `intentSlot`, `uiPick=1` |
| `DirectorIntentRejected` | 已有缓冲拒绝后来者 | `intentKind`, `intentSlot` |
| `DirectorIntentFlush` | 主线空闲后消化缓冲 | `intentKind`, `intentSlot` |
| `DirectorIntentHardClear` | Phase/战败/换层硬清空 | `reason` |
| `DirectorStepEnter` / `DirectorStepExit` | 时间线换步（非逐帧；可选） | `step`, `lane` |
| `DirectorForkBegin` / `DirectorForkEnd` | 并行子流（可选） | `childCount` |
| `DirectorBypassStart` | 旁路装饰道入队 | `lane=bypass`, `step` |
| `DirectorTriggerPulse` | FX/音效 Trigger 脉冲（发即完成） | `triggerId`, `channel`=`fx\|audio`, `degraded`=`0\|1` |
| `LeaseAcquire` | 租约申请 | `layer`, `verdict`, `commitment`, `leaseId`, `windowStart/End`, `disciplineB`, `commandeered` |
| `LeaseRelease` | 租约释放 | `layer`, `leaseId`, `reason` |
| `BarrierPlace` | 就位栅栏放置 | `presBeatId`, `barrierWall`, `sourceTime`, `startWall`（事件 `beatId`=DiagBeat） |
| `BarrierSatisfied` | 栅栏兑现 | `presBeatId`, `satisfied`, `regCount`, `nowWall` |
| `CommitmentArrive` | 同步承诺兑现 | `layer`, `commitment`, `leaseId` |
| `Handoff` | Evict/Admit/征用交接 | `layer`, `phase`, `x/y`, `vx/vy` |
| `BeatAlign` | 表现节拍↔诊断 beat | `presBeatId`, `sourceTime`, `startWall`（与事件 `beatId` 对齐） |
| `RegistryAudit` | 注册表完整性快照 | `registryCount`, `fieldCount`, `ghosts`, `orphans`, `trigger` |
| `RegistryDelta` | CardManager `_cardsByUid` 增删 | `op`, `countBefore`, `countAfter`, `reason`, `caller`, `defId` |
| `UserMark` | 用户/DevTest 现场戳点 | `label`, `registryCount` |
| `MotionPlan` | Hop 等已知 from→to | `fromSlot`,`toSlot`,`fromX/Y`,`toX/Y`,`reason`,`expectMs` |
| `MotionBegin` | tween 启动 | `motionId`, from/to, `reason`, `choreoSeqId` |
| `MotionEnd` | complete / kill | `motionId`, `endHow`, `x/y`, `killedBySite`, `choreoSeqId` |
| `SnapSet` | 瞬间写 position | `x/y`, `slot`, `killedTween`, `reason` |
| `VisChange` | DisplayMode / active 等 | `active`, `alpha`, `mode`, `renderOn` |
| `ParentChange` | SetParent | `parent`, `worldStays` |
| `BoardSnap` | Beat 边界 | `phase`, `full`=`0\|1`, `cards` 紧凑串 |
| `Anomaly` | 自动 | `code`, `detail`, `refIndex` |
| `SkeletonFusion` | 骷髅融合编排（PerfTraceRecorder） | `site` 见下；payload 含 skillId/resultUid/participants 等 |
| `ShuffleIntoDeck` | 洗回牌库编排（PerfTraceRecorder） | `site` 见下；payload 含 count/path |
| `ShuffleBurstScatter` | 洗回炸牌散点（PerfTraceRecorder） | `site` 见下；payload 含 actionId/count/path |

### SkeletonFusion `site`

| site | 含义 |
|------|------|
| `DrainStateBuilt` | Drain 构建融合索引 |
| `PresentBegin` / `PresentEnd` | 合体表演开闭（补牌已不在 onFusionStarted） |
| `RefillBatchBegin` / `RefillBatchEnd` | 非导演路径：表演后离散补牌 |
| `RefillSkipped` / `RefillSkippedNoCandidate` | 跳过补牌（无空位/无候选） |
| `RemoveStepPending` / `RemoveStepMiss` | Remove 步聚合等待 |

导演路径上融合伴随补牌走 `ResolveFusionRefill` 批次锁步，PerfLog 以独立 `SlotsFilled` 批 + 上述 site 对照验收。

### DrainRefill `site`

| site | 含义 |
|------|------|
| `RefillBatchBegin` / `RefillBatchEnd` | drain 退场补牌开闭；`path=director` 为导演离散批，`path=presentAdapter` 为反击等 Present 薄适配（#11 已删 `legacyPostPresent`） |

导演路径上非击杀移除后退场补牌走 `ResolveDrainRefill` 批次锁步；`DrainPostRemoveRefill` 在 `DirectorMainlineBusy` 时不可达。

### ShuffleIntoDeck `site`

| site | 含义 |
|------|------|
| `EnqueuedForDirector` | 解算批投影后入导演 sink（不再 Forget 旁路开播） |
| `PresentBegin` / `PresentEnd` | 洗回飞入卡组表演开闭（用牌 Present 前缀或盘面 Drain Flush） |

导演路径上洗回只经 Present/Drain Flush；旧 `_pendingShuffleInto` Forget 泵不可达。净土域入组仍走 DOTween 黑盒（Evict/Admit 边界）。

### ShuffleBurstScatter `site`

| site | 含义 |
|------|------|
| `PresentBegin` / `PresentEnd` | 同 ActionId 多张新生/随机洗入：圆上炸开 → 同步停稳 → 集体上飞入组 |

### BoardSnap `cards` 格式

- full：`uid,slot=…,xy=x,y,active=0|1,mode=…,tween=0|1;…`
- diff：`+…` 新增 / `~uid slot=a→b xy=…→…` 变更 / `-uid` 消失  
坐标为厘米级量化（×100 取整）。

## beatKind

| 值 | 场景 |
|----|------|
| `OpeningDeal` | 开局发牌表现 |
| `PostKillDrain` | 击杀后补牌/hop Drain |
| `BoardChoreo` | 单条盘面编排（旋转/交换/hop/探求） |
| `SyncBoard` | 独立 Sync（非 Drain 内） |
| `CombatHit` | 一次进攻或反击表现段 |
| `StartNode` / `Reward` / `Room` | 预留 |

## Anomaly code

| code | 含义 |
|------|------|
| `SnapDuringMotion` | 仍有未 End 的 motion 时 SnapSet |
| `MotionPlanWithoutBegin` | 有 Plan 无 Begin |
| `VisOffWhileCombatant` | CombatHit Beat 内战斗员 active=0 |
| `SlotWorldMismatch` | 登记 slot 锚点与世界坐标偏离 |
| `OrphanAtWrongAnchor` | 空槽锚点上贴了别的 uid |
| `MissingMotionEnd` | BeatClose 时 motion 未收尾 |
| `FieldOccupancyWithoutView` | 占格有 uid，CardManager 无视图（缺卡主嫌疑） |
| `ViewWithoutFieldOccupancy` | 有 GroundCardMode 视图，占格无登记 |
| `MotionOverlap` | 同 uid 未 End 又 Begin |
| `ChoreoPartialAnimate` | ChoreoEnd：`actualAnim < plannedAnim`（旋转无表现） |
| `ExplorePlaceWhileBusy` | Explore 就位时 field/deck busy 异常 |
| `PickupVisualEligibleButGateFail` | PickupEligibility 可响应但 PickupGate 失败 |
| `ChoreoIncompleteAtBeatClose` | BoardChoreo BeatClose 时仍有 open choreo/motion |
| `DisciplineBSyncConflict` | 纪律 B：同步撞同步租约 |
| `DisciplineBPreemptCommitted` | 纪律 B：抢占未兑现的 committed 目标 |

## 常用 site

| site | 来源 |
|------|------|
| `Ground.Place.Snap` / `Ground.Relocate.Snap` | GroundField 硬贴锚点 |
| `DeckTween.Move` / `DeckTween.Hop` / `DeckTween.Kill` | CardDeckTween |
| `SlotFrame.Converge` / `SlotFrame.BeginDeal` | L2 五次收敛 |
| `Lease.Arbiter` / `Lease.Commandeer` | 租约 / 征用交接 |
| `BeatGrid.Barrier` / `BeatGrid.Align` | 就位栅栏 / 节拍对齐 |
| `FinalStateGuard.SoftSnap` / `HardSnap` | 战斗终态守卫 |
| `Card.DisplayMode` / `Card.Spawn` / `Card.Release` | CardManager |
| `Ground.TryGetCardAt` / `Ground.Vacate` / `Ground.RingShift` | 占格查询/注销/旋转 |
| `Card.RegistryAudit` | 注册表审计 |
| `Ground.HopPlan` | hop MotionPlan |
| `DiagBeat` / `BoardSnap.Capture` / `Anomaly.Detect` | 记录器自身 |
| `Director.BatchGate` / `Director.PresentStep` / `Director.Intent` | `DirectorTrace` 剧本层 |

### BusySnapshot 导演字段（迁移期与旧 busy 并存）

| 字段 | 含义 |
|------|------|
| `directorMainlineBusy` | 主线在跑（唯一输入互斥真相） |
| `directorBypassBusy` | 旁路装饰道在跑（不占输入锁） |
| `bufferedIntent` | `0\|1`；有缓冲时另有 `bufferedIntentKind` |
| `activeBatchId` | Core PresentationSync 当前打开批；0=无 |

旧字段（`queueDepth` / `pumpRunning` / `presentationLocked` 等）迁移期保留便于 bisect；阶段 5 硬切后随旧泵清理（见 skill「阶段 5 诊断清理」）。

## 导演剧本层（skill 模式 E）

主键：`batchId` 串 Resolve→Present→Ack；`beatId` 对照 Motion/Choreo/Occupancy。  
共性 payload：`path=director`，`lane=mainline|bypass`。

期望垂直切片序（验收门）：

```
DirectorIntentAccepted
  → DirectorBatchOpen → DirectorPresentBegin → (Motion*/Choreo*/切片 path=director)
  → DirectorPresentAck
  → …（下一批同形）
```

卡死：末条为 `DirectorPresentBegin` 后出现 `DirectorPresentStall(phase=!complete|!ack)`，或长时间无 `DirectorPresentAck`。  
未 ack 却前进：同 `batchId` 未见 Ack 又出现下一 `DirectorBatchOpen`（或 OpenRejected `hasOpen` 缺失却仍 Open）。

实现：`DirectorTrace` → `PerfTraceRecorder`（**不另开第五轨**）。

## 收敛范式回放

1. 同 `sessionId` 打开 CoreLog + PerfLog。
2. 按 `beatId`（DiagBeat）对齐；payload `presBeatId` 关联表现节拍栅格。
3. 链路：`BeatAlign` → `BarrierPlace` → `LeaseAcquire` → `MotionBegin(SlotFrame.*)` → `CommitmentArrive` → `LeaseRelease` → `BarrierSatisfied`。
4. 纪律 B：Perf `Anomaly` code=`DisciplineB*` + Core `DisciplineBAlarm`。
5. 征用：`LeaseAcquire(commandeered=1)` → `Handoff(phase=commandeerAdmit)` → 下一段 `MotionBegin` 速度连续。

## 与 CoreLog / Battle 互指

- 同 `sessionId` + `seed`（+ 文件名 `runTag`）打开四文件：corelog / perflog / registrylog / battlelog。
- 同 `beatId`：CoreLog 看意图与逻辑占格；PerfLog 看画面与导演剧本。
- Battle：`refBattleOpIndex` 仍指向 BattleLog Op；节奏用 `beatId`（CombatHit Beat 与战斗表现段对齐）。

## 插桩入口

- Cards：`CardPresentationProbe` / `ConvergenceDiagProbe` → `PerfTraceSink` + `FlowFieldTraceSink`
- Flow：`PerfTraceRecorder.OpenBeat` / `CloseBeat` / `RecordBoardSnap`
- 导演：`DirectorTrace`（BatchGate / PresentStep / PresentationDirector）
- 导出：`BattleTraceRecorder.ExportBothNow` / Play 退出四件套
