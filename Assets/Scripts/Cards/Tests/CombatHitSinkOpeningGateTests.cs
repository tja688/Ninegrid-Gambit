using NUnit.Framework;
using NineGrid.Cards;

namespace NineGrid.Cards.Tests
{
    public sealed class CombatHitSinkOpeningGateTests
    {
        [TearDown]
        public void TearDown()
        {
            CombatHitSink.ResetInputGates("test-teardown");
        }

        [Test]
        public void ResetInputGates_ClearsOpeningPresentationActive()
        {
            CombatHitSink.OpeningPresentationActive = true;

            CombatHitSink.ResetInputGates("test");

            Assert.IsFalse(CombatHitSink.OpeningPresentationActive);
        }

        [Test]
        public void OpeningPresentationActive_DefaultsFalse()
        {
            Assert.IsFalse(CombatHitSink.OpeningPresentationActive);
        }
    }
}
