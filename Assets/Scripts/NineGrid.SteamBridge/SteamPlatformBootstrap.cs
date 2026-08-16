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
    /// Editor 下默认**不**初始化：须本机显式开启（EditorPrefs 开关 <c>EditorSteamEnabled</c>，
    /// 菜单 NineGrid/Steam/在本机 Editor 启用 Steam），避免未跑 Steam 的开发机进 Play 时
    /// 被拉起登录窗甚至崩溃。未上架 Steam 前（Development Build 或占位 AppId 的 Release 直发包）
    /// 整体跳过 Steam 自举，避免 RestartAppIfNecessary 秒退；注册正式 AppId 后 Release 经 Steam 启动走完整发行流程。
    /// </summary>
    public static class SteamPlatformBootstrap
    {
#if !DISABLESTEAMWORKS
        private static bool sInitialized;
        private static IRunSaveStore sInnerStore;

        public static bool IsInitialized => sInitialized;

#if UNITY_EDITOR
        /// <summary>
        /// Editor 下 Steam 自举的本机开关（EditorPrefs，按机器存储、不进版本库），默认关闭。
        /// 未跑 / 未登录 Steam 客户端的机器上，SteamAPI.Init 会拉起 Steam 引导进程弹登录窗，
        /// 个别环境还会让 Editor 原生崩溃——所以只有显式开启过本开关的开发机才尝试初始化。
        /// 开关入口：菜单 NineGrid/Steam/在本机 Editor 启用 Steam。
        /// </summary>
        internal const string EditorEnabledMenuPath = "NineGrid/Steam/在本机 Editor 启用 Steam";

        private const string EditorEnabledPrefKey = "NineGrid.Steam.EditorEnabled";

        internal static bool EditorSteamEnabled
        {
            get => UnityEditor.EditorPrefs.GetBool(EditorEnabledPrefKey, false);
            set => UnityEditor.EditorPrefs.SetBool(EditorEnabledPrefKey, value);
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (sInitialized)
            {
                return;
            }

#if UNITY_EDITOR
            if (!EditorSteamEnabled)
            {
                Debug.Log("[Steam] 本机 Editor 未启用 Steam（默认关闭），跳过初始化。"
                    + "需要验证 Steam 链路时用菜单 " + EditorEnabledMenuPath + "。");
                return;
            }
#endif

            // 临时措施（未上架 Steam 前）：Development Build 或仍为占位 AppId 的 Release 直发包
            // 均跳过 Steam 自举。Steamworks.NET 打包不拷 steam_appid.txt 到输出目录，
            // RestartAppIfNecessary 会拉起 Steam 登录验证并 Application.Quit 秒退。
            // 注册正式 AppId 并改 Current 后，仅经 Steam 客户端启动的 Release Build 走完整发行流程。
            if (!Application.isEditor && ShouldSkipSteamForPreLaunchDistribution())
            {
                Debug.Log("[Steam] 未上架 Steam 前的 Player 跳过 Steam 初始化（"
                    + (Debug.isDebugBuild ? "Development Build" : "占位 AppId")
                    + "；详见 docs/steam/README.md）。");
                return;
            }

            if (!Packsize.Test() || !DllCheck.Test())
            {
                Debug.LogError("[Steam] Steamworks.NET 结构体尺寸 / 原生库校验失败，跳过 Steam 初始化。");
                return;
            }

            try
            {
                // Editor 下先确认 Steam 客户端确实在运行：IsSteamRunning 只查本机状态、
                // 不会拉起客户端；直接 Init 则可能触发 Steam 引导进程（登录窗）甚至崩溃 Editor。
                if (Application.isEditor && !SteamAPI.IsSteamRunning())
                {
                    Debug.LogWarning("[Steam] Steam 客户端未运行，跳过初始化，以无 Steam 模式继续。");
                    return;
                }

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

        private static bool ShouldSkipSteamForPreLaunchDistribution()
        {
            return Debug.isDebugBuild || SteamAppIds.IsPreLaunchPlaceholder;
        }
#endif
    }
}
