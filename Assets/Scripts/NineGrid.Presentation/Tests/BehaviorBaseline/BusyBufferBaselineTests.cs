using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.BehaviorBaseline
{
    /// <summary>
    /// #30 行为基线：主线 busy 时只缓冲最早一条输入，idle 后 drain。
    /// 接缝：CompositionRoot → RuntimeSystem → PresentationDirector。
    /// </summary>
    public sealed class BusyBufferBaselineTests
    {
        [Test]
        public void Runtime_BusyBuffersEarliestIntent_EmitsUiPick_ThenDrainsWhenIdle()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch,
                       new RecordingScriptFactory(continueTicks: 2),
                       out var uiPick))
            {
                bool preview;
                Assert.IsTrue(runtime.TrySubmitIntent(
                    new InputIntent(InputIntentKinds.Explore, 1), out preview));
                Assert.IsFalse(preview);
                Assert.IsTrue(runtime.MainlineBusy.Value);

                Assert.IsTrue(runtime.TrySubmitIntent(
                    new InputIntent(InputIntentKinds.Explore, 3), out preview));
                Assert.IsTrue(preview);
                Assert.AreEqual(1, uiPick.Previews.Count);
                Assert.AreEqual(3, uiPick.Previews[0].TargetId);

                Assert.IsFalse(runtime.TrySubmitIntent(
                    new InputIntent(InputIntentKinds.Explore, 9), out preview));
                Assert.IsFalse(preview);
                Assert.AreEqual(1, uiPick.Previews.Count);

                runtime.TickUntilIdle();
                Assert.IsFalse(runtime.MainlineBusy.Value);
                Assert.AreEqual(2, runtime.ScriptFactory.Built.Count);
                Assert.AreEqual(1, runtime.ScriptFactory.Built[0].TargetId);
                Assert.AreEqual(3, runtime.ScriptFactory.Built[1].TargetId);
            }
        }

        [Test]
        public void Controller_CanReachRuntimeBusyProjection_ViaArchitecture()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch,
                       new RecordingScriptFactory(continueTicks: 1)))
            using (var host = PresentationControllerHost.Create())
            {
                host.Controller.DriveAwake();
                Assert.AreSame(arch.Architecture, host.Controller.GetArchitecture());

                bool preview;
                Assert.IsTrue(runtime.TrySubmitIntent(
                    new InputIntent(InputIntentKinds.Explore, 2), out preview));
                Assert.IsTrue(runtime.MainlineBusy.Value);

                var system = host.Controller.GetArchitecture().GetSystem<Systems.IPresentationRuntimeSystem>();
                Assert.AreSame(runtime.Runtime, system);
                Assert.IsTrue(system.MainlineBusy.Value);

                runtime.TickUntilIdle();
                Assert.IsFalse(system.MainlineBusy.Value);
            }
        }
    }
}
