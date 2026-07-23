using NineGrid.Cards;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #49：输入所有权轴只读投影（CurrentOwner）。
    /// </summary>
    public sealed class PresentationInputOwnershipTests
    {
        [TearDown]
        public void TearDown()
        {
            BoardCardSelectModeController.End();
            PresentationInputGates.Reset("test-teardown");
        }

        [Test]
        public void CurrentOwner_DefaultsToProtectedField()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);
                Assert.AreEqual(InputOwner.ProtectedField, input.CurrentOwner);
            }
        }

        [Test]
        public void SetOpening_ProjectsOwnerOpening()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);
                input.SetOpeningPresentationActive(true);
                Assert.AreEqual(InputOwner.Opening, input.CurrentOwner);
            }
        }

        [Test]
        public void SetChoiceOverlay_ProjectsOwnerChoiceOverlay()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);
                input.SetChoiceOverlayActive(true);
                Assert.AreEqual(InputOwner.ChoiceOverlay, input.CurrentOwner);
            }
        }

        [Test]
        public void SetBoardSelect_ProjectsOwnerBoardSelect()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);
                input.SetBoardSelectModeActive(true);
                Assert.AreEqual(InputOwner.BoardSelect, input.CurrentOwner);
            }
        }

        [Test]
        public void CurrentOwner_Priority_ChoiceOverlayBeatsOpeningAndBoardSelect()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);
                input.SetOpeningPresentationActive(true);
                input.SetBoardSelectModeActive(true);
                input.SetChoiceOverlayActive(true);

                Assert.AreEqual(InputOwner.ChoiceOverlay, input.CurrentOwner);

                input.SetChoiceOverlayActive(false);
                Assert.AreEqual(InputOwner.Opening, input.CurrentOwner);

                input.SetOpeningPresentationActive(false);
                Assert.AreEqual(InputOwner.BoardSelect, input.CurrentOwner);
            }
        }

        [Test]
        public void ResetGates_RestoresProtectedField()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);
                input.SetOpeningPresentationActive(true);
                input.SetChoiceOverlayActive(true);
                input.SetBoardSelectModeActive(true);

                input.ResetGates("test");

                Assert.AreEqual(InputOwner.ProtectedField, input.CurrentOwner);
                Assert.IsFalse(input.OpeningPresentationActive.Value);
                Assert.IsFalse(input.ChoiceOverlayActive.Value);
                Assert.IsFalse(input.BoardSelectModeActive.Value);
            }
        }

        [Test]
        public void ResetGates_ClearsStickyBoardSelectStaticSession()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);
                Assert.IsTrue(BoardCardSelectModeController.Begin(101, "help.test_multi", 2));
                Assert.AreEqual(InputOwner.BoardSelect, input.CurrentOwner);

                input.ResetGates("sticky-board-select");

                Assert.IsFalse(BoardCardSelectModeController.IsActive);
                Assert.IsFalse(input.BoardSelectModeActive.Value);
                Assert.AreEqual(InputOwner.ProtectedField, input.CurrentOwner);
            }
        }

        [Test]
        public void RequestAbort_ClearsBoardSelectGate_RestoresProtectedField()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                PresentationInputStateSystem.EnsureRegistered(arch.Architecture);
                Assert.IsTrue(BoardCardSelectModeController.Begin(202, "help.test_multi", 2));
                Assert.IsTrue(PresentationInputGates.BoardSelectModeActive);

                // 无 SelectionAbortedAsync 订阅时 End 同步执行。
                BoardCardSelectModeController.RequestAbort("test-abort");

                Assert.IsFalse(BoardCardSelectModeController.IsActive);
                Assert.IsFalse(PresentationInputGates.BoardSelectModeActive);
                Assert.AreEqual(InputOwner.ProtectedField, PresentationInputGates.CurrentOwner);
            }
        }

        [Test]
        public void Ownership_DoesNotImplyMainlineBusy()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);
                input.SetOpeningPresentationActive(true);
                input.SetChoiceOverlayActive(true);
                input.SetBoardSelectModeActive(true);

                Assert.IsFalse(input.MainlineBusy);
                Assert.IsFalse(input.HasExternalHold);
                Assert.AreEqual(InputOwner.ChoiceOverlay, input.CurrentOwner);
            }
        }
    }
}
