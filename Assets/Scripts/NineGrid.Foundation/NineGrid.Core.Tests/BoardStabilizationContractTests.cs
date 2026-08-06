using NineGrid.Content;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    public sealed class BoardStabilizationContractTests
    {
        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(
                ContentConfigKeys.DefaultCatalog,
                ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 34UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void RefillTriggerCreatesVacancy_ResolvesAsAckableSlicesUntilStable()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            SpawnBoardCard("trap.bear_trap", CardKind.Trap, 2);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 3);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 4);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 6);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 7);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 8);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 9);
            SpawnDrawCard("help.bomb", CardKind.HelpCard);
            SpawnDrawCard("help.bomb", CardKind.HelpCard);

            var stabilization = mArch.GetSystem<IBoardStabilizationSystem>();
            var eventStart = mPipeline.EventLog.Entries.Count;
            Assert.IsTrue(stabilization.NeedsRefill);

            Assert.IsTrue(stabilization.ResolveNextSlice().Accepted);
            Assert.IsTrue(
                stabilization.NeedsRefill,
                "捕熊在首轮 Fill 的入场触发中制造的新空位必须形成下一稳定化切片");

            Assert.IsTrue(stabilization.ResolveNextSlice().Accepted);
            Assert.IsFalse(stabilization.NeedsRefill);
            Assert.AreEqual(0, mArch.GetModel<DeckModel>().DrawPileUids.Count);
            Assert.AreEqual(2, CountEventsSince(eventStart, CoreEventType.SlotsFilled));
            Assert.AreEqual("help.bomb", CardAt(1).DefId);
            Assert.AreEqual(0, mArch.GetModel<BoardModel>().GetCardUid(SlotId.Board(2)));
        }

        [Test]
        public void StartNode_RecentersAvatarBeforeOpeningLayout()
        {
            var board = mArch.GetModel<BoardModel>();
            var avatar = mArch.GetModel<CardRegistry>().Get(board.AvatarUid.Value);
            board.SetAvatar(avatar, SlotId.Board(2));

            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            Assert.AreEqual(SlotId.Board(5), board.AvatarSlot.Value);
        }

        [Test]
        public void FusionResult_IsDeferredWithinStabilization_ThenRestoredToDrawPileTop()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            SpawnBoardCard("help.bomb", CardKind.HelpCard, 2);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 3);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 4);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 6);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 7);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 8);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 9);
            SpawnDrawCard("help.bomb", CardKind.HelpCard);
            mPipeline.Enqueue(new ShuffleIntoDrawPileAction(
                "monster.giant_skeleton",
                CardKind.Monster,
                1,
                top: true,
                cause: "fusion.test",
                deferDuringBoardStabilization: true));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var deck = mArch.GetModel<DeckModel>();
            var registry = mArch.GetModel<CardRegistry>();
            Assert.AreEqual("monster.giant_skeleton", registry.Get(deck.DrawPileUids[0]).DefId);

            var stabilization = mArch.GetSystem<IBoardStabilizationSystem>();
            Assert.IsTrue(stabilization.ResolveNextSlice().Accepted);
            Assert.IsFalse(stabilization.NeedsRefill);
            Assert.AreEqual("help.bomb", CardAt(1).DefId);
            Assert.AreEqual(1, deck.DrawPileUids.Count);
            Assert.AreEqual("monster.giant_skeleton", registry.Get(deck.DrawPileUids[0]).DefId);
        }

        private void SpawnBoardCard(string defId, CardKind kind, int slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(
                defId,
                kind,
                ZoneId.Board,
                SlotId.Board(slot),
                1,
                "boardStabilization.test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
        }

        private void SpawnDrawCard(string defId, CardKind kind)
        {
            mPipeline.Enqueue(new SpawnCardAction(
                defId,
                kind,
                ZoneId.DrawPile,
                SlotId.None,
                1,
                "boardStabilization.test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
        }

        private CardInstance CardAt(int slot)
        {
            var uid = mArch.GetModel<BoardModel>().GetCardUid(SlotId.Board(slot));
            Assert.Greater(uid, 0);
            return mArch.GetModel<CardRegistry>().Get(uid);
        }

        private int CountEventsSince(int startIndex, CoreEventType type)
        {
            var count = 0;
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
