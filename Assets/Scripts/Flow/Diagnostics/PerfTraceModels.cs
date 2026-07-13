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
    }
}
