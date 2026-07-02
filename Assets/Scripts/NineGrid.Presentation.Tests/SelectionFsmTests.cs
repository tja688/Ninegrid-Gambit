using NUnit.Framework;
using NineGrid.Presentation.Shell;

namespace NineGrid.Presentation.Tests
{
    public sealed class SelectionFsmTests
    {
        [Test]
        public void ChannelSwitch_ForceResetsState()
        {
            var fsm = new SelectionFsm();
            fsm.ActivateChannel(SelectionChannel.General);
            fsm.NotifyHover(0);
            Assert.AreEqual(SelectionState.Hover, fsm.State);

            fsm.ActivateChannel(SelectionChannel.RoomChoice);
            Assert.AreEqual(SelectionChannel.RoomChoice, fsm.ActiveChannel);
            Assert.AreEqual(SelectionState.Idle, fsm.State);
            Assert.AreEqual(-1, fsm.HoveredIndex);
        }

        [Test]
        public void Confirm_RaisesEvent_AndLocksUntilReset()
        {
            var fsm = new SelectionFsm();
            fsm.ActivateChannel(SelectionChannel.RoomChoice);

            int confirmed = -1;
            fsm.OptionConfirmed += index => confirmed = index;
            fsm.NotifyConfirm(1);

            Assert.AreEqual(1, confirmed);
            Assert.AreEqual(SelectionState.Confirming, fsm.State);

            fsm.ForceReset();
            Assert.AreEqual(SelectionState.Idle, fsm.State);
        }

        [Test]
        public void InputLocked_BlocksHoverAndConfirm()
        {
            var fsm = new SelectionFsm();
            fsm.ActivateChannel(SelectionChannel.General);
            fsm.InputLocked = true;

            int events = 0;
            fsm.OptionHovered += _ => events++;
            fsm.OptionConfirmed += _ => events++;

            fsm.NotifyHover(0);
            fsm.NotifyConfirm(0);

            Assert.AreEqual(0, events);
        }
    }
}
