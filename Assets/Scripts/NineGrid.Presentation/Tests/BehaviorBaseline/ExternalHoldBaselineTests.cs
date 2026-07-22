using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.BehaviorBaseline
{
    /// <summary>
    /// #43 批次0：external hold 经 Runtime 接口必定可释放（含 ForceEnd）。
    /// HoldStep 在释放标志后须 Tick 一次才能卸下主线 busy。
    /// </summary>
    public sealed class ExternalHoldBaselineTests
    {
        [Test]
        public void Runtime_ExternalHold_EndAndForceEnd_AlwaysReleaseBusy()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch,
                       new RecordingScriptFactory(continueTicks: 1)))
            {
                Assert.IsTrue(runtime.Runtime.TryBeginExternalHold("pickup"));
                Assert.IsTrue(runtime.MainlineBusy.Value);
                Assert.IsFalse(runtime.Runtime.TryBeginExternalHold("reentry"));

                runtime.Runtime.EndExternalHold("pickup");
                runtime.Tick();
                Assert.IsFalse(runtime.MainlineBusy.Value);

                Assert.IsTrue(runtime.Runtime.TryBeginExternalHold("drain"));
                runtime.Runtime.ForceEndExternalHold("cancel");
                runtime.Tick();
                Assert.IsFalse(runtime.MainlineBusy.Value);

                // ForceEnd 幂等：未持有时不抛、不粘 busy。
                runtime.Runtime.ForceEndExternalHold("already-clear");
                runtime.Tick();
                Assert.IsFalse(runtime.MainlineBusy.Value);
            }
        }

        [Test]
        public void Runtime_Stop_ForceReleasesExternalHold()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch,
                       new RecordingScriptFactory()))
            {
                Assert.IsTrue(runtime.Runtime.TryBeginExternalHold("pickup"));
                Assert.IsTrue(runtime.MainlineBusy.Value);

                runtime.Runtime.Stop(IntentClearReason.LayerChange);
                Assert.IsFalse(runtime.Runtime.IsStarted);
                Assert.IsFalse(runtime.MainlineBusy.Value);
            }
        }
    }
}
