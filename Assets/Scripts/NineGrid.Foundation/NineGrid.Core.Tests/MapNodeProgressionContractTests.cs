using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// ADR-0021 / #84：跑图节点编排、清关结算、困难房门槛、通关。
    /// </summary>
    public sealed class MapNodeProgressionContractTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IRewardSystem mReward;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildTestCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 17UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mReward = mArch.GetSystem<IRewardSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void NodesPerFloor_IsEight()
        {
            Assert.AreEqual(8, RunModel.NodesPerFloor);
            Assert.AreEqual(3, RunModel.FinalFloor);
        }

        [Test]
        public void Schedule_CoversFullTable()
        {
            AssertSchedule(0, 1, NodeRoomSource.RandomBattle, true, NodeOfferFamily.BattleRooms, false);
            AssertSchedule(1, 2, NodeRoomSource.PreviousChoice, true, NodeOfferFamily.BattleRooms, false);
            AssertSchedule(2, 3, NodeRoomSource.PreviousChoice, true, NodeOfferFamily.ConsumerRooms, false);
            AssertSchedule(3, 4, NodeRoomSource.PreviousChoice, false, NodeOfferFamily.Leave, false);
            AssertSchedule(4, 5, NodeRoomSource.RandomBattle, true, NodeOfferFamily.BattleRooms, true);
            AssertSchedule(5, 6, NodeRoomSource.PreviousChoice, true, NodeOfferFamily.SpecialRooms, true);
            AssertSchedule(6, 7, NodeRoomSource.PreviousChoice, false, NodeOfferFamily.Leave, false);
            AssertSchedule(7, 8, NodeRoomSource.Boss, true, NodeOfferFamily.GoDown, false);
        }

        [Test]
        public void StartNode_NonCombatNodes_DoNotEnterInteractionLoop()
        {
            var run = mArch.GetModel<RunModel>();
            run.NodeIndex.Value = 3;
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(1, 0)).Accepted);
            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.Navigation, mArch.GetModel<PendingChoiceModel>().Kind.Value);
            Assert.AreEqual(NavigationKind.Leave, mArch.GetModel<PendingChoiceModel>().NavigationOffer.Value);
            Assert.IsFalse(mPhase.CanExecute(GameCommandKind.Attack));

            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildTestCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 17UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            run = mArch.GetModel<RunModel>();
            run.NodeIndex.Value = 6;
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(1, 0)).Accepted);
            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);
            Assert.AreNotEqual(GamePhase.InteractionLoop, mPhase.CurrentPhase);
        }

        [Test]
        public void CompleteNodeIfCleared_SkipsHelpChoice_OffersRoomFamily()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            Assert.IsTrue(mPhase.Attack(sAdjacentSlot).Accepted);

            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.Room, mArch.GetModel<PendingChoiceModel>().Kind.Value);
            Assert.AreEqual(2, mArch.GetModel<PendingChoiceModel>().RoomOptions.Count);
            Assert.IsFalse(mPhase.CanExecute(GameCommandKind.SelectReward));
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.SelectRoom));
        }

        [Test]
        public void TryCompleteClearedNode_WhenCleared_OffersRoomChoice()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            // 模拟作弊清场：直接移走唯一怪并清空敌池/抽牌。
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var uid = board.GetCardUid(sAdjacentSlot);
            Assert.Greater(uid, 0);
            board.ClearSlot(sAdjacentSlot);
            if (registry.TryGet(uid, out var card))
            {
                card.Zone.Value = ZoneId.None;
                card.Slot.Value = SlotId.None;
            }

            while (deck.DrawPileUids.Count > 0)
            {
                deck.RemoveUid(deck.DrawPileUids[0]);
            }

            while (deck.EnemyCardPoolUids.Count > 0)
            {
                deck.RemoveUid(deck.EnemyCardPoolUids[0]);
            }

            Assert.IsTrue(mArch.GetSystem<IDeckSystem>().IsNodeCleared());
            Assert.IsTrue(mPhase.TryCompleteClearedNode().Accepted);
            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.Room, mArch.GetModel<PendingChoiceModel>().Kind.Value);
            Assert.AreNotEqual("help.choice", mArch.GetModel<PendingChoiceModel>().PoolId.Value);
        }

        [Test]
        public void SettleUnusedHelpCards_SkipsItemSlots_PreservesAcrossClear_AndClearsTraps()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            SpawnHelpIntoItemSlots("help.settle_gold");
            SpawnTrapAt(SlotId.Board(3));

            var player = mArch.GetModel<PlayerModel>();
            var coinsBefore = player.Coins.Value;
            Assert.IsTrue(mPhase.Attack(sAdjacentSlot).Accepted);

            Assert.AreEqual(coinsBefore, player.Coins.Value, "道具卡格不应因清关兑金");
            Assert.AreEqual(0, CountBoardTraps(), "残留机关应同拍清场");
            Assert.AreEqual(1, CountItemSlotHelpCards(), "清关后道具卡格应保留");
        }

        [Test]
        public void RollPostClearRoomChoices_EliteOnlyOnNodesFiveAndSix()
        {
            for (var seed = 1UL; seed <= 40UL; seed++)
            {
                ResetWithSeed(seed);
                var early = mReward.RollPostClearRoomChoices(0);
                Assert.IsFalse(ContainsRoom(early, RoomKind.Elite), "节点1清关不应放出困难房");
            }

            var sawElite = false;
            for (var seed = 1UL; seed <= 80UL; seed++)
            {
                ResetWithSeed(seed);
                var late = mReward.RollPostClearRoomChoices(4);
                if (ContainsRoom(late, RoomKind.Elite))
                {
                    sawElite = true;
                    break;
                }
            }

            Assert.IsTrue(sawElite, "节点5清关在足够采样下应能放出困难房");
        }

        [Test]
        public void AdvanceNode_Floor3Node8_GoesToVictory()
        {
            var run = mArch.GetModel<RunModel>();
            run.Floor.Value = 3;
            run.NodeIndex.Value = 7;
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            Assert.IsTrue(mPhase.Attack(sAdjacentSlot).Accepted);

            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.Navigation, mArch.GetModel<PendingChoiceModel>().Kind.Value);
            Assert.AreEqual(NavigationKind.GoDown, mArch.GetModel<PendingChoiceModel>().NavigationOffer.Value);

            Assert.IsTrue(mPhase.SelectRoom(0).Accepted);
            Assert.IsTrue(mPhase.EnterRoom().Accepted);
            Assert.AreEqual(GamePhase.Victory, mPhase.CurrentPhase);
        }

        private static void AssertSchedule(
            int nodeIndex,
            int displayNode,
            NodeRoomSource source,
            bool entersLoop,
            NodeOfferFamily family,
            bool allowsElite)
        {
            MapNodeSchedule schedule;
            Assert.IsTrue(MapNodeProgression.TryGetSchedule(nodeIndex, out schedule));
            Assert.AreEqual(displayNode, schedule.DisplayNode);
            Assert.AreEqual(source, schedule.RoomSource);
            Assert.AreEqual(entersLoop, schedule.EntersInteractionLoop);
            Assert.AreEqual(family, schedule.PostClearOfferFamily);
            Assert.AreEqual(allowsElite, schedule.AllowsEliteInBattleOffers);
        }

        private void ResetWithSeed(ulong seed)
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildTestCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = seed });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mReward = mArch.GetSystem<IRewardSystem>();
        }

        private static bool ContainsRoom(IReadOnlyList<RoomKind> rooms, RoomKind kind)
        {
            for (var i = 0; i < rooms.Count; i++)
            {
                if (rooms[i] == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private int SpawnHelpIntoItemSlots(string defId)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var deck = mArch.GetModel<DeckModel>();
            return deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];
        }

        private int SpawnTrapAt(SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction("trap.test", CardKind.Trap, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            return mArch.GetModel<BoardModel>().GetCardUid(slot);
        }

        private int CountBoardTraps()
        {
            var registry = mArch.GetModel<CardRegistry>();
            var count = 0;
            foreach (var uid in mArch.GetModel<BoardModel>().BoardCardUids())
            {
                CardInstance card;
                if (registry.TryGet(uid, out card) && card.Kind == CardKind.Trap)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountItemSlotHelpCards()
        {
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var count = 0;
            for (var i = 0; i < deck.ItemSlotUids.Count; i++)
            {
                CardInstance card;
                if (registry.TryGet(deck.ItemSlotUids[i], out card) && card.Kind == CardKind.HelpCard)
                {
                    count++;
                }
            }

            return count;
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

        private static GameContentCatalog BuildTestCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.Economy.UnusedHelpCardGold = 10;
            catalog.Economy.MonsterRemovedGold = 0;
            catalog.AddCard(new CardContentDefinition("help.settle_gold", "结算测试", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("trap.test", "测试机关", CardKind.Trap));
            catalog.AddCard(new CardContentDefinition("monster.test", "测试怪", CardKind.Monster));
            catalog.Rewards
                .AddRoom(new RoomDefinition(RoomKind.Gold, "金币") { Weight = 10 })
                .AddRoom(new RoomDefinition(RoomKind.Fountain, "喷泉") { Weight = 10 })
                .AddRoom(new RoomDefinition(RoomKind.Treasure, "宝藏") { Weight = 10 })
                .AddRoom(new RoomDefinition(RoomKind.Attribute, "属性") { Weight = 10 })
                .AddRoom(new RoomDefinition(RoomKind.Elite, "困难") { Weight = 10 })
                .AddRoom(new RoomDefinition(RoomKind.Shop, "商店") { Weight = 10 })
                .AddRoom(new RoomDefinition(RoomKind.Tavern, "酒馆") { Weight = 10 })
                .AddRoom(new RoomDefinition(RoomKind.TreasureReward, "宝箱奖励") { Weight = 10 })
                .AddRoom(new RoomDefinition(RoomKind.ItemReward, "道具奖励") { Weight = 10 });
            return catalog;
        }
    }
}
