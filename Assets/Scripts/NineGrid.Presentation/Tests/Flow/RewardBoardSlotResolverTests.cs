using NineGrid.Flow.RewardBoard;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Flow
{
    public sealed class RewardBoardSlotResolverTests
    {
        [Test]
        public void ShelfSlots_HoldFiveAroundAvatar_LeaveAtEight()
        {
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 7, 9 }, RewardBoardSlotResolver.ShelfSlots);
            Assert.AreEqual(5, RewardBoardSlotResolver.AvatarSlot);
            Assert.AreEqual(8, RewardBoardSlotResolver.LeaveSlot);
        }

        [Test]
        public void ShelfSlotAt_OutOfRange_ReturnsZero()
        {
            Assert.AreEqual(0, RewardBoardSlotResolver.ShelfSlotAt(-1));
            Assert.AreEqual(0, RewardBoardSlotResolver.ShelfSlotAt(5));
            Assert.AreEqual(1, RewardBoardSlotResolver.ShelfSlotAt(0));
            Assert.AreEqual(9, RewardBoardSlotResolver.ShelfSlotAt(4));
        }
    }
}
