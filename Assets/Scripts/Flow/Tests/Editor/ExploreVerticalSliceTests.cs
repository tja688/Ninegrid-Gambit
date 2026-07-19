using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Flow.Tests
{
    /// <summary>
    /// #4 空格 explore 垂直切片：导演缝上 ClickEmpty → Present → ResolvePostKill → Present 批次锁步。
    /// </summary>
    public sealed class ExploreVerticalSliceTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sFarCornerSlot = SlotId.Board(1);

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
        public void ExploreIntent_Lockstep_ClickEmptyThenPostKill_AcksBetweenBatches()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sFarCornerSlot);
            Assert.IsTrue(mArch.GetModel<BoardModel>().IsEmpty(sAdjacentSlot));

            var present = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new ExploreIntentScriptFactory(mArch, mDispatcher, present);
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(new InputIntent(InputIntentKinds.Explore, 2), out preview));
            Assert.IsFalse(preview);
            Assert.IsTrue(director.IsMainlineBusy);

            // Resolve ClickEmpty（本拍只解算，尚未 Present）
            director.Tick(0.016f);
            Assert.AreEqual(1, mSync.ActiveBatchId);
            Assert.AreEqual(0, present.BeginCount);
            Assert.IsFalse(ContainsBoardRotatedSince(0));

            // Present ack batch 1
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(1, present.BeginCount);
            Assert.AreEqual(1, present.PresentedBatchIds[0]);

            // Resolve PostKill
            director.Tick(0.016f);
            Assert.AreEqual(2, mSync.ActiveBatchId);
            Assert.AreEqual(1, present.BeginCount);
            Assert.IsTrue(ContainsBoardRotatedSince(0));

            // Present ack batch 2 → idle
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(2, present.BeginCount);
            Assert.AreEqual(2, present.PresentedBatchIds[1]);
            Assert.IsFalse(director.IsMainlineBusy);
        }

        [Test]
        public void ExploreScriptFactory_IgnoresNonExploreIntent()
        {
            var present = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new ExploreIntentScriptFactory(mArch, mDispatcher, present);
            var timeline = new BattleTimeline();
            factory.BuildScript(new InputIntent("pickup", 2), timeline);
            Assert.IsFalse(timeline.IsBusy);
        }

        [Test]
        public void Dispatcher_RejectedCommand_DoesNotOpenBatch()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            var result = mDispatcher.Send(new ClickEmptyCommand(sFarCornerSlot));
            Assert.IsFalse(result.Accepted);
            Assert.IsFalse(result.BatchOpened);
            Assert.AreEqual(0, mSync.ActiveBatchId);
        }

        private bool ContainsBoardRotatedSince(int startIndex)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.BoardRotated)
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>
        /// 假表演通道：Begin 后 N tick 完成；记录已播批次，供锁步断言。
        /// Ack 由 PresentStep 调 gate.TryAcknowledge，此处只记 Begin 次序。
        /// </summary>
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
            public readonly List<int> PresentedBatchIds = new List<int>();
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
                PresentedBatchIds.Add(batchId);
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
