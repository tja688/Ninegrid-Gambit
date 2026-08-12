namespace NineGrid.Flow.Platform
{
    /// <summary>
    /// 平台环境信息与 Rich Presence（Steam 等）。生产实现是 Steamworks 桥，
    /// 经 <see cref="PlatformInfoHook"/> 装配缝注册；后端缺位时经
    /// <see cref="PlatformInfo"/> 门面读取会拿到安全默认值。
    /// </summary>
    public interface IPlatformInfo
    {
        /// <summary>玩家在平台上的显示名（Steam persona name）。</summary>
        string PlayerDisplayName { get; }

        /// <summary>平台语言代码（Steam API language name，如 "schinese" / "english"），可用于默认本地化。</summary>
        string LanguageCode { get; }

        ulong PlayerId { get; }

        bool IsOverlayEnabled { get; }

        /// <summary>设置 Rich Presence 键值（好友列表状态展示；需在 Steamworks 后台配置本地化 token 后才有显示效果）。</summary>
        void SetRichPresence(string key, string value);

        void ClearRichPresence();
    }

    /// <summary>装配缝（对齐 <c>RunSaveStoreHook</c> 范式）：只承载装配，不读写规则状态。</summary>
    public static class PlatformInfoHook
    {
        private static IPlatformInfo sBackend;

        public static void Set(IPlatformInfo backend)
        {
            sBackend = backend;
        }

        public static IPlatformInfo BackendOrNull()
        {
            return sBackend;
        }
    }

    /// <summary>业务调用门面：后端缺位时返回安全默认值 / no-op。</summary>
    public static class PlatformInfo
    {
        public static bool IsAvailable => PlatformInfoHook.BackendOrNull() != null;

        public static string PlayerDisplayName => PlatformInfoHook.BackendOrNull()?.PlayerDisplayName ?? string.Empty;

        public static string LanguageCode => PlatformInfoHook.BackendOrNull()?.LanguageCode ?? string.Empty;

        public static void SetRichPresence(string key, string value)
        {
            PlatformInfoHook.BackendOrNull()?.SetRichPresence(key, value);
        }

        public static void ClearRichPresence()
        {
            PlatformInfoHook.BackendOrNull()?.ClearRichPresence();
        }
    }
}
