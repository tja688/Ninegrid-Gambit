#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

#if UNITY_EDITOR && !DISABLESTEAMWORKS
using NineGrid.Flow.Platform;
using Steamworks;
using UnityEditor;
using UnityEngine;

namespace NineGrid.SteamBridge
{
    /// <summary>
    /// Editor 调试菜单（须 Play Mode + Steam 客户端运行）：验证初始化 / 成就 / 云存档链路。
    /// 测试成就用 Spacewar (480) 自带的 ACH_WIN_ONE_GAME，会真的在 Steam 弹通知。
    /// </summary>
    internal static class SteamDebugMenu
    {
        private const string MenuRoot = "NineGrid/Steam/";

        [MenuItem(SteamPlatformBootstrap.EditorEnabledMenuPath, priority = -100)]
        private static void ToggleEditorSteam()
        {
            var enabled = !SteamPlatformBootstrap.EditorSteamEnabled;
            SteamPlatformBootstrap.EditorSteamEnabled = enabled;
            Debug.Log(enabled
                ? "[Steam] 已在本机 Editor 启用 Steam：下次进 Play 生效（需 Steam 客户端已登录并运行）。"
                  + "开关存 EditorPrefs，只影响本机，不进版本库。"
                : "[Steam] 已在本机 Editor 关闭 Steam：下次进 Play 起不再初始化 SteamAPI。");
        }

        [MenuItem(SteamPlatformBootstrap.EditorEnabledMenuPath, true)]
        private static bool ToggleEditorSteamValidate()
        {
            Menu.SetChecked(SteamPlatformBootstrap.EditorEnabledMenuPath,
                SteamPlatformBootstrap.EditorSteamEnabled);
            return true;
        }

        [MenuItem(MenuRoot + "打印平台状态")]
        private static void PrintStatus()
        {
            if (!RequireInitialized())
            {
                return;
            }

            var cloudAccount = SteamRemoteStorage.IsCloudEnabledForAccount();
            var cloudApp = SteamRemoteStorage.IsCloudEnabledForApp();
            SteamRemoteStorage.GetQuota(out var total, out var available);
            Debug.Log("[Steam] 状态：玩家=" + PlatformInfo.PlayerDisplayName
                + " | 语言=" + PlatformInfo.LanguageCode
                + " | AppId=" + SteamAppIds.Current
                + " | 云(账号/App)=" + cloudAccount + "/" + cloudApp
                + " | 云配额=" + available + "/" + total + " bytes");
        }

        [MenuItem(MenuRoot + "解锁测试成就 (Spacewar ACH_WIN_ONE_GAME)")]
        private static void UnlockTestAchievement()
        {
            if (!RequireInitialized())
            {
                return;
            }

            PlatformAchievements.Unlock(AchievementIds.DevSpacewarWinOneGame);
            Debug.Log("[Steam] 已请求解锁测试成就 ACH_WIN_ONE_GAME（如首次解锁，Steam 会弹通知）。");
        }

        [MenuItem(MenuRoot + "重置全部成就与统计 (Dev)")]
        private static void ResetAll()
        {
            if (!RequireInitialized())
            {
                return;
            }

            PlatformAchievementsHook.BackendOrNull()?.ResetAllForDev();
        }

        [MenuItem(MenuRoot + "列出云存档文件")]
        private static void ListCloudFiles()
        {
            if (!RequireInitialized())
            {
                return;
            }

            var count = SteamRemoteStorage.GetFileCount();
            if (count == 0)
            {
                Debug.Log("[Steam] 云端暂无文件。");
                return;
            }

            for (var i = 0; i < count; i++)
            {
                var name = SteamRemoteStorage.GetFileNameAndSize(i, out var size);
                var ts = SteamRemoteStorage.GetFileTimestamp(name);
                Debug.Log("[Steam] 云文件 " + (i + 1) + "/" + count + "：" + name
                    + " (" + size + " bytes, ts=" + ts + ")");
            }
        }

        private static bool RequireInitialized()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Steam] 调试菜单须在 Play Mode 下使用（Steam 自举发生在进 Play 时）。");
                return false;
            }

            if (!SteamPlatformBootstrap.IsInitialized)
            {
                Debug.LogWarning(SteamPlatformBootstrap.EditorSteamEnabled
                    ? "[Steam] Steam 未初始化（客户端未运行或 Init 失败），无法执行。"
                    : "[Steam] 本机 Editor 未启用 Steam。先勾选菜单 "
                      + SteamPlatformBootstrap.EditorEnabledMenuPath + "，再进 Play。");
                return false;
            }

            return true;
        }
    }
}
#endif
