using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #94 / #108 特殊房：宝箱奖励 / 道具奖励铺卡、免费拿直写道具卡格、离开放弃、满格拒领。
    /// Seam：IPhaseSystem + PendingChoiceModel 特殊房 pool + DeckModel.ItemSlots。
    /// </summary>
    public sealed class SpecialRewardSessionContractTests
    {
        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mArch.GetModel<PlayerModel>().ReplaceItemSourcePool(new[]
            {
                "help.hp_card",
                "help.armor_card",
                "help.attack_card",
                "help.food_card"
            });
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void EnterTreasureReward_OffersChestPlusThreeRandomItems()
        {
            EnterRoom(RoomKind.TreasureReward);
            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(PendingChoiceModel.TreasureRewardPoolId, pending.PoolId.Value);
            Assert.AreEqual(4, pending.RewardOptions.Count);
            Assert.AreEqual(RewardSystem.ShopChestDefId, pending.RewardOptions[0].DefId);
            Assert.IsTrue(IsItemSourceCard(pending.RewardOptions[1].DefId));
            Assert.IsTrue(IsItemSourceCard(pending.RewardOptions[2].DefId));
            Assert.IsTrue(IsItemSourceCard(pending.RewardOptions[3].DefId));
            Assert.IsFalse(mPhase.CanExecute(GameCommandKind.RefreshShop));
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.SelectReward));
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.SkipHelpChoice));
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.MoveAvatar));
        }

        [Test]
        public void EnterItemReward_OffersTwoAttributesPlusThreeRandomItems()
        {
            EnterRoom(RoomKind.ItemReward);
            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(PendingChoiceModel.ItemRewardPoolId, pending.PoolId.Value);
            Assert.AreEqual(5, pending.RewardOptions.Count);
            Assert.IsTrue(IsAttributeCard(pending.RewardOptions[0].DefId));
            Assert.IsTrue(IsAttributeCard(pending.RewardOptions[1].DefId));
            Assert.IsTrue(IsItemSourceCard(pending.RewardOptions[2].DefId));
            Assert.IsTrue(IsItemSourceCard(pending.RewardOptions[3].DefId));
            Assert.IsTrue(IsItemSourceCard(pending.RewardOptions[4].DefId));
        }

        [Test]
        public void SelectReward_SpecialRoom_FreeTakeStaysAndGoesToItemSlots()
        {
            EnterRoom(RoomKind.TreasureReward);
            var player = mArch.GetModel<PlayerModel>();
            var pending = mArch.GetModel<PendingChoiceModel>();
            var taken = pending.RewardOptions[0].DefId;
            var coinsBefore = player.Coins.Value;
            var beforeCount = pending.RewardOptions.Count;

            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.AreEqual(GamePhase.RewardItemChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.Reward, pending.Kind.Value);
            Assert.AreEqual(beforeCount - 1, pending.RewardOptions.Count);
            Assert.AreEqual(1, CountItemSlotsByDef(taken));
            Assert.AreEqual(coinsBefore, player.Coins.Value, "特殊房拿走免费");
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.SelectReward));
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.SkipHelpChoice));
        }

        [Test]
        public void SelectReward_SpecialRoom_RejectsWhenItemSlotsFull()
        {
            EnterRoom(RoomKind.TreasureReward);
            FillItemSlotsToCapacity("help.food_card");
            var player = mArch.GetModel<PlayerModel>();
            var pending = mArch.GetModel<PendingChoiceModel>();
            var coinsBefore = player.Coins.Value;
            var optionCount = pending.RewardOptions.Count;
            var occupied = SnapshotItemSlotUids();

            var take = mPhase.SelectReward(0);
            Assert.IsFalse(take.Accepted);
            Assert.AreEqual("道具卡格已满", take.Reason);
            Assert.AreEqual(coinsBefore, player.Coins.Value);
            Assert.AreEqual(optionCount, pending.RewardOptions.Count);
            CollectionAssert.AreEqual(occupied, SnapshotItemSlotUids());
        }

        [Test]
        public void SkipHelpChoice_LeavesSpecialRoom_UntakenNotInItemSlots_NoSkipGold()
        {
            EnterRoom(RoomKind.ItemReward);
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(50);
            var pending = mArch.GetModel<PendingChoiceModel>();
            var taken = pending.RewardOptions[0].DefId;
            // 属性卡可重复；挑一张与已拿不同 defId 的未选卡作「放弃」断言。
            string abandoned = null;
            for (var i = 1; i < pending.RewardOptions.Count; i++)
            {
                var id = pending.RewardOptions[i].DefId;
                if (!string.Equals(id, taken, System.StringComparison.Ordinal))
                {
                    abandoned = id;
                    break;
                }
            }

            Assert.IsNotNull(abandoned, "夹具应保证存在与已拿不同的未选卡");
            var coinsBefore = player.Coins.Value;

            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.IsTrue(mPhase.SkipHelpChoice().Accepted);

            Assert.AreEqual(GamePhase.NodeCompleted, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.None, pending.Kind.Value);
            Assert.AreEqual(1, CountItemSlotsByDef(taken));
            Assert.AreEqual(0, CountItemSlotsByDef(abandoned), "未拿的卡不进道具卡格");
            Assert.AreEqual(coinsBefore, player.Coins.Value, "离开不加 skip 金");
        }

        private void EnterRoom(RoomKind room)
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);
            mPipeline.Enqueue(new ChangePhaseAction(GamePhase.RoomEvent));
            mPipeline.RunToCompletion();
            mArch.GetModel<PendingChoiceModel>().SelectRoom(room);

            var enter = mPhase.EnterRoom();
            Assert.IsTrue(enter.Accepted, enter.Reason);
            Assert.AreEqual(GamePhase.RewardItemChoice, mPhase.CurrentPhase);
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

        private static bool IsAttributeCard(string defId)
        {
            return defId == "help.hp_card"
                   || defId == "help.armor_card"
                   || defId == "help.attack_card";
        }

        private static bool IsItemSourceCard(string defId)
        {
            return IsAttributeCard(defId) || defId == "help.food_card";
        }

        private static GameContentCatalog BuildCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.Economy.SkipHelpChoiceGold = 10;
            catalog.AddCard(new CardContentDefinition(RewardSystem.ShopChestDefId, "普通宝箱卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.hp_card", "血量卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.armor_card", "加甲卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.attack_card", "加攻卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.food_card", "食品卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("monster.hold", "占位怪", CardKind.Monster));
            catalog.Rewards.AddRoom(new RoomDefinition(RoomKind.TreasureReward, "宝箱奖励") { Weight = 1 });
            catalog.Rewards.AddRoom(new RoomDefinition(RoomKind.ItemReward, "道具奖励") { Weight = 1 });
            return catalog;
        }
    }
}
