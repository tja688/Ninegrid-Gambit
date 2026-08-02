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
    /// ADR-0022 / #95：战斗房开局注入真正生效（玩家侧 + 困难房怪物侧）。
    /// Seam：<see cref="IRewardSystem.BuildNodeDeckOptions"/> 读取 <see cref="RunModel.Room"/> 的 OpeningInjects。
    /// </summary>
    public sealed class RoomOpeningInjectContractTests
    {
        private IArchitecture mArch;
        private IRewardSystem mReward;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
            mReward = mArch.GetSystem<IRewardSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
            mArch = null;
            mReward = null;
        }

        [Test]
        public void GoldRoom_InjectsOneGoldCard_AfterCapacity()
        {
            var player = mArch.GetModel<PlayerModel>();
            player.SetItemDeckCapacity(3);
            mArch.GetModel<RunModel>().Room.Value = RoomKind.Gold;
            mArch.GetModel<RunModel>().NodeIndex.Value = 1;

            var options = mReward.BuildNodeDeckOptions(2, null);
            Assert.AreEqual(4, options.PlayerCards.Count, "容量3 + 金币卡1");
            Assert.AreEqual("help.gold_card", options.PlayerCards[3].DefId);
        }

        [Test]
        public void TreasureAndFountain_InjectFixedCards()
        {
            var player = mArch.GetModel<PlayerModel>();
            player.SetItemDeckCapacity(2);
            var run = mArch.GetModel<RunModel>();
            run.NodeIndex.Value = 1;

            run.Room.Value = RoomKind.Treasure;
            var treasure = mReward.BuildNodeDeckOptions(2, null);
            Assert.AreEqual(3, treasure.PlayerCards.Count);
            Assert.AreEqual("help.common_chest_card", treasure.PlayerCards[2].DefId);

            run.Room.Value = RoomKind.Fountain;
            var fountain = mReward.BuildNodeDeckOptions(2, null);
            Assert.AreEqual(3, fountain.PlayerCards.Count);
            Assert.AreEqual("help.food_card", fountain.PlayerCards[2].DefId);
        }

        [Test]
        public void AttributeRoom_DrawsTwoFromPool_AllowingDuplicates()
        {
            var player = mArch.GetModel<PlayerModel>();
            player.SetItemDeckCapacity(0);
            var run = mArch.GetModel<RunModel>();
            run.Room.Value = RoomKind.Attribute;
            run.NodeIndex.Value = 1;

            var sawDuplicate = false;
            for (var seed = 1UL; seed <= 80UL; seed++)
            {
                ResetWithSeed(seed);
                player = mArch.GetModel<PlayerModel>();
                player.SetItemDeckCapacity(0);
                run = mArch.GetModel<RunModel>();
                run.Room.Value = RoomKind.Attribute;
                run.NodeIndex.Value = 1;

                var options = mReward.BuildNodeDeckOptions(2, null);
                Assert.AreEqual(2, options.PlayerCards.Count);
                Assert.IsTrue(IsAttributeCard(options.PlayerCards[0].DefId));
                Assert.IsTrue(IsAttributeCard(options.PlayerCards[1].DefId));
                if (options.PlayerCards[0].DefId == options.PlayerCards[1].DefId)
                {
                    sawDuplicate = true;
                    break;
                }
            }

            Assert.IsTrue(sawDuplicate, "属性房池可重复抽到同一张");
        }

        [Test]
        public void EliteRoom_PlayerInjectsTwoDistinct_AndAddsSeq3AndSeq4Monsters()
        {
            var player = mArch.GetModel<PlayerModel>();
            player.SetItemDeckCapacity(4);
            var run = mArch.GetModel<RunModel>();
            run.Room.Value = RoomKind.Elite;
            run.NodeIndex.Value = 5;

            var options = mReward.BuildNodeDeckOptions(6, null);
            Assert.AreEqual(6, options.PlayerCards.Count, "容量4 + 注入2");
            Assert.AreNotEqual(options.PlayerCards[4].DefId, options.PlayerCards[5].DefId, "困难房玩家侧不可重复");

            // 节点 6 基数：4+4+3+3=14；困难房再 +序列3、+序列4
            Assert.AreEqual(16, options.EnemyCards.Count, "节点基数 + 2");
            var bySeq = CountEnemySequences(options);
            Assert.AreEqual(4, bySeq[1]);
            Assert.AreEqual(4, bySeq[2]);
            Assert.AreEqual(4, bySeq[3], "基数3 + 注入1");
            Assert.AreEqual(4, bySeq[4], "基数3 + 注入1");
        }

        [Test]
        public void BossAndNonBattleRooms_DoNotInject()
        {
            var player = mArch.GetModel<PlayerModel>();
            player.SetItemDeckCapacity(3);
            var run = mArch.GetModel<RunModel>();

            run.NodeIndex.Value = 7;
            run.Room.Value = RoomKind.Boss;
            Assert.AreEqual(3, mReward.BuildNodeDeckOptions(8, null).PlayerCards.Count);

            run.NodeIndex.Value = 3;
            foreach (var kind in new[]
                     {
                         RoomKind.Shop,
                         RoomKind.Tavern,
                         RoomKind.TreasureReward,
                         RoomKind.ItemReward
                     })
            {
                run.Room.Value = kind;
                Assert.AreEqual(3, mReward.BuildNodeDeckOptions(2, null).PlayerCards.Count, kind.ToString());
            }
        }

        [Test]
        public void InjectOrder_IsAfterFixed_BeforeCarryPack()
        {
            var player = mArch.GetModel<PlayerModel>();
            player.SetItemDeckCapacity(1);
            player.AddFixedItemCard("help.fixed_card");
            player.AddToCarryPack("help.carry_a");
            var run = mArch.GetModel<RunModel>();
            run.Room.Value = RoomKind.Gold;
            run.NodeIndex.Value = 1;

            var options = mReward.BuildNodeDeckOptions(2, null);
            Assert.AreEqual(4, options.PlayerCards.Count);
            Assert.AreEqual("help.fixed_card", options.PlayerCards[1].DefId);
            Assert.AreEqual("help.gold_card", options.PlayerCards[2].DefId);
            Assert.AreEqual("help.carry_a", options.PlayerCards[3].DefId);
        }

        [Test]
        public void RandomBattleNode_AssignsBattleRoomWhenUnset()
        {
            var run = mArch.GetModel<RunModel>();
            Assert.AreEqual(0, run.NodeIndex.Value);
            Assert.AreEqual(RoomKind.None, run.Room.Value);

            mReward.BuildNodeDeckOptions(1, null);
            Assert.IsTrue(IsBattleOfferRoom(run.Room.Value), "节点1 随机战斗房应写入 Run.Room");
            Assert.AreNotEqual(RoomKind.Elite, run.Room.Value, "节点1 不应抽到困难房");
        }

        private void ResetWithSeed(ulong seed)
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = seed });
            mReward = mArch.GetSystem<IRewardSystem>();
        }

        private Dictionary<int, int> CountEnemySequences(NodeDeckOptions options)
        {
            var catalog = mArch.GetSystem<IContentSystem>().Catalog;
            var bySeq = new Dictionary<int, int>();
            for (var i = 0; i < options.EnemyCards.Count; i++)
            {
                Assert.IsTrue(catalog.TryGetCard(options.EnemyCards[i].DefId, out var card));
                if (!bySeq.ContainsKey(card.Sequence))
                {
                    bySeq[card.Sequence] = 0;
                }

                bySeq[card.Sequence]++;
            }

            return bySeq;
        }

        private static bool IsAttributeCard(string defId)
        {
            return defId == "help.hp_card"
                || defId == "help.armor_card"
                || defId == "help.attack_card";
        }

        private static bool IsBattleOfferRoom(RoomKind kind)
        {
            return kind == RoomKind.Attribute
                || kind == RoomKind.Gold
                || kind == RoomKind.Fountain
                || kind == RoomKind.Treasure
                || kind == RoomKind.Elite;
        }

        private static GameContentCatalog BuildCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.Economy.UnusedHelpCardGold = 10;

            catalog.AddCard(new CardContentDefinition("help.pool_a", "池A", CardKind.HelpCard).InDeck("deck.help"));
            catalog.AddCard(new CardContentDefinition("help.gold_card", "金币卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.food_card", "食品卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.common_chest_card", "宝箱卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.hp_card", "血量卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.armor_card", "加甲卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.attack_card", "加攻卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.fixed_card", "固定", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.carry_a", "携带", CardKind.HelpCard));

            for (var seq = 1; seq <= 5; seq++)
            {
                catalog.AddCard(new CardContentDefinition("monster.theme_a" + seq, "怪" + seq, CardKind.Monster)
                    .InDeck("deck.theme_a")
                    .WithSequence(seq));
            }

            catalog.AddMonsterDeck(new MonsterDeckDefinition("deck.theme_a", "主题A", MonsterDeckKind.Unknown)
                .AddMonster("monster.theme_a1")
                .AddMonster("monster.theme_a2")
                .AddMonster("monster.theme_a3")
                .AddMonster("monster.theme_a4")
                .AddMonster("monster.theme_a5"));

            catalog.Rewards.AddNodeRule(new NodeDeckRule { NodeIndex = 1, Seq1Count = 1 });
            catalog.Rewards.AddNodeRule(new NodeDeckRule
            {
                NodeIndex = 2,
                Seq1Count = 1
            });
            catalog.Rewards.AddNodeRule(new NodeDeckRule
            {
                NodeIndex = 6,
                Seq1Count = 4,
                Seq2Count = 4,
                Seq3Count = 3,
                Seq4Count = 3
            });
            catalog.Rewards.AddNodeRule(new NodeDeckRule
            {
                NodeIndex = 8,
                Seq1Count = 1,
                Seq5Count = 1
            });

            catalog.Rewards
                .AddRoom(new RoomDefinition(RoomKind.Gold, "金币房") { Weight = 10 }
                    .AddOpeningInject(new RoomInjectDeclaration
                    {
                        Side = RoomInjectSide.Player,
                        SourceKind = RoomInjectSourceKind.FixedCard,
                        CardDefId = "help.gold_card",
                        Count = 1
                    }))
                .AddRoom(new RoomDefinition(RoomKind.Treasure, "宝箱房") { Weight = 10 }
                    .AddOpeningInject(new RoomInjectDeclaration
                    {
                        Side = RoomInjectSide.Player,
                        SourceKind = RoomInjectSourceKind.FixedCard,
                        CardDefId = "help.common_chest_card",
                        Count = 1
                    }))
                .AddRoom(new RoomDefinition(RoomKind.Fountain, "恢复房") { Weight = 10 }
                    .AddOpeningInject(new RoomInjectDeclaration
                    {
                        Side = RoomInjectSide.Player,
                        SourceKind = RoomInjectSourceKind.FixedCard,
                        CardDefId = "help.food_card",
                        Count = 1
                    }))
                .AddRoom(new RoomDefinition(RoomKind.Attribute, "属性房") { Weight = 10 }
                    .AddOpeningInject(new RoomInjectDeclaration
                    {
                        Side = RoomInjectSide.Player,
                        SourceKind = RoomInjectSourceKind.WeightedPool,
                        Count = 2,
                        AllowDuplicates = true
                    }
                        .AddPoolOption("help.hp_card", 40)
                        .AddPoolOption("help.armor_card", 40)
                        .AddPoolOption("help.attack_card", 20)))
                .AddRoom(new RoomDefinition(RoomKind.Elite, "困难房") { Weight = 10 }
                    .AddOpeningInject(new RoomInjectDeclaration
                    {
                        Side = RoomInjectSide.Player,
                        SourceKind = RoomInjectSourceKind.WeightedPool,
                        Count = 2,
                        AllowDuplicates = false
                    }
                        .AddPoolOption("help.gold_card", 1)
                        .AddPoolOption("help.common_chest_card", 1)
                        .AddPoolOption("help.food_card", 1)
                        .AddPoolOption("help.hp_card", 1)
                        .AddPoolOption("help.armor_card", 1)
                        .AddPoolOption("help.attack_card", 1))
                    .AddOpeningInject(new RoomInjectDeclaration
                    {
                        Side = RoomInjectSide.Monster,
                        SourceKind = RoomInjectSourceKind.FloorMonsterSequence,
                        Count = 1,
                        MonsterSequence = 3
                    })
                    .AddOpeningInject(new RoomInjectDeclaration
                    {
                        Side = RoomInjectSide.Monster,
                        SourceKind = RoomInjectSourceKind.FloorMonsterSequence,
                        Count = 1,
                        MonsterSequence = 4
                    }))
                .AddRoom(new RoomDefinition(RoomKind.Boss, "层主房") { Weight = 1 })
                .AddRoom(new RoomDefinition(RoomKind.Shop, "商店") { Weight = 1 })
                .AddRoom(new RoomDefinition(RoomKind.Tavern, "卡店") { Weight = 1 })
                .AddRoom(new RoomDefinition(RoomKind.TreasureReward, "宝箱奖励") { Weight = 1 })
                .AddRoom(new RoomDefinition(RoomKind.ItemReward, "道具奖励") { Weight = 1 });

            return catalog;
        }
    }
}
