using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using QFramework;
using NineGrid.Flow;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #4 空格 explore 垂直切片：导演缝上 ClickEmpty → Present → Fill → Present → Rotate → Present 批次锁步。
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
            OccupancyForceSyncGuard.ResetForTests();
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
            OccupancyForceSyncGuard.ResetForTests();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void ExploreIntent_Lockstep_ClickEmptyThenStabilizeThenRotate_AcksBetweenBatches()
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
            var clickStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(1, mSync.ActiveBatchId);
            Assert.AreEqual(0, present.BeginCount);
            Assert.IsFalse(ContainsEventTypeSince(clickStart, CoreEventType.BoardRotated));
            Assert.IsFalse(ContainsEventTypeSince(clickStart, CoreEventType.SlotsFilled));

            // Present ack batch 1
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(1, present.BeginCount);
            Assert.AreEqual(1, present.PresentedBatchIds[0]);

            // Resolve interaction advance
            var interactStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(2, mSync.ActiveBatchId);
            Assert.AreEqual(1, present.BeginCount);
            Assert.IsTrue(ContainsEventTypeSince(interactStart, CoreEventType.InteractionChanged));

            // Present ack batch 2
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(2, present.BeginCount);

            // Core stable-state check only schedules the due slice; it does not resolve ahead of ack.
            var fillStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.IsFalse(ContainsEventTypeSince(fillStart, CoreEventType.SlotsFilled));

            // Resolve first stabilization slice.
            director.Tick(0.016f);
            Assert.AreEqual(3, mSync.ActiveBatchId);
            Assert.AreEqual(2, present.BeginCount);
            Assert.IsTrue(ContainsEventTypeSince(fillStart, CoreEventType.SlotsFilled));

            // Present ack batch 3
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(3, present.BeginCount);

            // Stable check closes pre-rotate stabilization, then Rotate resolves.
            var rotateStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            director.Tick(0.016f);
            Assert.AreEqual(4, mSync.ActiveBatchId);
            Assert.IsTrue(ContainsEventTypeSince(rotateStart, CoreEventType.BoardRotated));

            // Present ack batch 4
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(4, present.BeginCount);
            Assert.AreEqual(4, present.PresentedBatchIds[3]);

            // Post-rotate stabilization and no-enemy checks are empty Core-owned checks.
            for (var i = 0; i < 4 && director.IsMainlineBusy; i++)
            {
                director.Tick(0.016f);
            }
            Assert.IsFalse(director.IsMainlineBusy);
            Assert.AreEqual(
                0,
                OccupancyForceSyncGuard.InvocationCount,
                "#10 idle 锁步路径不得触发占格强制对账断言");
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

        [Test]
        public void ExploreIntent_ClickEmptyRejected_AbortsMainline_DoesNotStickBusy()
        {
            // 非法空格（远角）→ Resolve ClickEmpty dispatchReject → 必须中止，不可永久 busy。
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            Assert.IsTrue(mArch.GetModel<BoardModel>().IsEmpty(sFarCornerSlot));

            var present = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new ExploreIntentScriptFactory(mArch, mDispatcher, present);
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Explore, sFarCornerSlot.Index),
                out preview));
            Assert.IsTrue(director.IsMainlineBusy);

            director.Tick(0.016f);
            Assert.IsFalse(director.IsMainlineBusy);
            Assert.AreEqual(0, present.BeginCount);
            Assert.AreEqual(0, mSync.ActiveBatchId);
        }

        private bool ContainsEventTypeSince(int startIndex, CoreEventType type)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
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
