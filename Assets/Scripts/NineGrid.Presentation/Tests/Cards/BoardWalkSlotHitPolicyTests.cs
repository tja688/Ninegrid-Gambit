using NineGrid.Cards;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class BoardWalkSlotHitPolicyTests
    {
        [Test]
        public void WalkEnabled_EmptyCorner_IsHittable()
        {
            Assert.IsTrue(BoardWalkSlotHitPolicy.ShouldEnableEmptySlotHit(
                isEmpty: true, slot: 7, walkEnabled: true));
            Assert.IsTrue(BoardWalkSlotHitPolicy.ShouldEnableEmptySlotHit(
                isEmpty: true, slot: 1, walkEnabled: true));
            Assert.IsTrue(BoardWalkSlotHitPolicy.ShouldEnableEmptySlotHit(
                isEmpty: true, slot: 3, walkEnabled: true));
            Assert.IsTrue(BoardWalkSlotHitPolicy.ShouldEnableEmptySlotHit(
                isEmpty: true, slot: 9, walkEnabled: true));
        }

        [Test]
        public void WalkEnabled_EmptyCenter_IsHittable()
        {
            Assert.IsTrue(BoardWalkSlotHitPolicy.ShouldEnableEmptySlotHit(
                isEmpty: true, slot: 5, walkEnabled: true));
        }

        [Test]
        public void WalkEnabled_Occupied_NotHittable()
        {
            Assert.IsFalse(BoardWalkSlotHitPolicy.ShouldEnableEmptySlotHit(
                isEmpty: false, slot: 7, walkEnabled: true));
        }

        [Test]
        public void WalkDisabled_OnlyOrthogonalToCenter_IsHittable()
        {
            Assert.IsTrue(BoardWalkSlotHitPolicy.ShouldEnableEmptySlotHit(
                isEmpty: true, slot: 2, walkEnabled: false));
            Assert.IsTrue(BoardWalkSlotHitPolicy.ShouldEnableEmptySlotHit(
                isEmpty: true, slot: 4, walkEnabled: false));
            Assert.IsTrue(BoardWalkSlotHitPolicy.ShouldEnableEmptySlotHit(
                isEmpty: true, slot: 6, walkEnabled: false));
            Assert.IsTrue(BoardWalkSlotHitPolicy.ShouldEnableEmptySlotHit(
                isEmpty: true, slot: 8, walkEnabled: false));

            Assert.IsFalse(BoardWalkSlotHitPolicy.ShouldEnableEmptySlotHit(
                isEmpty: true, slot: 1, walkEnabled: false));
            Assert.IsFalse(BoardWalkSlotHitPolicy.ShouldEnableEmptySlotHit(
                isEmpty: true, slot: 7, walkEnabled: false));
            Assert.IsFalse(BoardWalkSlotHitPolicy.ShouldEnableEmptySlotHit(
                isEmpty: true, slot: 5, walkEnabled: false));
        }
    }
}
