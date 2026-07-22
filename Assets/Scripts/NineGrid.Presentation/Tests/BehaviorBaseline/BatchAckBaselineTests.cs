using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.BehaviorBaseline
{
    /// <summary>
    /// #30 行为基线：Batch 未 ack 不得推进下一批 Resolve。
    /// 接缝：RuntimeSystem → Director → ResolveBatch/PresentStep → IPresentChannel → FinishBatch。
    /// </summary>
    public sealed class BatchAckBaselineTests
    {
        [Test]
        public void Runtime_SecondResolveWaits_UntilPresentAcks()
        {
            var gate = new FakeBatchGate();
            var present = new RecordingPresentChannel(ticksUntilComplete: 2);
            var factory = new LockstepScriptFactory(gate, present, batchCount: 2);

            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(arch, factory))
            {
                bool preview;
                Assert.IsTrue(runtime.TrySubmitIntent(new InputIntent("lockstep", 0), out preview));
                Assert.IsTrue(runtime.MainlineBusy.Value);

                runtime.Tick(); // Resolve batch 1
                Assert.AreEqual(1, gate.ActiveBatchId);
                Assert.AreEqual(0, present.BeginCount);

                runtime.Tick(); // Present tick 1/2
                Assert.AreEqual(1, present.BeginCount);
                Assert.IsTrue(gate.HasOpenBatch);

                runtime.Tick(); // Present tick 2/2 → ack
                Assert.IsFalse(gate.HasOpenBatch);

                runtime.Tick(); // Resolve batch 2
                Assert.AreEqual(2, gate.ActiveBatchId);

                runtime.TickUntilIdle();
                Assert.IsFalse(runtime.MainlineBusy.Value);
                Assert.AreEqual(2, present.BeginCount);
            }
        }

        [Test]
        public void SyncGate_EmptyNonLockingBatch_StillBlocksUntilFinishBatch()
        {
            var session = new SyncSessionStub();
            var resolveCount = 0;
            var gate = PresentationSyncGateFactory.FromSession(session, () =>
            {
                resolveCount++;
                var batch = new PresentationBatch(
                    session.NextBatchId(),
                    System.Array.Empty<PresentationInstruction>(),
                    null);
                session.OpenBatch(batch);
                return new CoreCommandDispatchResult(
                    CoreCommandResult.Accept(0),
                    batch,
                    true);
            });

            int firstId;
            Assert.AreEqual(BatchOpenResult.Opened, gate.TryOpenNextBatch(out firstId));
            Assert.AreEqual(1, resolveCount);

            int blocked;
            Assert.AreEqual(BatchOpenResult.WaitHasOpen, gate.TryOpenNextBatch(out blocked));
            Assert.AreEqual(1, resolveCount);

            Assert.IsTrue(gate.TryAcknowledge(firstId));
            Assert.IsFalse(gate.HasOpenBatch);

            int secondId;
            Assert.AreEqual(BatchOpenResult.Opened, gate.TryOpenNextBatch(out secondId));
            Assert.AreEqual(2, resolveCount);
            Assert.AreEqual(2, secondId);
        }
    }
}
