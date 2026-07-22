using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Setup;
using NineGrid.Presentation.Systems;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class PresentationCompositionRootTests
    {
        [SetUp]
        public void SetUp() { NineGridArchitecture.ResetForTests(); }

        [TearDown]
        public void TearDown() { NineGridArchitecture.ResetForTests(); }

        [Test]
        public void Install_RegistersAndStartsSingleRuntime_OnCoreArchitecture()
        {
            var architecture = NineGridArchitecture.Current;
            var root = new PresentationCompositionRoot();

            var runtime = root.Install(new EmptyScriptFactory());

            Assert.AreSame(runtime, architecture.GetSystem<IPresentationRuntimeSystem>());
            Assert.IsTrue(runtime.IsStarted);
            Assert.Throws<System.InvalidOperationException>(
                () => root.Install(new EmptyScriptFactory()));

            root.Shutdown(IntentClearReason.LayerChange);
            Assert.IsFalse(runtime.IsStarted);

            var restarted = root.Install(new EmptyScriptFactory());
            Assert.AreSame(runtime, restarted);
            Assert.IsTrue(restarted.IsStarted);
        }

        [Test]
        public void Install_WhenRuntimeAlreadyStarted_DoesNotAdoptForeignRuntime()
        {
            var owner = new PresentationCompositionRoot();
            var runtime = owner.Install(new EmptyScriptFactory());

            var intruder = new PresentationCompositionRoot();
            Assert.Throws<System.InvalidOperationException>(
                () => intruder.Install(new EmptyScriptFactory()));

            Assert.IsTrue(runtime.IsStarted);
            Assert.AreSame(runtime, NineGridArchitecture.Current.GetSystem<IPresentationRuntimeSystem>());

            intruder.Shutdown(IntentClearReason.LayerChange);
            Assert.IsTrue(runtime.IsStarted, "冲突方 Shutdown 不应关停并未接管的运行时。");

            owner.Shutdown(IntentClearReason.LayerChange);
            Assert.IsFalse(runtime.IsStarted);
        }

        private sealed class EmptyScriptFactory : IIntentScriptFactory
        {
            public void BuildScript(InputIntent intent, BattleTimeline timeline) { }
        }
    }
}
