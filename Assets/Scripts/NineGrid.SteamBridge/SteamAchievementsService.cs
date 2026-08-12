#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

#if !DISABLESTEAMWORKS
using NineGrid.Flow.Platform;
using Steamworks;
using UnityEngine;

namespace NineGrid.SteamBridge
{
    /// <summary>
    /// 成就 / 统计的 Steam 后端（ISteamUserStats）。
    /// SDK 1.61 起当前用户统计随 Init 自动拉取，无需 RequestCurrentStats。
    /// 成就 API Name 未在后台定义时 SetAchievement 返回 false，只打 Warning 不打断业务。
    /// </summary>
    internal sealed class SteamAchievementsService : IPlatformAchievements
    {
        public void Unlock(string achievementId)
        {
            if (SteamUserStats.GetAchievement(achievementId, out var achieved) && achieved)
            {
                return;
            }

            if (!SteamUserStats.SetAchievement(achievementId))
            {
                Debug.LogWarning("[Steam] 解锁成就失败（后台未定义该 API Name？）：" + achievementId);
                return;
            }

            SteamUserStats.StoreStats();
        }

        public bool IsUnlocked(string achievementId)
        {
            return SteamUserStats.GetAchievement(achievementId, out var achieved) && achieved;
        }

        public void SetStat(string statId, int value)
        {
            if (!SteamUserStats.SetStat(statId, value))
            {
                Debug.LogWarning("[Steam] 写统计失败（后台未定义该 API Name？）：" + statId);
            }
        }

        public void AddStat(string statId, int delta)
        {
            SteamUserStats.GetStat(statId, out int current);
            SetStat(statId, current + delta);
        }

        public int GetStat(string statId)
        {
            SteamUserStats.GetStat(statId, out int value);
            return value;
        }

        public void Flush()
        {
            SteamUserStats.StoreStats();
        }

        public void ResetAllForDev()
        {
            SteamUserStats.ResetAllStats(true);
            SteamUserStats.StoreStats();
            Debug.Log("[Steam] 已重置当前账号在本 App 下的全部成就与统计。");
        }
    }
}
#endif
