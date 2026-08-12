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
        public const uint Current = 480;
    }
}
