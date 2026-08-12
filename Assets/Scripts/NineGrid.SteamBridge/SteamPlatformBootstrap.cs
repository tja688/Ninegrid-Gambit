// Steamworks.NET 同款平台守卫：非桌面平台下整个文件降级为空实现。
#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using UnityEngine;
#if !DISABLESTEAMWORKS
using System;
using NineGrid.Flow;
using NineGrid.Flow.Platform;
using Steamworks;
#endif

namespace NineGrid.SteamBridge
{
    /// <summary>
    /// Steam 平台自举：初始化 SteamAPI、注册成就 / 平台信息后端、
    /// 把跑图存档（<c>RunSaveStoreHook</c>）包一层云镜像，并挂常驻回调泵。
    /// Steam 客户端未运行 / 初始化失败时静默降级——所有 Hook 不注册，
    /// 游戏行为与无 Steam 完全一致（本地存档照常）。
    /// </summary>
    public static class SteamPlatformBootstrap
    {
#if !DISABLESTEAMWORKS
        private static bool sInitialized;
        private static IRunSaveStore sInnerStore;

        public static bool IsInitialized => sInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (sInitialized)
            {
                return;
            }

            if (!Packsize.Test() || !DllCheck.Test())
            {
                Debug.LogError("[Steam] Steamworks.NET 结构体尺寸 / 原生库校验失败，跳过 Steam 初始化。");
                return;
            }

            try
            {
                // 正式发行后：玩家绕过 Steam 直启 exe 时，这里会拉起 Steam 客户端并要求经它重启。
                // 开发期仓库根有 steam_appid.txt，恒返回 false；Editor 下永不触发退出。
                if (!Application.isEditor
                    && SteamAPI.RestartAppIfNecessary(new AppId_t(SteamAppIds.Current)))
                {
                    Application.Quit();
                    return;
                }

                if (!SteamAPI.Init())
                {
                    Debug.LogWarning(
                        "[Steam] SteamAPI.Init 失败（Steam 客户端未运行？），以无 Steam 模式继续，本地存档不受影响。");
                    return;
                }
            }
            catch (DllNotFoundException ex)
            {
                Debug.LogError("[Steam] steam_api 原生库缺失，以无 Steam 模式继续：" + ex.Message);
                return;
            }

            sInitialized = true;
            SteamCallbackPump.Ensure();

            PlatformAchievementsHook.Set(new SteamAchievementsService());
            PlatformInfoHook.Set(new SteamPlatformInfo());

            sInnerStore = RunSaveStoreHook.StoreOrNull();
            if (sInnerStore != null)
            {
                var cloudStore = new SteamCloudRunSaveStore(sInnerStore);
                cloudStore.SyncOnBoot();
                RunSaveStoreHook.Set(cloudStore);
            }

            Debug.Log("[Steam] 初始化完成：" + SteamFriends.GetPersonaName()
                + " (AppId " + SteamAppIds.Current + ")，云存档镜像 "
                + (sInnerStore != null ? "已启用" : "跳过（本地存档后端缺失）") + "。");
        }

        /// <summary>
        /// 由回调泵在应用退出（含 Editor 退出 Play）时调用。
        /// 显式还原 Hook，保证关闭 Domain Reload 的 Enter Play Mode 下二次进 Play 状态干净。
        /// </summary>
        internal static void ShutdownFromPump()
        {
            if (!sInitialized)
            {
                return;
            }

            sInitialized = false;
            PlatformAchievementsHook.Set(null);
            PlatformInfoHook.Set(null);
            if (sInnerStore != null)
            {
                RunSaveStoreHook.Set(sInnerStore);
                sInnerStore = null;
            }

            SteamAPI.Shutdown();
        }
#endif
    }
}
