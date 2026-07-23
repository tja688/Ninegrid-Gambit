using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.BehaviorBaseline
{
    /// <summary>
    /// #47：须阻塞输入的交战/场地表演以 Step/ExternalHold 持主线，
    /// 使 <see cref="IPresentationRuntimeSystem.MainlineBusy"/> 在窗口内为真。
    /// </summary>
    public sealed class MainlineHoldBlockingPresentTests
    {
        [Test]
        public void BlockingPresentHold_WhenIdle_MakesMainlineBusy_UntilReleaseAndTick()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch,
                       new RecordingScriptFactory(continueTicks: 1)))
            {
                Assert.IsFalse(runtime.MainlineBusy.Value);

                bool acquiredHere;
                Assert.IsTrue(PresentationMainlineHold.TryAcquire("FieldBattlePresent", out acquiredHere));
                Assert.IsTrue(acquiredHere);
                Assert.IsTrue(runtime.MainlineBusy.Value);
                Assert.IsTrue(runtime.Runtime.HasExternalHold);

                PresentationMainlineHold.Release(acquiredHere, "FieldBattlePresent");
                runtime.Tick();
                Assert.IsFalse(runtime.MainlineBusy.Value);
                Assert.IsFalse(runtime.Runtime.HasExternalHold);
            }
        }

        [Test]
        public void BlockingPresentHold_WhenMainlineAlreadyBusy_NestsWithoutStealingOuterStep()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch,
                       new RecordingScriptFactory(continueTicks: 3)))
            {
                runtime.Runtime.MutateMainline(timeline =>
                    timeline.Enqueue(new ScriptedTimelineStep(continueTicks: 3)));
                Assert.IsTrue(runtime.MainlineBusy.Value);
                Assert.IsFalse(runtime.Runtime.HasExternalHold);

                bool acquiredHere;
                Assert.IsTrue(PresentationMainlineHold.TryAcquire("FieldMotion", out acquiredHere));
                Assert.IsTrue(acquiredHere);
                Assert.IsTrue(runtime.MainlineBusy.Value);
                Assert.IsTrue(runtime.Runtime.HasExternalHold);

                PresentationMainlineHold.Release(acquiredHere, "FieldMotion");
                Assert.IsTrue(runtime.MainlineBusy.Value);
                Assert.IsFalse(runtime.Runtime.HasExternalHold);

                runtime.TickUntilIdle();
                Assert.IsFalse(runtime.MainlineBusy.Value);
            }
        }

        [Test]
        public void BlockingPresentHold_WhenOuterExternalHold_DoesNotReenterOrDoubleEnd()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch,
                       new RecordingScriptFactory(continueTicks: 1)))
            {
                Assert.IsTrue(runtime.Runtime.TryBeginExternalHold("BoardPresentDrain"));
                Assert.IsTrue(runtime.MainlineBusy.Value);

                bool acquiredHere;
                Assert.IsTrue(PresentationMainlineHold.TryAcquire("FieldMotion", out acquiredHere));
                Assert.IsFalse(acquiredHere);

                PresentationMainlineHold.Release(acquiredHere, "FieldMotion");
                Assert.IsTrue(runtime.MainlineBusy.Value);
                Assert.IsTrue(runtime.Runtime.HasExternalHold);

                runtime.Runtime.EndExternalHold("BoardPresentDrain");
                runtime.Tick();
                Assert.IsFalse(runtime.MainlineBusy.Value);
            }
        }
    }
}
