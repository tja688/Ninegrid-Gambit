using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #43 批次2：Opening 门禁迁入 PresentationInputStateSystem。
    /// </summary>
    public sealed class PresentationInputStateOpeningGateTests
    {
        [TearDown]
        public void TearDown()
        {
            PresentationInputGates.Reset("test-teardown");
        }

        [Test]
        public void ResetGates_ClearsOpeningPresentationActive()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);
                input.SetOpeningPresentationActive(true);

                input.ResetGates("test");

                Assert.IsFalse(input.OpeningPresentationActive.Value);
            }
        }

        [Test]
        public void OpeningPresentationActive_DefaultsFalse()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);
                Assert.IsFalse(input.OpeningPresentationActive.Value);
            }
        }
    }
}
