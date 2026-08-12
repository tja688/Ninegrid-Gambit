using System;
using System.Collections.Generic;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 一局 CoreLog（FlowTrace）会话：全流程事件时间线，与 Battle/Perf 共享 sessionId/seed。
    /// schemaVersion 3：新增 beatId，落盘目录 Assets/Notes/Logs/CoreLog。
    /// schemaVersion 4（#142）：事件新增 floor/nodeIndex 关联字段，统一最低日志字段。
    /// </summary>
    [Serializable]
    public sealed class FlowTraceSession
    {
        public int schemaVersion = 4;
        public string seed = "0";
        public string sessionId = string.Empty;
        public string runTag = string.Empty;
        public string runTagNote = string.Empty;
        public List<FlowTraceEvent> events = new List<FlowTraceEvent>();
    }

    /// <summary>
    /// 一条流程事件（Loop / UI / CoreGate / CombatSummary / Deck / Field / Presentation / Hand）。
    /// floor / nodeIndex 由 Recorder 记录时自动从 RunModel 解析，供 Run-Floor-Node-Battle 关联。
    /// </summary>
    [Serializable]
    public sealed class FlowTraceEvent
    {
        public int index;
        public int beatId;
        public string category = string.Empty;
        public string name = string.Empty;
        public string loopState = string.Empty;
        public string phaseBefore = string.Empty;
        public string phaseAfter = string.Empty;
        public bool accepted = true;
        public int refBattleOpIndex = -1;
        /// <summary>#142：当前层号（RunModel.Floor，1-based），日志关联 Floor 用。</summary>
        public string floor = string.Empty;
        /// <summary>#142：当前节点索引（shell NodeIndex 优先，回退 RunModel.NodeIndex），日志关联 Node 用。</summary>
        public string nodeIndex = string.Empty;
        public Dictionary<string, string> payload = new Dictionary<string, string>();
    }

    /// <summary>
    /// 稳定 category 常量（V1 + V2）。
    /// </summary>
    public static class FlowTraceCategory
    {
        public const string Loop = "Loop";
        public const string UI = "UI";
        public const string CoreGate = "CoreGate";
        public const string CombatSummary = "CombatSummary";
        public const string Deck = "Deck";
        public const string Economy = "Economy";
        public const string Field = "Field";
        public const string Presentation = "Presentation";
        public const string Hand = "Hand";
        /// <summary>#206：卡级节奏 / 翻面 / 敌方行动裁决诊断轨。</summary>
        public const string Rhythm = "Rhythm";
    }

    /// <summary>
    /// 稳定 name 常量，便于 skill 检索。
    /// </summary>
    public static class FlowTraceNames
    {
        public const string EnterMainMenu = "EnterMainMenu";
        public const string StartRun = "StartRun";
        public const string ReturnMainMenu = "ReturnMainMenu";
        public const string SetState = "SetState";
        public const string RewardPresented = "RewardPresented";
        public const string RewardChosen = "RewardChosen";
        public const string RoomPresented = "RoomPresented";
        public const string RoomChosen = "RoomChosen";
        public const string EnterRoom = "EnterRoom";
        public const string StartNode = "StartNode";
        public const string BootstrapRun = "BootstrapRun";
        public const string CombatHitSummary = "CombatHitSummary";
        public const string PostKillBoard = "PostKillBoard";
        public const string Victory = "Victory";
        public const string Defeat = "Defeat";
        public const string GoldGained = "GoldGained";
        public const string GoldSpent = "GoldSpent";
        /// <summary>EventLog 切片内 EffectTriggered（Intent 击杀等路径补点）。</summary>
        public const string EffectTriggered = "EffectTriggered";
        /// <summary>EventLog 切片内 BaseStatModified（含献身 ATK+1 等）。</summary>
        public const string BaseStatModified = "BaseStatModified";
        /// <summary>卡面 Settled 认领 ModifyBaseStat 后的 Commit 回执。</summary>
        public const string CardFaceBaseStatCommit = "CardFaceBaseStatCommit";

        // #206 Rhythm 诊断轨（RhythmFaceFlowTraceBinder 全链路扫描）
        /// <summary>牌面朝向变化（Flip/Conceal/Reveal，含 source/cause）。</summary>
        public const string CardFaceChanged = "CardFaceChanged";
        /// <summary>卡级共享倒计时变化（delta/remaining）。</summary>
        public const string ActionCountdownChanged = "ActionCountdownChanged";
        /// <summary>卡级开火窗口开启（ADR-0038）。</summary>
        public const string RhythmFireOpened = "RhythmFireOpened";
        /// <summary>敌方行动窗口裁决（roster/fired/void*/skip*）。</summary>
        public const string EnemyActionVerdict = "EnemyActionVerdict";

        // V2 Field / Presentation / Hand
        public const string DrainBegin = "DrainBegin";
        public const string DrainEnd = "DrainEnd";
        public const string DrainLegacyFallback = "DrainLegacyFallback";
        public const string DealAttempt = "DealAttempt";
        public const string DealResult = "DealResult";
        public const string HopPlan = "HopPlan";
        public const string OccupancyConflict = "OccupancyConflict";
        public const string OccupancySnapshot = "OccupancySnapshot";
        public const string SyncDiff = "SyncDiff";
        public const string OpeningDealProgress = "OpeningDealProgress";
        public const string HandAcquire = "HandAcquire";
        public const string HandRelease = "HandRelease";
        public const string OccupancyVacate = "OccupancyVacate";
        public const string RegistryAudit = "RegistryAudit";

        // Board choreo / pickup
        public const string BoardQueueEnqueue = "BoardQueueEnqueue";
        public const string BoardQueueDequeue = "BoardQueueDequeue";
        public const string BoardQueueSkip = "BoardQueueSkip";
        public const string RotateClassify = "RotateClassify";
        public const string BoardStepBegin = "BoardStepBegin";
        public const string BoardStepEnd = "BoardStepEnd";
        public const string BoardStepFail = "BoardStepFail";
        public const string BoardSyncDeferred = "BoardSyncDeferred";
        public const string BoardStepFallbackGeneralHop = "BoardStepFallbackGeneralHop";
        public const string BoardRotateChoreoMismatch = "BoardRotateChoreoMismatch";
        public const string PickupAttempt = "PickupAttempt";
        public const string PickupGate = "PickupGate";
        public const string PickupSuccess = "PickupSuccess";
        public const string SessionChoreoSummary = "SessionChoreoSummary";

        // 收敛范式：租约 / 栅栏 / 纪律 B / 交接
        public const string DisciplineBAlarm = "DisciplineBAlarm";
        public const string LeaseAcquire = "LeaseAcquire";
        public const string LeaseRelease = "LeaseRelease";
        public const string BarrierPlace = "BarrierPlace";
        public const string BarrierSatisfied = "BarrierSatisfied";
        public const string CommitmentArrive = "CommitmentArrive";
        public const string Handoff = "Handoff";
        public const string BeatAlign = "BeatAlign";
    }

    /// <summary>
    /// 可选场景标记常量（写入单条 OccupancySnapshot 的 sceneTag），非全局关联键。
    /// </summary>
    public static class FlowTraceBatchTags
    {
        public const string PostKill = "postKill";
        public const string Opening = "opening";
        public const string Sync = "sync";
        public const string StartNode = "startNode";
        public const string Pickup = "pickup";
        public const string Hop = "hop";
        public const string Deal = "deal";
        public const string BoardPresentationQueue = "boardPresentationQueue";
    }
}
