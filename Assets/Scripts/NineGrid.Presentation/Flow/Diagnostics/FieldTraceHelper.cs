using System;
using System.Collections.Generic;
using System.Text;
using NineGrid.Cards;
using NineGrid.Core;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// FlowTrace V2 占格/表现诊断门面。打点失败一律吞掉。
    /// </summary>
    public static class FieldTraceHelper
    {
        private static string sBatchTag = string.Empty;

        public static string CurrentBatchTag => sBatchTag;

        public static void SetBatchTag(string tag)
        {
            sBatchTag = tag ?? string.Empty;
            FlowFieldTraceSink.CurrentBatchTag = sBatchTag;
        }

        public static void ClearBatchTag()
        {
            sBatchTag = string.Empty;
            FlowFieldTraceSink.CurrentBatchTag = string.Empty;
        }

        public static void RegisterSinkHandlers()
        {
            FlowFieldTraceSink.OccupancyConflict = OnOccupancyConflict;
            FlowFieldTraceSink.HopPlan = OnHopPlan;
            FlowFieldTraceSink.DealAttempt = OnDealAttempt;
            FlowFieldTraceSink.DealResult = OnDealResult;
            FlowFieldTraceSink.HandLifecycle = OnHandLifecycle;
            FlowFieldTraceSink.OccupancyVacate = OnOccupancyVacate;
            FlowFieldTraceSink.RegistryAudit = OnRegistryAudit;
            FlowFieldTraceSink.SetBatchTag = SetBatchTag;
            FlowFieldTraceSink.ClearBatchTag = ClearBatchTag;
            FlowFieldTraceSink.PickupAttempt = (uid, groundSlot, defId, coreKind) =>
                RecordPickupAttempt(uid, groundSlot, defId, coreKind);
            FlowFieldTraceSink.PickupGate = (uid, gate, accepted) =>
                RecordPickupGate(uid, gate, accepted);
            FlowFieldTraceSink.PickupSuccess = RecordPickupSuccess;
            FlowFieldTraceSink.RotateClassify = (accepted, clockwise, moveCount, ringOccupied) =>
                RecordRotateClassify(accepted, clockwise, moveCount, ringOccupied);
            FlowFieldTraceSink.DisciplineBAlarm = OnDisciplineBAlarm;
            FlowFieldTraceSink.LeaseAcquire = OnLeaseAcquire;
            FlowFieldTraceSink.LeaseRelease = OnLeaseRelease;
            FlowFieldTraceSink.CommitmentArrive = OnCommitmentArrive;
            FlowFieldTraceSink.BarrierPlace = OnBarrierPlace;
            FlowFieldTraceSink.BarrierSatisfied = OnBarrierSatisfied;
            FlowFieldTraceSink.BeatAlign = OnBeatAlign;
            FlowFieldTraceSink.Handoff = OnHandoff;
            RegisterChoreoSinkHandlers();
        }

        public static void UnregisterSinkHandlers()
        {
            FlowFieldTraceSink.ClearHandlers();
            ChoreoTraceSink.ClearHandlers();
            ClearBatchTag();
        }

        private static void RegisterChoreoSinkHandlers()
        {
            ChoreoTraceSink.BeginChoreo = (kind, pairs) =>
                BeginChoreoFromPairs(kind, PairsToDict(pairs));
            ChoreoTraceSink.EndChoreo = (outcome, plannedAnim, actualAnim, pairs) =>
                ChoreoTraceContext.EndChoreo(outcome, plannedAnim, actualAnim, PairsToDict(pairs));
            ChoreoTraceSink.GetCurrentSeqId = () => ChoreoTraceContext.CurrentSeqId;
            ChoreoTraceSink.RecordExploreTrace = (uid, phase, birthSlot, trackedSlot, pairs) =>
                ChoreoTraceContext.RecordExploreTrace(uid, phase, birthSlot, trackedSlot, PairsToDict(pairs));
            ChoreoTraceSink.RecordBusySnapshot = (trigger, pairs) =>
                ChoreoTraceContext.RecordBusySnapshot(trigger, PairsToDict(pairs));
            ChoreoTraceSink.EmitAnomaly = (code, uid, detail) =>
                PerfTraceRecorder.EmitChoreoAnomaly(code, uid, detail);
        }

        private static Dictionary<string, string> PairsToDict(string[] pairs)
        {
            var dict = new Dictionary<string, string>();
            if (pairs == null)
            {
                return dict;
            }

            for (var i = 0; i + 1 < pairs.Length; i += 2)
            {
                var key = pairs[i] ?? string.Empty;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                dict[key] = pairs[i + 1] ?? string.Empty;
            }

            return dict;
        }

        private static int BeginChoreoFromPairs(string kind, Dictionary<string, string> extra)
        {
            return ChoreoTraceContext.BeginChoreo(kind, extra);
        }

        public static string ResolveNodeIndex()
        {
            try
            {
                var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
                var shell = arch?.GetSystem<NineGrid.Presentation.Systems.IGameFlowShellSystem>();
                if (shell != null && shell.NodeIndex > 0)
                {
                    return shell.NodeIndex.ToString();
                }

                if (arch != null)
                {
                    var run = arch.GetModel<RunModel>();
                    if (run?.NodeIndex != null)
                    {
                        return run.NodeIndex.Value.ToString();
                    }
                }
            }
            catch
            {
                // ignore
            }

            return string.Empty;
        }

        public static void Record(
            string category,
            string name,
            Dictionary<string, string> payload = null,
            bool accepted = true,
            string phaseBefore = null,
            string phaseAfter = null)
        {
            if (!FlowTraceRecorder.Enabled)
            {
                return;
            }

            try
            {
                payload ??= new Dictionary<string, string>();
                EnrichCommon(payload);
                FlowTraceRecorder.Record(
                    category,
                    name,
                    payload,
                    phaseBefore: phaseBefore,
                    phaseAfter: phaseAfter,
                    accepted: accepted,
                    refBattleOpIndex: BattleTraceRecorder.LastOpIndex);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FieldTrace] Record failed: " + ex.Message);
            }
        }

        public static void RecordDrainBegin(
            int moves,
            int deals,
            bool drainInFlight,
            bool fieldBusy,
            bool presentationLocked,
            int stepCount = 0,
            int requestId = 0)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.DrainBegin,
                new Dictionary<string, string>
                {
                    { "moves", moves.ToString() },
                    { "deals", deals.ToString() },
                    { "stepCount", stepCount.ToString() },
                    { "requestId", requestId.ToString() },
                    { "drainInFlight", drainInFlight ? "true" : "false" },
                    { "fieldBusy", fieldBusy ? "true" : "false" },
                    { "presentationLocked", presentationLocked ? "true" : "false" },
                });
        }

        public static void RecordDrainEnd(
            int moves,
            int deals,
            bool drainInFlight,
            bool fieldBusy,
            bool presentationLocked,
            int stepCount = 0,
            int requestId = 0)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.DrainEnd,
                new Dictionary<string, string>
                {
                    { "moves", moves.ToString() },
                    { "deals", deals.ToString() },
                    { "stepCount", stepCount.ToString() },
                    { "requestId", requestId.ToString() },
                    { "drainInFlight", drainInFlight ? "true" : "false" },
                    { "fieldBusy", fieldBusy ? "true" : "false" },
                    { "presentationLocked", presentationLocked ? "true" : "false" },
                });
        }

        /// <summary>stepCount==0 时回退 Legacy Deals/Moves/Removes 路径；用于观测回退频率。</summary>
        public static void RecordDrainLegacyFallback(
            int requestId,
            int moves,
            int deals,
            int removes)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.DrainLegacyFallback,
                new Dictionary<string, string>
                {
                    { "requestId", requestId.ToString() },
                    { "moves", moves.ToString() },
                    { "deals", deals.ToString() },
                    { "removes", removes.ToString() },
                    { "stepCount", "0" },
                });
        }

        public static void RecordOccupancySnapshot(string phase, string batchTag = null)
        {
            if (!FlowTraceRecorder.Enabled)
            {
                return;
            }

            try
            {
                var previousTag = sBatchTag;
                if (!string.IsNullOrEmpty(batchTag))
                {
                    SetBatchTag(batchTag);
                }

                BuildOccupancy(out var coreHash, out var presHash, out var diffSlots,
                    out var coreCount, out var presCount);

                Record(
                    FlowTraceCategory.Field,
                    FlowTraceNames.OccupancySnapshot,
                    new Dictionary<string, string>
                    {
                        { "phase", phase ?? string.Empty },
                        { "coreHash", coreHash },
                        { "presHash", presHash },
                        { "diffSlots", diffSlots },
                        { "coreOccupantCount", coreCount.ToString() },
                        { "presOccupantCount", presCount.ToString() },
                        { "hasDiff", string.IsNullOrEmpty(diffSlots) ? "false" : "true" },
                    },
                    accepted: string.IsNullOrEmpty(diffSlots));

                if (!string.IsNullOrEmpty(batchTag))
                {
                    SetBatchTag(previousTag);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FieldTrace] OccupancySnapshot failed: " + ex.Message);
            }
        }

        public static void RecordSyncDiff(
            int vacated,
            int placed,
            int spawned,
            int swept,
            string vacatedUids = null,
            string placedUids = null,
            string spawnedUids = null,
            string sweptUids = null)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.SyncDiff,
                new Dictionary<string, string>
                {
                    { "vacated", vacated.ToString() },
                    { "placed", placed.ToString() },
                    { "spawned", spawned.ToString() },
                    { "swept", swept.ToString() },
                    { "vacatedUids", vacatedUids ?? string.Empty },
                    { "placedUids", placedUids ?? string.Empty },
                    { "spawnedUids", spawnedUids ?? string.Empty },
                    { "sweptUids", sweptUids ?? string.Empty },
                },
                accepted: vacated == 0 && spawned == 0 && swept == 0);
        }

        public static void RecordOpeningDealProgress(int uid, int slot, bool ok, int ringIndex)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.OpeningDealProgress,
                new Dictionary<string, string>
                {
                    { "uid", uid.ToString() },
                    { "slot", slot.ToString() },
                    { "ok", ok ? "true" : "false" },
                    { "ringIndex", ringIndex.ToString() },
                },
                accepted: ok);
        }

        public static void RecordOpeningGrantProgress(
            int uid,
            string sourceDefId,
            string target,
            bool ok,
            int grantIndex)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.OpeningDealProgress,
                new Dictionary<string, string>
                {
                    { "uid", uid.ToString() },
                    { "sourceDefId", sourceDefId ?? string.Empty },
                    { "target", target ?? string.Empty },
                    { "ok", ok ? "true" : "false" },
                    { "grantIndex", grantIndex.ToString() },
                },
                accepted: ok);
        }

        public static void RecordOpeningHandDealProgress(int uid, string sourceDefId, bool ok, int handIndex)
        {
            RecordOpeningGrantProgress(uid, sourceDefId, "hand", ok, handIndex);
        }

        public static void RecordHopPlanFromMoves(IReadOnlyList<PostKillCardMove> moves)
        {
            if (!FlowTraceRecorder.Enabled || moves == null || moves.Count == 0)
            {
                return;
            }

            try
            {
                var sb = new StringBuilder(moves.Count * 12);
                for (var i = 0; i < moves.Count; i++)
                {
                    var m = moves[i];
                    if (m.Uid <= 0)
                    {
                        continue;
                    }

                    if (sb.Length > 0)
                    {
                        sb.Append(';');
                    }

                    sb.Append(m.Uid).Append(':').Append(m.FromSlot).Append('\u2192').Append(m.ToSlot);
                }

                Record(
                    FlowTraceCategory.Field,
                    FlowTraceNames.HopPlan,
                    new Dictionary<string, string>
                    {
                        { "planCount", moves.Count.ToString() },
                        { "plans", sb.ToString() },
                    });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FieldTrace] HopPlan failed: " + ex.Message);
            }
        }

        public static void RecordBoardQueueEnqueue(
            int queueDepth,
            int moves,
            int deals,
            int choreoSeqId = 0)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.BoardQueueEnqueue,
                new Dictionary<string, string>
                {
                    { "queueDepth", queueDepth.ToString() },
                    { "moves", moves.ToString() },
                    { "deals", deals.ToString() },
                    { "choreoSeqId", choreoSeqId.ToString() },
                });
        }

        public static void RecordBoardQueueDequeue(
            int queueDepth,
            bool lockAcquired,
            int choreoSeqId = 0)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.BoardQueueDequeue,
                new Dictionary<string, string>
                {
                    { "queueDepth", queueDepth.ToString() },
                    { "lockAcquired", lockAcquired ? "true" : "false" },
                    { "choreoSeqId", choreoSeqId.ToString() },
                });
        }

        public static void RecordBoardQueueSkip(string reason, int pendingCount)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.BoardQueueSkip,
                new Dictionary<string, string>
                {
                    { "reason", reason ?? string.Empty },
                    { "pendingCount", pendingCount.ToString() },
                },
                accepted: false);
        }

        public static void RecordBoardStepBegin(
            int requestId,
            int stepIndex,
            int stepCount,
            BoardPresentationStep step)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.BoardStepBegin,
                new Dictionary<string, string>
                {
                    { "requestId", requestId.ToString() },
                    { "stepIndex", stepIndex.ToString() },
                    { "stepCount", stepCount.ToString() },
                    { "stepKind", step.Kind.ToString() },
                    { "commitment", step.Commitment.ToString() },
                    { "multiHopStrategy", step.MultiHopStrategy.ToString() },
                    { "coreSequence", step.CoreSequence.ToString() },
                    { "actionId", step.ActionId.ToString() },
                    { "moveCount", (step.Moves?.Length ?? 0).ToString() },
                    { "dealCount", (step.Deals?.Length ?? 0).ToString() },
                    { "removeCount", (step.RemovedUids?.Length ?? 0).ToString() },
                    { "clockwise", step.Clockwise ? "true" : "false" },
                    { "choreoSeqId", ChoreoTraceContext.CurrentSeqId.ToString() },
                });
        }

        public static void RecordBoardStepEnd(
            int requestId,
            int stepIndex,
            int stepCount,
            BoardPresentationStep step,
            int choreoSeqId)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.BoardStepEnd,
                new Dictionary<string, string>
                {
                    { "requestId", requestId.ToString() },
                    { "stepIndex", stepIndex.ToString() },
                    { "stepCount", stepCount.ToString() },
                    { "stepKind", step.Kind.ToString() },
                    { "commitment", step.Commitment.ToString() },
                    { "multiHopStrategy", step.MultiHopStrategy.ToString() },
                    { "coreSequence", step.CoreSequence.ToString() },
                    { "actionId", step.ActionId.ToString() },
                    { "choreoSeqId", choreoSeqId.ToString() },
                });
        }

        public static void RecordBoardStepFail(
            int requestId,
            int stepIndex,
            BoardPresentationStep step,
            Exception ex)
        {
            var uid = 0;
            var slot = 0;
            if (step.Deals != null && step.Deals.Length > 0)
            {
                uid = step.Deals[0].Uid;
                slot = step.Deals[0].Slot;
            }
            else if (step.Moves != null && step.Moves.Length > 0)
            {
                uid = step.Moves[0].Uid;
                slot = step.Moves[0].ToSlot;
            }
            else if (step.RemovedUids != null && step.RemovedUids.Length > 0)
            {
                uid = step.RemovedUids[0];
            }

            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.BoardStepFail,
                new Dictionary<string, string>
                {
                    { "requestId", requestId.ToString() },
                    { "stepIndex", stepIndex.ToString() },
                    { "stepKind", step.Kind.ToString() },
                    { "uid", uid.ToString() },
                    { "slot", slot.ToString() },
                    { "exType", ex?.GetType().Name ?? string.Empty },
                    { "exMessage", ex?.Message ?? string.Empty },
                },
                accepted: false);
        }

        public static void RecordBoardSyncDeferred(string phase, string reason)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.BoardSyncDeferred,
                new Dictionary<string, string>
                {
                    { "phase", phase ?? string.Empty },
                    { "reason", reason ?? string.Empty },
                    { "pumpRunning", ChoreoTraceContext.PumpRunning ? "true" : "false" },
                    { "drainInFlight", ChoreoTraceContext.DrainInFlight ? "true" : "false" },
                },
                accepted: false);
            PerfTraceRecorder.EmitChoreoAnomaly(
                PerfTraceAnomalyCodes.SyncDuringBoardTimeline,
                -1,
                phase + ":" + reason);
        }

        public static void RecordBoardStepFallbackGeneralHop(
            int requestId,
            int stepIndex,
            int moveCount)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.BoardStepFallbackGeneralHop,
                new Dictionary<string, string>
                {
                    { "requestId", requestId.ToString() },
                    { "stepIndex", stepIndex.ToString() },
                    { "moveCount", moveCount.ToString() },
                },
                accepted: false);
            PerfTraceRecorder.EmitChoreoAnomaly(
                PerfTraceAnomalyCodes.BoardStepGeneralHopFallback,
                -1,
                "requestId=" + requestId + " step=" + stepIndex + " moves=" + moveCount);
        }

        public static void RecordBoardRotateChoreoMismatch(
            int requestId,
            int coreRotateCount,
            int choreoRotateCount)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.BoardRotateChoreoMismatch,
                new Dictionary<string, string>
                {
                    { "requestId", requestId.ToString() },
                    { "coreRotateCount", coreRotateCount.ToString() },
                    { "choreoRotateCount", choreoRotateCount.ToString() },
                },
                accepted: false);
            PerfTraceRecorder.EmitChoreoAnomaly(
                PerfTraceAnomalyCodes.BoardRotateChoreoCountMismatch,
                -1,
                "requestId=" + requestId + " core=" + coreRotateCount + " choreo=" + choreoRotateCount);
        }

        public static void RecordRotateClassify(
            bool accepted,
            bool clockwise,
            int moveCount,
            int ringOccupied,
            string mismatchDetail = null)
        {
            Record(
                FlowTraceCategory.Field,
                FlowTraceNames.RotateClassify,
                new Dictionary<string, string>
                {
                    { "accepted", accepted ? "true" : "false" },
                    { "clockwise", clockwise ? "true" : "false" },
                    { "moveCount", moveCount.ToString() },
                    { "ringOccupied", ringOccupied.ToString() },
                    { "mismatchDetail", mismatchDetail ?? string.Empty },
                    { "choreoSeqId", ChoreoTraceContext.CurrentSeqId.ToString() },
                },
                accepted: accepted);
        }

        public static void RecordPickupAttempt(int uid, int groundSlot, string defId, string coreKind)
        {
            ChoreoTraceContext.RecordBusySnapshot("Pickup.Attempt");
            Record(
                FlowTraceCategory.Hand,
                FlowTraceNames.PickupAttempt,
                new Dictionary<string, string>
                {
                    { "uid", uid.ToString() },
                    { "groundSlot", groundSlot.ToString() },
                    { "defId", defId ?? string.Empty },
                    { "coreKind", coreKind ?? string.Empty },
                    { "choreoSeqId", ChoreoTraceContext.CurrentSeqId.ToString() },
                });
        }

        public static void RecordPickupGate(
            int uid,
            string gate,
            bool accepted,
            string coreReason = null)
        {
            if (!accepted)
            {
                ChoreoTraceContext.NotePickupGateFailure(gate);
            }

            RecordOccupancySnapshot("pickupClick", FlowTraceBatchTags.Pickup);
            Record(
                FlowTraceCategory.Hand,
                FlowTraceNames.PickupGate,
                new Dictionary<string, string>
                {
                    { "uid", uid.ToString() },
                    { "gate", gate ?? string.Empty },
                    { "accepted", accepted ? "true" : "false" },
                    { "coreReason", coreReason ?? string.Empty },
                    { "choreoSeqId", ChoreoTraceContext.CurrentSeqId.ToString() },
                },
                accepted: accepted);
        }

        public static void RecordPickupSuccess(int uid, int handSlot)
        {
            Record(
                FlowTraceCategory.Hand,
                FlowTraceNames.PickupSuccess,
                new Dictionary<string, string>
                {
                    { "uid", uid.ToString() },
                    { "handSlot", handSlot.ToString() },
                });
        }

        public static Dictionary<string, string> BuildStartNodePayload(
            int avatarUid,
            int deckCount,
            int boardOccupantCount,
            int presOccupantCount)
        {
            return new Dictionary<string, string>
            {
                { "avatarUid", avatarUid.ToString() },
                { "nodeIndex", ResolveNodeIndex() },
                { "deckCount", deckCount.ToString() },
                { "boardOccupantCount", boardOccupantCount.ToString() },
                { "presOccupantCount", presOccupantCount.ToString() },
            };
        }

        public static void CountBoardOccupants(out int coreCount, out int presCount)
        {
            coreCount = 0;
            presCount = 0;
            try
            {
                var arch = NineGridArchitecture.Current;
                var board = arch?.GetModel<BoardModel>();
                if (board != null)
                {
                    for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
                    {
                        if (slot == GroundSlotTopology.AvatarReservedSlot)
                        {
                            continue;
                        }

                        if (board.GetCardUid(SlotId.Board(slot)) > 0)
                        {
                            coreCount++;
                        }
                    }
                }

                var field = GroundFieldGeometryHook.FieldOrNull();
                if (field != null)
                {
                    var snap = field.GetSnapshot();
                    for (var i = 0; i < snap.Slots.Length; i++)
                    {
                        var occ = snap.Slots[i];
                        if (!occ.IsEmpty && !occ.IsAvatarReserved)
                        {
                            presCount++;
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        private static void OnOccupancyConflict(int slot, int existingUid, int incomingUid, string caller)
        {
            Record(
                FlowTraceCategory.Field,
                FlowTraceNames.OccupancyConflict,
                new Dictionary<string, string>
                {
                    { "slot", slot.ToString() },
                    { "existingUid", existingUid.ToString() },
                    { "incomingUid", incomingUid.ToString() },
                    { "caller", caller ?? string.Empty },
                },
                accepted: false);
        }

        private static void OnHopPlan(string plans)
        {
            Record(
                FlowTraceCategory.Field,
                FlowTraceNames.HopPlan,
                new Dictionary<string, string>
                {
                    { "plans", plans ?? string.Empty },
                });
        }

        private static void OnDealAttempt(int uid, int slot, string caller)
        {
            Record(
                FlowTraceCategory.Deck,
                FlowTraceNames.DealAttempt,
                new Dictionary<string, string>
                {
                    { "uid", uid.ToString() },
                    { "slot", slot.ToString() },
                    { "caller", caller ?? string.Empty },
                });
        }

        private static void OnDealResult(
            int uid,
            int slot,
            bool placeable,
            bool ok,
            bool rollback,
            string caller,
            string reason)
        {
            var payload = new Dictionary<string, string>
            {
                { "uid", uid.ToString() },
                { "slot", slot.ToString() },
                { "placeable", placeable ? "true" : "false" },
                { "ok", ok ? "true" : "false" },
                { "rollback", rollback ? "true" : "false" },
                { "caller", caller ?? string.Empty },
                { "reason", reason ?? string.Empty },
            };

            Record(
                FlowTraceCategory.Deck,
                FlowTraceNames.DealResult,
                payload,
                accepted: ok);
        }

        private static void OnHandLifecycle(int uid, string phase, bool handContains, string displayMode)
        {
            var name = string.Equals(phase, "acquire", StringComparison.OrdinalIgnoreCase)
                || string.Equals(phase, "HandAcquire", StringComparison.OrdinalIgnoreCase)
                ? FlowTraceNames.HandAcquire
                : FlowTraceNames.HandRelease;

            Record(
                FlowTraceCategory.Hand,
                name,
                new Dictionary<string, string>
                {
                    { "uid", uid.ToString() },
                    { "phase", phase ?? string.Empty },
                    { "handContains", handContains ? "true" : "false" },
                    { "displayMode", displayMode ?? string.Empty },
                });
        }

        private static void OnOccupancyVacate(int slot, int uid, string caller)
        {
            Record(
                FlowTraceCategory.Field,
                FlowTraceNames.OccupancyVacate,
                new Dictionary<string, string>
                {
                    { "slot", slot.ToString() },
                    { "uid", uid.ToString() },
                    { "caller", caller ?? string.Empty },
                });
        }

        private static void OnRegistryAudit(
            string trigger,
            int registryCount,
            int fieldCount,
            string ghosts,
            string orphans)
        {
            var hasGhosts = !string.IsNullOrEmpty(ghosts);
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.RegistryAudit,
                new Dictionary<string, string>
                {
                    { "trigger", trigger ?? string.Empty },
                    { "registryCount", registryCount.ToString() },
                    { "fieldCount", fieldCount.ToString() },
                    { "ghosts", ghosts ?? string.Empty },
                    { "orphans", orphans ?? string.Empty },
                    { "hasGhosts", hasGhosts ? "true" : "false" },
                },
                accepted: !hasGhosts && string.IsNullOrEmpty(orphans));

            RegistryTraceRecorder.OnAuditCompleted(
                trigger,
                registryCount,
                fieldCount,
                ghosts,
                orphans);
        }

        private static void OnDisciplineBAlarm(
            int uid,
            string code,
            string reason,
            string layer,
            string verdict)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.DisciplineBAlarm,
                new Dictionary<string, string>
                {
                    { "uid", uid.ToString() },
                    { "code", code ?? string.Empty },
                    { "reason", reason ?? string.Empty },
                    { "layer", layer ?? string.Empty },
                    { "verdict", verdict ?? string.Empty },
                },
                accepted: false);
        }

        private static void OnLeaseAcquire(
            int uid,
            string layer,
            string verdict,
            string commitment,
            int leaseId,
            float windowStart,
            float windowEnd,
            bool disciplineB,
            bool commandeered)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.LeaseAcquire,
                new Dictionary<string, string>
                {
                    { "uid", uid.ToString() },
                    { "layer", layer ?? string.Empty },
                    { "verdict", verdict ?? string.Empty },
                    { "commitment", commitment ?? string.Empty },
                    { "leaseId", leaseId.ToString() },
                    { "windowStart", windowStart.ToString("0.###") },
                    { "windowEnd", windowEnd.ToString("0.###") },
                    { "disciplineB", disciplineB ? "true" : "false" },
                    { "commandeered", commandeered ? "true" : "false" },
                },
                accepted: string.Equals(verdict, "Accepted", StringComparison.Ordinal)
                    || string.Equals(verdict, "Commandeered", StringComparison.Ordinal));
        }

        private static void OnLeaseRelease(int uid, string layer, int leaseId, string reason)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.LeaseRelease,
                new Dictionary<string, string>
                {
                    { "uid", uid.ToString() },
                    { "layer", layer ?? string.Empty },
                    { "leaseId", leaseId.ToString() },
                    { "reason", reason ?? string.Empty },
                });
        }

        private static void OnCommitmentArrive(
            int uid,
            string layer,
            string commitment,
            int leaseId,
            string site)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.CommitmentArrive,
                new Dictionary<string, string>
                {
                    { "uid", uid.ToString() },
                    { "layer", layer ?? string.Empty },
                    { "commitment", commitment ?? string.Empty },
                    { "leaseId", leaseId.ToString() },
                    { "site", site ?? string.Empty },
                });
        }

        private static void OnBarrierPlace(
            int presBeatId,
            float barrierWall,
            float sourceTime,
            float startWall,
            int regHint)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.BarrierPlace,
                new Dictionary<string, string>
                {
                    { "presBeatId", presBeatId.ToString() },
                    { "barrierWall", barrierWall.ToString("0.###") },
                    { "sourceTime", sourceTime.ToString("0.###") },
                    { "startWall", startWall.ToString("0.###") },
                    { "regHint", regHint.ToString() },
                });
        }

        private static void OnBarrierSatisfied(
            int presBeatId,
            bool satisfied,
            int regCount,
            float nowWall)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.BarrierSatisfied,
                new Dictionary<string, string>
                {
                    { "presBeatId", presBeatId.ToString() },
                    { "satisfied", satisfied ? "true" : "false" },
                    { "regCount", regCount.ToString() },
                    { "nowWall", nowWall.ToString("0.###") },
                },
                accepted: satisfied);
        }

        private static void OnBeatAlign(int presBeatId, float sourceTime, float startWall)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.BeatAlign,
                new Dictionary<string, string>
                {
                    { "presBeatId", presBeatId.ToString() },
                    { "sourceTime", sourceTime.ToString("0.###") },
                    { "startWall", startWall.ToString("0.###") },
                });
        }

        private static void OnHandoff(
            int uid,
            string layer,
            string phase,
            string vx,
            string vy,
            string site)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.Handoff,
                new Dictionary<string, string>
                {
                    { "uid", uid.ToString() },
                    { "layer", layer ?? string.Empty },
                    { "phase", phase ?? string.Empty },
                    { "vx", vx ?? string.Empty },
                    { "vy", vy ?? string.Empty },
                    { "site", site ?? string.Empty },
                });
        }

        private static void EnrichCommon(Dictionary<string, string> payload)
        {
            if (!payload.ContainsKey("nodeIndex"))
            {
                payload["nodeIndex"] = ResolveNodeIndex();
            }

            if (!payload.ContainsKey("batchTag") && !string.IsNullOrEmpty(sBatchTag))
            {
                payload["batchTag"] = sBatchTag;
            }
        }

        private static void BuildOccupancy(
            out string coreHash,
            out string presHash,
            out string diffSlots,
            out int coreCount,
            out int presCount)
        {
            coreHash = string.Empty;
            presHash = string.Empty;
            diffSlots = string.Empty;
            coreCount = 0;
            presCount = 0;

            var coreSb = new StringBuilder(64);
            var presSb = new StringBuilder(64);
            var diffSb = new StringBuilder(64);

            var arch = NineGridArchitecture.Current;
            var board = arch?.GetModel<BoardModel>();
            var field = GroundFieldGeometryHook.FieldOrNull();
            GroundFieldSnapshot snap = null;
            var hasSnap = false;
            if (field != null)
            {
                snap = field.GetSnapshot();
                hasSnap = snap?.Slots != null;
            }

            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                if (slot == GroundSlotTopology.AvatarReservedSlot)
                {
                    continue;
                }

                var coreUid = 0;
                if (board != null)
                {
                    coreUid = board.GetCardUid(SlotId.Board(slot));
                }

                var presUid = 0;
                if (hasSnap)
                {
                    var idx = slot - 1;
                    if (idx >= 0 && idx < snap.Slots.Length)
                    {
                        presUid = snap.Slots[idx].Uid;
                    }
                }

                if (coreUid > 0)
                {
                    coreCount++;
                }

                if (presUid > 0)
                {
                    presCount++;
                }

                if (coreSb.Length > 0)
                {
                    coreSb.Append('|');
                }

                coreSb.Append(slot).Append(':').Append(coreUid);

                if (presSb.Length > 0)
                {
                    presSb.Append('|');
                }

                presSb.Append(slot).Append(':').Append(presUid);

                if (coreUid != presUid)
                {
                    if (diffSb.Length > 0)
                    {
                        diffSb.Append(',');
                    }

                    diffSb.Append(slot)
                        .Append(":C").Append(coreUid)
                        .Append("/P").Append(presUid);
                }
            }

            coreHash = coreSb.ToString();
            presHash = presSb.ToString();
            diffSlots = diffSb.ToString();
        }
    }
}
