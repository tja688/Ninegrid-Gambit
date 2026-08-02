using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #96 / ADR-0022：携带卡包跨节点闭环。
    /// Seam：商店/特殊房 SelectReward → CarryPack；BuildNodeDeckOptions 倒空注入；清关 SettleUnusedHelpCards。
    /// </summary>
    public sealed class CarryPackClosedLoopContractTests
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
        public void ShopBuyThree_NextBattleDeckCount_EqualsCapacityPlusRoomInjectPlusThree()
        {
            EnterShop();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(500);
            player.SetItemDeckCapacity(2);

            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.AreEqual(3, player.CarryPackDefIds.Count);
            Assert.AreEqual(0, player.FixedItemCardDefIds.Count, "商店购买不得写入固定卡列表");

            Assert.IsTrue(mPhase.SkipHelpChoice().Accepted);
            Assert.AreEqual(GamePhase.NodeCompleted, mPhase.CurrentPhase);

            var run = mArch.GetModel<RunModel>();
            run.Room.Value = RoomKind.Gold;
            run.NodeIndex.Value = 0;

            var options = mReward.BuildNodeDeckOptions(1, null);
            Assert.AreEqual(
                2 + 1 + 3,
                options.PlayerCards.Count,
                "容量 + 金币房注入1 + 携带3");
            Assert.AreEqual(0, player.CarryPackDefIds.Count, "开局倒空后携带卡包为空");

            options.AddEnemyCard(new CardDraft("monster.hold", CardKind.Monster) { MaxHp = 1, Attack = 0 });
            options.EnemyOpeningCount = 1;
            options.PlayerOpeningCount = 0;
            Assert.IsTrue(mPhase.StartNode(options).Accepted);
            Assert.AreEqual(GamePhase.InteractionLoop, mPhase.CurrentPhase);
            Assert.AreEqual(0, player.CarryPackDefIds.Count, "战斗进行中包仍为空");

            var monsterSlot = EnsureMonsterAdjacentToAvatar();
            Assert.IsTrue(mPhase.Attack(monsterSlot).Accepted);
            Assert.AreEqual(0, player.CarryPackDefIds.Count, "该节点结束后携带卡包为空");
        }

        [Test]
        public void CarryInjectedUnused_SettlesAsGoldOnClear_AndPackStaysEmpty()
        {
            var player = mArch.GetModel<PlayerModel>();
            player.SetItemDeckCapacity(0);
            player.AddToCarryPack(RewardSystem.ShopPotionDefId);
            player.AddToCarryPack(RewardSystem.ShopFoodDefId);
            player.AddToCarryPack("help.hp_card");
            Assert.AreEqual(3, player.CarryPackDefIds.Count);

            // 金币房注入 1：容量0 + 注入1 + 携带3 = 4；NodeIndex=0 且已是战斗房，Ensure 不重掷。
            var run = mArch.GetModel<RunModel>();
            run.Room.Value = RoomKind.Gold;
            run.NodeIndex.Value = 0;

            var options = mReward.BuildNodeDeckOptions(1, null);
            Assert.AreEqual(4, options.PlayerCards.Count, "容量0 + 金币注入1 + 携带3");
            Assert.AreEqual("help.gold_card", options.PlayerCards[0].DefId);
            Assert.AreEqual(RewardSystem.ShopPotionDefId, options.PlayerCards[1].DefId);
            Assert.AreEqual(RewardSystem.ShopFoodDefId, options.PlayerCards[2].DefId);
            Assert.AreEqual("help.hp_card", options.PlayerCards[3].DefId);
            Assert.AreEqual(0, player.CarryPackDefIds.Count);

            options.AddEnemyCard(new CardDraft("monster.hold", CardKind.Monster) { MaxHp = 1, Attack = 0 });
            options.EnemyOpeningCount = 1;
            options.PlayerOpeningCount = 0;
            Assert.IsTrue(mPhase.StartNode(options).Accepted);

            var monsterSlot = EnsureMonsterAdjacentToAvatar();
            var coinsBefore = player.Coins.Value;
            Assert.IsTrue(mPhase.Attack(monsterSlot).Accepted);

            Assert.AreEqual(
                coinsBefore + 40,
                player.Coins.Value,
                "4 张未用帮助卡（含 3 张携带注入）按 UnusedHelpCardGold=10 结算");
            Assert.AreEqual(0, player.CarryPackDefIds.Count, "清关后携带卡包仍为空");
        }

        [Test]
        public void SpecialReward_GoesToCarryPack_NotFixedList_ThenDrainsOnNextBattle()
        {
            EnterSpecialReward(RoomKind.TreasureReward);
            var player = mArch.GetModel<PlayerModel>();
            var taken = mArch.GetModel<PendingChoiceModel>().RewardOptions[0].DefId;

            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.AreEqual(1, player.CountCarryPackByDefId(taken));
            Assert.AreEqual(0, player.FixedItemCardDefIds.Count);
            Assert.IsTrue(mPhase.SkipHelpChoice().Accepted);

            player.SetItemDeckCapacity(0);
            var run = mArch.GetModel<RunModel>();
            run.Room.Value = RoomKind.Gold;
            run.NodeIndex.Value = 0;

            var options = mReward.BuildNodeDeckOptions(1, null);
            Assert.AreEqual(2, options.PlayerCards.Count, "金币注入1 + 携带1");
            Assert.AreEqual("help.gold_card", options.PlayerCards[0].DefId);
            Assert.AreEqual(taken, options.PlayerCards[1].DefId);
            Assert.AreEqual(0, player.CarryPackDefIds.Count);
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
            Assert.AreEqual(4, mArch.GetModel<PendingChoiceModel>().RewardOptions.Count);
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

        private SlotId EnsureMonsterAdjacentToAvatar()
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var boardSystem = mArch.GetSystem<IBoardSystem>();
            var avatarSlot = board.AvatarSlot.Value;
            var monsterSlot = FindMonsterBoardSlot();
            Assert.AreNotEqual(SlotId.None, monsterSlot, "开局应有一只怪物在盘面");
            if (boardSystem.AreAdjacent(avatarSlot, monsterSlot))
            {
                return monsterSlot;
            }

            var monster = registry.Get(board.GetCardUid(monsterSlot));
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var candidate = SlotId.Board(i);
                if (candidate == avatarSlot || !boardSystem.AreAdjacent(avatarSlot, candidate))
                {
                    continue;
                }

                var occupantUid = board.GetCardUid(candidate);
                if (occupantUid != 0)
                {
                    var occupant = registry.Get(occupantUid);
                    if (occupant.Kind == CardKind.Monster)
                    {
                        continue;
                    }

                    board.ClearSlot(candidate);
                    deck.AddToPlayerCardPool(occupant);
                }

                board.ClearSlot(monster.Slot.Value);
                board.PlaceCard(monster, candidate);
                return candidate;
            }

            Assert.Fail("Avatar 旁无可用邻格安置怪物");
            return SlotId.None;
        }

        private SlotId FindMonsterBoardSlot()
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var uid = board.GetCardUid(slot);
                if (uid == 0)
                {
                    continue;
                }

                CardInstance card;
                if (registry.TryGet(uid, out card)
                    && card != null
                    && card.Kind == CardKind.Monster)
                {
                    return slot;
                }
            }

            return SlotId.None;
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
            catalog.Economy.MonsterRemovedGold = 0;

            catalog.AddCard(new CardContentDefinition(RewardSystem.ShopChestDefId, "普通宝箱卡", CardKind.HelpCard)
                .WithPrice(100));
            catalog.AddCard(new CardContentDefinition("help.hp_card", "血量卡", CardKind.HelpCard).WithPrice(50)
                .InDeck("deck.help"));
            catalog.AddCard(new CardContentDefinition("help.armor_card", "加甲卡", CardKind.HelpCard).WithPrice(50)
                .InDeck("deck.help"));
            catalog.AddCard(new CardContentDefinition("help.attack_card", "加攻卡", CardKind.HelpCard).WithPrice(50)
                .InDeck("deck.help"));
            catalog.AddCard(new CardContentDefinition(RewardSystem.ShopPotionDefId, "恢复药水", CardKind.HelpCard)
                .WithPrice(30));
            catalog.AddCard(new CardContentDefinition(RewardSystem.ShopFoodDefId, "食品卡", CardKind.HelpCard)
                .WithPrice(50));
            catalog.AddCard(new CardContentDefinition("help.gold_card", "金币卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("monster.hold", "占位怪", CardKind.Monster));

            catalog.Rewards
                .AddRoom(new RoomDefinition(RoomKind.Shop, "商店") { Weight = 1 })
                .AddRoom(new RoomDefinition(RoomKind.TreasureReward, "宝箱奖励") { Weight = 1 })
                .AddRoom(new RoomDefinition(RoomKind.ItemReward, "道具奖励") { Weight = 1 })
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
