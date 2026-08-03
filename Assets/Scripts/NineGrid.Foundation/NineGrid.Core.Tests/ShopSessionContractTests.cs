using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #92 商店会话：四货架 + 道具牌格升级 / 购买留店 / 刷新翻倍（本次进店）/ 离开。
    /// Seam：IPhaseSystem + PendingChoiceModel.ShopRefreshPriceGold。
    /// </summary>
    public sealed class ShopSessionContractTests
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
        public void EnterShop_OffersFourShelvesAndInitialRefreshPrice()
        {
            EnterShop();
            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(5, pending.RewardOptions.Count);
            Assert.AreEqual(RewardSystem.ShopChestDefId, pending.RewardOptions[0].DefId);
            Assert.IsTrue(
                pending.RewardOptions[1].DefId == "help.hp_card"
                || pending.RewardOptions[1].DefId == "help.armor_card"
                || pending.RewardOptions[1].DefId == "help.attack_card");
            Assert.AreEqual(RewardSystem.ShopPotionDefId, pending.RewardOptions[2].DefId);
            Assert.AreEqual(RewardSystem.ShopFoodDefId, pending.RewardOptions[3].DefId);
            Assert.AreEqual(RewardSystem.ShopExpandItemSlotsDefId, pending.RewardOptions[4].DefId);
            Assert.AreEqual(10, pending.ShopRefreshPriceGold.Value);
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.RefreshShop));
        }

        [Test]
        public void SelectReward_Shop_StaysInShopAfterBuy()
        {
            EnterShop();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(200);
            var pending = mArch.GetModel<PendingChoiceModel>();
            var bought = pending.RewardOptions[0].DefId;
            var beforeCount = pending.RewardOptions.Count;

            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.AreEqual(GamePhase.RewardItemChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.Reward, pending.Kind.Value);
            Assert.AreEqual(beforeCount - 1, pending.RewardOptions.Count);
            Assert.AreEqual(1, CountItemSlotsByDef(bought));
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.SelectReward));
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.SkipHelpChoice));
        }

        [Test]
        public void RefreshShop_DeductsAndDoublesPrice_VisitScoped()
        {
            EnterShop();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(100);
            var pending = mArch.GetModel<PendingChoiceModel>();
            var coinsBefore = player.Coins.Value;
            var firstAttr = pending.RewardOptions[1].DefId;

            var refresh = mPhase.RefreshShop();
            Assert.IsTrue(refresh.Accepted, refresh.Reason);
            Assert.AreEqual(coinsBefore - 10, player.Coins.Value);
            Assert.AreEqual(20, pending.ShopRefreshPriceGold.Value);
            Assert.AreEqual(5, pending.RewardOptions.Count);
            Assert.AreEqual(RewardSystem.ShopChestDefId, pending.RewardOptions[0].DefId);
            Assert.AreEqual(RewardSystem.ShopPotionDefId, pending.RewardOptions[2].DefId);
            Assert.AreEqual(RewardSystem.ShopFoodDefId, pending.RewardOptions[3].DefId);
            Assert.AreEqual(RewardSystem.ShopExpandItemSlotsDefId, pending.RewardOptions[4].DefId);
            _ = firstAttr;

            Assert.IsTrue(mPhase.RefreshShop().Accepted);
            Assert.AreEqual(40, pending.ShopRefreshPriceGold.Value);
        }

        [Test]
        public void RefreshShop_RejectsWhenNotEnoughGold()
        {
            EnterShop();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(5);
            var coinsBefore = player.Coins.Value;

            var refresh = mPhase.RefreshShop();
            Assert.IsFalse(refresh.Accepted);
            Assert.AreEqual("Not enough gold", refresh.Reason);
            Assert.AreEqual(coinsBefore, player.Coins.Value);
            Assert.AreEqual(10, mArch.GetModel<PendingChoiceModel>().ShopRefreshPriceGold.Value);
        }

        [Test]
        public void SkipHelpChoice_LeavesShop_AndClearsRefreshPrice()
        {
            EnterShop();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(200);
            Assert.IsTrue(mPhase.SelectReward(2).Accepted); // 药水 30

            var leave = mPhase.SkipHelpChoice();
            Assert.IsTrue(leave.Accepted, leave.Reason);
            Assert.AreEqual(GamePhase.NodeCompleted, mPhase.CurrentPhase);
            Assert.AreEqual(0, mArch.GetModel<PendingChoiceModel>().ShopRefreshPriceGold.Value);
            Assert.AreEqual(PendingChoiceKind.None, mArch.GetModel<PendingChoiceModel>().Kind.Value);
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
    }
}
