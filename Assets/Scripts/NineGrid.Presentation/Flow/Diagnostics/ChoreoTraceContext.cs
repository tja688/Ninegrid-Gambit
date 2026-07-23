using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NineGrid.Cards;
using UnityEngine;
using NineGrid.Presentation;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 场地编排批次关联键：会话内单调递增 choreoSeqId，串起旋转/交换/探求/队列项。
    /// </summary>
    public static class ChoreoTraceContext
    {
        private const int RecentSummaryCap = 3;

        private static int sNextSeqId = 1;
        private static readonly Stack<OpenChoreo> sOpenStack = new Stack<OpenChoreo>();
        private static readonly List<ChoreoSummaryEntry> sRecent = new List<ChoreoSummaryEntry>(RecentSummaryCap);

        private static int sPartialAnimateCount;
        private static int sPickupGateFailCount;
        private static string sLastPickupGate = string.Empty;
        private static int sLastChoreoSeqId;
        private static bool sLastPickupEligibilityCanRespond;
        private static bool sOccupancyDesyncLatched;

        public static int BoardQueueDepth { get; set; }
        public static bool PumpRunning { get; set; }
        public static bool DrainInFlight { get; set; }

        /// <summary>
        /// drainAfter 粘滞 Core↔Pres 占格分叉时置位；输入门全拒，直至清幽灵成功或 Reset。
        /// </summary>
        public static bool OccupancyDesyncLatched => sOccupancyDesyncLatched;

        public static int CurrentSeqId => sOpenStack.Count > 0 ? sOpenStack.Peek().SeqId : 0;

        public static int OpenChoreoCount => sOpenStack.Count;

        public static int PartialAnimateCount => sPartialAnimateCount;

        public static int PickupGateFailCount => sPickupGateFailCount;

        public static string LastPickupGate => sLastPickupGate ?? string.Empty;

        public static int LastChoreoSeqId => sLastChoreoSeqId;

        public static void Reset()
        {
            sNextSeqId = 1;
            sOpenStack.Clear();
            sRecent.Clear();
            sPartialAnimateCount = 0;
            sPickupGateFailCount = 0;
            sLastPickupGate = string.Empty;
            sLastChoreoSeqId = 0;
            sLastPickupEligibilityCanRespond = false;
            sOccupancyDesyncLatched = false;
            BoardQueueDepth = 0;
            PumpRunning = false;
            DrainInFlight = false;
        }

        public static void LatchOccupancyDesync(string detail = null)
        {
            sOccupancyDesyncLatched = true;
            PerfTraceRecorder.EmitChoreoAnomaly(
                "OccupancyDesyncLatched",
                -1,
                detail ?? "drainAfter.hasDiff");
        }

        public static void ClearOccupancyDesyncLatch(string reason = null)
        {
            if (!sOccupancyDesyncLatched)
            {
                return;
            }

            sOccupancyDesyncLatched = false;
            PerfTraceRecorder.EmitChoreoAnomaly(
                "OccupancyDesyncCleared",
                -1,
                reason ?? "cleared");
        }

        public static int BeginChoreo(string kind, Dictionary<string, string> extra = null)
        {
            try
            {
                var seqId = sNextSeqId++;
                sLastChoreoSeqId = seqId;
                var open = new OpenChoreo
                {
                    SeqId = seqId,
                    Kind = kind ?? string.Empty,
                    StartMs = ElapsedMs(),
                    PlannedAnim = 0,
                };
                sOpenStack.Push(open);

                var payload = BuildBusyPayload(extra);
                payload["choreoSeqId"] = seqId.ToString(CultureInfo.InvariantCulture);
                payload["kind"] = open.Kind;
                CardPresentationProbe.ChoreoBegin(payload);
                return seqId;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ChoreoTrace] BeginChoreo failed: " + ex.Message);
                return 0;
            }
        }

        public static void EndChoreo(
            string outcome,
            int plannedAnim = 0,
            int actualAnim = 0,
            Dictionary<string, string> extra = null)
        {
            try
            {
                if (sOpenStack.Count == 0)
                {
                    return;
                }

                var open = sOpenStack.Pop();
                var durationMs = Math.Max(0, ElapsedMs() - open.StartMs);
                if (plannedAnim > 0 && actualAnim < plannedAnim)
                {
                    sPartialAnimateCount++;
                    PerfTraceRecorder.EmitChoreoAnomaly(
                        PerfTraceAnomalyCodes.ChoreoPartialAnimate,
                        -1,
                        "seqId=" + open.SeqId + " planned=" + plannedAnim + " actual=" + actualAnim);
                }

                var payload = BuildBusyPayload(extra);
                payload["choreoSeqId"] = open.SeqId.ToString(CultureInfo.InvariantCulture);
                payload["kind"] = open.Kind;
                payload["outcome"] = outcome ?? string.Empty;
                payload["plannedAnim"] = plannedAnim.ToString(CultureInfo.InvariantCulture);
                payload["actualAnim"] = actualAnim.ToString(CultureInfo.InvariantCulture);
                payload["durationMs"] = durationMs.ToString(CultureInfo.InvariantCulture);
                CardPresentationProbe.ChoreoEnd(payload);

                PushRecent(open.Kind, outcome, plannedAnim, actualAnim, open.SeqId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ChoreoTrace] EndChoreo failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 取消/异常恢复：关闭所有未结束的 choreo，避免泄漏到后续 Sync 诊断。
        /// </summary>
        public static void ForceCloseOpenChoreos(string reason)
        {
            while (sOpenStack.Count > 0)
            {
                EndChoreo("cancelled:" + (reason ?? string.Empty));
            }
        }

        public static void RecordExploreTrace(
            int uid,
            string phase,
            int birthSlot,
            int trackedSlot,
            Dictionary<string, string> extra = null)
        {
            try
            {
                var payload = extra != null
                    ? new Dictionary<string, string>(extra)
                    : new Dictionary<string, string>();
                // ADR-0003：Cards/Flow 表演诊断 payload 必须同带 chainId + choreoSeqId。
                if (!payload.ContainsKey("choreoSeqId"))
                {
                    payload["choreoSeqId"] = CurrentSeqId.ToString(CultureInfo.InvariantCulture);
                }

                if (!payload.ContainsKey("chainId"))
                {
                    payload["chainId"] = DirectorTrace.CurrentChainId.ToString(CultureInfo.InvariantCulture);
                }

                payload["phase"] = phase ?? string.Empty;
                payload["birthSlot"] = birthSlot.ToString(CultureInfo.InvariantCulture);
                payload["trackedSlot"] = trackedSlot.ToString(CultureInfo.InvariantCulture);
                CardPresentationProbe.ExploreTrace(uid, payload);
            }
            catch
            {
                // swallow
            }
        }

        public static void RecordBusySnapshot(string trigger, Dictionary<string, string> extra = null)
        {
            try
            {
                var payload = BuildBusyPayload(extra);
                payload["trigger"] = trigger ?? string.Empty;
                if (CurrentSeqId > 0)
                {
                    payload["choreoSeqId"] = CurrentSeqId.ToString(CultureInfo.InvariantCulture);
                }

                CardPresentationProbe.BusySnapshot(payload);
            }
            catch
            {
                // swallow
            }
        }

        public static void NotePickupEligibility(bool canRespond)
        {
            sLastPickupEligibilityCanRespond = canRespond;
        }

        public static void NotePickupGateFailure(string gate)
        {
            sPickupGateFailCount++;
            sLastPickupGate = gate ?? string.Empty;
            if (sLastPickupEligibilityCanRespond)
            {
                PerfTraceRecorder.EmitChoreoAnomaly(
                    PerfTraceAnomalyCodes.PickupVisualEligibleButGateFail,
                    -1,
                    "gate=" + gate);
            }
        }

        public static string GetOpenChoreoSummary()
        {
            if (sOpenStack.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(sOpenStack.Count * 16);
            foreach (var open in sOpenStack)
            {
                if (sb.Length > 0)
                {
                    sb.Append(';');
                }

                sb.Append(open.SeqId).Append(':').Append(open.Kind);
            }

            return sb.ToString();
        }

        public static string GetRecentChoreoSummary()
        {
            if (sRecent.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(sRecent.Count * 24);
            for (var i = 0; i < sRecent.Count; i++)
            {
                var e = sRecent[i];
                if (sb.Length > 0)
                {
                    sb.Append(';');
                }

                sb.Append(e.SeqId)
                    .Append(':')
                    .Append(e.Kind)
                    .Append('=')
                    .Append(e.Outcome)
                    .Append('(')
                    .Append(e.ActualAnim)
                    .Append('/')
                    .Append(e.PlannedAnim)
                    .Append(')');
            }

            return sb.ToString();
        }

        public static Dictionary<string, string> BuildSessionSummaryPayload()
        {
            return new Dictionary<string, string>
            {
                { "choreoOpenAtExit", OpenChoreoCount.ToString(CultureInfo.InvariantCulture) },
                { "choreoPartialAnimateCount", sPartialAnimateCount.ToString(CultureInfo.InvariantCulture) },
                { "pickupGateFailCount", sPickupGateFailCount.ToString(CultureInfo.InvariantCulture) },
                { "lastPickupGate", sLastPickupGate ?? string.Empty },
                { "lastChoreoSeqId", sLastChoreoSeqId.ToString(CultureInfo.InvariantCulture) },
                { "openChoreoSummary", GetOpenChoreoSummary() },
                { "recentChoreoSummary", GetRecentChoreoSummary() },
                { "openMotionCount", PerfTraceRecorder.OpenMotionCount.ToString(CultureInfo.InvariantCulture) },
            };
        }

        public static void AppendExportSummaryEvents()
        {
            try
            {
                var payload = BuildSessionSummaryPayload();
                PerfTraceRecorder.RecordSessionChoreoSummary(payload);
                FlowTraceRecorder.RecordSessionChoreoSummary(payload);
                RegistryTraceRecorder.RecordSessionChoreoSummary(payload);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ChoreoTrace] AppendExportSummary failed: " + ex.Message);
            }
        }

        public static Dictionary<string, string> BuildBusyPayload(Dictionary<string, string> extra = null)
        {
            var payload = extra != null
                ? new Dictionary<string, string>(extra)
                : new Dictionary<string, string>();

            try
            {
                var field = GroundFieldGeometryHook.FieldOrNull();
                var deck = CardEntityLifecycleHook.DeckOrNull();
                var hand = CardEntityLifecycleHook.HandOrNull();
                var battle = UnityEngine.Object.FindFirstObjectByType<BattleSessionController>();

                payload["fieldBusy"] = field != null && field.IsBusy ? "true" : "false";
                payload["fieldSelfBusy"] = field != null && field.IsFieldBusy ? "true" : "false";
                payload["deckBusy"] = deck != null && deck.IsBusy ? "true" : "false";
                payload["handBusy"] = hand != null && hand.IsBusy ? "true" : "false";
                payload["drainInFlight"] = DrainInFlight ? "true" : "false";
                payload["pumpRunning"] = PumpRunning ? "true" : "false";
                payload["queueDepth"] = BoardQueueDepth.ToString(CultureInfo.InvariantCulture);
                payload["presentationLocked"] = PresentationInputGates.HasExternalHold ? "true" : "false";
                payload["choiceOverlay"] = PresentationInputGates.ChoiceOverlayActive ? "true" : "false";
                payload["battleBusy"] = NineGrid.Core.NineGridArchitecture.Interface
                    ?.GetSystem<NineGrid.Presentation.Systems.IFieldBattlePresentationSystem>()
                    ?.IsBusy == true
                    ? "true"
                    : "false";
                payload["openMotionCount"] = PerfTraceRecorder.OpenMotionCount.ToString(CultureInfo.InvariantCulture);
                if (battle != null)
                {
                    payload["inBattleBusy"] = battle.IsBusy ? "true" : "false";
                }

                DirectorTrace.AppendBusyFields(payload);
            }
            catch
            {
                // swallow
            }

            return payload;
        }

        private static void PushRecent(
            string kind,
            string outcome,
            int plannedAnim,
            int actualAnim,
            int seqId)
        {
            sRecent.Insert(0, new ChoreoSummaryEntry
            {
                SeqId = seqId,
                Kind = kind ?? string.Empty,
                Outcome = outcome ?? string.Empty,
                PlannedAnim = plannedAnim,
                ActualAnim = actualAnim,
            });

            while (sRecent.Count > RecentSummaryCap)
            {
                sRecent.RemoveAt(sRecent.Count - 1);
            }
        }

        private static int ElapsedMs()
        {
            return Mathf.Max(0, Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f));
        }

        private struct OpenChoreo
        {
            public int SeqId;
            public string Kind;
            public int StartMs;
            public int PlannedAnim;
        }

        private struct ChoreoSummaryEntry
        {
            public int SeqId;
            public string Kind;
            public string Outcome;
            public int PlannedAnim;
            public int ActualAnim;
        }
    }
}
