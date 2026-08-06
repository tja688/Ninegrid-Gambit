using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #98 遗物栏经济：上限 12、满栏拒选、丢弃 +20、宝箱跳过遗物 +20。
    /// Seam：<see cref="IPhaseSystem.SelectReward"/> / <see cref="IPhaseSystem.SkipHelpChoice"/> /
    /// <see cref="IPhaseSystem.DiscardRelic"/> + <see cref="PlayerModel"/> 容量。
    /// </summary>
    public sealed class RelicInventoryEconomyContractTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildTestCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 11UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void PlayerModel_AddRelic_StopsAtTwelve()
        {
            var player = mArch.GetModel<PlayerModel>();
            Assert.AreEqual(12, PlayerModel.MaxRelicSlots);
            ClearRelics(player);

            for (var i = 0; i < PlayerModel.MaxRelicSlots; i++)
            {
                player.AddRelic("relic.slot_" + i);
            }

            Assert.AreEqual(PlayerModel.MaxRelicSlots, player.RelicDefIds.Count);
            Assert.IsTrue(player.IsRelicInventoryFull);

            player.AddRelic("relic.overflow");
            Assert.AreEqual(PlayerModel.MaxRelicSlots, player.RelicDefIds.Count);
            Assert.IsFalse(ContainsRelic(player, "relic.overflow"));
        }

        [Test]
        public void SkipHelpChoice_RelicChest_AwardsSkipRelicChoiceGold()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var chestUid = SpawnHelpIntoItemSlots("help.common_chest_card");
            Assert.IsTrue(mPhase.ApplyUseItem(chestUid, null, null).Accepted);

            var player = mArch.GetModel<PlayerModel>();
            var goldBefore = player.Coins.Value;
            var catalog = mArch.GetSystem<IContentSystem>().Catalog;
            Assert.AreEqual(20, catalog.Economy.SkipRelicChoiceGold);
            Assert.AreEqual(10, catalog.Economy.SkipHelpChoiceGold);

            var skip = mPhase.SkipHelpChoice();
            Assert.IsTrue(skip.Accepted, skip.Reason);
            Assert.AreEqual(goldBefore + catalog.Economy.SkipRelicChoiceGold, player.Coins.Value);
        }

        [Test]
        public void UseItem_RelicChest_WhenFull_RejectsUpfrontAndKeepsPendingEmpty()
        {
            // ADR-0027 addendum / #143：满遗物栏（12/12）时宝箱 UseItem 须前置拒收，
            // 卡片不消耗、不开 PendingChoice；避免表现层 Bounce while(true) 死循环。
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            var player = mArch.GetModel<PlayerModel>();
            FillRelicInventory(player);

            var chestUid = SpawnHelpIntoItemSlots("help.common_chest_card");
            var use = mPhase.ApplyUseItem(chestUid, null, null);
            Assert.IsFalse(use.Accepted, "满遗物栏宝箱使用应被拒收");
            Assert.AreEqual("遗物格子已满", use.Reason);

            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(PendingChoiceKind.None, pending.Kind.Value, "不得开 PendingChoice");
            Assert.AreEqual(0, pending.RewardOptions.Count, "不得生成奖励选项");

            // 卡片未消耗，仍在道具卡格。
            Assert.AreEqual(PlayerModel.MaxRelicSlots, player.RelicDefIds.Count, "未资助遗物");
            var deck = mArch.GetModel<DeckModel>();
            CollectionAssert.Contains(deck.ItemSlotUids, chestUid, "宝箱应回手保留在道具卡格");
        }

        [Test]
        public void DiscardRelic_WhilePendingRelicChoice_IsLegalAndFreesSlotForSelect()
        {
            // 满栏（12/12）下宝箱已被 UseItem 前置拒收（同上），因此「弃遗物腾空再开宝箱」
            // 的完整流程：满 → 弃遗物（12→11）→ UseItem 接受 → PendingChoice 开 → 选入遗物（11→12）。
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            var player = mArch.GetModel<PlayerModel>();
            FillRelicInventory(player);
            var discardId = player.RelicDefIds[0];

            var chestUid = SpawnHelpIntoItemSlots("help.common_chest_card");

            // 满栏：宝箱先被拒收。
            Assert.IsFalse(mPhase.ApplyUseItem(chestUid, null, null).Accepted, "满栏宝箱首用应拒");

            // 弃一件腾空（11/12）；DiscardRelic 须合法、给付 +20。
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.DiscardRelic));
            Assert.IsTrue(mPhase.DiscardRelic(discardId).Accepted);
            Assert.AreEqual(PlayerModel.MaxRelicSlots - 1, player.RelicDefIds.Count);

            // 现在宝箱可解锁：UseItem 接受 → PendingChoice.Reward 开 → 选遗物成功（11→12）。
            Assert.IsTrue(mPhase.ApplyUseItem(chestUid, null, null).Accepted, "弃遗物后宝箱应可使用");

            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(PendingChoiceKind.Reward, pending.Kind.Value);
            Assert.Greater(pending.RewardOptions.Count, 0);

            var select = mPhase.SelectReward(0);
            Assert.IsTrue(select.Accepted, select.Reason);
            Assert.AreEqual(PendingChoiceKind.None, mArch.GetModel<PendingChoiceModel>().Kind.Value);
            Assert.AreEqual(PlayerModel.MaxRelicSlots, player.RelicDefIds.Count);
        }

        [Test]
        public void DiscardRelic_RemovesRelicAndAwardsDiscardGold()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);

            var player = mArch.GetModel<PlayerModel>();
            ClearRelics(player);
            player.AddRelic("relic.wood_shield");
            var goldBefore = player.Coins.Value;
            var catalog = mArch.GetSystem<IContentSystem>().Catalog;
            Assert.AreEqual(20, catalog.Economy.DiscardRelicGold);

            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.DiscardRelic));
            var discard = mPhase.DiscardRelic("relic.wood_shield");
            Assert.IsTrue(discard.Accepted, discard.Reason);
            Assert.IsFalse(ContainsRelic(player, "relic.wood_shield"));
            Assert.AreEqual(goldBefore + catalog.Economy.DiscardRelicGold, player.Coins.Value);
        }

        private int SpawnHelpIntoItemSlots(string defId)
        {
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(pipeline.RunToCompletion(), 0);

            var deck = mArch.GetModel<DeckModel>();
            Assert.Greater(deck.ItemSlotUids.Count, 0);
            return deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];
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

        private static void ClearRelics(PlayerModel player)
        {
            while (player.RelicDefIds.Count > 0)
            {
                player.RemoveRelic(player.RelicDefIds[0]);
            }
        }

        private static void FillRelicInventory(PlayerModel player)
        {
            ClearRelics(player);
            for (var i = 0; i < PlayerModel.MaxRelicSlots; i++)
            {
                player.AddRelic("relic.slot_" + i);
            }

            Assert.IsTrue(player.IsRelicInventoryFull);
        }

        private static bool ContainsRelic(PlayerModel player, string defId)
        {
            var relics = player.RelicDefIds;
            for (var i = 0; i < relics.Count; i++)
            {
                if (relics[i] == defId)
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
            }.AddEnemyCard(new CardDraft("monster.test_fodder", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }

        private static GameContentCatalog BuildTestCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.Economy.SkipHelpChoiceGold = 10;
            catalog.Economy.SkipRelicChoiceGold = 20;
            catalog.Economy.DiscardRelicGold = 20;

            catalog.AddEffect(new ContentEffectDefinition(
                "help.common_chest_card.use",
                EffectContainerType.HelpCard,
                TriggeredJson(
                    "help.common_chest_card.use",
                    "{\"atom\":\"OnSelfUsed\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"OfferRewardChoice\",\"poolId\":\"relic.common_chest\"}"),
                ContentImplementationState.Implemented,
                "chest"));

            catalog.AddCard(new CardContentDefinition("help.common_chest_card", "普通宝箱卡", CardKind.HelpCard)
                .AddEffect("help.common_chest_card.use"));
            catalog.AddCard(new CardContentDefinition("monster.test_fodder", "测试怪", CardKind.Monster));

            for (var i = 0; i < PlayerModel.MaxRelicSlots + 2; i++)
            {
                catalog.AddRelic(new RelicContentDefinition("relic.slot_" + i, "槽" + i, ContentRarity.White, "t"));
            }

            catalog.AddRelic(new RelicContentDefinition("relic.wood_shield", "木盾", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.vitality_amulet", "活力护符", ContentRarity.Blue, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.dragon_scale_armor", "龙鳞甲", ContentRarity.Gold, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.overflow", "溢出", ContentRarity.White, "t"));

            var pool = new RewardPoolDefinition("relic.common_chest", 3)
                .Add("relic.wood_shield", CardKind.Relic, 50)
                .Add("relic.vitality_amulet", CardKind.Relic, 30)
                .Add("relic.dragon_scale_armor", CardKind.Relic, 20);
            catalog.Rewards.AddPool(pool);

            return catalog;
        }

        private static string TriggeredJson(string id, string trigger, string target, string action)
        {
            return "{"
                + "\"id\":\"" + id + "\","
                + "\"typeTag\":\"【类型帮助卡】\","
                + "\"containerType\":\"HelpCard\","
                + "\"kind\":\"Triggered\","
                + "\"requires\":[\"ActivatedByUse\",\"HasOwnerEntity\"],"
                + "\"trigger\":" + trigger + ","
                + "\"target\":" + target + ","
                + "\"action\":" + action
                + "}";
        }
    }
}
