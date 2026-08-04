using System.Collections.Generic;
using NineGrid.Flow.Transitions;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Flow
{
    public sealed class DirectionalBiasPickerTests
    {
        [Test]
        public void FourDraws_CoverAllDirections_BeforeRepeat()
        {
            var picker = new DirectionalBiasPicker(seed: 42);
            var seen = new HashSet<DirectionalBiasPicker.Direction>();
            for (var i = 0; i < 4; i++)
            {
                Assert.AreEqual(4 - i, picker.RemainingInBag);
                seen.Add(picker.Next());
            }

            Assert.AreEqual(4, seen.Count);
            Assert.AreEqual(0, picker.RemainingInBag);
        }

        [Test]
        public void AfterBagEmpty_RefillsAndContinues()
        {
            var picker = new DirectionalBiasPicker(seed: 7);
            for (var i = 0; i < 4; i++)
            {
                picker.Next();
            }

            Assert.AreEqual(0, picker.RemainingInBag);
            var next = picker.Next();
            Assert.AreEqual(3, picker.RemainingInBag);
            Assert.IsTrue(System.Enum.IsDefined(typeof(DirectionalBiasPicker.Direction), next));
        }

        [Test]
        public void EightDraws_EachDirectionAppearsTwice()
        {
            var picker = new DirectionalBiasPicker(seed: 99);
            var counts = new Dictionary<DirectionalBiasPicker.Direction, int>();
            for (var i = 0; i < 8; i++)
            {
                var d = picker.Next();
                counts.TryGetValue(d, out var c);
                counts[d] = c + 1;
            }

            Assert.AreEqual(4, counts.Count);
            foreach (var pair in counts)
            {
                Assert.AreEqual(2, pair.Value, pair.Key.ToString());
            }
        }
    }
}
