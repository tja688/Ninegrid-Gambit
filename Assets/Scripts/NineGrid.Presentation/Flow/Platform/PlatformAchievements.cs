namespace NineGrid.Flow.Platform
{
    /// <summary>
    /// 平台成就 / 统计后端（Steam 等）。生产实现是 Steamworks 桥
    /// （<c>NineGrid.SteamBridge</c>，见 <c>Assets/Scripts/NineGrid.SteamBridge/</c>），
    /// 经 <see cref="PlatformAchievementsHook"/> 装配缝注册；
    /// Steam 客户端未运行 / 初始化失败时后端缺位，业务经
    /// <see cref="PlatformAchievements"/> 门面调用时静默跳过，游戏行为不受影响。
    /// </summary>
    public interface IPlatformAchievements
    {
        /// <summary>解锁成就。id 必须与 Steamworks 后台配置的 API Name 一致（见 <see cref="AchievementIds"/>）。</summary>
        void Unlock(string achievementId);

        bool IsUnlocked(string achievementId);

        void SetStat(string statId, int value);

        void AddStat(string statId, int delta);

        int GetStat(string statId);

        /// <summary>把本地缓存的成就 / 统计变更推送给平台（Steam StoreStats）。</summary>
        void Flush();

        /// <summary>Dev 专用：清空当前账号在本 App 下的全部成就与统计。</summary>
        void ResetAllForDev();
    }

    /// <summary>
    /// 装配缝（对齐 <c>RunSaveStoreHook</c> 范式）：桥程序集在 RuntimeInitializeOnLoad 时注册后端。
    /// 不是业务 Sink——只承载「谁来记成就」的装配，不读写规则状态。
    /// </summary>
    public static class PlatformAchievementsHook
    {
        private static IPlatformAchievements sBackend;

        public static void Set(IPlatformAchievements backend)
        {
            sBackend = backend;
        }

        public static IPlatformAchievements BackendOrNull()
        {
            return sBackend;
        }
    }

    /// <summary>业务调用门面：后端缺位时安全 no-op。成就 / 统计触发点直接调这里。</summary>
    public static class PlatformAchievements
    {
        public static bool IsAvailable => PlatformAchievementsHook.BackendOrNull() != null;

        public static void Unlock(string achievementId)
        {
            PlatformAchievementsHook.BackendOrNull()?.Unlock(achievementId);
        }

        public static bool IsUnlocked(string achievementId)
        {
            var backend = PlatformAchievementsHook.BackendOrNull();
            return backend != null && backend.IsUnlocked(achievementId);
        }

        public static void SetStat(string statId, int value)
        {
            PlatformAchievementsHook.BackendOrNull()?.SetStat(statId, value);
        }

        public static void AddStat(string statId, int delta = 1)
        {
            PlatformAchievementsHook.BackendOrNull()?.AddStat(statId, delta);
        }

        public static int GetStat(string statId)
        {
            var backend = PlatformAchievementsHook.BackendOrNull();
            return backend != null ? backend.GetStat(statId) : 0;
        }

        public static void Flush()
        {
            PlatformAchievementsHook.BackendOrNull()?.Flush();
        }
    }
}
