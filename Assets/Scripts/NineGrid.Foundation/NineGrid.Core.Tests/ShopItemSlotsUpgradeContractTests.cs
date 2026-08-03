using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #109 / ADR-0025：商店「道具牌格升级」— 独立道具卡格容量 3…5、50 金 +1、满级隐藏；
    /// 卡店 ExpandItemCapacity 仍只动玩家侧卡组容量。
    /// Seam：IPhaseSystem.SelectReward / RefreshShop + RewardSystem.BuildShopShelves + PlayerModel.ItemSlotsCapacity。
    /// </summary>
    public sealed class ShopItemSlotsUpgradeContractTests
    {
        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildShopCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void EnterShop_OffersSlotUpgrade_WhenBelowMax()
        {
            EnterShop();
            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(PlayerModel.DefaultItemSlotsCapacity, mArch.GetModel<PlayerModel>().ItemSlotsCapacity);
            Assert.AreEqual(5, pending.RewardOptions.Count);
            Assert.AreEqual(RewardSystem.ShopExpandItemSlotsDefId, pending.RewardOptions[4].DefId);
        }

        [Test]
        public void EnterShop_HidesSlotUpgrade_AtMaxCapacity()
        {
            mArch.GetModel<PlayerModel>().SetItemSlotsCapacity(PlayerModel.MaxItemSlotsCapacity);
            EnterShop();
            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(4, pending.RewardOptions.Count);
            for (var i = 0; i < pending.RewardOptions.Count; i++)
            {
                Assert.AreNotEqual(
                    RewardSystem.ShopExpandItemSlotsDefId,
                    pending.RewardOptions[i].DefId);
            }
        }

        [Test]
        public void SelectReward_ShopSlotUpgrade_Deducts50_IncreasesItemSlotsCapacity_NotDeckCapacity()
        {
            EnterShop();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(200);
            var slotsBefore = player.ItemSlotsCapacity;
            var deckBefore = player.ItemDeckCapacity;
            var coinsBefore = player.Coins.Value;

            Assert.IsTrue(mPhase.SelectReward(4).Accepted);
            Assert.AreEqual(coinsBefore - RewardSystem.ShopExpandItemSlotsPriceGold, player.Coins.Value);
            Assert.AreEqual(slotsBefore + 1, player.ItemSlotsCapacity);
            Assert.AreEqual(deckBefore, player.ItemDeckCapacity, "商店格升级不得改玩家侧卡组容量");
            Assert.AreEqual(GamePhase.RewardItemChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceModel.ShopPoolId, mArch.GetModel<PendingChoiceModel>().PoolId.Value);
            Assert.AreEqual(5, mArch.GetModel<PendingChoiceModel>().RewardOptions.Count,
                "未满级时应保留升级选项");
        }

        [Test]
        public void SelectReward_ShopSlotUpgrade_RejectsWhenNotEnoughGold()
        {
            EnterShop();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(20);
            var coinsBefore = player.Coins.Value;
            var slotsBefore = player.ItemSlotsCapacity;

            var result = mPhase.SelectReward(4);
            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("Not enough gold", result.Reason);
            Assert.AreEqual(coinsBefore, player.Coins.Value);
            Assert.AreEqual(slotsBefore, player.ItemSlotsCapacity);
        }

        [Test]
        public void SelectReward_ShopSlotUpgrade_RemovesOption_WhenReachingMax()
        {
            EnterShop();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(200);
            player.SetItemSlotsCapacity(PlayerModel.MaxItemSlotsCapacity - 1);

            Assert.IsTrue(mPhase.SelectReward(4).Accepted);
            Assert.AreEqual(PlayerModel.MaxItemSlotsCapacity, player.ItemSlotsCapacity);
            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(4, pending.RewardOptions.Count);
            for (var i = 0; i < pending.RewardOptions.Count; i++)
            {
                Assert.AreNotEqual(RewardSystem.ShopExpandItemSlotsDefId, pending.RewardOptions[i].DefId);
            }
        }

        [Test]
        public void RefreshShop_PreservesSlotUpgradeVisibility()
        {
            EnterShop();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(100);
            var pending = mArch.GetModel<PendingChoiceModel>();

            Assert.IsTrue(mPhase.RefreshShop().Accepted);
            Assert.AreEqual(5, pending.RewardOptions.Count);
            Assert.AreEqual(RewardSystem.ShopExpandItemSlotsDefId, pending.RewardOptions[4].DefId);

            player.SetItemSlotsCapacity(PlayerModel.MaxItemSlotsCapacity);
            Assert.IsTrue(mPhase.RefreshShop().Accepted);
            Assert.AreEqual(4, pending.RewardOptions.Count);
            for (var i = 0; i < pending.RewardOptions.Count; i++)
            {
                Assert.AreNotEqual(RewardSystem.ShopExpandItemSlotsDefId, pending.RewardOptions[i].DefId);
            }
        }

        [Test]
        public void TavernExpand_StillOnlyIncreasesItemDeckCapacity()
        {
            // 回归：卡店 ExpandItemCapacity 与商店格升级拆开。
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildTavernCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();

            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);
            mPipeline.Enqueue(new ChangePhaseAction(GamePhase.RoomEvent));
            mPipeline.RunToCompletion();
            mArch.GetModel<PendingChoiceModel>().SelectRoom(RoomKind.Tavern);
            Assert.IsTrue(mPhase.EnterRoom().Accepted);

            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(200);
            var deckBefore = player.ItemDeckCapacity;
            var slotsBefore = player.ItemSlotsCapacity;

            Assert.IsTrue(mPhase.SelectReward(2).Accepted);
            Assert.AreEqual(deckBefore + 1, player.ItemDeckCapacity);
            Assert.AreEqual(slotsBefore, player.ItemSlotsCapacity, "卡店扩容不得改道具卡格容量");
        }

        private void EnterShop()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);
            mPipeline.Enqueue(new ChangePhaseAction(GamePhase.RoomEvent));
            mPipeline.RunToCompletion();
            mArch.GetModel<PendingChoiceModel>().SelectRoom(RoomKind.Shop);

            var enter = mPhase.EnterRoom();
            Assert.IsTrue(enter.Accepted, enter.Reason);
            Assert.AreEqual(GamePhase.RewardItemChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceModel.ShopPoolId, mArch.GetModel<PendingChoiceModel>().PoolId.Value);
        }

        private static GameContentCatalog BuildShopCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.Economy.SkipHelpChoiceGold = 10;
            catalog.AddCard(new CardContentDefinition(RewardSystem.ShopChestDefId, "普通宝箱卡", CardKind.HelpCard)
                .WithPrice(100));
            catalog.AddCard(new CardContentDefinition("help.hp_card", "血量卡", CardKind.HelpCard).WithPrice(50));
            catalog.AddCard(new CardContentDefinition("help.armor_card", "加甲卡", CardKind.HelpCard).WithPrice(50));
            catalog.AddCard(new CardContentDefinition("help.attack_card", "加攻卡", CardKind.HelpCard).WithPrice(50));
            catalog.AddCard(new CardContentDefinition(RewardSystem.ShopPotionDefId, "恢复药水", CardKind.HelpCard)
                .WithPrice(30));
            catalog.AddCard(new CardContentDefinition(RewardSystem.ShopFoodDefId, "食品卡", CardKind.HelpCard)
                .WithPrice(50));
            catalog.AddCard(new CardContentDefinition(
                    RewardSystem.ShopExpandItemSlotsDefId, "道具牌格升级", CardKind.HelpCard)
                .WithPrice(RewardSystem.ShopExpandItemSlotsPriceGold));
            catalog.AddCard(new CardContentDefinition("monster.hold", "占位怪", CardKind.Monster));
            catalog.Rewards.AddRoom(new RoomDefinition(RoomKind.Shop, "商店") { Weight = 1 });
            return catalog;
        }

        private static GameContentCatalog BuildTavernCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.Economy.SkipHelpChoiceGold = 10;
            catalog.AddCard(new CardContentDefinition(RewardSystem.TavernUpgradeDefId, "道具数值强化", CardKind.HelpCard)
                .WithPrice(RewardSystem.TavernServicePriceGold));
            catalog.AddCard(new CardContentDefinition(RewardSystem.TavernFixItemDefId, "道具卡固定", CardKind.HelpCard)
                .WithPrice(RewardSystem.TavernServicePriceGold));
            catalog.AddCard(new CardContentDefinition(RewardSystem.TavernExpandDefId, "道具卡扩容", CardKind.HelpCard)
                .WithPrice(RewardSystem.TavernServicePriceGold));
            catalog.AddCard(new CardContentDefinition("monster.hold", "占位怪", CardKind.Monster));
            catalog.Rewards.AddRoom(new RoomDefinition(RoomKind.Tavern, "卡店") { Weight = 1 });
            return catalog;
        }
    }
}
