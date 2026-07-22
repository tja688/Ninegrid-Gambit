using NineGrid.Cards;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.BehaviorBaseline
{
    /// <summary>
    /// #43 批次0：Opening / overlay / board-select 门禁矩阵行为基线（接口级标志语义）。
    /// 批次2 迁入 PresentationInputStateSystem 后本测试应改读只读投影。
    /// </summary>
    public sealed class InputGateMatrixBaselineTests
    {
        [TearDown]
        public void TearDown()
        {
            CombatHitSink.ResetInputGates("InputGateMatrixBaselineTests");
        }

        [Test]
        public void GateFlags_AreIndependent_UntilResetClearsAll()
        {
            CombatHitSink.OpeningPresentationActive = true;
            CombatHitSink.ChoiceOverlayActive = true;
            CombatHitSink.BoardSelectModeActive = true;
            CombatHitSink.DirectorMainlineBusy = true;

            Assert.IsTrue(CombatHitSink.OpeningPresentationActive);
            Assert.IsTrue(CombatHitSink.ChoiceOverlayActive);
            Assert.IsTrue(CombatHitSink.BoardSelectModeActive);
            Assert.IsTrue(CombatHitSink.DirectorMainlineBusy);

            CombatHitSink.OpeningPresentationActive = false;
            Assert.IsFalse(CombatHitSink.OpeningPresentationActive);
            Assert.IsTrue(CombatHitSink.ChoiceOverlayActive);
            Assert.IsTrue(CombatHitSink.BoardSelectModeActive);

            CombatHitSink.ResetInputGates("matrix");
            Assert.IsFalse(CombatHitSink.OpeningPresentationActive);
            Assert.IsFalse(CombatHitSink.ChoiceOverlayActive);
            Assert.IsFalse(CombatHitSink.BoardSelectModeActive);
            Assert.IsFalse(CombatHitSink.DirectorMainlineBusy);
            Assert.IsFalse(CombatHitSink.PresentationLocked);
        }

        [Test]
        public void PresentationLock_RejectsReentry_AndForceEndAlwaysReleases()
        {
            Assert.IsTrue(CombatHitSink.TryBeginPresentationLock("pickup"));
            Assert.IsTrue(CombatHitSink.PresentationLocked);
            Assert.IsFalse(CombatHitSink.TryBeginPresentationLock("pickup-reentry"));

            CombatHitSink.EndPresentationLock("pickup");
            Assert.IsFalse(CombatHitSink.PresentationLocked);

            Assert.IsTrue(CombatHitSink.TryBeginPresentationLock("drain"));
            CombatHitSink.ForceEndPresentationLock("cancel");
            Assert.IsFalse(CombatHitSink.PresentationLocked);

            // ForceEnd 在未持锁时也是幂等 no-op。
            CombatHitSink.ForceEndPresentationLock("already-clear");
            Assert.IsFalse(CombatHitSink.PresentationLocked);
        }

        [Test]
        public void Opening_Overlay_BoardSelect_DoNotImplyPresentationLocked()
        {
            CombatHitSink.OpeningPresentationActive = true;
            CombatHitSink.ChoiceOverlayActive = true;
            CombatHitSink.BoardSelectModeActive = true;

            Assert.IsFalse(CombatHitSink.PresentationLocked);
            Assert.IsFalse(CombatHitSink.DirectorMainlineBusy);
        }
    }
}
