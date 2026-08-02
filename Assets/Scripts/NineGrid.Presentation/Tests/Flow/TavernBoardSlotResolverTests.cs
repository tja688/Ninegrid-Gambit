using NineGrid.Flow.TavernBoard;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Flow
{
    public sealed class TavernBoardSlotResolverTests
    {
        [Test]
        public void ServiceSlots_AreThreeCorners_AroundAvatar()
        {
            CollectionAssert.AreEqual(new[] { 1, 3, 7 }, TavernBoardSlotResolver.ServiceSlots);
            Assert.AreEqual(5, TavernBoardSlotResolver.AvatarSlot);
            Assert.AreEqual(2, TavernBoardSlotResolver.RefreshSlot);
            Assert.AreEqual(8, TavernBoardSlotResolver.LeaveSlot);
        }

        [Test]
        public void CandidateSlots_CoverEmptyBoardExceptAvatarRefreshLeave()
        {
            CollectionAssert.AreEqual(new[] { 1, 3, 4, 6, 7, 9 }, TavernBoardSlotResolver.CandidateSlots);
            Assert.AreEqual(0, TavernBoardSlotResolver.CandidateSlotAt(-1));
            Assert.AreEqual(0, TavernBoardSlotResolver.CandidateSlotAt(6));
            Assert.AreEqual(1, TavernBoardSlotResolver.CandidateSlotAt(0));
        }

        [Test]
        public void ServiceSlotAt_OutOfRange_ReturnsZero()
        {
            Assert.AreEqual(0, TavernBoardSlotResolver.ServiceSlotAt(-1));
            Assert.AreEqual(0, TavernBoardSlotResolver.ServiceSlotAt(3));
            Assert.AreEqual(1, TavernBoardSlotResolver.ServiceSlotAt(0));
        }
    }
}
