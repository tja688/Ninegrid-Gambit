using System;
using System.Collections.Generic;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 一局 PerfTrace 会话：表现层卡牌位移/显隐时间线。
    /// </summary>
    [Serializable]
    public sealed class PerfTraceSession
    {
        public int schemaVersion = 2;
        public string seed = "0";
        public string sessionId = string.Empty;
        public string runTag = string.Empty;
        public string runTagNote = string.Empty;
        public List<PerfTraceEvent> events = new List<PerfTraceEvent>();
    }

    [Serializable]
    public sealed class PerfTraceEvent
    {
        public int index;
        public int beatId;
        public int tMs;
        public string kind = string.Empty;
        public int uid = -1;
        public string site = string.Empty;
        public Dictionary<string, string> payload = new Dictionary<string, string>();
    }

    public static class PerfTraceKinds
    {
        public const string BeatOpen = "BeatOpen";
        public const string BeatClose = "BeatClose";
        public const string Spawn = "Spawn";
        public const string Despawn = "Despawn";
        public const string MotionPlan = "MotionPlan";
        public const string MotionBegin = "MotionBegin";
        public const string MotionEnd = "MotionEnd";
        public const string SnapSet = "SnapSet";
        public const string VisChange = "VisChange";
        public const string ParentChange = "ParentChange";
        public const string BoardSnap = "BoardSnap";
        public const string Anomaly = "Anomaly";
        /// <summary>战斗 Timeline 命中帧：参战方 world xy / sorting / renderOn。</summary>
        public const string CombatHitFrame = "CombatHitFrame";
        public const string BattleBind = "BattleBind";
        public const string DeathCallback = "DeathCallback";
        /// <summary>CardManager TryGet 失败：占格/Core 仍引用该 uid。</summary>
        public const string RegistryMiss = "RegistryMiss";
        /// <summary>场地占格注销（未必 Despawn）。</summary>
        public const string Vacate = "Vacate";
        /// <summary>外圈旋转 Vacate/重登记计划。</summary>
        public const string RingShift = "RingShift";
        /// <summary>CardManager ↔ 场地占格完整性快照。</summary>
        public const string RegistryAudit = "RegistryAudit";
        /// <summary>CardManager _cardsByUid 增删（每次 Spawn/Release）。</summary>
        public const string RegistryDelta = "RegistryDelta";
        /// <summary>用户/DevTest 现场戳点（BUG 复现瞬间）。</summary>
        public const string UserMark = "UserMark";
        /// <summary>场地编排批次开始。</summary>
        public const string ChoreoBegin = "ChoreoBegin";
        /// <summary>场地编排批次结束。</summary>
        public const string ChoreoEnd = "ChoreoEnd";
        /// <summary>空槽探求生命周期。</summary>
        public const string ExploreTrace = "ExploreTrace";
        /// <summary>各管理器 busy 位快照。</summary>
        public const string BusySnapshot = "BusySnapshot";
        /// <summary>已废弃：旧 ChaseAnchor 追锚采样。保留常量供旧日志兼容。</summary>
        public const string ChaseSample = "ChaseSample";
        /// <summary>Play 退出前会话编排摘要。</summary>
        public const string SessionChoreoSummary = "SessionChoreoSummary";
        /// <summary>租约申请结果（含征用）。</summary>
        public const string LeaseAcquire = "LeaseAcquire";
        /// <summary>租约释放。</summary>
        public const string LeaseRelease = "LeaseRelease";
        /// <summary>就位栅栏放置。</summary>
        public const string BarrierPlace = "BarrierPlace";
        /// <summary>就位栅栏兑现/未兑现。</summary>
        public const string BarrierSatisfied = "BarrierSatisfied";
        /// <summary>同步承诺兑现时刻（MarkFulfilled）。</summary>
        public const string CommitmentArrive = "CommitmentArrive";
        /// <summary>域边界 Evict/Admit/Commandeer 交接。</summary>
        public const string Handoff = "Handoff";
        /// <summary>表现节拍与诊断 beat 对齐（payload.presBeatId + 事件 beatId）。</summary>
        public const string BeatAlign = "BeatAlign";

        // --- PresentationDirector 剧本层（同轨 PerfLog；见 DirectorTrace）---

        /// <summary>批次门成功 OpenBatch。</summary>
        public const string DirectorBatchOpen = "DirectorBatchOpen";
        /// <summary>批次门拒绝打开下一批。</summary>
        public const string DirectorBatchOpenRejected = "DirectorBatchOpenRejected";
        /// <summary>Resolve 终态失败等：剧本中止，主线 idle。</summary>
        public const string DirectorScriptAborted = "DirectorScriptAborted";
        /// <summary>PresentStep 开始播当前批。</summary>
        public const string DirectorPresentBegin = "DirectorPresentBegin";
        /// <summary>PresentStep 就位回执成功 FinishBatch。</summary>
        public const string DirectorPresentAck = "DirectorPresentAck";
        /// <summary>PresentStep 就位回执被拒。</summary>
        public const string DirectorPresentAckRejected = "DirectorPresentAckRejected";
        /// <summary>PresentStep 长时间 Continue（卡死探针）。</summary>
        public const string DirectorPresentStall = "DirectorPresentStall";
        /// <summary>意图立即开主线剧本。</summary>
        public const string DirectorIntentAccepted = "DirectorIntentAccepted";
        /// <summary>忙时缓冲意图（可含 uiPick）。</summary>
        public const string DirectorIntentBuffered = "DirectorIntentBuffered";
        /// <summary>已有缓冲时拒绝后来意图。</summary>
        public const string DirectorIntentRejected = "DirectorIntentRejected";
        /// <summary>主线空闲后消化缓冲意图。</summary>
        public const string DirectorIntentFlush = "DirectorIntentFlush";
        /// <summary>Phase/战败/换层硬清空。</summary>
        public const string DirectorIntentHardClear = "DirectorIntentHardClear";
        /// <summary>时间线换步进入（非逐帧）。</summary>
        public const string DirectorStepEnter = "DirectorStepEnter";
        /// <summary>时间线当前步 Finished。</summary>
        public const string DirectorStepExit = "DirectorStepExit";
        /// <summary>并行子流开始。</summary>
        public const string DirectorForkBegin = "DirectorForkBegin";
        /// <summary>并行子流全部结束。</summary>
        public const string DirectorForkEnd = "DirectorForkEnd";
        /// <summary>旁路装饰道入队（不占输入锁）。</summary>
        public const string DirectorBypassStart = "DirectorBypassStart";
        /// <summary>FX/音效 Trigger 脉冲（发即完成；可 degraded）。</summary>
        public const string DirectorTriggerPulse = "DirectorTriggerPulse";
    }

    public static class PerfTraceSites
    {
        public const string GroundPlaceSnap = "Ground.Place.Snap";
        public const string GroundRelocateSnap = "Ground.Relocate.Snap";
        public const string GroundVacate = "Ground.Vacate";
        public const string DeckTweenMove = "DeckTween.Move";
        public const string DeckTweenHop = "DeckTween.Hop";
        public const string DeckTweenKill = "DeckTween.Kill";
        public const string FinalStateGuardSoft = "FinalStateGuard.SoftSnap";
        public const string FinalStateGuardHard = "FinalStateGuard.HardSnap";
        public const string CardDisplayMode = "Card.DisplayMode";
        public const string CardStageFieldDead = "Card.StageFieldDead";
        public const string CardActive = "Card.SetActive";
        public const string CombatRigLungeAttacker = "CombatRig.Lunge.Attacker";
        public const string CombatRigLungeVictim = "CombatRig.Lunge.Victim";
        public const string CombatRigHitFrameAttacker = "CombatRig.HitFrame.Attacker";
        public const string CombatRigHitFrameVictim = "CombatRig.HitFrame.Victim";
        public const string CombatBindResolve = "Combat.BindResolve";
        public const string CombatDeathCallback = "Combat.DeathCallback";
        public const string BeatClock = "DiagBeat";
        public const string BoardSnapCapture = "BoardSnap.Capture";
        public const string AnomalyDetect = "Anomaly.Detect";
        public const string CardRegistryMiss = "Card.RegistryMiss";
        public const string GroundRingShift = "Ground.RingShift";
        public const string CardRegistryAudit = "Card.RegistryAudit";
        public const string LeaseArbiter = "Lease.Arbiter";
        public const string BeatGridBarrier = "BeatGrid.Barrier";
        public const string BeatGridAlign = "BeatGrid.Align";
        public const string SlotFrameConverge = "SlotFrame.Converge";
        public const string EffectFrameConverge = "EffectFrame.Converge";
        public const string LayerHandoff = "Layer.Handoff";
        public const string DirectorBatchGate = "Director.BatchGate";
        public const string DirectorPresentStep = "Director.PresentStep";
        public const string DirectorIntent = "Director.Intent";
        public const string DirectorTimeline = "Director.Timeline";
        public const string DirectorFork = "Director.Fork";
        public const string DirectorBypass = "Director.Bypass";
    }

    public static class PerfTraceAnomalyCodes
    {
        public const string SnapDuringMotion = "SnapDuringMotion";
        public const string MotionPlanWithoutBegin = "MotionPlanWithoutBegin";
        public const string VisOffWhileCombatant = "VisOffWhileCombatant";
        public const string SlotWorldMismatch = "SlotWorldMismatch";
        public const string OrphanAtWrongAnchor = "OrphanAtWrongAnchor";
        public const string DeadCorpseAtVacatedSlot = "DeadCorpseAtVacatedSlot";
        public const string MissingMotionEnd = "MissingMotionEnd";
        public const string CombatantOffscreenWhileHit = "CombatantOffscreenWhileHit";
        public const string DeathCallbackOnSurvivor = "DeathCallbackOnSurvivor";
        public const string LethalEstimateMismatch = "LethalEstimateMismatch";
        /// <summary>场地占格有 uid，CardManager 无对应视图（缺卡主嫌疑）。</summary>
        public const string FieldOccupancyWithoutView = "FieldOccupancyWithoutView";
        /// <summary>CardManager 有 GroundCardMode 视图，场地占格无登记。</summary>
        public const string ViewWithoutFieldOccupancy = "ViewWithoutFieldOccupancy";
        /// <summary>同 uid 有未 End 的 motion 时又 Begin。</summary>
        public const string MotionOverlap = "MotionOverlap";
        /// <summary>编排结束 actualAnim &lt; plannedAnim（旋转无表现）。</summary>
        public const string ChoreoPartialAnimate = "ChoreoPartialAnimate";
        /// <summary>Explore 就位时 busy 标志异常。</summary>
        public const string ExplorePlaceWhileBusy = "ExplorePlaceWhileBusy";
        /// <summary>视觉可响应但 PickupGate 失败。</summary>
        public const string PickupVisualEligibleButGateFail = "PickupVisualEligibleButGateFail";
        /// <summary>BeatClose 时仍有未完成 choreo。</summary>
        public const string ChoreoIncompleteAtBeatClose = "ChoreoIncompleteAtBeatClose";
        /// <summary>盘面步骤流播放期间外部 Sync/Snap 被延迟。</summary>
        public const string SyncDuringBoardTimeline = "SyncDuringBoardTimeline";
        /// <summary>单步 Move 回退 general-hop，语义可能丢失。</summary>
        public const string BoardStepGeneralHopFallback = "BoardStepGeneralHopFallback";
        /// <summary>Core BoardRotated 数与 ChoreoBegin rotate 数不等。</summary>
        public const string BoardRotateChoreoCountMismatch = "BoardRotateChoreoCountMismatch";
        /// <summary>飞牌预算耗尽后软着陆。</summary>
        public const string DealFlightBudgetExhausted = "DealFlightBudgetExhausted";
        /// <summary>旋转 hop 接管 in-flight 飞牌。</summary>
        public const string DealFlightRotateHopTakeover = "DealFlightRotateHopTakeover";
        /// <summary>纪律 B：同步撞同步租约冲突。</summary>
        public const string DisciplineBSyncConflict = "DisciplineBSyncConflict";
        /// <summary>纪律 B：抢占未兑现的 committed 异步目标。</summary>
        public const string DisciplineBPreemptCommitted = "DisciplineBPreemptCommitted";
        /// <summary>切入净土域时 L2/L3 仍有残留偏移（Sanitize 前探测）。</summary>
        public const string TowerResidueOnSanctuary = "TowerResidueOnSanctuary";
        /// <summary>Sync Phase2 试图在开放 DeckTween 期间 reanchor（门禁失效时报警）。</summary>
        public const string SyncReanchorDuringDeckTween = "SyncReanchorDuringDeckTween";
    }
}
