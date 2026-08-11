using NineGrid.Flow;
using NineGrid.Presentation.Systems;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>#199 HUD 金币数字时间窗：首达前不推进、末达精确收敛。</summary>
    public sealed class GoldHudNumberWindowTests
    {
        [Test]
        public void SampleDisplayed_BeforeFirstArrival_KeepsAmountBefore()
        {
            var plan = new VfxPresentationPlan(0.4f, 1.2f);
            Assert.AreEqual(
                10,
                GoldHudNumberWindow.SampleDisplayed(10, 25, plan.FirstArrivalDelay, plan.LastArrivalDelay, 0f));
            Assert.AreEqual(
                10,
                GoldHudNumberWindow.SampleDisplayed(10, 25, plan.FirstArrivalDelay, plan.LastArrivalDelay, 0.399f));
        }

        [Test]
        public void SampleDisplayed_AtLastArrival_EqualsAmountAfterExactly()
        {
            var plan = new VfxPresentationPlan(0.4f, 1.2f);
            Assert.AreEqual(
                25,
                GoldHudNumberWindow.SampleDisplayed(10, 25, plan.FirstArrivalDelay, plan.LastArrivalDelay, 1.2f));
            Assert.AreEqual(
                25,
                GoldHudNumberWindow.SampleDisplayed(10, 25, plan.FirstArrivalDelay, plan.LastArrivalDelay, 2f));
        }

        [Test]
        public void SampleDisplayed_DuringWindow_MovesTowardAmountAfter()
        {
            var plan = new VfxPresentationPlan(0.5f, 1.5f);
            var mid = GoldHudNumberWindow.SampleDisplayed(
                0,
                100,
                plan.FirstArrivalDelay,
                plan.LastArrivalDelay,
                1.0f);
            Assert.Greater(mid, 0);
            Assert.Less(mid, 100);
        }

        [Test]
        public void SampleDisplayed_InvalidWindow_SnapsToAmountAfter()
        {
            Assert.AreEqual(42, GoldHudNumberWindow.SampleDisplayed(7, 42, -1f, -1f, 0f));
            Assert.AreEqual(42, GoldHudNumberWindow.SampleDisplayed(7, 42, 0f, 1f, 0.5f));
        }
    }
}
