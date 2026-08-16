namespace NineGrid.SteamBridge
{
    /// <summary>
    /// Steam AppId 常量。当前为 Valve 官方开发测试用 App「Spacewar」(480)。
    /// 注册 Steamworks 开发者并拿到正式 AppId 后需要改两处：
    /// 1. 本文件的 <see cref="Current"/>；
    /// 2. 仓库根目录 <c>steam_appid.txt</c>（仅开发期生效，不随 Build 发行）。
    /// </summary>
    public static class SteamAppIds
    {
        /// <summary>Valve 官方开发测试 App「Spacewar」，未拿到正式 AppId 前占位。</summary>
        public const uint SpacewarPlaceholder = 480;

        public const uint Current = SpacewarPlaceholder;

        /// <summary>仍为占位 AppId 时视为未上架 Steam，Player 直启 exe 不走发行校验。</summary>
        public static bool IsPreLaunchPlaceholder => Current == SpacewarPlaceholder;
    }
}
