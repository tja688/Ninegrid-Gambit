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
        public int schemaVersion = 1;
        public string seed = "0";
        public string sessionId = string.Empty;
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
        public const string BeatClock = "DiagBeat";
        public const string BoardSnapCapture = "BoardSnap.Capture";
        public const string AnomalyDetect = "Anomaly.Detect";
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
    }
}
