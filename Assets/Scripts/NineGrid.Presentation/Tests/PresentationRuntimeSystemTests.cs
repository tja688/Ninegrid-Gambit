using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class PresentationRuntimeSystemTests
    {
        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Runtime_OwnsDirectorLifecycle_AndProjectsMainlineBusy()
        {
            var architecture = NineGridArchitecture.Current;
            var runtime = new PresentationRuntimeSystem();
            architecture.RegisterSystem<IPresentationRuntimeSystem>(runtime);
            runtime.Start(new SingleStepScriptFactory());

            Assert.IsTrue(runtime.IsStarted);
            Assert.IsFalse(runtime.MainlineBusy.Value);

            bool preview;
            Assert.IsTrue(runtime.TrySubmitIntent(new InputIntent("explore", 3), out preview));
            Assert.IsFalse(preview);
            Assert.IsTrue(runtime.MainlineBusy.Value);

            runtime.Stop(IntentClearReason.LayerChange);
            Assert.IsFalse(runtime.IsStarted);
            Assert.IsFalse(runtime.MainlineBusy.Value);
        }

        private sealed class SingleStepScriptFactory : IIntentScriptFactory
        {
            public void BuildScript(InputIntent intent, BattleTimeline timeline)
            {
                timeline.Enqueue(new WaitingStep());
            }
        }

        private sealed class WaitingStep : ITimelineStep
        {
            public TimelineStepStatus Tick(float deltaTime)
            {
                return TimelineStepStatus.Continue;
            }
        }
    }
}
