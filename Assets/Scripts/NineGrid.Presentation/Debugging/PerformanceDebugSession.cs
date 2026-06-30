namespace NineGrid.Presentation.Debugging
{
    /// <summary>
    /// Play Mode 会话定位：Editor 窗口通过此处获取 Bootstrap，无需每次 FindObject。
    /// </summary>
    public static class PerformanceDebugSession
    {
        public static PerformanceDebugBootstrap Current { get; private set; }

        public static bool IsConnected => Current != null;

        internal static void Register(PerformanceDebugBootstrap bootstrap)
        {
            Current = bootstrap;
        }

        internal static void Unregister(PerformanceDebugBootstrap bootstrap)
        {
            if (Current == bootstrap)
            {
                Current = null;
            }
        }
    }
}
