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
            FlowFieldTraceSink.DealResult = OnDealResult;
            FlowFieldTraceSink.HandLifecycle = OnHandLifecycle;
        }

        public static void UnregisterSinkHandlers()
        {
            FlowFieldTraceSink.ClearHandlers();
            ClearBatchTag();
        }

        public static string ResolveNodeIndex()
        {
            try
            {
                var loop = MainGameLoopManagerSingleton.Instance;
                if (loop != null && loop.NodeIndex > 0)
                {
                    return loop.NodeIndex.ToString();
                }

                var arch = NineGridArchitecture.Current;
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
            bool presentationLocked)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.DrainBegin,
                new Dictionary<string, string>
                {
                    { "moves", moves.ToString() },
                    { "deals", deals.ToString() },
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
            bool presentationLocked)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.DrainEnd,
                new Dictionary<string, string>
                {
                    { "moves", moves.ToString() },
                    { "deals", deals.ToString() },
                    { "drainInFlight", drainInFlight ? "true" : "false" },
                    { "fieldBusy", fieldBusy ? "true" : "false" },
                    { "presentationLocked", presentationLocked ? "true" : "false" },
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

                var field = GroundFieldManagerSingleton.Instance;
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

        private static void OnDealResult(
            int uid,
            int slot,
            bool placeable,
            bool ok,
            bool rollback,
            string caller)
        {
            var payload = new Dictionary<string, string>
            {
                { "uid", uid.ToString() },
                { "slot", slot.ToString() },
                { "placeable", placeable ? "true" : "false" },
                { "ok", ok ? "true" : "false" },
                { "rollback", rollback ? "true" : "false" },
                { "caller", caller ?? string.Empty },
            };

            Record(FlowTraceCategory.Deck, FlowTraceNames.DealAttempt, payload, accepted: placeable);
            Record(
                FlowTraceCategory.Deck,
                FlowTraceNames.DealResult,
                new Dictionary<string, string>(payload),
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
            var field = GroundFieldManagerSingleton.Instance;
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
