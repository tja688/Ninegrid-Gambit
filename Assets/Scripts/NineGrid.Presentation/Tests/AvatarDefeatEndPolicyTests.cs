using NineGrid.Flow.Presentation;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 战败收口不得在导演链未演完时 Raise：HardClearIntents 会清掉后续
    /// 机关位移 / 效果打击 / 血条归零，死亡面板抢拍。
    /// </summary>
    public sealed class AvatarDefeatEndPolicyTests
    {
        [SetUp]
        public void SetUp()
        {
            BattleBeatHook.Reset();
            FlipPlaybackCoordinator.Reset();
            EffectStrikeHook.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            BattleBeatHook.Reset();
            FlipPlaybackCoordinator.Reset();
            EffectStrikeHook.Reset();
        }

        [Test]
        public void ShouldDeferRaise_WhenDrainInFlight()
        {
            Assert.IsTrue(AvatarDefeatEndPolicy.ShouldDeferRaise(drainInFlight: true, mainlineBusy: false));
        }

        [Test]
        public void ShouldDeferRaise_WhenMainlineBusy()
        {
            Assert.IsTrue(AvatarDefeatEndPolicy.ShouldDeferRaise(drainInFlight: false, mainlineBusy: true));
        }

        [Test]
        public void ShouldNotDeferRaise_WhenPresentationIdle()
        {
            Assert.IsFalse(AvatarDefeatEndPolicy.ShouldDeferRaise(drainInFlight: false, mainlineBusy: false));
        }

        [Test]
        public void DirectorBusy_KeepsLaterPresentSteps_UntilIdle()
        {
            var channel = new SlowCompleteChannel(ticksToComplete: 2);
            var director = new PresentationDirector(new TwoPresentFactory(channel));
            Assert.IsTrue(director.TrySubmitIntent(new InputIntent(InputIntentKinds.Explore, 1), out _));
            Assert.IsTrue(director.IsMainlineBusy);
            Assert.IsTrue(
                AvatarDefeatEndPolicy.ShouldDeferRaise(false, director.IsMainlineBusy),
                "主线未跑空时武装战败不得 Raise / HardClear");

            for (var i = 0; i < 40 && director.IsMainlineBusy; i++)
            {
                director.Tick(0.016f);
            }

            Assert.IsFalse(director.IsMainlineBusy);
            Assert.IsFalse(AvatarDefeatEndPolicy.ShouldDeferRaise(false, director.IsMainlineBusy));
            Assert.AreEqual(2, channel.BeginCount, "两段 Present 都应开播，证明未因战败臂旗提前 HardClear");
        }

        private sealed class SlowCompleteChannel : IPresentChannel
        {
            private readonly int mTicksToComplete;
            private int mTicks;

            public SlowCompleteChannel(int ticksToComplete)
            {
                mTicksToComplete = ticksToComplete;
            }

            public int BeginCount { get; private set; }

            public int ActiveBatchId { get; private set; }

            public bool IsComplete { get; private set; } = true;

            public void Begin(int batchId)
            {
                ActiveBatchId = batchId;
                BeginCount++;
                mTicks = 0;
                IsComplete = mTicksToComplete <= 0;
            }

            public void Tick(float deltaTime)
            {
                if (IsComplete)
                {
                    return;
                }

                mTicks++;
                if (mTicks >= mTicksToComplete)
                {
                    IsComplete = true;
                }
            }
        }

        private sealed class TwoPresentFactory : IIntentScriptFactory
        {
            private readonly IPresentChannel mChannel;

            public TwoPresentFactory(IPresentChannel channel)
            {
                mChannel = channel;
            }

            public void BuildScript(InputIntent intent, BattleTimeline timeline)
            {
                var first = new ImmediateOpenGate(1);
                var second = new ImmediateOpenGate(2);
                timeline.Enqueue(new ResolveBatchStep(first));
                timeline.Enqueue(new PresentStep(first, mChannel, "FirstPresent"));
                timeline.Enqueue(new ResolveBatchStep(second));
                timeline.Enqueue(new PresentStep(second, mChannel, "SecondPresent"));
            }
        }

        private sealed class ImmediateOpenGate : IPresentationBatchGate
        {
            private readonly int mBatchId;
            private bool mOpen = true;

            public ImmediateOpenGate(int batchId)
            {
                mBatchId = batchId;
            }

            public bool HasOpenBatch => mOpen;

            public int ActiveBatchId => mOpen ? mBatchId : 0;

            public BatchOpenResult TryOpenNextBatch(out int batchId)
            {
                batchId = mBatchId;
                mOpen = true;
                return BatchOpenResult.Opened;
            }

            public bool TryAcknowledge(int batchId)
            {
                if (batchId != mBatchId)
                {
                    return false;
                }

                mOpen = false;
                return true;
            }
        }
    }
}
