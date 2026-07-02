using NineGrid.Core;
using NineGrid.Presentation.Shell;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class GamePhaseScreenMapperTests
    {
        [TestCase(GamePhase.InteractionLoop, MainFlowScreen.NodePlaying)]
        [TestCase(GamePhase.RewardItemChoice, MainFlowScreen.RewardScreen)]
        [TestCase(GamePhase.RoomChoice, MainFlowScreen.RoomChoiceScreen)]
        [TestCase(GamePhase.RoomEvent, MainFlowScreen.RoomEventScreen)]
        [TestCase(GamePhase.NodeCompleted, MainFlowScreen.NodeAdvance)]
        public void TryMap_MapsPlayablePhases(GamePhase phase, MainFlowScreen expectedScreen)
        {
            Assert.IsTrue(GamePhaseScreenMapper.TryMap(phase, out GamePhaseScreenMapping mapping));
            Assert.AreEqual(expectedScreen, mapping.Screen);
            Assert.IsFalse(mapping.AnnounceOutcome);
        }

        [Test]
        public void TryMap_Victory_AnnouncesOutcome()
        {
            Assert.IsTrue(GamePhaseScreenMapper.TryMap(GamePhase.Victory, out GamePhaseScreenMapping mapping));
            Assert.AreEqual(MainFlowScreen.Victory, mapping.Screen);
            Assert.IsTrue(mapping.AnnounceOutcome);
            Assert.AreEqual(RunOutcome.Victory, mapping.Outcome);
        }

        [Test]
        public void TryMap_Defeat_AnnouncesOutcome()
        {
            Assert.IsTrue(GamePhaseScreenMapper.TryMap(GamePhase.Defeat, out GamePhaseScreenMapping mapping));
            Assert.AreEqual(MainFlowScreen.Defeat, mapping.Screen);
            Assert.IsTrue(mapping.AnnounceOutcome);
            Assert.AreEqual(RunOutcome.Defeat, mapping.Outcome);
        }
    }
}
