#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

#if !DISABLESTEAMWORKS
using Steamworks;
using UnityEngine;

namespace NineGrid.SteamBridge
{
    /// <summary>
    /// 常驻回调泵：每帧 <c>SteamAPI.RunCallbacks</c>（成就通知、Overlay、云回调都靠它派发），
    /// 应用退出时收口 <c>SteamAPI.Shutdown</c>。由 <see cref="SteamPlatformBootstrap"/> 创建，场景无需摆放。
    /// </summary>
    internal sealed class SteamCallbackPump : MonoBehaviour
    {
        private static SteamCallbackPump sInstance;

        public static void Ensure()
        {
            if (sInstance != null)
            {
                return;
            }

            var go = new GameObject("[SteamCallbackPump]");
            DontDestroyOnLoad(go);
            sInstance = go.AddComponent<SteamCallbackPump>();
        }

        private void Update()
        {
            SteamAPI.RunCallbacks();
        }

        private void OnApplicationQuit()
        {
            sInstance = null;
            SteamPlatformBootstrap.ShutdownFromPump();
        }
    }
}
#endif
