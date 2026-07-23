using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.BehaviorBaseline
{
    /// <summary>
    /// #49：输入所有权轴 + ExternalHold / MainlineBusy 只读投影基线。
    /// </summary>
    public sealed class InputGateMatrixBaselineTests
    {
        [TearDown]
        public void TearDown()
        {
            PresentationInputGates.Reset("InputGateMatrixBaselineTests");
        }

        [Test]
        public void GateFlags_AreIndependent_UntilResetClearsAll()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(arch, new AcceptAllScriptFactory()))
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);

                input.SetOpeningPresentationActive(true);
                input.SetChoiceOverlayActive(true);
                input.SetBoardSelectModeActive(true);
                Assert.IsTrue(runtime.Runtime.TryBeginExternalHold("busy-mirror"));

                Assert.IsTrue(input.OpeningPresentationActive.Value);
                Assert.IsTrue(input.ChoiceOverlayActive.Value);
                Assert.IsTrue(input.BoardSelectModeActive.Value);
                Assert.AreEqual(InputOwner.ChoiceOverlay, input.CurrentOwner);
                Assert.IsTrue(input.MainlineBusy);

                input.SetOpeningPresentationActive(false);
                Assert.IsFalse(input.OpeningPresentationActive.Value);
                Assert.IsTrue(input.ChoiceOverlayActive.Value);
                Assert.IsTrue(input.BoardSelectModeActive.Value);
                Assert.AreEqual(InputOwner.ChoiceOverlay, input.CurrentOwner);

                input.ResetGates("matrix");
                runtime.Tick();
                Assert.AreEqual(InputOwner.ProtectedField, input.CurrentOwner);
                Assert.IsFalse(input.OpeningPresentationActive.Value);
                Assert.IsFalse(input.ChoiceOverlayActive.Value);
                Assert.IsFalse(input.BoardSelectModeActive.Value);
                Assert.IsFalse(input.MainlineBusy);
                Assert.IsFalse(input.HasExternalHold);
            }
        }

        [Test]
        public void ExternalHold_RejectsReentry_AndForceEndAlwaysReleases()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch,
                       new RecordingScriptFactory(continueTicks: 1)))
            {
                PresentationInputStateSystem.EnsureRegistered(arch.Architecture);

                Assert.IsTrue(PresentationInputGates.TryBeginExternalHold("pickup"));
                Assert.IsTrue(PresentationInputGates.HasExternalHold);
                Assert.IsFalse(PresentationInputGates.TryBeginExternalHold("pickup-reentry"));

                PresentationInputGates.EndExternalHold("pickup");
                runtime.Tick();
                Assert.IsFalse(PresentationInputGates.HasExternalHold);

                Assert.IsTrue(PresentationInputGates.TryBeginExternalHold("drain"));
                PresentationInputGates.ForceEndExternalHold("cancel");
                runtime.Tick();
                Assert.IsFalse(PresentationInputGates.HasExternalHold);

                PresentationInputGates.ForceEndExternalHold("already-clear");
                runtime.Tick();
                Assert.IsFalse(PresentationInputGates.HasExternalHold);
            }
        }

        [Test]
        public void Opening_Overlay_BoardSelect_DoNotImplyExternalHold()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);
                input.SetOpeningPresentationActive(true);
                input.SetChoiceOverlayActive(true);
                input.SetBoardSelectModeActive(true);

                Assert.IsFalse(input.HasExternalHold);
                Assert.IsFalse(input.MainlineBusy);
                Assert.AreEqual(InputOwner.ChoiceOverlay, input.CurrentOwner);
            }
        }

        [Test]
        public void CurrentOwner_OrthogonalToMainlineBusy()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(arch, new AcceptAllScriptFactory()))
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);

                Assert.AreEqual(InputOwner.ProtectedField, input.CurrentOwner);
                Assert.IsTrue(runtime.Runtime.TryBeginExternalHold("hold"));
                Assert.IsTrue(input.MainlineBusy);
                Assert.AreEqual(InputOwner.ProtectedField, input.CurrentOwner);

                input.SetChoiceOverlayActive(true);
                Assert.AreEqual(InputOwner.ChoiceOverlay, input.CurrentOwner);
                Assert.IsTrue(input.MainlineBusy);
            }
        }

        private sealed class AcceptAllScriptFactory : IIntentScriptFactory
        {
            public void BuildScript(InputIntent intent, BattleTimeline mainline)
            {
            }
        }
    }
}
