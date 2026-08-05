using NineGrid.Flow.AttributeBoard;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Flow
{
    public sealed class AttributeBoardSlotResolverTests
    {
        [Test]
        public void CandidateSlots_HoldThreeAroundAvatar_LeaveAtEight()
        {
            CollectionAssert.AreEqual(new[] { 1, 3, 7 }, AttributeBoardSlotResolver.CandidateSlots);
            Assert.AreEqual(5, AttributeBoardSlotResolver.AvatarSlot);
            Assert.AreEqual(8, AttributeBoardSlotResolver.LeaveSlot);
        }

        [Test]
        public void CandidateSlotAt_OutOfRange_ReturnsZero()
        {
            Assert.AreEqual(0, AttributeBoardSlotResolver.CandidateSlotAt(-1));
            Assert.AreEqual(0, AttributeBoardSlotResolver.CandidateSlotAt(3));
            Assert.AreEqual(1, AttributeBoardSlotResolver.CandidateSlotAt(0));
            Assert.AreEqual(7, AttributeBoardSlotResolver.CandidateSlotAt(2));
        }
    }
}
