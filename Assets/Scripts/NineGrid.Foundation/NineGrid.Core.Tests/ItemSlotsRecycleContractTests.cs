using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #110 / ADR-0025：从道具卡格回收一张 ⇒ +10 金并移除；可用流程段可执行。
    /// Seam：<see cref="IPhaseSystem.RecycleItemSlot"/> + <see cref="EconomyConfig.RecycleItemSlotGold"/>。
    /// </summary>
    public sealed class ItemSlotsRecycleContractTests
    {
        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildTestCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 17UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void RecycleItemSlot_RemovesCardAndAwardsTenGold()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            var uid = SpawnHelpIntoItemSlots("help.hp_card");
            var player = mArch.GetModel<PlayerModel>();
            var goldBefore = player.Coins.Value;
            var catalog = mArch.GetSystem<IContentSystem>().Catalog;
            Assert.AreEqual(10, catalog.Economy.RecycleItemSlotGold);

            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.RecycleItemSlot));
            var recycle = mPhase.RecycleItemSlot(uid);
            Assert.IsTrue(recycle.Accepted, recycle.Reason);

            var deck = mArch.GetModel<DeckModel>();
            Assert.AreEqual(0, deck.ItemSlotUids.Count);
            CardInstance card;
            Assert.IsTrue(mArch.GetModel<CardRegistry>().TryGet(uid, out card));
            Assert.AreEqual(ZoneId.Removed, card.Zone.Value);
            Assert.AreEqual(goldBefore + catalog.Economy.RecycleItemSlotGold, player.Coins.Value);
        }

        [Test]
        public void RecycleItemSlot_RejectsWhenNotInItemSlots()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            var goldBefore = mArch.GetModel<PlayerModel>().Coins.Value;

            var missing = mPhase.RecycleItemSlot(99999);
            Assert.IsFalse(missing.Accepted);
            Assert.AreEqual(goldBefore, mArch.GetModel<PlayerModel>().Coins.Value);

            var uid = SpawnHelpIntoItemSlots("help.hp_card");
            Assert.IsTrue(mPhase.ApplyUseItem(uid, null, null).Accepted);
            var afterConsume = mPhase.RecycleItemSlot(uid);
            Assert.IsFalse(afterConsume.Accepted);
            Assert.AreEqual(goldBefore, mArch.GetModel<PlayerModel>().Coins.Value);
        }

        [Test]
        public void RecycleItemSlot_IsLegalInShopRewardPhase()
        {
            EnterShop();
            Assert.AreEqual(GamePhase.RewardItemChoice, mPhase.CurrentPhase);
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.RecycleItemSlot));

            var uid = SpawnHelpIntoItemSlots("help.hp_card");
            var goldBefore = mArch.GetModel<PlayerModel>().Coins.Value;
            var recycle = mPhase.RecycleItemSlot(uid);
            Assert.IsTrue(recycle.Accepted, recycle.Reason);
            Assert.AreEqual(
                goldBefore + mArch.GetSystem<IContentSystem>().Catalog.Economy.RecycleItemSlotGold,
                mArch.GetModel<PlayerModel>().Coins.Value);
        }

        [Test]
        public void ApplyRecycleItemSlot_SkipsPhaseGate_StillRemovesAndPays()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            var uid = SpawnHelpIntoItemSlots("help.hp_card");
            var goldBefore = mArch.GetModel<PlayerModel>().Coins.Value;

            var sync = mArch.GetSystem<IPresentationSyncSystem>();
            var map = PresentationEventMap.Get(CoreEventType.ItemUsed);
            Assert.IsTrue(map.LocksInput);
            sync.OpenBatch(new PresentationBatch(
                42,
                new[] { new PresentationInstruction(new CoreGameEvent(CoreEventType.ItemUsed, 1, "UseItem"), map) },
                null));
            Assert.IsTrue(sync.IsInputLocked);
            Assert.IsFalse(mPhase.CanExecute(GameCommandKind.RecycleItemSlot));

            var apply = mPhase.ApplyRecycleItemSlot(uid);
            Assert.IsTrue(apply.Accepted, apply.Reason);
            Assert.AreEqual(0, mArch.GetModel<DeckModel>().ItemSlotUids.Count);
            Assert.AreEqual(
                goldBefore + mArch.GetSystem<IContentSystem>().Catalog.Economy.RecycleItemSlotGold,
                mArch.GetModel<PlayerModel>().Coins.Value);

            Assert.IsTrue(sync.FinishBatch(42).Accepted);
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
        }

        private int SpawnHelpIntoItemSlots(string defId)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var deck = mArch.GetModel<DeckModel>();
            Assert.Greater(deck.ItemSlotUids.Count, 0);
            return deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test_fodder", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }

        private static GameContentCatalog BuildTestCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.Economy.RecycleItemSlotGold = 10;
            catalog.AddCard(new CardContentDefinition("help.hp_card", "血瓶", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("monster.test_fodder", "测试怪", CardKind.Monster));
            catalog.AddCard(new CardContentDefinition(RewardSystem.ShopPotionDefId, "药水", CardKind.HelpCard)
                .WithPrice(30));
            catalog.AddCard(new CardContentDefinition(RewardSystem.ShopFoodDefId, "食品", CardKind.HelpCard)
                .WithPrice(20));
            catalog.AddCard(new CardContentDefinition(RewardSystem.ShopChestDefId, "宝箱", CardKind.HelpCard)
                .WithPrice(50));
            catalog.AddCard(new CardContentDefinition("help.armor_card", "甲", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.attack_card", "攻", CardKind.HelpCard));
            catalog.Rewards.AddRoom(new RoomDefinition(RoomKind.Shop, "商店") { Weight = 1 });
            return catalog;
        }
    }
}
