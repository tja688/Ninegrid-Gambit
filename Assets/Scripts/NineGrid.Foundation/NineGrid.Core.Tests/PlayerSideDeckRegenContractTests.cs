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
    /// ADR-0022 / #87：玩家侧卡组每关重生成；跨节点持有生成规则而非持久牌堆。
    /// Seams：PlayerModel 规则位、BuildNodeDeckOptions 生成顺序、Grant→道具卡格、清关保留道具卡格。
    /// </summary>
    public sealed class PlayerSideDeckRegenContractTests
    {
        private IArchitecture mArch;
        private IRewardSystem mReward;
        private IPhaseSystem mPhase;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 11UL });
            mReward = mArch.GetSystem<IRewardSystem>();
            mPhase = mArch.GetSystem<IPhaseSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
            mArch = null;
            mReward = null;
            mPhase = null;
        }

        [Test]
        public void Bootstrap_SeedsCapacityAndSourcePool_NotPersistentStacks()
        {
            var player = mArch.GetModel<PlayerModel>();
            Assert.AreEqual(PlayerModel.DefaultItemDeckCapacity, player.ItemDeckCapacity);
            CollectionAssert.Contains(player.ItemSourcePoolDefIds, "help.pool_a");
            CollectionAssert.Contains(player.ItemSourcePoolDefIds, "help.pool_b");
            CollectionAssert.Contains(player.ItemSourcePoolDefIds, "help.warrior_only");
            CollectionAssert.DoesNotContain(player.ItemSourcePoolDefIds, "help.fixed_card");
            CollectionAssert.DoesNotContain(player.ItemSourcePoolDefIds, "help.carry_a");
            Assert.AreEqual(3, player.ItemSourcePoolDefIds.Count, "仅 deck.help + deck.player 的道具卡");
            Assert.AreEqual(0, player.FixedItemCardDefIds.Count);
            Assert.AreEqual(PlayerModel.DefaultItemSlotsCapacity, player.ItemSlotsCapacity);
        }

        [Test]
        public void BuildNodeDeckOptions_DrawsCapacityFromPool_AndKeepsRules()
        {
            var player = mArch.GetModel<PlayerModel>();
            var poolBefore = player.ItemSourcePoolDefIds.Count;
            var first = mReward.BuildNodeDeckOptions(1, null);
            Assert.AreEqual(player.ItemDeckCapacity, first.PlayerCards.Count);
            Assert.AreEqual(PlayerModel.DefaultItemDeckCapacity, player.ItemDeckCapacity);
            Assert.AreEqual(poolBefore, player.ItemSourcePoolDefIds.Count, "来源池跨节点不消耗");

            var second = mReward.BuildNodeDeckOptions(2, null);
            Assert.AreEqual(player.ItemDeckCapacity, second.PlayerCards.Count);
        }

        [Test]
        public void BuildNodeDeckOptions_AppendsFixed_WithoutCarryPackInject()
        {
            var player = mArch.GetModel<PlayerModel>();
            player.SetItemDeckCapacity(2);
            player.AddFixedItemCard("help.fixed_card");

            var options = mReward.BuildNodeDeckOptions(1, null);
            Assert.AreEqual(3, options.PlayerCards.Count, "容量2 + 固定1");
            Assert.AreEqual("help.fixed_card", options.PlayerCards[2].DefId);
            Assert.AreEqual(1, player.FixedItemCardDefIds.Count, "固定卡跨节点保留");
        }

        [Test]
        public void GrantHelpCardToPlayerSideDeck_GoesToItemSlots()
        {
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new GrantHelpCardToPlayerSideDeckAction("help.carry_a", 2));
            pipeline.RunToCompletion();

            Assert.AreEqual(2, CountItemSlotsByDef("help.carry_a"));
        }

        [Test]
        public void SettleUnusedHelpCards_PreservesItemSlots_DoesNotRefillStacks()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(SlotId.Board(2));
            SpawnHelpIntoItemSlots("help.settle_gold");

            var player = mArch.GetModel<PlayerModel>();
            var coinsBefore = player.Coins.Value;
            var capacity = player.ItemDeckCapacity;
            var poolCount = player.ItemSourcePoolDefIds.Count;

            mArch.GetModel<BattleContextModel>().MarkLeaveTrapBroken();
            Assert.IsTrue(mPhase.TryCompleteClearedNode().Accepted);

            Assert.AreEqual(coinsBefore, player.Coins.Value, "道具卡格不应因清关兑金");
            Assert.AreEqual(1, CountItemSlotHelpCards(), "清关后道具卡格应保留");
            Assert.AreEqual(capacity, player.ItemDeckCapacity, "清关不改容量");
            Assert.AreEqual(poolCount, player.ItemSourcePoolDefIds.Count, "清关不改来源池");
        }

        [Test]
        public void GenerationRules_CanRoundTripViaPublicSetters()
        {
            var player = mArch.GetModel<PlayerModel>();
            player.SetItemDeckCapacity(9);
            player.SetItemSlotsCapacity(4);
            player.ReplaceItemSourcePool(new[] { "help.pool_a", "help.fixed_card" });
            player.ReplaceFixedItemCards(new[] { "help.fixed_card" });

            Assert.AreEqual(9, player.ItemDeckCapacity);
            Assert.AreEqual(4, player.ItemSlotsCapacity);
            Assert.AreEqual(2, player.ItemSourcePoolDefIds.Count);
            Assert.AreEqual(1, player.FixedItemCardDefIds.Count);

            player.SetItemSlotsCapacity(PlayerModel.MaxItemSlotsCapacity + 3);
            Assert.AreEqual(PlayerModel.MaxItemSlotsCapacity, player.ItemSlotsCapacity);
            Assert.AreEqual(9, player.ItemDeckCapacity);
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

        private void SpawnHelpIntoItemSlots(string defId)
        {
            var content = mArch.GetSystem<IContentSystem>();
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var card = content.CreateDraft(defId).Create(registry);
            deck.AddToItemSlots(card);
        }

        private int CountItemSlotHelpCards()
        {
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var count = 0;
            for (var i = 0; i < deck.ItemSlotUids.Count; i++)
            {
                CardInstance card;
                if (registry.TryGet(deck.ItemSlotUids[i], out card)
                    && card != null
                    && card.Kind == CardKind.HelpCard)
                {
                    count++;
                }
            }

            return count;
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

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }

        private static GameContentCatalog BuildCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.Economy.UnusedHelpCardGold = 10;
            catalog.Economy.MonsterRemovedGold = 0;

            catalog.AddCard(new CardContentDefinition("help.pool_a", "池A", CardKind.HelpCard).InDeck("deck.help"));
            catalog.AddCard(new CardContentDefinition("help.pool_b", "池B", CardKind.HelpCard).InDeck("deck.help"));
            catalog.AddCard(new CardContentDefinition("help.warrior_only", "战士专属", CardKind.HelpCard).InDeck("deck.player"));
            catalog.AddCard(new CardContentDefinition("help.fixed_card", "固定", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.carry_a", "携带A", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.carry_b", "携带B", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.settle_gold", "结算", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("monster.test", "测试怪", CardKind.Monster));
            catalog.AddCard(new CardContentDefinition("monster.theme_a1", "怪1", CardKind.Monster)
                .InDeck("deck.theme_a")
                .WithSequence(1));

            catalog.AddMonsterDeck(new MonsterDeckDefinition("deck.theme_a", "主题A", MonsterDeckKind.Unknown)
                .AddMonster("monster.theme_a1"));
            catalog.Rewards.AddNodeRule(new NodeDeckRule { NodeIndex = 1, Seq1Count = 1 });
            catalog.Rewards.AddNodeRule(new NodeDeckRule { NodeIndex = 2, Seq1Count = 1 });
            catalog.Rewards
                .AddRoom(new RoomDefinition(RoomKind.Gold, "金币房") { Weight = 1 })
                .AddRoom(new RoomDefinition(RoomKind.Fountain, "恢复房") { Weight = 1 });

            return catalog;
        }
    }
}
