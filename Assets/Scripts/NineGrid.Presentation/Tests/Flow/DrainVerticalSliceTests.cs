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
    /// #9 drain 补牌 refill：非击杀移除后退场补牌升格为独立 Resolve/Present 批次，禁止 in-flight FillEmptySlots。
    /// </summary>
    public sealed class DrainVerticalSliceTests
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
        public void UseItemIntent_NonKillBoardRemove_Lockstep_DrainRefillIsSeparateBatchAfterUsePresent()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0, armor: 3)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var targetUid = mArch.GetModel<BoardModel>().GetCardUid(sAdjacentSlot);
            Assert.Greater(targetUid, 0);
            SeedDrawPileFiller("monster.test", count: 8);
            var kidnapUid = SpawnHelpIntoItemSlots("help.kidnapping");

            var usePresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new UseItemIntentScriptFactory(mArch, mDispatcher, usePresent, boardPresent);
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.UseItem, kidnapUid, new[] { targetUid }, null),
                out preview));
            Assert.IsFalse(preview);
            Assert.IsTrue(director.IsMainlineBusy);

            // Resolve ApplyUseItem：移除盘面怪，不得 in-flight FillEmptySlots
            var useStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(1, mSync.ActiveBatchId);
            Assert.IsTrue(ContainsTypeSince(useStart, CoreEventType.CardRemoved),
                "绑票应 RemoveCard 盘面怪");
            Assert.IsFalse(ContainsTypeSince(useStart, CoreEventType.CardKilled),
                "绑票移除不是击杀路径");
            Assert.IsFalse(ContainsTypeSince(useStart, CoreEventType.SlotsFilled),
                "drain 补牌不得在 Use 解算批内 in-flight 写入");
            Assert.IsTrue(FusionRefillPlanner.HasEmptyBoardSlot(mArch.GetModel<BoardModel>()),
                "移除后盘面应有空位");

            // Present use ack
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(1, usePresent.BeginCount);

            // Kill branch（非击杀空过）
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(0, boardPresent.BeginCount);

            // Use-batch FusionRefill 门（无融合空过）
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(0, boardPresent.BeginCount);

            // DrainRefill branch：入队补牌批（本 Tick 只入队）
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(0, boardPresent.BeginCount);

            // Resolve DrainRefill：离散 SlotsFilled 批次
            var refillStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(2, mSync.ActiveBatchId);
            Assert.IsTrue(ContainsTypeSince(refillStart, CoreEventType.SlotsFilled),
                "drain 补牌须在独立 Resolve 批次写入 Core");

            // Present refill ack → idle
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(1, boardPresent.BeginCount);
            Assert.IsFalse(director.IsMainlineBusy);
        }

        [Test]
        public void UseItemIntent_Kill_DoesNotEnqueueDrainRefill_FillHandlesSlots()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var targetUid = mArch.GetModel<BoardModel>().GetCardUid(sAdjacentSlot);
            SeedDrawPileFiller("monster.test", count: 8);
            var knifeUid = SpawnHelpIntoItemSlots("help.throwing_knife");

            var usePresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new UseItemIntentScriptFactory(mArch, mDispatcher, usePresent, boardPresent);
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.UseItem, knifeUid, new[] { targetUid }, null),
                out preview));

            // use resolve + present + kill branch（入队 Fill/Rotate）+ use-fusion 门空过 + drain 门空过
            director.Tick(0.016f);
            director.Tick(0.016f);
            director.Tick(0.016f);
            director.Tick(0.016f);
            director.Tick(0.016f);

            // Fill resolve：击杀补牌走 ResolvePostKillFill，不是 DrainRefill
            var fillStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(2, mSync.ActiveBatchId);
            Assert.IsTrue(ContainsTypeSince(fillStart, CoreEventType.SlotsFilled));

            // fill present + rotate resolve/present + fusion branch → idle
            for (var i = 0; i < 8; i++)
            {
                director.Tick(0.016f);
                if (!director.IsMainlineBusy)
                {
                    break;
                }
            }

            Assert.IsFalse(director.IsMainlineBusy);
            Assert.AreEqual(2, boardPresent.BeginCount, "击杀路径仅 Fill/Rotate 两次板 Present，无额外 drain refill Present");
        }

        private int SpawnHelpIntoItemSlots(string defId)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var deck = mArch.GetModel<DeckModel>();
            Assert.Greater(deck.ItemSlotUids.Count, 0);
            return deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];
        }

        private void SeedDrawPileFiller(string defId, int count)
        {
            for (var i = 0; i < count; i++)
            {
                mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.Monster, ZoneId.DrawPile, SlotId.None, 1, "test"));
            }

            Assert.Greater(mPipeline.RunToCompletion(), 0);
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

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack, int armor = 0)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster)
            {
                MaxHp = hp,
                Attack = attack,
                Armor = armor,
            });
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
