using NineGrid.Core;
using NineGrid.Flow;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.BattleSession
{
    /// <summary>
    /// 回归：清关后 RoomChoice / Navigation 必须能唤醒主循环刷场地图标（#83 / ADR-0021）。
    /// </summary>
    public sealed class NodeSettlementReadinessTests
    {
        [Test]
        public void RoomChoice_WithRoomOptions_IsReady()
        {
            Assert.IsTrue(NodeSettlementReadiness.IsReady(
                GamePhase.RoomChoice,
                PendingChoiceKind.Room,
                roomOptionCount: 2,
                NavigationKind.None,
                rewardOptionCount: 0));
        }

        [Test]
        public void RoomChoice_WithLeaveNavigation_IsReady()
        {
            Assert.IsTrue(NodeSettlementReadiness.IsReady(
                GamePhase.RoomChoice,
                PendingChoiceKind.Navigation,
                roomOptionCount: 0,
                NavigationKind.Leave,
                rewardOptionCount: 0));
        }

        [Test]
        public void RoomChoice_WithEmptyPending_IsNotReady()
        {
            Assert.IsFalse(NodeSettlementReadiness.IsReady(
                GamePhase.RoomChoice,
                PendingChoiceKind.None,
                roomOptionCount: 0,
                NavigationKind.None,
                rewardOptionCount: 0));
        }

        [Test]
        public void InteractionLoop_EvenWithRoomOptions_IsNotReady()
        {
            Assert.IsFalse(
                NodeSettlementReadiness.IsReady(
                    GamePhase.InteractionLoop,
                    PendingChoiceKind.Room,
                    roomOptionCount: 2,
                    NavigationKind.None,
                    rewardOptionCount: 0),
                "InteractionLoop 不得误唤醒（局内宝箱仍在此相位）");
        }

        [Test]
        public void RewardItemChoice_WithRewardOptions_IsReady()
        {
            Assert.IsTrue(NodeSettlementReadiness.IsReady(
                GamePhase.RewardItemChoice,
                PendingChoiceKind.Reward,
                roomOptionCount: 0,
                NavigationKind.None,
                rewardOptionCount: 3));
        }

        [Test]
        public void IsPostClearPhase_IncludesRoomChoice()
        {
            Assert.IsTrue(NodeSettlementReadiness.IsPostClearPhase(GamePhase.RoomChoice, false));
            Assert.IsTrue(NodeSettlementReadiness.IsPostClearPhase(GamePhase.InteractionLoop, true));
            Assert.IsFalse(NodeSettlementReadiness.IsPostClearPhase(GamePhase.InteractionLoop, false));
        }
    }
}
