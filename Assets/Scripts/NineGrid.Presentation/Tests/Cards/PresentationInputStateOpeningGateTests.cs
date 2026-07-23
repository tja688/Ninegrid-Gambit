using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #49：Opening 归入输入所有权轴（对齐既有 Opening 门禁 EditMode）。
    /// </summary>
    public sealed class PresentationInputStateOpeningGateTests
    {
        [TearDown]
        public void TearDown()
        {
            PresentationInputGates.Reset("test-teardown");
        }

        [Test]
        public void ResetGates_ClearsOpeningOwner()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);
                input.SetOpeningPresentationActive(true);
                Assert.AreEqual(InputOwner.Opening, input.CurrentOwner);

                input.ResetGates("test");

                Assert.AreEqual(InputOwner.ProtectedField, input.CurrentOwner);
                Assert.IsFalse(input.OpeningPresentationActive.Value);
            }
        }

        [Test]
        public void OpeningPresentationActive_DefaultsFalse_OwnerProtectedField()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);
                Assert.IsFalse(input.OpeningPresentationActive.Value);
                Assert.AreEqual(InputOwner.ProtectedField, input.CurrentOwner);
            }
        }
    }
}
