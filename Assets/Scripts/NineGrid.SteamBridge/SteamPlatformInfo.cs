#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

#if !DISABLESTEAMWORKS
using NineGrid.Flow.Platform;
using Steamworks;

namespace NineGrid.SteamBridge
{
    /// <summary>平台环境信息 / Rich Presence 的 Steam 后端。</summary>
    internal sealed class SteamPlatformInfo : IPlatformInfo
    {
        public string PlayerDisplayName => SteamFriends.GetPersonaName();

        public string LanguageCode => SteamApps.GetCurrentGameLanguage();

        public ulong PlayerId => SteamUser.GetSteamID().m_SteamID;

        public bool IsOverlayEnabled => SteamUtils.IsOverlayEnabled();

        public void SetRichPresence(string key, string value)
        {
            SteamFriends.SetRichPresence(key, value);
        }

        public void ClearRichPresence()
        {
            SteamFriends.ClearRichPresence();
        }
    }
}
#endif
