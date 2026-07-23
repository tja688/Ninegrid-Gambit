using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using NineGrid.Flow;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 表演导演对外缝：时间线步进、批次锁步、意图缓冲、并发语义。假时钟/假通道，不迁真实局内流程。
    /// </summary>
    public sealed class PresentationDirectorTests
    {
        [SetUp]
        public void SetUp()
        {
            DirectorTrace.Reset();
        }

        [Test]
        public void ChainId_AllocatesOnIntentAccept_StaysUntilIdle_ThenClears()
        {
            var factory = new RecordingScriptFactory(continueTicks: 2);
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(new InputIntent("explore", 1), out preview));
            Assert.IsFalse(preview);
            var chainId = DirectorTrace.CurrentChainId;
            Assert.Greater(chainId, 0, "受理 intent / BuildScript 应分配 chainId");
            Assert.IsTrue(director.IsMainlineBusy);

            director.Tick(0.016f);
            Assert.AreEqual(chainId, DirectorTrace.CurrentChainId, "脚本跑空前 chainId 应保持不变");
            Assert.IsTrue(director.IsMainlineBusy);

            director.Tick(0.016f);
            director.Tick(0.016f);
            Assert.IsFalse(director.IsMainlineBusy);
            Assert.AreEqual(0, DirectorTrace.CurrentChainId, "主线 idle 且无缓冲 intent 后 chainId 应归零");
        }

        [Test]
        public void ChainId_HardClear_ClearsToZero()
        {
            var director = new PresentationDirector(new RecordingScriptFactory(continueTicks: 5));
            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(new InputIntent("explore", 1), out preview));
            Assert.Greater(DirectorTrace.CurrentChainId, 0);

            director.HardClearIntents(IntentClearReason.Defeat);
            Assert.AreEqual(0, DirectorTrace.CurrentChainId);
            Assert.IsFalse(director.IsMainlineBusy);
        }

        [Test]
        public void ChainId_BufferedFlush_DoesNotReallocateMidScript_AndGetsNewId()
        {
            var factory = new RecordingScriptFactory(continueTicks: 1);
            var director = new PresentationDirector(factory);

            director.EnqueueMainline(new ScriptedStep(continueTicks: 1));
            Assert.AreEqual(0, DirectorTrace.CurrentChainId, "无 intent 的主线步骤不分配 chainId");

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(new InputIntent("explore", 3), out preview));
            Assert.IsTrue(preview);
            Assert.AreEqual(0, DirectorTrace.CurrentChainId, "仅缓冲时不分配（避免覆盖在跑脚本）");

            director.Tick(0.016f); // Continue hold step
            director.Tick(0.016f); // Finished → flush BuildScript → BeginChain
            var flushedChainId = DirectorTrace.CurrentChainId;
            Assert.Greater(flushedChainId, 0);
            Assert.AreEqual(1, factory.Built.Count);
            Assert.IsTrue(director.IsMainlineBusy);

            director.Tick(0.016f); // Continue flushed script
            Assert.AreEqual(flushedChainId, DirectorTrace.CurrentChainId);
            director.Tick(0.016f); // Finished → idle clear
            Assert.IsFalse(director.IsMainlineBusy);
            Assert.AreEqual(0, DirectorTrace.CurrentChainId);
        }

        [Test]
        public void ChainId_SequentialIntents_AreMonotonicDistinct()
        {
            var factory = new RecordingScriptFactory(continueTicks: 0);
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(new InputIntent("explore", 1), out preview));
            var first = DirectorTrace.CurrentChainId;
            Assert.Greater(first, 0);
            director.Tick(0.016f);
            Assert.AreEqual(0, DirectorTrace.CurrentChainId);

            Assert.IsTrue(director.TrySubmitIntent(new InputIntent("explore", 2), out preview));
            var second = DirectorTrace.CurrentChainId;
            Assert.Greater(second, first);
            director.Tick(0.016f);
            Assert.AreEqual(0, DirectorTrace.CurrentChainId);
        }

        [Test]
        public void Timeline_StepContinuesUntilFinished_ThenAdvances()
        {
            var timeline = new BattleTimeline();
            var first = new ScriptedStep(continueTicks: 2);
            var second = new ScriptedStep(continueTicks: 0);
            timeline.Enqueue(first);
            timeline.Enqueue(second);

            Assert.IsTrue(timeline.IsBusy);
            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.016f));
            Assert.AreEqual(1, first.TickCount);
            Assert.AreEqual(0, second.TickCount);

            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.016f));
            Assert.AreEqual(2, first.TickCount);
            Assert.AreEqual(0, second.TickCount);

            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.016f));
            Assert.AreEqual(3, first.TickCount);
            Assert.AreEqual(0, second.TickCount);
            Assert.IsTrue(timeline.IsBusy);

            Assert.AreEqual(TimelineStepStatus.Finished, timeline.Tick(0.016f));
            Assert.AreEqual(1, second.TickCount);
            Assert.IsFalse(timeline.IsBusy);
        }

        [Test]
        public void FourStepKinds_ComposeScript_AndRunToIdle()
        {
            var gate = new FakeBatchGate();
            var present = new FakePresentChannel(ticksUntilComplete: 1);
            var triggers = new RecordingTriggerSink();
            var timeline = new BattleTimeline();

            timeline.Enqueue(new ResolveBatchStep(gate));
            timeline.Enqueue(new PresentStep(gate, present));
            timeline.Enqueue(new DelayStep(0.05f));
            timeline.Enqueue(new TriggerStep(triggers, "hit_spark"));

            // Resolve opens batch 1
            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.016f));
            Assert.AreEqual(1, gate.ActiveBatchId);
            Assert.IsTrue(gate.HasOpenBatch);

            // Present Begin+Tick → complete → ack
            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.016f));
            Assert.IsTrue(present.Began);
            Assert.IsFalse(gate.HasOpenBatch);

            // Delay 0.05s with fake clock
            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.04f));
            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.02f));
            Assert.AreEqual(TimelineStepStatus.Finished, timeline.Tick(0.016f));

            CollectionAssert.AreEqual(new[] { "hit_spark" }, triggers.Pulses);
            Assert.IsFalse(timeline.IsBusy);
        }

        [Test]
        public void SyncBatchGate_EmptyNonLockingBatch_StillBlocksUntilFinishBatch()
        {
            var session = new SyncSessionStub();
            var resolveCount = 0;
            var gate = CreateGateFromSession(session, () =>
            {
                resolveCount++;
                var batch = new PresentationBatch(
                    session.NextBatchId(),
                    System.Array.Empty<PresentationInstruction>(),
                    null);
                Assert.IsFalse(batch.RequiresAcknowledgement);
                session.OpenBatch(batch);
                return new CoreCommandDispatchResult(CoreCommandResult.Accept(0), batch, true);
            });

            int firstId;
            Assert.AreEqual(BatchOpenResult.Opened, gate.TryOpenNextBatch(out firstId));
            Assert.AreEqual(1, firstId);
            Assert.AreEqual(1, resolveCount);
            Assert.IsTrue(gate.HasOpenBatch);
            Assert.IsFalse(session.IsInputLocked);

            int blocked;
            Assert.AreEqual(BatchOpenResult.WaitHasOpen, gate.TryOpenNextBatch(out blocked));
            Assert.AreEqual(1, resolveCount);

            Assert.IsTrue(gate.TryAcknowledge(firstId));
            Assert.IsFalse(gate.HasOpenBatch);
            Assert.AreEqual(0, session.ActiveBatchId);

            int secondId;
            Assert.AreEqual(BatchOpenResult.Opened, gate.TryOpenNextBatch(out secondId));
            Assert.AreEqual(2, secondId);
            Assert.AreEqual(2, resolveCount);
        }

        [Test]
        public void PresentStep_WithSyncGate_EmptyBatch_DoesNotDeadlock()
        {
            var session = new SyncSessionStub();
            var gate = CreateGateFromSession(session, () =>
            {
                var batch = new PresentationBatch(
                    session.NextBatchId(),
                    System.Array.Empty<PresentationInstruction>(),
                    null);
                session.OpenBatch(batch);
                return new CoreCommandDispatchResult(CoreCommandResult.Accept(0), batch, true);
            });
            var present = new FakePresentChannel(ticksUntilComplete: 1);
            var timeline = new BattleTimeline();
            timeline.Enqueue(new ResolveBatchStep(gate));
            timeline.Enqueue(new PresentStep(gate, present));

            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.016f));
            Assert.IsTrue(gate.HasOpenBatch);
            Assert.AreEqual(TimelineStepStatus.Finished, timeline.Tick(0.016f));
            Assert.IsTrue(present.Began);
            Assert.IsFalse(gate.HasOpenBatch);
            Assert.IsFalse(timeline.IsBusy);
        }

        [Test]
        public void ResolveBatch_DispatchReject_AbortsMainline_DoesNotStickBusy()
        {
            // P0：dispatchReject 若 Continue 重试会永久粘住 IsMainlineBusy（空格 explore 卡死）。
            var gate = new FakeBatchGate(alwaysFail: true);
            var present = new FakePresentChannel(ticksUntilComplete: 1);
            var director = new PresentationDirector(new RecordingScriptFactory());
            director.EnqueueMainline(new ResolveBatchStep(gate));
            director.EnqueueMainline(new PresentStep(gate, present));
            Assert.IsTrue(director.IsMainlineBusy);

            director.Tick(0.016f);
            Assert.IsFalse(director.IsMainlineBusy);
            Assert.IsFalse(present.Began);
        }

        [Test]
        public void SyncBatchGate_RejectedDispatch_ReturnsFailed_NotWaitHasOpen()
        {
            var session = new SyncSessionStub();
            var gate = CreateGateFromSession(
                session,
                () => new CoreCommandDispatchResult(
                    CoreCommandResult.Reject("Clicked slot is outside interaction range."),
                    null,
                    false));

            int batchId;
            Assert.AreEqual(BatchOpenResult.Failed, gate.TryOpenNextBatch(out batchId));
            Assert.AreEqual(0, batchId);
            Assert.IsFalse(gate.HasOpenBatch);
        }

        private static PresentationSyncBatchGate CreateGateFromSession(
            SyncSessionStub session,
            System.Func<CoreCommandDispatchResult> resolveAndOpen)
        {
            return new PresentationSyncBatchGate(
                () => session.ActiveBatchId > 0,
                () => session.ActiveBatchId,
                resolveAndOpen,
                session.FinishBatch);
        }

        /// <summary>忠实镜像 PresentationSyncSystem OpenBatch/FinishBatch（无 QFramework 架构）。</summary>
        private sealed class SyncSessionStub
        {
            private int mNextId = 1;

            public bool IsInputLocked { get; private set; }
            public int ActiveBatchId { get; private set; }

            public int NextBatchId()
            {
                return mNextId++;
            }

            public void OpenBatch(PresentationBatch batch)
            {
                ActiveBatchId = batch != null ? batch.BatchId : 0;
                IsInputLocked = batch != null && batch.RequiresAcknowledgement;
            }

            public CoreCommandResult FinishBatch(int batchId)
            {
                if (ActiveBatchId <= 0)
                {
                    return CoreCommandResult.Reject("No presentation batch is waiting.");
                }

                if (batchId != ActiveBatchId)
                {
                    return CoreCommandResult.Reject("Presentation batch mismatch.");
                }

                IsInputLocked = false;
                ActiveBatchId = 0;
                return CoreCommandResult.Accept(0);
            }
        }

        [Test]
        public void LockstepScript_SecondResolveWaits_UntilPresentAcks()
        {
            var gate = new FakeBatchGate();
            // Need 2 channel ticks so Present spans two timeline ticks (second resolve cannot sneak ahead).
            var present = new FakePresentChannel(ticksUntilComplete: 2);
            var timeline = new BattleTimeline();
            var resolve1 = new ResolveBatchStep(gate);
            var resolve2 = new ResolveBatchStep(gate);

            timeline.Enqueue(resolve1);
            timeline.Enqueue(new PresentStep(gate, present));
            timeline.Enqueue(resolve2);
            timeline.Enqueue(new PresentStep(gate, new FakePresentChannel(ticksUntilComplete: 1)));

            timeline.Tick(0.016f);
            Assert.AreEqual(1, resolve1.OpenedBatchId);
            Assert.AreEqual(0, resolve2.OpenedBatchId);

            // Present tick 1/2 — still open
            timeline.Tick(0.016f);
            Assert.AreEqual(0, resolve2.OpenedBatchId);
            Assert.IsTrue(gate.HasOpenBatch);

            // Present tick 2/2 — ack, then next tick can resolve2
            timeline.Tick(0.016f);
            Assert.IsFalse(gate.HasOpenBatch);
            Assert.AreEqual(0, resolve2.OpenedBatchId);

            timeline.Tick(0.016f);
            Assert.AreEqual(2, resolve2.OpenedBatchId);
            Assert.AreEqual(TimelineStepStatus.Finished, timeline.Tick(0.016f));
        }

        [Test]
        public void Intent_BusyBuffersLatestWins_AndEmitsUiPick_ThenDrainsWhenIdle()
        {
            var uiPick = new RecordingUiPickSink();
            var factory = new RecordingScriptFactory();
            var director = new PresentationDirector(factory, uiPick);

            director.EnqueueMainline(new ScriptedStep(continueTicks: 2));
            Assert.IsTrue(director.IsMainlineBusy);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(new InputIntent("explore", 3), out preview));
            Assert.IsTrue(preview);
            Assert.IsTrue(director.HasBufferedIntent);
            Assert.AreEqual(1, uiPick.Previews.Count);
            Assert.AreEqual("explore", uiPick.Previews[0].Kind);

            Assert.IsTrue(director.TrySubmitIntent(new InputIntent("explore", 9), out preview));
            Assert.IsTrue(preview);
            Assert.AreEqual(9, director.BufferedIntent.TargetId);
            Assert.AreEqual(2, uiPick.Previews.Count);
            Assert.AreEqual(9, uiPick.Previews[1].TargetId);

            director.Tick(0.016f);
            director.Tick(0.016f);
            director.Tick(0.016f);
            Assert.IsFalse(director.HasBufferedIntent);
            Assert.AreEqual(1, factory.Built.Count);
            Assert.AreEqual(9, factory.Built[0].TargetId);
            Assert.IsTrue(director.IsMainlineBusy);

            director.Tick(0.016f);
            Assert.IsFalse(director.IsMainlineBusy);
        }

        [Test]
        public void Intent_BusyNClicks_BufferDepthStaysOne_SameFrameBurstKeepsLatest()
        {
            var uiPick = new RecordingUiPickSink();
            var factory = new RecordingScriptFactory();
            var director = new PresentationDirector(factory, uiPick);

            director.EnqueueMainline(new ScriptedStep(continueTicks: 2));
            Assert.IsTrue(director.IsMainlineBusy);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(new InputIntent("explore", 3), out preview));
            Assert.IsTrue(director.TrySubmitIntent(new InputIntent("explore", 4), out preview));
            Assert.IsTrue(director.TrySubmitIntent(new InputIntent("explore", 5), out preview));
            Assert.IsTrue(director.TrySubmitIntent(new InputIntent("explore", 6), out preview));
            Assert.IsTrue(director.TrySubmitIntent(new InputIntent("explore", 7), out preview));

            Assert.IsTrue(director.HasBufferedIntent);
            Assert.AreEqual(7, director.BufferedIntent.TargetId);
            Assert.AreEqual(5, uiPick.Previews.Count);
            Assert.AreEqual(7, uiPick.Previews[4].TargetId);

            director.Tick(0.016f);
            director.Tick(0.016f);
            director.Tick(0.016f);
            Assert.IsFalse(director.HasBufferedIntent);
            Assert.AreEqual(1, factory.Built.Count);
            Assert.AreEqual(7, factory.Built[0].TargetId);
        }

        [Test]
        public void Intent_FlushDropsBufferedWhenLegalityFails_DoesNotBuildScript()
        {
            var toggleLegality = new ToggleLegality { Legal = true };
            var factory = new RecordingScriptFactory();
            var director = new PresentationDirector(factory, null, null, toggleLegality);

            director.EnqueueMainline(new ScriptedStep(continueTicks: 1));
            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(new InputIntent("explore", 3), out preview));
            Assert.IsTrue(preview);
            Assert.IsTrue(director.HasBufferedIntent);

            toggleLegality.Legal = false;

            director.Tick(0.016f);
            director.Tick(0.016f);
            Assert.IsFalse(director.HasBufferedIntent);
            Assert.IsFalse(director.IsMainlineBusy);
            Assert.AreEqual(0, factory.Built.Count);
        }

        [Test]
        public void Intent_HardClear_DropsBufferAndMainline_OnPhaseDefeatOrLayer()
        {
            var director = new PresentationDirector(new RecordingScriptFactory());
            director.EnqueueMainline(new ScriptedStep(continueTicks: 5));
            bool preview;
            director.TrySubmitIntent(new InputIntent("pickup", 1), out preview);
            Assert.IsTrue(director.HasBufferedIntent);

            director.HardClearIntents(IntentClearReason.Defeat);
            Assert.AreEqual(IntentClearReason.Defeat, director.LastClearReason);
            Assert.IsFalse(director.HasBufferedIntent);
            Assert.IsFalse(director.IsMainlineBusy);

            director.EnqueueMainline(new ScriptedStep(continueTicks: 1));
            director.TrySubmitIntent(new InputIntent("pickup", 2), out preview);
            director.HardClearIntents(IntentClearReason.PhaseChange);
            Assert.AreEqual(IntentClearReason.PhaseChange, director.LastClearReason);
            Assert.IsFalse(director.HasBufferedIntent);

            director.EnqueueMainline(new ScriptedStep(continueTicks: 1));
            director.TrySubmitIntent(new InputIntent("pickup", 3), out preview);
            director.HardClearIntents(IntentClearReason.LayerChange);
            Assert.AreEqual(IntentClearReason.LayerChange, director.LastClearReason);
            Assert.IsFalse(director.IsMainlineBusy);
        }

        [Test]
        public void ParallelFork_WaitsForAllChildren()
        {
            var a = new ScriptedStep(continueTicks: 1);
            var b = new ScriptedStep(continueTicks: 3);
            var fork = new ParallelForkStep(new ITimelineStep[] { a, b });
            var timeline = new BattleTimeline();
            timeline.Enqueue(fork);

            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.016f));
            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.016f));
            Assert.AreEqual(TimelineStepStatus.Continue, timeline.Tick(0.016f));
            Assert.AreEqual(TimelineStepStatus.Finished, timeline.Tick(0.016f));
            Assert.AreEqual(2, a.TickCount);
            Assert.AreEqual(4, b.TickCount);
        }

        [Test]
        public void BypassLane_DoesNotCountAsMainlineBusy_ForInputMutex()
        {
            var factory = new RecordingScriptFactory(continueTicks: 3);
            var director = new PresentationDirector(factory);
            director.StartBypass(new ScriptedStep(continueTicks: 5));

            Assert.IsFalse(director.IsMainlineBusy);
            Assert.IsTrue(director.IsBypassBusy);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(new InputIntent("explore", 7), out preview));
            Assert.IsFalse(preview);
            Assert.AreEqual(1, factory.Built.Count);
            Assert.IsTrue(director.IsMainlineBusy);
            Assert.IsTrue(director.IsBypassBusy);

            director.Tick(0.016f);
            Assert.IsTrue(director.IsMainlineBusy);
            Assert.IsTrue(director.IsBypassBusy);
        }

        [Test]
        public void ExternalHold_KeepsMainlineBusy_UntilReleased()
        {
            var director = new PresentationDirector(new RecordingScriptFactory());
            Assert.IsTrue(director.TryBeginExternalHold("pickup"));
            Assert.IsTrue(director.IsMainlineBusy);
            Assert.IsFalse(director.TryBeginExternalHold("pickup-reentry"));

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(new InputIntent("explore", 1), out preview));
            Assert.IsTrue(preview);
            Assert.IsTrue(director.HasBufferedIntent);

            director.EndExternalHold("pickup");
            director.Tick(0.016f);
            Assert.IsFalse(director.HasBufferedIntent);
        }

        [Test]
        public void ExternalHold_WhenMainlineBusy_EnqueuesHoldThatSurvivesOuterStep()
        {
            var director = new PresentationDirector(new RecordingScriptFactory());
            director.EnqueueMainline(new ScriptedStep(continueTicks: 1));
            Assert.IsTrue(director.TryBeginExternalHold("drain-after-present"));
            Assert.IsTrue(director.IsMainlineBusy);
            Assert.IsTrue(director.HasExternalHold);

            director.Tick(0.016f); // ScriptedStep Continue
            director.Tick(0.016f); // ScriptedStep Finished → Hold step still busy
            Assert.IsTrue(director.IsMainlineBusy, "Hold step 须在外层 Present 结束后继续持 MainlineBusy");
            Assert.IsTrue(director.HasExternalHold);

            director.EndExternalHold("drain-after-present");
            director.Tick(0.016f);
            Assert.IsFalse(director.IsMainlineBusy);
            Assert.IsFalse(director.HasExternalHold);
        }

        private sealed class ScriptedStep : ITimelineStep
        {
            private readonly int mContinueTicks;
            private int mTicksSeen;

            public ScriptedStep(int continueTicks)
            {
                mContinueTicks = continueTicks;
            }

            public int TickCount
            {
                get { return mTicksSeen; }
            }

            public TimelineStepStatus Tick(float deltaTime)
            {
                mTicksSeen++;
                return mTicksSeen <= mContinueTicks
                    ? TimelineStepStatus.Continue
                    : TimelineStepStatus.Finished;
            }
        }

        private sealed class FakeBatchGate : IPresentationBatchGate
        {
            private int mNextId = 1;
            private readonly bool mAlwaysFail;

            public FakeBatchGate(bool alwaysFail = false)
            {
                mAlwaysFail = alwaysFail;
            }

            public bool HasOpenBatch { get; private set; }
            public int ActiveBatchId { get; private set; }

            public BatchOpenResult TryOpenNextBatch(out int batchId)
            {
                if (mAlwaysFail)
                {
                    batchId = 0;
                    return BatchOpenResult.Failed;
                }

                if (HasOpenBatch)
                {
                    batchId = 0;
                    return BatchOpenResult.WaitHasOpen;
                }

                batchId = mNextId++;
                ActiveBatchId = batchId;
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

        private sealed class RecordingTriggerSink : ITriggerPulseSink
        {
            public readonly List<string> Pulses = new List<string>();

            public void Pulse(string triggerId)
            {
                Pulses.Add(triggerId);
            }
        }

        private sealed class RecordingUiPickSink : IUiPickPreviewSink
        {
            public readonly List<InputIntent> Previews = new List<InputIntent>();

            public void Preview(InputIntent intent)
            {
                Previews.Add(intent);
            }
        }

        private sealed class ToggleLegality : IBufferedIntentLegality
        {
            public bool Legal = true;

            public bool IsStillLegal(InputIntent intent)
            {
                return Legal;
            }
        }

        private sealed class RecordingScriptFactory : IIntentScriptFactory
        {
            private readonly int mContinueTicks;
            public readonly List<InputIntent> Built = new List<InputIntent>();

            public RecordingScriptFactory(int continueTicks = 0)
            {
                mContinueTicks = continueTicks;
            }

            public void BuildScript(InputIntent intent, BattleTimeline timeline)
            {
                Built.Add(intent);
                timeline.Enqueue(new ScriptedStep(continueTicks: mContinueTicks));
            }
        }
    }
}
