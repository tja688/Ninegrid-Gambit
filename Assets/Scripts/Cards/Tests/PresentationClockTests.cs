using NUnit.Framework;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards.Tests
{
    public sealed class PresentationClockTests
    {
        [Test]
        public void Advance_AccumulatesWallTime()
        {
            var clock = new PresentationClock();
            Assert.AreEqual(0f, clock.Now);

            clock.Advance(0.1f);
            clock.Advance(0.25f);

            Assert.AreEqual(0.35f, clock.Now, 1e-5f);
        }

        [Test]
        public void Advance_NegativeDelta_TreatedAsZero()
        {
            var clock = new PresentationClock();
            clock.Advance(1f);
            clock.Advance(-0.5f);
            Assert.AreEqual(1f, clock.Now, 1e-5f);
        }

        [Test]
        public void SeekAndReset()
        {
            var clock = new PresentationClock();
            clock.Seek(3.5f);
            Assert.AreEqual(3.5f, clock.Now, 1e-5f);
            clock.Reset();
            Assert.AreEqual(0f, clock.Now);
        }
    }
}
