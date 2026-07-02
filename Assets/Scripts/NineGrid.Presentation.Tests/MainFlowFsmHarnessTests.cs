using NUnit.Framework;
using NineGrid.Presentation.Shell;

namespace NineGrid.Presentation.Tests
{
    public sealed class MainFlowFsmHarnessTests
    {
        [Test]
        public void StartRun_Chain_ReachesNodePlaying()
        {
            var fsm = new MainFlowFsm { IsHarnessMode = true };
            fsm.Enter(MainFlowScreen.MainMenu);

            Assert.IsTrue(fsm.RequestTransition(MainFlowTransition.StartRun));
            Assert.AreEqual(MainFlowScreen.RunSession, fsm.CurrentScreen);

            Assert.IsTrue(fsm.RequestTransition(MainFlowTransition.BeginNode));
            Assert.AreEqual(MainFlowScreen.NodePlaying, fsm.CurrentScreen);
        }

        [Test]
        public void NodeComplete_ToRoomEvent_ViaRewardAndRoomChoice()
        {
            var fsm = new MainFlowFsm();
            fsm.Enter(MainFlowScreen.NodePlaying);

            Assert.IsTrue(fsm.RequestTransition(MainFlowTransition.NodeComplete));
            Assert.AreEqual(MainFlowScreen.RewardScreen, fsm.CurrentScreen);

            Assert.IsTrue(fsm.RequestTransition(MainFlowTransition.ConfirmReward));
            Assert.AreEqual(MainFlowScreen.RoomChoiceScreen, fsm.CurrentScreen);

            Assert.IsTrue(fsm.RequestTransition(MainFlowTransition.RoomSelected));
            Assert.AreEqual(MainFlowScreen.RoomEventScreen, fsm.CurrentScreen);
        }

        [Test]
        public void RoomEventClick_AdvancesBackToNodePlaying()
        {
            var fsm = new MainFlowFsm();
            fsm.Enter(MainFlowScreen.RoomEventScreen);

            Assert.IsTrue(fsm.RequestTransition(MainFlowTransition.RoomEventClicked));
            Assert.AreEqual(MainFlowScreen.NodeAdvance, fsm.CurrentScreen);

            Assert.IsTrue(fsm.RequestTransition(MainFlowTransition.NodeAdvanceDone));
            Assert.AreEqual(MainFlowScreen.NodePlaying, fsm.CurrentScreen);
        }

        [Test]
        public void Outcome_ReturnsToMainMenu()
        {
            var fsm = new MainFlowFsm();
            fsm.Enter(MainFlowScreen.NodePlaying);

            fsm.ShowOutcome(RunOutcome.Victory);
            Assert.AreEqual(MainFlowScreen.Victory, fsm.CurrentScreen);

            Assert.IsTrue(fsm.RequestTransition(MainFlowTransition.ReturnToMainMenu));
            Assert.AreEqual(MainFlowScreen.MainMenu, fsm.CurrentScreen);
        }
    }
}
