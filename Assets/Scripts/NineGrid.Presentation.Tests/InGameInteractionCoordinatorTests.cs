using NineGrid.Presentation.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class InGameInteractionCoordinatorTests
    {
        [Test]
        public void HandEngage_AndRelease_TogglesMode()
        {
            var go = new GameObject("InteractionCoordinatorTest");
            var coordinator = go.AddComponent<InGameInteractionCoordinator>();

            Assert.AreEqual(InGameInteractionMode.Board, coordinator.Mode);

            coordinator.NotifyHandEngaged();
            Assert.AreEqual(InGameInteractionMode.HandItem, coordinator.Mode);
            Assert.IsFalse(coordinator.IsBoardInputAllowed());
            Assert.IsTrue(coordinator.IsHandInputAllowed());

            coordinator.TryReleaseHandMode();
            Assert.AreEqual(InGameInteractionMode.Board, coordinator.Mode);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BoardFsm_InputLocked_BlocksStateChange()
        {
            var fsm = new BoardInteractionFsm();
            fsm.InputLocked = true;
            fsm.NotifyCardHover(1, null);

            Assert.AreEqual(BoardInteractionState.Idle, fsm.State);
            Assert.AreEqual(0, fsm.HoveredCardUid);
        }

        [Test]
        public void HandFsm_InputLocked_BlocksStateChange()
        {
            var fsm = new HandInteractionFsm();
            fsm.InputLocked = true;
            fsm.NotifyHover(1, null);

            Assert.AreEqual(HandInteractionState.Idle, fsm.State);
            Assert.AreEqual(0, fsm.HoveredItemUid);
        }
    }
}
