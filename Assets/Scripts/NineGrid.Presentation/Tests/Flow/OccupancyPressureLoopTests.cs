using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Flow
{
    /// <summary>
    /// P5：固定 seed 下同帧突发 + 多局循环，断言 latest-wins 缓冲与主线租约不破门禁不变量。
    /// </summary>
    public sealed class OccupancyPressureLoopTests
    {
        private const int Games = 100;
        private const int BurstsPerGame = 40;

        [Test]
        public void HundredGames_SameFrameBursts_LatestWinsAndNoForceSync()
        {
            OccupancyForceSyncGuard.ResetForTests();

            for (var game = 0; game < Games; game++)
            {
                using (var arch = PresentationArchitectureFixture.CreateBare())
                using (var runtime = PresentationRuntimeFixture.Install(
                           arch,
                           new RecordingScriptFactory(continueTicks: 2),
                           out var uiPick))
                {
                    var intake = IntentIntakeSystem.EnsureRegistered(
                        arch.Architecture,
                        legalityOverride: _ => true);

                    for (var burst = 0; burst < BurstsPerGame; burst++)
                    {
                        bool preview;
                        Assert.AreEqual(
                            IntentDisposition.Allow,
                            intake.Submit(
                                new InputIntent(InputIntentKinds.Explore, 1 + (burst % 8)),
                                InputOwner.ProtectedField,
                                out preview));
                        Assert.IsTrue(runtime.MainlineBusy.Value);

                        // 同帧突发：Attack / Explore / Pickup 覆盖缓冲
                        Assert.AreEqual(
                            IntentDisposition.BufferToDirector,
                            intake.Submit(
                                new InputIntent(InputIntentKinds.Attack, 3),
                                InputOwner.ProtectedField,
                                out preview));
                        Assert.IsTrue(preview);

                        Assert.AreEqual(
                            IntentDisposition.BufferToDirector,
                            intake.Submit(
                                new InputIntent(InputIntentKinds.Explore, 7),
                                InputOwner.ProtectedField,
                                out preview));
                        Assert.IsTrue(preview);

                        Assert.AreEqual(
                            IntentDisposition.BufferToDirector,
                            intake.Submit(
                                new InputIntent(InputIntentKinds.Pickup, 2),
                                InputOwner.ProtectedField,
                                out preview));
                        Assert.IsTrue(preview);
                        Assert.AreEqual(2, uiPick.Previews[uiPick.Previews.Count - 1].TargetId);

                        // 表现租约：忙时仍可入队 Hold，释放后主线可排空
                        Assert.IsTrue(runtime.Runtime.TryBeginExternalHold("PressurePresent"));
                        Assert.IsTrue(runtime.Runtime.HasExternalHold);
                        runtime.Runtime.EndExternalHold("PressurePresent");

                        runtime.TickUntilIdle();
                        Assert.IsFalse(runtime.MainlineBusy.Value);
                        Assert.IsFalse(runtime.Runtime.HasExternalHold);
                        Assert.IsFalse(runtime.Runtime.HasBufferedIntent);
                    }
                }
            }

            Assert.AreEqual(
                0,
                OccupancyForceSyncGuard.InvocationCount,
                "高压循环不得触发 OccupancyForceSync");
        }

        [Test]
        public void ExternalHold_AfterBusyPresent_KeepsMainlineBusyUntilReleased()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch, new RecordingScriptFactory(continueTicks: 1)))
            {
                runtime.Runtime.MutateMainline(tl =>
                    tl.Enqueue(new ShortStep(continueTicks: 1)));
                Assert.IsTrue(runtime.MainlineBusy.Value);

                Assert.IsTrue(runtime.Runtime.TryBeginExternalHold("AfterPresent"));
                runtime.Tick();
                runtime.Tick();
                Assert.IsTrue(
                    runtime.MainlineBusy.Value,
                    "外层 step 结束后 Hold 须继续占主线");

                runtime.Runtime.EndExternalHold("AfterPresent");
                runtime.Tick();
                Assert.IsFalse(runtime.MainlineBusy.Value);
            }
        }

        private sealed class ShortStep : ITimelineStep
        {
            private readonly int mContinueTicks;
            private int mSeen;

            public ShortStep(int continueTicks)
            {
                mContinueTicks = continueTicks;
            }

            public TimelineStepStatus Tick(float deltaTime)
            {
                mSeen++;
                return mSeen <= mContinueTicks
                    ? TimelineStepStatus.Continue
                    : TimelineStepStatus.Finished;
            }
        }
    }
}
