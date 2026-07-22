using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class PresentationRuntimeSystemTests
    {
        [Test]
        public void Runtime_OwnsDirectorLifecycle_AndProjectsMainlineBusy()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch,
                       new RecordingScriptFactory(continueTicks: 1)))
            {
                Assert.IsTrue(runtime.Runtime.IsStarted);
                Assert.IsFalse(runtime.MainlineBusy.Value);

                bool preview;
                Assert.IsTrue(runtime.TrySubmitIntent(
                    new InputIntent(InputIntentKinds.Explore, 3), out preview));
                Assert.IsFalse(preview);
                Assert.IsTrue(runtime.MainlineBusy.Value);

                runtime.Runtime.Stop(IntentClearReason.LayerChange);
                Assert.IsFalse(runtime.Runtime.IsStarted);
                Assert.IsFalse(runtime.MainlineBusy.Value);
                Assert.AreSame(
                    runtime.Runtime,
                    arch.Architecture.GetSystem<IPresentationRuntimeSystem>());
            }
        }
    }
}
