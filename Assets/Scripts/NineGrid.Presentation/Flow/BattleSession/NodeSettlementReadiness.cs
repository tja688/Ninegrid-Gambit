using NineGrid.Core;
using NineGrid.Core.Systems;

namespace NineGrid.Flow
{
    /// <summary>
    /// 节点清关后是否应唤醒主循环（ADR-0021：清关直接 RoomChoice / Navigation，无 help.choice）。
    /// </summary>
    public static class NodeSettlementReadiness
    {
        public static bool IsReady(GamePhase phase, PendingChoiceModel pending)
        {
            if (pending == null)
            {
                return false;
            }

            return IsReady(
                phase,
                pending.Kind.Value,
                pending.RoomOptions != null ? pending.RoomOptions.Count : 0,
                pending.NavigationOffer.Value,
                pending.RewardOptions != null ? pending.RewardOptions.Count : 0);
        }

        public static bool IsReady(
            GamePhase phase,
            PendingChoiceKind kind,
            int roomOptionCount,
            NavigationKind navigationOffer,
            int rewardOptionCount)
        {
            // ADR-0021：清关即放出选房/导航图标。
            if (phase == GamePhase.RoomChoice)
            {
                if (kind == PendingChoiceKind.Room)
                {
                    return roomOptionCount > 0;
                }

                if (kind == PendingChoiceKind.Navigation)
                {
                    return navigationOffer != NavigationKind.None;
                }

                return false;
            }

            // 局内宝箱等仍走 RewardItemChoice；通关三选一已退役，保留判定防孤儿 Pending。
            return phase == GamePhase.RewardItemChoice
                && kind == PendingChoiceKind.Reward
                && rewardOptionCount > 0;
        }

        /// <summary>投影批是否已进入清关后相位（用于排期 Settlement）。</summary>
        public static bool IsPostClearPhase(GamePhase phase, bool isNodeCleared)
        {
            return phase == GamePhase.RoomChoice
                || phase == GamePhase.RewardItemChoice
                || phase == GamePhase.ClearCheck
                || phase == GamePhase.NodeCompleted
                || isNodeCleared;
        }
    }
}
