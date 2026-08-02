using NineGrid.Flow.ShopBoard;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Flow
{
    public sealed class ShopBoardSlotResolverTests
    {
        [Test]
        public void ShelfSlots_AreFourCorners_AroundAvatar()
        {
            CollectionAssert.AreEqual(new[] { 1, 3, 7, 9 }, ShopBoardSlotResolver.ShelfSlots);
            Assert.AreEqual(5, ShopBoardSlotResolver.AvatarSlot);
            Assert.AreEqual(2, ShopBoardSlotResolver.RefreshSlot);
            Assert.AreEqual(8, ShopBoardSlotResolver.LeaveSlot);
        }

        [Test]
        public void ShelfSlotAt_OutOfRange_ReturnsZero()
        {
            Assert.AreEqual(0, ShopBoardSlotResolver.ShelfSlotAt(-1));
            Assert.AreEqual(0, ShopBoardSlotResolver.ShelfSlotAt(4));
            Assert.AreEqual(1, ShopBoardSlotResolver.ShelfSlotAt(0));
        }
    }
}
