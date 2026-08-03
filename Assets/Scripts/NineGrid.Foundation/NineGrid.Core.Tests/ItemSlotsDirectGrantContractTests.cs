using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #108 / ADR-0025：购领直写道具卡格；退役携带卡包开局注入；满格拒写。
    /// 取代旧 <c>CarryPackClosedLoopContractTests</c>。
    /// </summary>
    public sealed class ItemSlotsDirectGrantContractTests
    {
        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IRewardSystem mReward;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mReward = mArch.GetSystem<IRewardSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
            mArch = null;
            mPhase = null;
            mPipeline = null;
            mReward = null;
        }

        [Test]
        public void ShopBuyThree_WritesItemSlots_NotInjectedIntoNextBattleDeck()
        {
            EnterShop();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(500);
            player.SetItemDeckCapacity(0);
            player.SetItemSlotsCapacity(PlayerModel.MaxItemSlotsCapacity);

            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.AreEqual(3, mArch.GetModel<DeckModel>().ItemSlotUids.Count);
            Assert.AreEqual(0, player.FixedItemCardDefIds.Count, "商店购买不得写入固定卡列表");

            Assert.IsTrue(mPhase.SkipHelpChoice().Accepted);
            Assert.AreEqual(GamePhase.NodeCompleted, mPhase.CurrentPhase);

            var run = mArch.GetModel<RunModel>();
            run.Room.Value = RoomKind.Gold;
            run.NodeIndex.Value = 0;

            var options = mReward.BuildNodeDeckOptions(1, null);
            Assert.AreEqual(
                1,
                options.PlayerCards.Count,
                "仅金币房注入1；无携带注入");
            Assert.AreEqual("help.gold_card", options.PlayerCards[0].DefId);
            Assert.AreEqual(3, mArch.GetModel<DeckModel>().ItemSlotUids.Count, "道具卡格跨节点保留");
        }

        [Test]
        public void SpecialReward_GoesToItemSlots_NotFixedList_AndNotDrainedIntoBattleDeck()
        {
            EnterSpecialReward(RoomKind.TreasureReward);
            var player = mArch.GetModel<PlayerModel>();
            var taken = mArch.GetModel<PendingChoiceModel>().RewardOptions[0].DefId;

            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.AreEqual(1, CountItemSlotsByDef(taken));
            Assert.AreEqual(0, player.FixedItemCardDefIds.Count);
            Assert.IsTrue(mPhase.SkipHelpChoice().Accepted);

            player.SetItemDeckCapacity(0);
            var run = mArch.GetModel<RunModel>();
            run.Room.Value = RoomKind.Gold;
            run.NodeIndex.Value = 0;

            var options = mReward.BuildNodeDeckOptions(1, null);
            Assert.AreEqual(1, options.PlayerCards.Count, "仅金币注入1");
            Assert.AreEqual("help.gold_card", options.PlayerCards[0].DefId);
            Assert.AreEqual(1, CountItemSlotsByDef(taken), "领取物仍在道具卡格");
        }

        [Test]
        public void PickupItem_RejectsWhenItemSlotsFull_LeavesCardOnBoard()
        {
            Assert.IsTrue(mPhase.StartNode(CreateMinimalNode()).Accepted);
            var board = mArch.GetModel<BoardModel>();
            var avatarSlot = board.AvatarSlot.Value;
            var adjacent = FindAdjacentEmpty(avatarSlot);
            Assert.AreNotEqual(SlotId.None, adjacent);

            FillItemSlotsToCapacity("help.hp_card");
            var occupied = SnapshotItemSlotUids();
            SpawnHelpOnBoard("help.food_card", adjacent);

            var pickup = mPhase.PickupItem(adjacent);
            Assert.IsFalse(pickup.Accepted);
            Assert.AreEqual("道具卡格已满", pickup.Reason);
            Assert.AreEqual(board.GetCardUid(adjacent), mArch.GetModel<CardRegistry>()
                .Get(board.GetCardUid(adjacent)).Uid);
            Assert.AreNotEqual(0, board.GetCardUid(adjacent), "满格拒拾，卡仍在原格");
            CollectionAssert.AreEqual(occupied, SnapshotItemSlotUids());
        }

        [Test]
        public void Bootstrap_SeedsDefaultItemSlotsCapacity()
        {
            Assert.AreEqual(
                PlayerModel.DefaultItemSlotsCapacity,
                mArch.GetModel<PlayerModel>().ItemSlotsCapacity);
        }

        private void EnterShop()
        {
            Assert.IsTrue(mPhase.StartNode(CreateMinimalNode()).Accepted);
            mPipeline.Enqueue(new ChangePhaseAction(GamePhase.RoomEvent));
            mPipeline.RunToCompletion();
            mArch.GetModel<PendingChoiceModel>().SelectRoom(RoomKind.Shop);

            var enter = mPhase.EnterRoom();
            Assert.IsTrue(enter.Accepted, enter.Reason);
            Assert.AreEqual(GamePhase.RewardItemChoice, mPhase.CurrentPhase);
            Assert.AreEqual(5, mArch.GetModel<PendingChoiceModel>().RewardOptions.Count);
        }

        private void EnterSpecialReward(RoomKind room)
        {
            Assert.IsTrue(mPhase.StartNode(CreateMinimalNode()).Accepted);
            mPipeline.Enqueue(new ChangePhaseAction(GamePhase.RoomEvent));
            mPipeline.RunToCompletion();
            mArch.GetModel<PendingChoiceModel>().SelectRoom(room);

            var enter = mPhase.EnterRoom();
            Assert.IsTrue(enter.Accepted, enter.Reason);
        }

        private void FillItemSlotsToCapacity(string defId)
        {
            var content = mArch.GetSystem<IContentSystem>();
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var player = mArch.GetModel<PlayerModel>();
            while (deck.ItemSlotUids.Count < player.ItemSlotsCapacity)
            {
                deck.AddToItemSlots(content.CreateDraft(defId).Create(registry));
            }
        }

        private void SpawnHelpOnBoard(string defId, SlotId slot)
        {
            var content = mArch.GetSystem<IContentSystem>();
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var card = content.CreateDraft(defId).Create(registry);
            board.PlaceCard(card, slot);
        }

        private SlotId FindAdjacentEmpty(SlotId avatarSlot)
        {
            var board = mArch.GetModel<BoardModel>();
            var boardSystem = mArch.GetSystem<IBoardSystem>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var candidate = SlotId.Board(i);
                if (candidate == avatarSlot
                    || !boardSystem.AreAdjacent(avatarSlot, candidate)
                    || !board.IsEmpty(candidate))
                {
                    continue;
                }

                return candidate;
            }

            return SlotId.None;
        }

        private int[] SnapshotItemSlotUids()
        {
            var deck = mArch.GetModel<DeckModel>();
            var copy = new int[deck.ItemSlotUids.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = deck.ItemSlotUids[i];
            }

            return copy;
        }

        private int CountItemSlotsByDef(string defId)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var count = 0;
            for (var i = 0; i < deck.ItemSlotUids.Count; i++)
            {
                CardInstance card;
                if (registry.TryGet(deck.ItemSlotUids[i], out card)
                    && card != null
                    && card.DefId == defId)
                {
                    count++;
                }
            }

            return count;
        }

        private static NodeDeckOptions CreateMinimalNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
        }

        private static GameContentCatalog BuildCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.Economy.SkipHelpChoiceGold = 10;
            catalog.Economy.UnusedHelpCardGold = 10;
            catalog.Economy.MonsterRemovedGold = 5;
            catalog.AddCard(new CardContentDefinition(RewardSystem.ShopChestDefId, "普通宝箱卡", CardKind.HelpCard)
                .WithPrice(100));
            catalog.AddCard(new CardContentDefinition("help.hp_card", "血量卡", CardKind.HelpCard).WithPrice(50));
            catalog.AddCard(new CardContentDefinition("help.armor_card", "加甲卡", CardKind.HelpCard).WithPrice(50));
            catalog.AddCard(new CardContentDefinition("help.attack_card", "加攻卡", CardKind.HelpCard).WithPrice(50));
            catalog.AddCard(new CardContentDefinition(RewardSystem.ShopPotionDefId, "恢复药水", CardKind.HelpCard)
                .WithPrice(30));
            catalog.AddCard(new CardContentDefinition(RewardSystem.ShopFoodDefId, "食品卡", CardKind.HelpCard)
                .WithPrice(50));
            catalog.AddCard(new CardContentDefinition("help.food_card", "食品卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.gold_card", "金币卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("monster.hold", "占位怪", CardKind.Monster));
            catalog.Rewards
                .AddRoom(new RoomDefinition(RoomKind.Shop, "商店") { Weight = 1 })
                .AddRoom(new RoomDefinition(RoomKind.TreasureReward, "宝箱奖励") { Weight = 1 })
                .AddRoom(new RoomDefinition(RoomKind.Gold, "金币房") { Weight = 1 }
                    .AddOpeningInject(new RoomInjectDeclaration
                    {
                        Side = RoomInjectSide.Player,
                        SourceKind = RoomInjectSourceKind.FixedCard,
                        CardDefId = "help.gold_card",
                        Count = 1
                    }));
            catalog.Rewards.AddNodeRule(new NodeDeckRule { NodeIndex = 1, Seq1Count = 1 });
            catalog.Rewards.AddNodeRule(new NodeDeckRule { NodeIndex = 2, Seq1Count = 1 });
            return catalog;
        }
    }
}
