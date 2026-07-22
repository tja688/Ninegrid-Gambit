using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Queries;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.Shuffle
{
    /// <summary>
    /// V5：EnqueueShuffleIntoDeckFromEventLogCommand / HasShuffleExistingSinceQuery / Prefixed Present 接缝。
    /// </summary>
    public sealed class EnqueueShuffleIntoDeckFromEventLogCommandTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        [Test]
        public void UseItemIntent_Teleport_ShuffleOwnedByDirectorPresent_BeforeAck()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);
                var targetUid = arch.Board.GetCardUid(sAdjacentSlot);
                Assert.Greater(targetUid, 0);
                var teleportUid = SpawnHelpIntoItemSlots(arch, "help.teleport_card");

                var shuffleSink = new ShuffleIntoDeckPresentSink();
                var scheduler = new ShuffleIntoDeckScheduler();
                var useInner = new RecordingPresentChannel(ticksUntilComplete: 1);
                var usePresent = new ShufflePrefixedPresentChannel(shuffleSink, useInner, 2, scheduler);
                var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var factory = new UseItemIntentScriptFactory(
                    arch.Architecture,
                    arch.Dispatcher,
                    usePresent,
                    boardPresent,
                    onUseBatchProjected: (startIndex, _, __) =>
                    {
                        arch.Architecture.SendCommand(
                            new EnqueueShuffleIntoDeckFromEventLogCommand(
                                shuffleSink,
                                startIndex,
                                _ => false,
                                scheduler));
                    });

                using (var runtime = PresentationRuntimeFixture.Install(arch, factory))
                {
                    Assert.IsTrue(arch.Architecture.SendCommand(
                        new SubmitUseItemIntentCommand(teleportUid, new[] { targetUid }, null)));
                    Assert.IsTrue(runtime.MainlineBusy.Value);

                    var useStart = arch.Pipeline.EventLog.Entries.Count;
                    runtime.Tick();
                    Assert.AreEqual(1, arch.Sync.ActiveBatchId);
                    Assert.IsTrue(
                        arch.Architecture.SendQuery(new HasShuffleExistingSinceQuery(useStart, scheduler)),
                        "传送卡解算须产生 ExistingCard 洗回事件");
                    Assert.Greater(shuffleSink.PendingCount, 0, "洗回应入导演 sink，而非旁路开播");
                    Assert.AreEqual(0, usePresent.ShuffleBeginCount);
                    Assert.AreEqual(0, useInner.BeginCount);

                    runtime.Tick();
                    Assert.AreEqual(1, usePresent.ShuffleBeginCount);
                    Assert.AreEqual(0, useInner.BeginCount);
                    Assert.AreEqual(1, arch.Sync.ActiveBatchId, "洗回未完成前不得 ack 下一批");
                    Assert.IsTrue(runtime.MainlineBusy.Value);

                    runtime.Tick();
                    Assert.AreEqual(0, shuffleSink.PendingCount);
                    Assert.AreEqual(1, useInner.BeginCount);
                    Assert.AreEqual(1, arch.Sync.ActiveBatchId);

                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    for (var i = 0; i < 8; i++)
                    {
                        if (!runtime.MainlineBusy.Value)
                        {
                            break;
                        }

                        runtime.Tick();
                    }

                    Assert.IsFalse(runtime.MainlineBusy.Value);
                }
            }
        }

        [Test]
        public void Command_EnqueuesCollectedEntriesIntoSink()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);
                var targetUid = arch.Board.GetCardUid(sAdjacentSlot);
                var teleportUid = SpawnHelpIntoItemSlots(arch, "help.teleport_card");

                var useStart = arch.Pipeline.EventLog.Entries.Count;
                var dispatch = arch.Dispatcher.Send(
                    new ApplyUseItemCommand(teleportUid, new[] { targetUid }, null));
                Assert.IsTrue(dispatch.Accepted);

                var sink = new ShuffleIntoDeckPresentSink();
                var enqueued = arch.Architecture.SendCommand(
                    new EnqueueShuffleIntoDeckFromEventLogCommand(sink, useStart, _ => false));
                Assert.Greater(enqueued, 0);
                Assert.AreEqual(enqueued, sink.PendingCount);
                Assert.IsTrue(arch.Architecture.SendQuery(new HasShuffleExistingSinceQuery(useStart)));
            }
        }

        [Test]
        public void ShufflePrefixedPresentChannel_NoPending_StartsInnerImmediately()
        {
            var sink = new ShuffleIntoDeckPresentSink();
            var inner = new RecordingPresentChannel(ticksUntilComplete: 1);
            var channel = new ShufflePrefixedPresentChannel(sink, inner, 1);

            channel.Begin(7);
            Assert.AreEqual(0, channel.ShuffleBeginCount);
            Assert.AreEqual(1, inner.BeginCount);
            Assert.IsFalse(channel.IsComplete);

            channel.Tick(0.016f);
            Assert.IsTrue(channel.IsComplete);
        }

        private static int SpawnHelpIntoItemSlots(PresentationArchitectureFixture arch, string defId)
        {
            arch.Pipeline.Enqueue(
                new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(arch.Pipeline.RunToCompletion(), 0);
            var deck = arch.Architecture.GetModel<DeckModel>();
            Assert.Greater(deck.ItemSlotUids.Count, 0);
            return deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }

        private sealed class RecordingPresentChannel : IPresentChannel
        {
            private readonly int mTicksUntilComplete;
            private int mTicks;
            private bool mBegan;

            public RecordingPresentChannel(int ticksUntilComplete)
            {
                mTicksUntilComplete = ticksUntilComplete;
            }

            public int BeginCount { get; private set; }
            public int ActiveBatchId { get; private set; }

            public bool IsComplete
            {
                get { return mBegan && mTicks >= mTicksUntilComplete; }
            }

            public void Begin(int batchId)
            {
                mBegan = true;
                mTicks = 0;
                ActiveBatchId = batchId;
                BeginCount++;
            }

            public void Tick(float deltaTime)
            {
                if (mBegan)
                {
                    mTicks++;
                }
            }
        }
    }
}
