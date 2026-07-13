using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// Cards → Flow RegistryTrace 旁路。由 Flow <c>RegistryTraceRecorder</c> 注册。
    /// Cards 不可引用 Flow，故用静态 Action 解耦。
    /// </summary>
    public static class RegistryTraceSink
    {
        public static Action<string> NotifyUserInteraction;

        public static Action<int, string, string, int> RecordSuspectGroundRelease;

        /// <summary>Pickup 点击前 eligibility 审计（参数见 RegistryTraceRecorder.RecordPickupEligibility）。</summary>
        public static Action<
            int,
            string,
            string,
            string,
            float,
            float,
            int,
            int,
            float,
            bool,
            bool,
            bool,
            bool,
            bool,
            bool> RecordPickupEligibility;

        public static void ClearHandlers()
        {
            NotifyUserInteraction = null;
            RecordSuspectGroundRelease = null;
            RecordPickupEligibility = null;
        }
    }
}
