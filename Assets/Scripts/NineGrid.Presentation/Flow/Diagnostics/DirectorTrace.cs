using System;
using System.Collections.Generic;
using System.Globalization;
using NineGrid.Flow.Presentation;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// PresentationDirector 剧本层 Perf 打点薄封装。统一 path/lane/batchId；
    /// 并缓存 busy 快照字段供 BusySnapshot 读取（避免 Diagnostics↔Director 循环引用）。
    /// </summary>
    public static class DirectorTrace
    {
        public const string PathDirector = "director";
        public const string LaneMainline = "mainline";
        public const string LaneBypass = "bypass";

        public const string RejectHasOpen = "hasOpen";
        public const string RejectDispatch = "dispatchReject";
        public const string RejectNoBatch = "noBatch";

        public const string StallPhaseNotComplete = "!complete";
        public const string StallPhaseNotAck = "!ack";

        /// <summary>Present 连续 Continue 超过此时长（秒）后打 Stall。</summary>
        public const float PresentStallThresholdSec = 1f;

        /// <summary>Stall 重复上报最小间隔（秒）。</summary>
        public const float PresentStallRepeatSec = 1f;

        private static bool sMainlineBusy;
        private static bool sBypassBusy;
        private static bool sHasBufferedIntent;
        private static string sBufferedIntentKind = string.Empty;
        private static int sActiveBatchId;
        private static string sOpenRejectReason = string.Empty;
        private static bool sAckRejectLogged;

        public static bool DirectorMainlineBusy => sMainlineBusy;

        public static bool DirectorBypassBusy => sBypassBusy;

        public static bool HasBufferedIntent => sHasBufferedIntent;

        public static string BufferedIntentKind => sBufferedIntentKind ?? string.Empty;

        public static int ActiveBatchId => sActiveBatchId;

        public static void Reset()
        {
            sMainlineBusy = false;
            sBypassBusy = false;
            sHasBufferedIntent = false;
            sBufferedIntentKind = string.Empty;
            sActiveBatchId = 0;
            sOpenRejectReason = string.Empty;
            sAckRejectLogged = false;
        }

        /// <summary>由 PresentationDirector.Tick / 意图变更时刷新 BusySnapshot 字段。</summary>
        public static void PublishBusyState(
            bool mainlineBusy,
            bool bypassBusy,
            bool hasBufferedIntent,
            string bufferedIntentKind)
        {
            sMainlineBusy = mainlineBusy;
            sBypassBusy = bypassBusy;
            sHasBufferedIntent = hasBufferedIntent;
            sBufferedIntentKind = bufferedIntentKind ?? string.Empty;
        }

        public static void PublishActiveBatchId(int batchId)
        {
            sActiveBatchId = batchId > 0 ? batchId : 0;
            if (batchId <= 0)
            {
                sOpenRejectReason = string.Empty;
                sAckRejectLogged = false;
            }
        }

        public static void AppendBusyFields(Dictionary<string, string> payload)
        {
            if (payload == null)
            {
                return;
            }

            payload["directorMainlineBusy"] = sMainlineBusy ? "true" : "false";
            payload["directorBypassBusy"] = sBypassBusy ? "true" : "false";
            payload["bufferedIntent"] = sHasBufferedIntent ? "1" : "0";
            if (sHasBufferedIntent && !string.IsNullOrEmpty(sBufferedIntentKind))
            {
                payload["bufferedIntentKind"] = sBufferedIntentKind;
            }

            payload["activeBatchId"] = sActiveBatchId.ToString(CultureInfo.InvariantCulture);
        }

        public static void BatchOpen(int batchId, string slice = null)
        {
            sOpenRejectReason = string.Empty;
            sAckRejectLogged = false;
            PublishActiveBatchId(batchId);
            Record(
                PerfTraceKinds.DirectorBatchOpen,
                PerfTraceSites.DirectorBatchGate,
                batchId,
                BasePayload(batchId, extra: slice != null
                    ? new Dictionary<string, string> { ["slice"] = slice }
                    : null));
        }

        /// <summary>
        /// 打开失败。同 reason 连续拒绝只记首条，避免 ResolveBatch Continue 逐帧刷屏。
        /// detail 可选（如 Core 拒因），不参与去重键。
        /// </summary>
        public static void BatchOpenRejected(string reason, string detail = null)
        {
            var r = reason ?? string.Empty;
            if (string.Equals(sOpenRejectReason, r, StringComparison.Ordinal))
            {
                return;
            }

            sOpenRejectReason = r;
            var extra = new Dictionary<string, string> { ["reason"] = r };
            if (!string.IsNullOrEmpty(detail))
            {
                extra["detail"] = detail;
            }

            Record(
                PerfTraceKinds.DirectorBatchOpenRejected,
                PerfTraceSites.DirectorBatchGate,
                sActiveBatchId,
                BasePayload(sActiveBatchId, extra: extra));
        }

        /// <summary>Resolve 终态失败等：剧本中止，主线应立即 idle。</summary>
        public static void ScriptAborted(string reason)
        {
            sOpenRejectReason = string.Empty;
            Record(
                PerfTraceKinds.DirectorScriptAborted,
                PerfTraceSites.DirectorBatchGate,
                sActiveBatchId,
                BasePayload(
                    sActiveBatchId,
                    extra: new Dictionary<string, string>
                    {
                        ["reason"] = reason ?? string.Empty,
                    }));
        }

        public static void PresentBegin(int batchId, string channel = null)
        {
            PublishActiveBatchId(batchId);
            sAckRejectLogged = false;
            var extra = channel != null
                ? new Dictionary<string, string> { ["channel"] = channel }
                : null;
            Record(
                PerfTraceKinds.DirectorPresentBegin,
                PerfTraceSites.DirectorPresentStep,
                batchId,
                BasePayload(batchId, extra: extra));
        }

        public static void PresentAck(int batchId)
        {
            sAckRejectLogged = false;
            Record(
                PerfTraceKinds.DirectorPresentAck,
                PerfTraceSites.DirectorPresentStep,
                batchId,
                BasePayload(batchId));
            PublishActiveBatchId(0);
        }

        public static void PresentAckRejected(int batchId)
        {
            if (sAckRejectLogged)
            {
                return;
            }

            sAckRejectLogged = true;
            Record(
                PerfTraceKinds.DirectorPresentAckRejected,
                PerfTraceSites.DirectorPresentStep,
                batchId,
                BasePayload(
                    batchId,
                    extra: new Dictionary<string, string>
                    {
                        ["expected"] = batchId.ToString(CultureInfo.InvariantCulture),
                    }));
        }

        public static void PresentStall(int batchId, float waitSec, string phase)
        {
            Record(
                PerfTraceKinds.DirectorPresentStall,
                PerfTraceSites.DirectorPresentStep,
                batchId,
                BasePayload(
                    batchId,
                    extra: new Dictionary<string, string>
                    {
                        ["waitMs"] = ((int)(waitSec * 1000f)).ToString(CultureInfo.InvariantCulture),
                        ["phase"] = phase ?? string.Empty,
                    }));
        }

        public static void IntentAccepted(string intentKind, int targetId)
        {
            Record(
                PerfTraceKinds.DirectorIntentAccepted,
                PerfTraceSites.DirectorIntent,
                -1,
                IntentPayload(intentKind, targetId));
        }

        public static void IntentBuffered(string intentKind, int targetId, bool uiPick)
        {
            var payload = IntentPayload(intentKind, targetId);
            payload["uiPick"] = uiPick ? "1" : "0";
            Record(
                PerfTraceKinds.DirectorIntentBuffered,
                PerfTraceSites.DirectorIntent,
                -1,
                payload);
        }

        public static void IntentRejected(string intentKind, int targetId)
        {
            Record(
                PerfTraceKinds.DirectorIntentRejected,
                PerfTraceSites.DirectorIntent,
                -1,
                IntentPayload(intentKind, targetId));
        }

        public static void IntentFlush(string intentKind, int targetId)
        {
            Record(
                PerfTraceKinds.DirectorIntentFlush,
                PerfTraceSites.DirectorIntent,
                -1,
                IntentPayload(intentKind, targetId));
        }

        public static void IntentHardClear(string reason)
        {
            Record(
                PerfTraceKinds.DirectorIntentHardClear,
                PerfTraceSites.DirectorIntent,
                -1,
                BasePayload(
                    0,
                    extra: new Dictionary<string, string>
                    {
                        ["reason"] = reason ?? string.Empty,
                    }));
            PublishBusyState(false, false, false, string.Empty);
            PublishActiveBatchId(0);
        }

        public static void StepEnter(string step, string lane = LaneMainline)
        {
            Record(
                PerfTraceKinds.DirectorStepEnter,
                PerfTraceSites.DirectorTimeline,
                -1,
                BasePayload(
                    sActiveBatchId,
                    lane: lane,
                    extra: new Dictionary<string, string>
                    {
                        ["step"] = step ?? string.Empty,
                    }));
        }

        public static void StepExit(string step, string lane = LaneMainline)
        {
            Record(
                PerfTraceKinds.DirectorStepExit,
                PerfTraceSites.DirectorTimeline,
                -1,
                BasePayload(
                    sActiveBatchId,
                    lane: lane,
                    extra: new Dictionary<string, string>
                    {
                        ["step"] = step ?? string.Empty,
                    }));
        }

        public static void ForkBegin(int childCount)
        {
            Record(
                PerfTraceKinds.DirectorForkBegin,
                PerfTraceSites.DirectorFork,
                -1,
                BasePayload(
                    sActiveBatchId,
                    extra: new Dictionary<string, string>
                    {
                        ["step"] = "Fork",
                        ["childCount"] = childCount.ToString(CultureInfo.InvariantCulture),
                    }));
        }

        public static void ForkEnd(int childCount)
        {
            Record(
                PerfTraceKinds.DirectorForkEnd,
                PerfTraceSites.DirectorFork,
                -1,
                BasePayload(
                    sActiveBatchId,
                    extra: new Dictionary<string, string>
                    {
                        ["step"] = "Fork",
                        ["childCount"] = childCount.ToString(CultureInfo.InvariantCulture),
                    }));
        }

        public static void BypassStart(string step = null)
        {
            Record(
                PerfTraceKinds.DirectorBypassStart,
                PerfTraceSites.DirectorBypass,
                -1,
                BasePayload(
                    sActiveBatchId,
                    lane: LaneBypass,
                    extra: new Dictionary<string, string>
                    {
                        ["step"] = step ?? string.Empty,
                    }));
        }

        /// <summary>FX/音效脉冲：发即完成；degraded=关闭或 sink 失败。</summary>
        public static void TriggerPulse(string triggerId, string channel, bool degraded)
        {
            Record(
                PerfTraceKinds.DirectorTriggerPulse,
                PerfTraceSites.DirectorTimeline,
                -1,
                BasePayload(
                    sActiveBatchId,
                    extra: new Dictionary<string, string>
                    {
                        ["triggerId"] = triggerId ?? string.Empty,
                        ["channel"] = channel ?? string.Empty,
                        ["degraded"] = degraded ? "1" : "0",
                    }));
        }

        /// <summary>BattleTimeline 诊断适配：换步写入 PerfLog 供回放。</summary>
        public static readonly ITimelineDiagnosticSink TimelineSink = new TimelineDiagnosticAdapter();

        private sealed class TimelineDiagnosticAdapter : ITimelineDiagnosticSink
        {
            public void StepEnter(string step, string lane)
            {
                DirectorTrace.StepEnter(step, lane ?? LaneMainline);
            }

            public void StepExit(string step, string lane)
            {
                DirectorTrace.StepExit(step, lane ?? LaneMainline);
            }
        }

        private static Dictionary<string, string> IntentPayload(string intentKind, int targetId)
        {
            var payload = BasePayload(0);
            payload["intentKind"] = intentKind ?? string.Empty;
            payload["intentSlot"] = targetId.ToString(CultureInfo.InvariantCulture);
            return payload;
        }

        private static Dictionary<string, string> BasePayload(
            int batchId,
            string lane = LaneMainline,
            Dictionary<string, string> extra = null)
        {
            var payload = extra != null
                ? new Dictionary<string, string>(extra)
                : new Dictionary<string, string>();
            payload["path"] = PathDirector;
            payload["lane"] = lane ?? LaneMainline;
            if (batchId > 0)
            {
                payload["batchId"] = batchId.ToString(CultureInfo.InvariantCulture);
            }

            return payload;
        }

        private static void Record(
            string kind,
            string site,
            int uid,
            Dictionary<string, string> payload)
        {
            try
            {
                PerfTraceRecorder.Record(kind, uid, site, payload);
            }
            catch (Exception)
            {
                // swallow — 与其他 Diag sink 一致
            }
        }
    }
}
