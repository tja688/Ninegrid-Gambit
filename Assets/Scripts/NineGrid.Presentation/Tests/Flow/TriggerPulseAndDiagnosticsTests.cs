using System;
using System.Collections.Generic;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using NineGrid.Flow;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #11：Trigger 脉冲可降级、音效 debounce、时间线诊断步进——表演导演对外缝。
    /// </summary>
    public sealed class TriggerPulseAndDiagnosticsTests
    {
        [Test]
        public void DebouncingAudioSink_SameTriggerWithinWindow_EmitsOnce()
        {
            var clock = 0f;
            var inner = new RecordingTriggerSink();
            var sink = new DebouncingTriggerPulseSink(inner, windowSeconds: 0.1f, nowSeconds: () => clock);

            sink.Pulse("sfx.hit");
            clock = 0.05f;
            sink.Pulse("sfx.hit");
            clock = 0.11f;
            sink.Pulse("sfx.hit");

            CollectionAssert.AreEqual(new[] { "sfx.hit", "sfx.hit" }, inner.Pulses);
        }

        [Test]
        public void DebouncingAudioSink_DifferentTriggers_DoNotShareWindow()
        {
            var clock = 0f;
            var inner = new RecordingTriggerSink();
            var sink = new DebouncingTriggerPulseSink(inner, windowSeconds: 0.1f, nowSeconds: () => clock);

            sink.Pulse("sfx.hit");
            sink.Pulse("sfx.block");

            CollectionAssert.AreEqual(new[] { "sfx.hit", "sfx.block" }, inner.Pulses);
        }

        [Test]
        public void TriggerStep_NoopSink_StillFinishesAndDoesNotBlockTimeline()
        {
            var timeline = new BattleTimeline();
            var after = new ScriptedStep(continueTicks: 0);
            timeline.Enqueue(new TriggerStep(NullTriggerPulseSink.Instance, "fx.spark"));
            timeline.Enqueue(after);

            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.016f));
            Assert.AreEqual(TimelineStepStatus.Finished, timeline.Tick(0.016f));
            Assert.AreEqual(1, after.TickCount);
            Assert.IsFalse(timeline.IsBusy);
        }

        [Test]
        public void TriggerStep_ThrowingSink_DegradesAndTimelineContinues()
        {
            var timeline = new BattleTimeline();
            var after = new ScriptedStep(continueTicks: 0);
            timeline.Enqueue(new TriggerStep(new ThrowingTriggerSink(), "fx.broken"));
            timeline.Enqueue(after);

            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.016f));
            Assert.AreEqual(TimelineStepStatus.Finished, timeline.Tick(0.016f));
            Assert.AreEqual(1, after.TickCount);
        }

        [Test]
        public void GatedTriggerSink_WhenDisabled_DropsPulseWithoutBlocking()
        {
            var inner = new RecordingTriggerSink();
            var enabled = false;
            var gated = new GatedTriggerPulseSink(inner, () => enabled);
            var timeline = new BattleTimeline();
            timeline.Enqueue(new TriggerStep(gated, "fx.spark"));
            timeline.Enqueue(new TriggerStep(gated, "sfx.hit"));

            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.016f));
            Assert.AreEqual(TimelineStepStatus.Finished, timeline.Tick(0.016f));
            CollectionAssert.IsEmpty(inner.Pulses);

            enabled = true;
            timeline.Enqueue(new TriggerStep(gated, "fx.spark"));
            Assert.AreEqual(TimelineStepStatus.Finished, timeline.Tick(0.016f));
            CollectionAssert.AreEqual(new[] { "fx.spark" }, inner.Pulses);
        }

        [Test]
        public void Timeline_EmitsStepEnterExit_ForReplayDiagnostics()
        {
            var diag = new RecordingTimelineDiagnosticSink();
            var timeline = new BattleTimeline(diag);
            timeline.Enqueue(new TriggerStep(NullTriggerPulseSink.Instance, "fx.a"));
            timeline.Enqueue(new DelayStep(0.02f));

            // Trigger 发即完成；有后续时本 Tick 只 exit，下一步 Enter 在下一帧 dequeue。
            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.016f));
            CollectionAssert.AreEqual(new[] { "enter:TriggerStep", "exit:TriggerStep" }, diag.Events);

            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.01f));
            CollectionAssert.AreEqual(
                new[] { "enter:TriggerStep", "exit:TriggerStep", "enter:DelayStep" },
                diag.Events);

            Assert.AreEqual(TimelineStepStatus.Finished, timeline.Tick(0.02f));
            CollectionAssert.AreEqual(
                new[] { "enter:TriggerStep", "exit:TriggerStep", "enter:DelayStep", "exit:DelayStep" },
                diag.Events);
        }

        [Test]
        public void FourStepScript_WithDisabledFx_StillCompletesBatchLockstep()
        {
            var gate = new FakeBatchGate();
            var present = new FakePresentChannel(ticksUntilComplete: 0);
            var fxEnabled = false;
            var fx = new GatedTriggerPulseSink(new RecordingTriggerSink(), () => fxEnabled);
            var timeline = new BattleTimeline();

            timeline.Enqueue(new ResolveBatchStep(gate));
            timeline.Enqueue(new PresentStep(gate, present));
            timeline.Enqueue(new TriggerStep(fx, "fx.hit"));

            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.016f));
            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.016f));
            Assert.AreEqual(TimelineStepStatus.Finished, timeline.Tick(0.016f));
            Assert.IsFalse(timeline.IsBusy);
            Assert.IsFalse(gate.HasOpenBatch);
        }

        private sealed class RecordingTriggerSink : ITriggerPulseSink
        {
            public readonly List<string> Pulses = new List<string>();

            public void Pulse(string triggerId)
            {
                Pulses.Add(triggerId);
            }
        }

        private sealed class ThrowingTriggerSink : ITriggerPulseSink
        {
            public void Pulse(string triggerId)
            {
                throw new InvalidOperationException("fx disabled boom");
            }
        }

        private sealed class RecordingTimelineDiagnosticSink : ITimelineDiagnosticSink
        {
            public readonly List<string> Events = new List<string>();

            public void StepEnter(string step, string lane)
            {
                Events.Add("enter:" + step);
            }

            public void StepExit(string step, string lane)
            {
                Events.Add("exit:" + step);
            }
        }

        private sealed class ScriptedStep : ITimelineStep
        {
            private readonly int mContinueTicks;
            private int mTicks;

            public ScriptedStep(int continueTicks)
            {
                mContinueTicks = continueTicks;
            }

            public int TickCount
            {
                get { return mTicks; }
            }

            public TimelineStepStatus Tick(float deltaTime)
            {
                mTicks++;
                return mTicks <= mContinueTicks
                    ? TimelineStepStatus.Continue
                    : TimelineStepStatus.Finished;
            }
        }

        private sealed class FakeBatchGate : IPresentationBatchGate
        {
            private int mNextId = 1;

            public bool HasOpenBatch { get; private set; }

            public int ActiveBatchId { get; private set; }

            public BatchOpenResult TryOpenNextBatch(out int batchId)
            {
                if (HasOpenBatch)
                {
                    batchId = ActiveBatchId;
                    return BatchOpenResult.WaitHasOpen;
                }

                ActiveBatchId = mNextId++;
                batchId = ActiveBatchId;
                HasOpenBatch = true;
                return BatchOpenResult.Opened;
            }

            public bool TryAcknowledge(int batchId)
            {
                if (!HasOpenBatch || batchId != ActiveBatchId)
                {
                    return false;
                }

                HasOpenBatch = false;
                ActiveBatchId = 0;
                return true;
            }
        }

        private sealed class FakePresentChannel : IPresentChannel
        {
            private readonly int mTicksUntilComplete;
            private int mTicks;

            public FakePresentChannel(int ticksUntilComplete)
            {
                mTicksUntilComplete = ticksUntilComplete;
            }

            public bool Began { get; private set; }

            public int ActiveBatchId { get; private set; }

            public bool IsComplete
            {
                get { return Began && mTicks >= mTicksUntilComplete; }
            }

            public void Begin(int batchId)
            {
                Began = true;
                ActiveBatchId = batchId;
                mTicks = 0;
            }

            public void Tick(float deltaTime)
            {
                if (Began)
                {
                    mTicks++;
                }
            }
        }
    }
}
