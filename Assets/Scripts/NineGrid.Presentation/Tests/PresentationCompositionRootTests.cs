using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Setup;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class PresentationCompositionRootTests
    {
        [Test]
        public void Install_RegistersAndStartsSingleRuntime_OnCoreArchitecture()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var root = new PresentationCompositionRoot();
                var runtime = root.Install(new RecordingScriptFactory());

                Assert.AreSame(runtime, arch.Architecture.GetSystem<IPresentationRuntimeSystem>());
                Assert.IsTrue(runtime.IsStarted);
                Assert.Throws<System.InvalidOperationException>(
                    () => root.Install(new RecordingScriptFactory()));

                root.Shutdown(IntentClearReason.LayerChange);
                Assert.IsFalse(runtime.IsStarted);

                var restarted = root.Install(new RecordingScriptFactory());
                Assert.AreSame(runtime, restarted);
                Assert.IsTrue(restarted.IsStarted);
                root.Shutdown(IntentClearReason.LayerChange);
            }
        }

        [Test]
        public void Install_WhenRuntimeAlreadyStarted_DoesNotAdoptForeignRuntime()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var owner = new PresentationCompositionRoot();
                var runtime = owner.Install(new RecordingScriptFactory());

                var intruder = new PresentationCompositionRoot();
                Assert.Throws<System.InvalidOperationException>(
                    () => intruder.Install(new RecordingScriptFactory()));

                Assert.IsTrue(runtime.IsStarted);
                Assert.AreSame(
                    runtime,
                    NineGridArchitecture.Current.GetSystem<IPresentationRuntimeSystem>());

                intruder.Shutdown(IntentClearReason.LayerChange);
                Assert.IsTrue(runtime.IsStarted, "冲突方 Shutdown 不应关停并未接管的运行时。");

                owner.Shutdown(IntentClearReason.LayerChange);
                Assert.IsFalse(runtime.IsStarted);
            }
        }
    }
}
