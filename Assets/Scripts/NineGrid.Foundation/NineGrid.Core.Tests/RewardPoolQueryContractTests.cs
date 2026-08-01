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
    /// #71：奖池查询规则展开 + role 均衡契约。
    /// </summary>
    public sealed class RewardPoolQueryContractTests
    {
        private IArchitecture mArch;
        private IRewardSystem mReward;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            var catalog = BuildCatalog();
            RewardPoolQueryExpander.ExpandAll(catalog);
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
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
        public void HelpChoice_QueryExcludesRedAndIncludesRoleTaggedCards()
        {
            var catalog = mArch.GetSystem<IContentSystem>().Catalog;
            Assert.IsTrue(catalog.Rewards.TryGetPool("help.choice", out var pool));
            Assert.IsNotNull(pool.Query);
            Assert.GreaterOrEqual(pool.Entries.Count, 5);

            var ids = new HashSet<string>();
            for (var i = 0; i < pool.Entries.Count; i++)
            {
                ids.Add(pool.Entries[i].DefId);
            }

            Assert.IsTrue(ids.Contains("help.brutality_card"));
            Assert.IsTrue(ids.Contains("help.sturdy_shield"));
            Assert.IsFalse(ids.Contains("help.flame"));
            Assert.IsFalse(ids.Contains("trap.flame"), "烈焰已迁 Trap，不应出现在 help.choice");
        }

        [Test]
        public void HelpChoice_RollHonorsMinAttackAndDefenseRoles()
        {
            for (var i = 0; i < 20; i++)
            {
                var rolled = mReward.RollPool("help.choice");
                Assert.AreEqual(3, rolled.Count);

                var catalog = mArch.GetSystem<IContentSystem>().Catalog;
                var attack = 0;
                var defense = 0;
                for (var j = 0; j < rolled.Count; j++)
                {
                    Assert.IsTrue(catalog.Cards.TryGetValue(rolled[j].DefId, out var card));
                    if (card.Role == ContentRole.Attack)
                    {
                        attack++;
                    }
                    else if (card.Role == ContentRole.Defense)
                    {
                        defense++;
                    }
                }

                Assert.GreaterOrEqual(attack, 1, "help.choice 应至少含一张 Attack");
                Assert.GreaterOrEqual(defense, 1, "help.choice 应至少含一张 Defense");
            }
        }

        [Test]
        public void KillBoss_QueryUsesTagFilter()
        {
            var catalog = mArch.GetSystem<IContentSystem>().Catalog;
            Assert.IsTrue(catalog.Rewards.TryGetPool("kill.boss", out var pool));
            Assert.AreEqual(3, pool.Entries.Count);
            var ids = new HashSet<string>();
            for (var i = 0; i < pool.Entries.Count; i++)
            {
                ids.Add(pool.Entries[i].DefId);
            }

            Assert.IsTrue(ids.Contains("help.golden_chest_card"));
            Assert.IsTrue(ids.Contains("help.gold_card"));
            Assert.IsTrue(ids.Contains("help.stat_boost_card"));
        }

        private static GameContentCatalog BuildCatalog()
        {
            var catalog = new GameContentCatalog();

            catalog.AddCard(new CardContentDefinition("help.healing_potion", "恢复药水", CardKind.HelpCard)
                .WithRarity(ContentRarity.White).WithRole(ContentRole.Utility).AddTag("tag.heal"));
            catalog.AddCard(new CardContentDefinition("help.throwing_knife", "飞刀", CardKind.HelpCard)
                .WithRarity(ContentRarity.White).WithRole(ContentRole.Attack).AddTag("tag.direct_damage"));
            catalog.AddCard(new CardContentDefinition("help.brutality_card", "暴力卡", CardKind.HelpCard)
                .WithRarity(ContentRarity.White).WithRole(ContentRole.Attack).AddTag("tag.attack_buff"));
            catalog.AddCard(new CardContentDefinition("help.sturdy_shield", "耐用盾牌", CardKind.HelpCard)
                .WithRarity(ContentRarity.White).WithRole(ContentRole.Defense).AddTag("tag.armor"));
            catalog.AddCard(new CardContentDefinition("help.ward_magic_card", "守护", CardKind.HelpCard)
                .WithRarity(ContentRarity.White).WithRole(ContentRole.Defense).AddTag("tag.armor"));
            catalog.AddCard(new CardContentDefinition("help.gold_card", "金币卡", CardKind.HelpCard)
                .WithRarity(ContentRarity.Blue).WithRole(ContentRole.Utility)
                .AddTag("tag.economy").AddTag("tag.kill_boss").AddTag("tag.kill_elite"));
            catalog.AddCard(new CardContentDefinition("help.stat_boost_card", "属性提升卡", CardKind.HelpCard)
                .WithRarity(ContentRarity.Gold).WithRole(ContentRole.Utility)
                .AddTag("tag.special").AddTag("tag.kill_boss").AddTag("tag.kill_elite"));
            catalog.AddCard(new CardContentDefinition("help.golden_chest_card", "金色宝箱卡", CardKind.HelpCard)
                .WithRarity(ContentRarity.Red).WithRole(ContentRole.Utility)
                .AddTag("tag.special").AddTag("tag.kill_boss"));
            catalog.AddCard(new CardContentDefinition("help.flame", "烈焰", CardKind.HelpCard)
                .WithRarity(ContentRarity.Red).WithRole(ContentRole.Utility).AddTag("tag.special"));

            catalog.Rewards.AddPool(new RewardPoolDefinition("help.choice", 3)
                .WithQuery(new RewardPoolQueryRule
                {
                    Kind = CardKind.HelpCard,
                    DefaultWeight = 1,
                    RarityWeightWhite = 65,
                    RarityWeightBlue = 30,
                    RarityWeightGold = 5,
                    BalanceMinAttack = 1,
                    BalanceMinDefense = 1,
                }
                    .AllowRarity(ContentRarity.White)
                    .AllowRarity(ContentRarity.Blue)
                    .AllowRarity(ContentRarity.Gold)));

            catalog.Rewards.AddPool(new RewardPoolDefinition("kill.boss", 3)
                .WithQuery(new RewardPoolQueryRule
                {
                    Kind = CardKind.HelpCard,
                    DefaultWeight = 1,
                }.AllowTag("tag.kill_boss")));

            return catalog;
        }
    }
}
