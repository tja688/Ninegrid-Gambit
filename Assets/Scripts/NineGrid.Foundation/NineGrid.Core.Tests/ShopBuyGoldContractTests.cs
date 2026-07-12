using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 商店购买扣金 / 余额不足拒买 / 商店 Skip 不加跳过金。
    /// </summary>
    public sealed class ShopBuyGoldContractTests
    {
        private const string ShopItemDefId = "help.shop_item";
        private const int ShopItemPrice = 50;

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
        public void SelectReward_Shop_DeductsCardPrice()
        {
            EnterShop();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(100);
            var coinsBefore = player.Coins.Value;
            var startIndex = mPipeline.EventLog.Entries.Count;

            var buy = mPhase.SelectReward(0);
            Assert.IsTrue(buy.Accepted, buy.Reason);
            Assert.AreEqual(coinsBefore - ShopItemPrice, player.Coins.Value);

            var sawSpend = false;
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type != CoreEventType.GoldModified || e.Delta >= 0)
                {
                    continue;
                }

                sawSpend = true;
                Assert.AreEqual(-ShopItemPrice, e.Delta);
                Assert.IsTrue(
                    (e.Message ?? string.Empty).StartsWith("shopBuy:"),
                    "期望 reason 为 shopBuy:<defId>，实际=" + e.Message);
                break;
            }

            Assert.IsTrue(sawSpend, "EventLog 应有商店扣金 GoldModified");
            Assert.IsTrue(HasCardInPlayerSideDeck(ShopItemDefId), "购买后应写入玩家侧 run 卡组");
        }

        [Test]
        public void SelectReward_Shop_RejectsWhenNotEnoughGold()
        {
            EnterShop();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(10);
            var coinsBefore = player.Coins.Value;

            var buy = mPhase.SelectReward(0);
            Assert.IsFalse(buy.Accepted, "金币不足应拒买");
            Assert.AreEqual("Not enough gold", buy.Reason);
            Assert.AreEqual(coinsBefore, player.Coins.Value);
            Assert.AreEqual(PendingChoiceKind.Reward, mArch.GetModel<PendingChoiceModel>().Kind.Value);
            Assert.IsFalse(HasCardDefInDeck(ShopItemDefId));
        }

        [Test]
        public void SkipHelpChoice_Shop_DoesNotAwardSkipGold()
        {
            EnterShop();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(20);
            var coinsBefore = player.Coins.Value;

            var skip = mPhase.SkipHelpChoice();
            Assert.IsTrue(skip.Accepted, skip.Reason);
            Assert.AreEqual(
                coinsBefore,
                player.Coins.Value,
                "商店离开不应发放 skipHelpChoice 金币");
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

            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(PendingChoiceKind.Reward, pending.Kind.Value);
            Assert.AreEqual("shop.helpCards", pending.PoolId.Value);
            Assert.Greater(pending.RewardOptions.Count, 0);
            Assert.AreEqual(ShopItemDefId, pending.RewardOptions[0].DefId);
        }

        [Test]
        public void SelectReward_Shop_PersistsAcrossNextNode()
        {
            EnterShop();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(100);
            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.GreaterOrEqual(player.CountHelpCardsByDefId(ShopItemDefId), 1);
            Assert.AreEqual(GamePhase.NodeCompleted, mPhase.CurrentPhase);

            var options = mArch.GetSystem<IRewardSystem>().BuildNodeDeckOptions(1, null);
            options.AddEnemyCard(new CardDraft("monster.hold", CardKind.Monster) { MaxHp = 20, Attack = 0 });
            options.EnemyOpeningCount = 1;
            Assert.IsTrue(mPhase.StartNode(options).Accepted);
            Assert.AreEqual(GamePhase.InteractionLoop, mPhase.CurrentPhase);
            Assert.IsTrue(HasHelpCardInBattleDeck(ShopItemDefId), "商店购买卡下一节点应可见");
        }

        private bool HasCardInPlayerSideDeck(string defId)
        {
            return mArch.GetModel<PlayerModel>().CountHelpCardsByDefId(defId) > 0;
        }

        private bool HasHelpCardInBattleDeck(string defId)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var board = mArch.GetModel<BoardModel>();
            if (HasCardDefInDeck(defId))
            {
                return true;
            }

            foreach (var uid in board.BoardCardUids())
            {
                CardInstance card;
                if (registry.TryGet(uid, out card) && card != null && card.DefId == defId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasCardDefInDeck(string defId)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            return ContainsDef(registry, deck.DrawPileUids, defId)
                || ContainsDef(registry, deck.PlayerCardPoolUids, defId)
                || ContainsDef(registry, deck.ItemSlotUids, defId);
        }

        private static bool ContainsDef(
            CardRegistry registry,
            System.Collections.Generic.IReadOnlyList<int> uids,
            string defId)
        {
            for (var i = 0; i < uids.Count; i++)
            {
                CardInstance card;
                if (registry.TryGet(uids[i], out card)
                    && card != null
                    && card.DefId == defId)
                {
                    return true;
                }
            }

            return false;
        }

        private static GameContentCatalog BuildShopCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.Economy.SkipHelpChoiceGold = 10;
            catalog.Economy.UnusedHelpCardGold = 0;
            catalog.Economy.MonsterRemovedGold = 5;
            catalog.AddCard(new CardContentDefinition(ShopItemDefId, "店货", CardKind.HelpCard)
                .WithRarity(ContentRarity.White)
                .WithPrice(ShopItemPrice));
            catalog.AddCard(new CardContentDefinition("monster.hold", "占位怪", CardKind.Monster));
            catalog.Rewards
                .AddPool(new RewardPoolDefinition("shop.helpCards", 1)
                    .Add(ShopItemDefId, CardKind.HelpCard, 1))
                .AddRoom(new RoomDefinition(RoomKind.Shop, "商店")
                {
                    Weight = 1,
                    ShopOfferCount = 1
                });
            return catalog;
        }

        private static NodeDeckOptions CreateMinimalNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
        }
    }
}
