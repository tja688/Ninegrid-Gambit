using System;
using System.Collections.Generic;
using NineGrid.Flow.Diagnostics;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// #10 占格强制对账退场门：正常路径永不触发；触发即记诊断并禁止静默修补。
    /// </summary>
    public static class OccupancyForceSyncGuard
    {
        public const string AnomalyCode = "OccupancyForceSyncAssert";

        public static int InvocationCount { get; private set; }

        public static string LastReason { get; private set; }

        public static string LastDetail { get; private set; }

        public static void ResetForTests()
        {
            InvocationCount = 0;
            LastReason = null;
            LastDetail = null;
        }

        /// <summary>
        /// 记录一次被禁止的对账请求并留下诊断。永远返回 false（调用方不得继续 heal）。
        /// </summary>
        public static bool RecordForbiddenSync(string reason, string detail = null)
        {
            InvocationCount++;
            LastReason = reason ?? string.Empty;
            LastDetail = detail ?? string.Empty;

            try
            {
                var payload = new Dictionary<string, string>
                {
                    ["anomaly"] = AnomalyCode,
                    ["reason"] = LastReason,
                    ["detail"] = LastDetail,
                    ["path"] = DirectorTrace.PathDirector,
                };
                PerfTraceRecorder.Record(
                    PerfTraceKinds.DirectorScriptAborted,
                    -1,
                    PerfTraceSites.DirectorBatchGate,
                    payload);
            }
            catch (Exception)
            {
                // swallow — 与其他 Diag sink 一致
            }

            return false;
        }
    }
}
