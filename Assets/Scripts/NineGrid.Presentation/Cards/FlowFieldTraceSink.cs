using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// Cards → Flow 占格/发牌/手牌诊断旁路。由 Flow <c>FieldTraceHelper</c> 注册，失败不影响游戏路径。
    /// Cards 程序集不可引用 Flow，故用静态 Action 解耦。
    /// 关联键由 Flow 侧 EnrichCommon 注入 chainId + choreoSeqId（废全局 batchTag）。
    /// </summary>
    public static class FlowFieldTraceSink
    {
        /// <summary>slot, existingUid, incomingUid, caller</summary>
        public static Action<int, int, int, string> OccupancyConflict;

        /// <summary>plans 摘要字符串，如 "26:1→2;32:2→3"</summary>
        public static Action<string> HopPlan;

        /// <summary>uid, slot, caller — 变更前发牌意图。</summary>
        public static Action<int, int, string> DealAttempt;

        /// <summary>uid, slot, placeable, ok, rollback, caller, reason</summary>
        public static Action<int, int, bool, bool, bool, string, string> DealResult;

        /// <summary>uid, phase, handContains, displayMode</summary>
        public static Action<int, string, bool, string> HandLifecycle;

        /// <summary>slot, uid, caller</summary>
        public static Action<int, int, string> OccupancyVacate;

        /// <summary>trigger, registryCount, fieldCount, ghosts, orphans</summary>
        public static Action<string, int, int, string, string> RegistryAudit;

        /// <summary>uid, groundSlot, defId, coreKind</summary>
        public static Action<int, int, string, string> PickupAttempt;

        /// <summary>uid, gate, accepted</summary>
        public static Action<int, string, bool> PickupGate;

        /// <summary>uid, handSlot</summary>
        public static Action<int, int> PickupSuccess;

        /// <summary>accepted, clockwise, moveCount, ringOccupied</summary>
        public static Action<bool, bool, int, int> RotateClassify;

        /// <summary>uid, code, reason, layer, verdict</summary>
        public static Action<int, string, string, string, string> DisciplineBAlarm;

        /// <summary>uid, layer, verdict, commitment, leaseId, windowStart, windowEnd, disciplineB, commandeered</summary>
        public static Action<int, string, string, string, int, float, float, bool, bool> LeaseAcquire;

        /// <summary>uid, layer, leaseId, reason</summary>
        public static Action<int, string, int, string> LeaseRelease;

        /// <summary>uid, layer, commitment, leaseId, site</summary>
        public static Action<int, string, string, int, string> CommitmentArrive;

        /// <summary>presBeatId, barrierWall, sourceTime, startWall, regHint</summary>
        public static Action<int, float, float, float, int> BarrierPlace;

        /// <summary>presBeatId, satisfied, regCount, nowWall</summary>
        public static Action<int, bool, int, float> BarrierSatisfied;

        /// <summary>presBeatId, sourceTime, startWall</summary>
        public static Action<int, float, float> BeatAlign;

        /// <summary>uid, layer, phase, vx, vy, site</summary>
        public static Action<int, string, string, string, string, string> Handoff;

        public static void SafeOccupancyConflict(int slot, int existingUid, int incomingUid, string caller)
        {
            try
            {
                OccupancyConflict?.Invoke(slot, existingUid, incomingUid, caller);
            }
            catch
            {
            }
        }

        public static void ClearHandlers()
        {
            OccupancyConflict = null;
            HopPlan = null;
            DealAttempt = null;
            DealResult = null;
            HandLifecycle = null;
            OccupancyVacate = null;
            RegistryAudit = null;
            PickupAttempt = null;
            PickupGate = null;
            PickupSuccess = null;
            RotateClassify = null;
            DisciplineBAlarm = null;
            LeaseAcquire = null;
            LeaseRelease = null;
            CommitmentArrive = null;
            BarrierPlace = null;
            BarrierSatisfied = null;
            BeatAlign = null;
            Handoff = null;
        }
    }
}
