using UnityEngine;

namespace NineGrid.Presentation.Diagnostics
{
    [CreateAssetMenu(
        fileName = "PresentationTraceConfig",
        menuName = "NineGrid/Presentation Trace Config")]
    public sealed class PresentationTraceConfig : ScriptableObject
    {
        [Header("Channels")]
        public PresentationTraceChannel EnabledChannels = PresentationTraceChannel.Default;

        [Header("Levels")]
        public bool EnableTrace = true;
        public bool EnableInfo = true;
        public bool EnableWarn = true;
        public bool EnableError = true;
        public bool EnableStall = true;

        [Header("Buffer")]
        [Min(64)] public int RingBufferCapacity = 512;
        public bool WriteDumpOnStall = true;

        [Header("Watchdog Thresholds (seconds)")]
        [Min(1f)] public float InputLockStallSeconds = 12f;
        [Min(0.01f)] public float OrphanBatchFrameSeconds = 0.05f;
        [Min(1f)] public float AwaitingDismissStallSeconds = 10f;
        [Min(1f)] public float AutoStartStallSeconds = 20f;
        [Min(1f)] public float OverlayBusyWarnSeconds = 15f;
        [Min(0.5f)] public float NodeAutoWaitLogIntervalSeconds = 2f;

        [Header("Runtime HUD")]
        public bool ShowRuntimeHud;

        public bool IsChannelEnabled(PresentationTraceChannel channel)
        {
            return (EnabledChannels & channel) != 0;
        }

        public bool IsLevelEnabled(PresentationTraceLevel level)
        {
            switch (level)
            {
                case PresentationTraceLevel.Trace:
                    return EnableTrace;
                case PresentationTraceLevel.Info:
                    return EnableInfo;
                case PresentationTraceLevel.Warn:
                    return EnableWarn;
                case PresentationTraceLevel.Error:
                    return EnableError;
                case PresentationTraceLevel.Stall:
                    return EnableStall;
                default:
                    return true;
            }
        }
    }
}
