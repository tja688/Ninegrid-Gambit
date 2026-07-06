#if UNITY_EDITOR || DEVELOPMENT_BUILD

namespace NineGrid.DevTest
{
    /// <summary>
    /// 开发测试程序集编译门闩。所有 DevTest API 仅在 Editor 或 Development Build 下可用。
    /// </summary>
    internal static class DevTestCompileGate
    {
        public const bool IsEnabled = true;
    }
}

#endif
