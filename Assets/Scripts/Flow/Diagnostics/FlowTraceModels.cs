using System;
using System.Collections.Generic;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 一局 CoreLog（FlowTrace）会话：全流程事件时间线，与 Battle/Perf 共享 sessionId/seed。
    /// schemaVersion 3：新增 beatId，落盘目录 Assets/Notes/Logs/CoreLog。
    /// </summary>
    [Serializable]
    public sealed class FlowTraceSession
    {
        public int schemaVersion = 3;
        public string seed = "0";
        public string sessionId = string.Empty;
        public string runTag = string.Empty;
        public string runTagNote = string.Empty;
        public List<FlowTraceEvent> events = new List<FlowTraceEvent>();
    }

    /// <summary>
    /// 一条流程事件（Loop / UI / CoreGate / CombatSummary / Deck / Field / Presentation / Hand）。
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
        public const string CombatHitSummary = "CombatHitSummary";
        public const string PostKillBoard = "PostKillBoard";
        public const string Victory = "Victory";
        public const string Defeat = "Defeat";
        public const string GoldGained = "GoldGained";
        public const string GoldSpent = "GoldSpent";

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
    /// V2 batchTag 约定，便于按阶段过滤。
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
