using System;
using NineGrid.Core.Systems;

namespace NineGrid.Flow.TavernBoard
{
    /// <summary>
    /// 卡店占格：3 服务选项按 defId 家格 + 刷新 + 离开；二级选择占剩余空格（#93）。Avatar 落格 5。
    /// </summary>
    public static class TavernBoardSlotResolver
    {
        public static readonly int[] ServiceSlots = { 1, 3, 7 };
        public static readonly int[] CandidatePreferredSlots = { 1, 3, 4 };
        public static readonly int[] CandidateFallbackPool = { 1, 3, 4, 6, 7, 9 };
        public const int RefreshSlot = 2;
        public const int LeaveSlot = 8;
        public const int AvatarSlot = 5;

        public const string RefreshContentId = "RefreshShop";
        public const string LeaveContentId = "Leave";

        /// <summary>服务选项家格（按 defId，购后留洞不换格）。</summary>
        public static int HomeSlotForService(string defId)
        {
            if (string.Equals(defId, RewardSystem.TavernUpgradeDefId, StringComparison.Ordinal))
            {
                return 1;
            }

            if (string.Equals(defId, RewardSystem.TavernFixItemDefId, StringComparison.Ordinal))
            {
                return 3;
            }

            if (string.Equals(defId, RewardSystem.TavernExpandDefId, StringComparison.Ordinal))
            {
                return 7;
            }

            return 0;
        }

        /// <summary>已废弃：请用 <see cref="HomeSlotForService"/> 按 defId 取家格。</summary>
        [Obsolete("Use HomeSlotForService(defId) for sticky home slots.")]
        public static int ServiceSlotAt(int serviceIndex)
        {
            if (serviceIndex < 0 || serviceIndex >= ServiceSlots.Length)
            {
                return 0;
            }

            return ServiceSlots[serviceIndex];
        }

        public static int CandidateSlotAt(int candidateIndex)
        {
            if (candidateIndex < 0 || candidateIndex >= CandidatePreferredSlots.Length)
            {
                return 0;
            }

            return CandidatePreferredSlots[candidateIndex];
        }
    }
}
