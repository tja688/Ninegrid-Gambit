using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #93 卡店会话：三项服务 / 扣费留店 / 刷新翻倍 / 离开 / 道具卡固定二级选择。
    /// Seam：IPhaseSystem + PendingChoiceModel（TavernPool / FixItem 子池）+ PlayerModel 容量/固定卡/数值加成。
    /// </summary>
    public sealed class TavernSessionContractTests
    {
        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildTavernCatalog());
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
        public void EnterTavern_OffersThreeServicesAndInitialRefreshPrice()
        {
            EnterTavern();
            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(3, pending.RewardOptions.Count);
            Assert.AreEqual(RewardSystem.TavernUpgradeDefId, pending.RewardOptions[0].DefId);
            Assert.AreEqual(RewardSystem.TavernFixItemDefId, pending.RewardOptions[1].DefId);
            Assert.AreEqual(RewardSystem.TavernExpandDefId, pending.RewardOptions[2].DefId);
            Assert.AreEqual(10, pending.ShopRefreshPriceGold.Value);
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.RefreshShop));
            Assert.AreEqual(PendingChoiceModel.TavernPoolId, pending.PoolId.Value);
        }

        [Test]
        public void SelectReward_Expand_DeductsGold_IncreasesCapacity_StaysInTavern()
        {
            EnterTavern();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(200);
            var capacityBefore = player.ItemDeckCapacity;
            var coinsBefore = player.Coins.Value;

            Assert.IsTrue(mPhase.SelectReward(2).Accepted);
            Assert.AreEqual(coinsBefore - RewardSystem.TavernServicePriceGold, player.Coins.Value);
            Assert.AreEqual(capacityBefore + 1, player.ItemDeckCapacity);
            Assert.AreEqual(GamePhase.RewardItemChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceModel.TavernPoolId, mArch.GetModel<PendingChoiceModel>().PoolId.Value);
            Assert.AreEqual(3, mArch.GetModel<PendingChoiceModel>().RewardOptions.Count);
        }

        [Test]
        public void SelectReward_Upgrade_DeductsGold_AddsStatBonus_StaysInTavern()
        {
            EnterTavern();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(200);
            var coinsBefore = player.Coins.Value;
            var bonusBefore = player.ItemStatBonus;

            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.AreEqual(coinsBefore - RewardSystem.TavernServicePriceGold, player.Coins.Value);
            Assert.AreEqual(bonusBefore + RewardSystem.TavernUpgradeStatDelta, player.ItemStatBonus);
            Assert.AreEqual(PendingChoiceModel.TavernPoolId, mArch.GetModel<PendingChoiceModel>().PoolId.Value);
        }

        [Test]
        public void SelectReward_Tavern_RejectsWhenNotEnoughGold()
        {
            EnterTavern();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(20);
            var coinsBefore = player.Coins.Value;
            var capacityBefore = player.ItemDeckCapacity;

            var result = mPhase.SelectReward(2);
            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("Not enough gold", result.Reason);
            Assert.AreEqual(coinsBefore, player.Coins.Value);
            Assert.AreEqual(capacityBefore, player.ItemDeckCapacity);
        }

        [Test]
        public void RefreshShop_Tavern_DeductsAndDoublesPrice_VisitScoped()
        {
            EnterTavern();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(100);
            var pending = mArch.GetModel<PendingChoiceModel>();
            var coinsBefore = player.Coins.Value;

            var refresh = mPhase.RefreshShop();
            Assert.IsTrue(refresh.Accepted, refresh.Reason);
            Assert.AreEqual(coinsBefore - 10, player.Coins.Value);
            Assert.AreEqual(20, pending.ShopRefreshPriceGold.Value);
            Assert.AreEqual(3, pending.RewardOptions.Count);
            Assert.AreEqual(PendingChoiceModel.TavernPoolId, pending.PoolId.Value);

            Assert.IsTrue(mPhase.RefreshShop().Accepted);
            Assert.AreEqual(40, pending.ShopRefreshPriceGold.Value);
        }

        [Test]
        public void SkipHelpChoice_LeavesTavern_AndClearsRefreshPrice()
        {
            EnterTavern();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(200);
            Assert.IsTrue(mPhase.SelectReward(2).Accepted);

            var leave = mPhase.SkipHelpChoice();
            Assert.IsTrue(leave.Accepted, leave.Reason);
            Assert.AreEqual(GamePhase.NodeCompleted, mPhase.CurrentPhase);
            Assert.AreEqual(0, mArch.GetModel<PendingChoiceModel>().ShopRefreshPriceGold.Value);
            Assert.AreEqual(PendingChoiceKind.None, mArch.GetModel<PendingChoiceModel>().Kind.Value);
        }

        [Test]
        public void FixItem_EntersNested_ConfirmAddsFixedCard_AndCharges()
        {
            EnterTavern();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(200);
            player.ReplaceItemSourcePool(new[] { "help.hp_card", "help.armor_card" });
            var coinsBefore = player.Coins.Value;

            Assert.IsTrue(mPhase.SelectReward(1).Accepted);
            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(PendingChoiceModel.TavernFixItemPoolId, pending.PoolId.Value);
            Assert.AreEqual(2, pending.RewardOptions.Count);
            Assert.AreEqual(coinsBefore, player.Coins.Value);
            Assert.AreEqual(0, player.FixedItemCardDefIds.Count);

            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.AreEqual(coinsBefore - RewardSystem.TavernServicePriceGold, player.Coins.Value);
            Assert.AreEqual(1, player.FixedItemCardDefIds.Count);
            Assert.AreEqual("help.hp_card", player.FixedItemCardDefIds[0]);
            Assert.AreEqual(PendingChoiceModel.TavernPoolId, pending.PoolId.Value);
            Assert.AreEqual(3, pending.RewardOptions.Count);
        }

        [Test]
        public void FixItem_CancelNested_RestoresServices_WithoutCharge()
        {
            EnterTavern();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(200);
            player.ReplaceItemSourcePool(new[] { "help.hp_card" });
            var coinsBefore = player.Coins.Value;

            Assert.IsTrue(mPhase.SelectReward(1).Accepted);
            Assert.AreEqual(PendingChoiceModel.TavernFixItemPoolId, mArch.GetModel<PendingChoiceModel>().PoolId.Value);

            var cancel = mPhase.SkipHelpChoice();
            Assert.IsTrue(cancel.Accepted, cancel.Reason);
            Assert.AreEqual(GamePhase.RewardItemChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceModel.TavernPoolId, mArch.GetModel<PendingChoiceModel>().PoolId.Value);
            Assert.AreEqual(3, mArch.GetModel<PendingChoiceModel>().RewardOptions.Count);
            Assert.AreEqual(coinsBefore, player.Coins.Value);
            Assert.AreEqual(0, player.FixedItemCardDefIds.Count);
            Assert.AreEqual(10, mArch.GetModel<PendingChoiceModel>().ShopRefreshPriceGold.Value);
        }

        [Test]
        public void FixItem_RejectsWhenSourcePoolEmpty()
        {
            EnterTavern();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(200);
            player.ReplaceItemSourcePool(System.Array.Empty<string>());

            var result = mPhase.SelectReward(1);
            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("No item source pool", result.Reason);
            Assert.AreEqual(PendingChoiceModel.TavernPoolId, mArch.GetModel<PendingChoiceModel>().PoolId.Value);
        }

        private void EnterTavern()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);
            mPipeline.Enqueue(new ChangePhaseAction(GamePhase.RoomEvent));
            mPipeline.RunToCompletion();
            mArch.GetModel<PendingChoiceModel>().SelectRoom(RoomKind.Tavern);

            var enter = mPhase.EnterRoom();
            Assert.IsTrue(enter.Accepted, enter.Reason);
            Assert.AreEqual(GamePhase.RewardItemChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceModel.TavernPoolId, mArch.GetModel<PendingChoiceModel>().PoolId.Value);
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
            catalog.AddCard(new CardContentDefinition("help.hp_card", "血量卡", CardKind.HelpCard).WithPrice(50));
            catalog.AddCard(new CardContentDefinition("help.armor_card", "加甲卡", CardKind.HelpCard).WithPrice(50));
            catalog.AddCard(new CardContentDefinition("monster.hold", "占位怪", CardKind.Monster));
            catalog.Rewards.AddRoom(new RoomDefinition(RoomKind.Tavern, "卡店") { Weight = 1 });
            return catalog;
        }
    }
}
