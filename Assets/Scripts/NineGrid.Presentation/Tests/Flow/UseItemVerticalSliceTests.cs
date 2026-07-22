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
using NineGrid.Flow;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #6 用牌/帮助卡：ApplyUseItem → Present → Fill → Present → Rotate → Present 批次锁步。
    /// </summary>
    public sealed class UseItemVerticalSliceTests
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
        public void UseItemIntent_Kill_Lockstep_UseThenFillThenRotate_AcksBetweenBatches()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var targetUid = mArch.GetModel<BoardModel>().GetCardUid(sAdjacentSlot);
            Assert.Greater(targetUid, 0);
            var knifeUid = SpawnHelpIntoItemSlots("help.throwing_knife");

            var usePresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new UseItemIntentScriptFactory(mArch, mDispatcher, usePresent, boardPresent);
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.UseItem, knifeUid, new[] { targetUid }, null),
                out preview));
            Assert.IsFalse(preview);
            Assert.IsTrue(director.IsMainlineBusy);

            // Resolve ApplyUseItem
            var useStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(1, mSync.ActiveBatchId);
            Assert.AreEqual(0, usePresent.BeginCount);
            Assert.IsFalse(ContainsTypeSince(useStart, CoreEventType.BoardRotated));
            Assert.IsTrue(ContainsTypeSince(useStart, CoreEventType.CardKilled));

            // Present use ack
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(1, usePresent.BeginCount);

            // Kill branch enqueues aftermath（本 Tick 只入队，不推进 Fill）
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(0, boardPresent.BeginCount);

            // Drain refill branch（击杀路径 needsDrainRefill=false，空过）
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(0, boardPresent.BeginCount);

            // Resolve Fill
            var fillStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(2, mSync.ActiveBatchId);
            Assert.IsTrue(ContainsTypeSince(fillStart, CoreEventType.SlotsFilled));

            // Present fill ack
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(1, boardPresent.BeginCount);

            // Resolve Rotate
            var rotateStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(3, mSync.ActiveBatchId);
            Assert.IsTrue(ContainsTypeSince(rotateStart, CoreEventType.BoardRotated));

            // Present rotate ack
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(2, boardPresent.BeginCount);

            // Fusion aftermath branch（无融合则空过）→ idle
            director.Tick(0.016f);
            Assert.IsFalse(director.IsMainlineBusy);
        }

        [Test]
        public void UseItemIntent_NonKill_DoesNotEnqueueFillRotate()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var targetUid = mArch.GetModel<BoardModel>().GetCardUid(sAdjacentSlot);
            var knifeUid = SpawnHelpIntoItemSlots("help.throwing_knife");

            var resolvedWithoutKill = false;
            var usePresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new UseItemIntentScriptFactory(
                mArch,
                mDispatcher,
                usePresent,
                boardPresent,
                onUseBatchProjected: null,
                onBoardBatchProjected: null,
                onResolvedWithoutKill: () => resolvedWithoutKill = true);
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.UseItem, knifeUid, new[] { targetUid }, null),
                out preview));

            var useStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f); // resolve use
            director.Tick(0.016f); // present use
            director.Tick(0.016f); // kill branch → without kill
            director.Tick(0.016f); // drain refill branch（无空位则空过）

            Assert.IsTrue(resolvedWithoutKill);
            Assert.AreEqual(0, boardPresent.BeginCount);
            Assert.IsFalse(ContainsTypeSince(useStart, CoreEventType.BoardRotated));
            Assert.IsFalse(ContainsTypeSince(useStart, CoreEventType.SlotsFilled));
            Assert.IsFalse(director.IsMainlineBusy);
        }

        [Test]
        public void UseItemScriptFactory_IgnoresNonUseItemIntent()
        {
            var usePresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new UseItemIntentScriptFactory(mArch, mDispatcher, usePresent, boardPresent);
            var timeline = new BattleTimeline();
            factory.BuildScript(new InputIntent(InputIntentKinds.Attack, 2), timeline);
            Assert.IsFalse(timeline.IsBusy);
        }

        private int SpawnHelpIntoItemSlots(string defId)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var deck = mArch.GetModel<DeckModel>();
            Assert.Greater(deck.ItemSlotUids.Count, 0);
            return deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];
        }

        private bool ContainsTypeSince(int startIndex, CoreEventType type)
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
