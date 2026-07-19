using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Flow.Tests
{
    /// <summary>
    /// #8 洗回牌库：导演 Present 前缀拥有洗回表演；解算后仅入 sink，Present 前不旁路开播。
    /// </summary>
    public sealed class ShuffleVerticalSliceTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IPresentationSyncSystem mSync;
        private CoreCommandDispatcher mDispatcher;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(
                ContentConfigKeys.DefaultCatalog,
                TableNineContentCatalog.CreateDefault());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mSync = mArch.GetSystem<IPresentationSyncSystem>();
            mDispatcher = new CoreCommandDispatcher(mArch);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void UseItemIntent_Teleport_ShuffleOwnedByDirectorPresent_BeforeAck()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var targetUid = mArch.GetModel<BoardModel>().GetCardUid(sAdjacentSlot);
            Assert.Greater(targetUid, 0);
            var teleportUid = SpawnHelpIntoItemSlots("help.teleport_card");

            var shuffleSink = new ShuffleIntoDeckPresentSink();
            var useInner = new RecordingPresentChannel(ticksUntilComplete: 1);
            // 2 tick：与 PresentStep 同帧 Begin+Tick 错开，便于断言「先洗回、后内层」。
            var usePresent = new ShufflePrefixedPresentChannel(shuffleSink, useInner, 2);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new UseItemIntentScriptFactory(
                mArch,
                mDispatcher,
                usePresent,
                boardPresent,
                onUseBatchProjected: (startIndex, _, __) =>
                {
                    ShuffleIntoDeckLockstep.EnqueueFromEventLog(
                        shuffleSink,
                        mArch,
                        startIndex,
                        _ => false);
                });
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.UseItem, teleportUid, new[] { targetUid }, null),
                out preview));
            Assert.IsFalse(preview);
            Assert.IsTrue(director.IsMainlineBusy);

            // Resolve ApplyUseItem：洗回写入 EventLog，仅入 sink，尚未 Present
            var useStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(1, mSync.ActiveBatchId);
            Assert.IsTrue(
                ShuffleIntoDeckLockstep.HasShuffleExistingSince(mPipeline, useStart),
                "传送卡解算须产生 ExistingCard 洗回事件");
            Assert.Greater(shuffleSink.PendingCount, 0, "洗回应入导演 sink，而非旁路开播");
            Assert.AreEqual(0, usePresent.ShuffleBeginCount);
            Assert.AreEqual(0, useInner.BeginCount);

            // Present tick1：前缀开洗回（2 tick），内层未开，批次未 ack
            director.Tick(0.016f);
            Assert.AreEqual(1, usePresent.ShuffleBeginCount);
            Assert.AreEqual(0, useInner.BeginCount);
            Assert.AreEqual(1, mSync.ActiveBatchId, "洗回未完成前不得 ack 下一批");
            Assert.IsTrue(director.IsMainlineBusy);

            // Present tick2：洗回结束 → 内层 Begin，仍未 ack
            director.Tick(0.016f);
            Assert.AreEqual(0, shuffleSink.PendingCount);
            Assert.AreEqual(1, useInner.BeginCount);
            Assert.AreEqual(1, mSync.ActiveBatchId);

            // Present tick3：内层完成 → ack → 非击杀分支；传送留空位时 #9 再入队 DrainRefill
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            director.Tick(0.016f); // without-kill branch
            for (var i = 0; i < 6; i++)
            {
                if (!director.IsMainlineBusy)
                {
                    break;
                }

                director.Tick(0.016f);
            }

            Assert.IsFalse(director.IsMainlineBusy);
            // 洗回已在 Use Present 前缀完成；传送留空后的 drain 补牌属 #9 独立批（可 0/1 次 Present）。
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

        private int SpawnHelpIntoItemSlots(string defId)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var deck = mArch.GetModel<DeckModel>();
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

        private void PlaceSoleBoardCardAt(SlotId targetSlot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            CardInstance sole = null;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == board.AvatarSlot.Value)
                {
                    continue;
                }

                var uid = board.GetCardUid(slot);
                if (uid == 0)
                {
                    continue;
                }

                Assert.IsNull(sole, "Expected at most one non-avatar board card for relocate helper.");
                sole = registry.Get(uid);
            }

            Assert.IsNotNull(sole, "No board card to relocate.");
            if (sole.Slot.Value == targetSlot)
            {
                return;
            }

            board.ClearSlot(sole.Slot.Value);
            board.PlaceCard(sole, targetSlot);
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
