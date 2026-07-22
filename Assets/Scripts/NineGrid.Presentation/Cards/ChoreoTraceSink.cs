using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// Cards → Flow 场地编排诊断旁路。由 Flow <c>ChoreoTraceContext</c> 注册。
    /// </summary>
    public static class ChoreoTraceSink
    {
        public static Func<string, string[], int> BeginChoreo;
        public static Action<string, int, int, string[]> EndChoreo;
        public static Func<int> GetCurrentSeqId;
        public static Action<int, string, int, int, string[]> RecordExploreTrace;
        public static Action<string, string[]> RecordBusySnapshot;
        public static Action<string, int, string> EmitAnomaly;

        public static int SafeBeginChoreo(string kind, params string[] pairs)
        {
            try
            {
                return BeginChoreo?.Invoke(kind, pairs) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        public static void SafeEndChoreo(
            string outcome,
            int plannedAnim = 0,
            int actualAnim = 0,
            params string[] pairs)
        {
            try
            {
                EndChoreo?.Invoke(outcome, plannedAnim, actualAnim, pairs);
            }
            catch
            {
                // swallow
            }
        }

        public static int SafeCurrentSeqId()
        {
            try
            {
                return GetCurrentSeqId?.Invoke() ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        public static void SafeExploreTrace(
            int uid,
            string phase,
            int birthSlot,
            int trackedSlot,
            params string[] pairs)
        {
            try
            {
                RecordExploreTrace?.Invoke(uid, phase, birthSlot, trackedSlot, pairs);
            }
            catch
            {
                // swallow
            }
        }

        public static void SafeBusySnapshot(string trigger, params string[] pairs)
        {
            try
            {
                RecordBusySnapshot?.Invoke(trigger, pairs);
            }
            catch
            {
                // swallow
            }
        }

        public static void SafeEmitAnomaly(string code, int uid, string detail)
        {
            try
            {
                EmitAnomaly?.Invoke(code, uid, detail);
            }
            catch
            {
                // swallow
            }
        }

        public static void ClearHandlers()
        {
            BeginChoreo = null;
            EndChoreo = null;
            GetCurrentSeqId = null;
            RecordExploreTrace = null;
            RecordBusySnapshot = null;
            EmitAnomaly = null;
        }
    }
}
