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
    /// 奖励池分层抽取契约（主题卡组按层绑定见 <see cref="ThemeMonsterDeckContractTests"/>）。
    /// </summary>
    public sealed class RewardPoolDiversityContractTests
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
        public void HelpChoice_PoolContainsNonRedHelpCards()
        {
            var catalog = mArch.GetSystem<IContentSystem>().Catalog;
            Assert.IsTrue(catalog.Rewards.TryGetPool("help.choice", out var pool));
            Assert.GreaterOrEqual(pool.Entries.Count, 5);

            var ids = new HashSet<string>();
            for (var i = 0; i < pool.Entries.Count; i++)
            {
                ids.Add(pool.Entries[i].DefId);
            }

            Assert.IsTrue(ids.Contains("help.brutality_card"));
            Assert.IsTrue(ids.Contains("help.watchtower") || ids.Contains("help.sturdy_shield"));
            Assert.IsFalse(ids.Contains("help.flame"));
            Assert.IsFalse(ids.Contains("help.golden_chest_card"));
        }

        [Test]
        public void RelicCommonChest_PoolContainsImplementedRelics()
        {
            var catalog = mArch.GetSystem<IContentSystem>().Catalog;
            Assert.IsTrue(catalog.Rewards.TryGetPool("relic.common_chest", out var pool));
            Assert.GreaterOrEqual(pool.Entries.Count, 5);

            var ids = new HashSet<string>();
            for (var i = 0; i < pool.Entries.Count; i++)
            {
                ids.Add(pool.Entries[i].DefId);
            }

            Assert.IsTrue(ids.Contains("relic.wood_sword"));
            Assert.IsTrue(ids.Contains("relic.junk_recycler") || ids.Contains("relic.wood_shield"));
            Assert.IsTrue(ids.Contains("relic.craving") || ids.Contains("relic.phoenix_feather"));
        }

        [Test]
        public void HelpChoice_RollsThreeDistinctOptions()
        {
            var rolled = mReward.RollPool("help.choice");
            Assert.AreEqual(3, rolled.Count);
            Assert.AreNotEqual(rolled[0].DefId, rolled[1].DefId);
            Assert.AreNotEqual(rolled[0].DefId, rolled[2].DefId);
            Assert.AreNotEqual(rolled[1].DefId, rolled[2].DefId);
        }

        private static GameContentCatalog BuildCatalog()
        {
            var catalog = new GameContentCatalog();

            catalog.AddCard(new CardContentDefinition("help.healing_potion", "恢复药水", CardKind.HelpCard)
                .WithRarity(ContentRarity.White));
            catalog.AddCard(new CardContentDefinition("help.throwing_knife", "飞刀", CardKind.HelpCard)
                .WithRarity(ContentRarity.White));
            catalog.AddCard(new CardContentDefinition("help.brutality_card", "暴力卡", CardKind.HelpCard)
                .WithRarity(ContentRarity.White));
            catalog.AddCard(new CardContentDefinition("help.sturdy_shield", "耐用盾牌", CardKind.HelpCard)
                .WithRarity(ContentRarity.White));
            catalog.AddCard(new CardContentDefinition("help.bomb", "爆弹", CardKind.HelpCard)
                .WithRarity(ContentRarity.White));
            catalog.AddCard(new CardContentDefinition("help.gold_card", "金币卡", CardKind.HelpCard)
                .WithRarity(ContentRarity.Blue));
            catalog.AddCard(new CardContentDefinition("help.food_card", "食品卡", CardKind.HelpCard)
                .WithRarity(ContentRarity.Blue));
            catalog.AddCard(new CardContentDefinition("help.watchtower", "瞭望塔", CardKind.HelpCard)
                .WithRarity(ContentRarity.Gold));
            catalog.AddCard(new CardContentDefinition("help.stat_boost_card", "属性提升卡", CardKind.HelpCard)
                .WithRarity(ContentRarity.Gold));
            catalog.AddCard(new CardContentDefinition("help.flame", "烈焰", CardKind.HelpCard)
                .WithRarity(ContentRarity.Red));
            catalog.AddCard(new CardContentDefinition("help.golden_chest_card", "金色宝箱卡", CardKind.HelpCard)
                .WithRarity(ContentRarity.Red));

            catalog.AddRelic(new RelicContentDefinition("relic.wood_shield", "木盾", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.wood_sword", "木剑", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.junk_recycler", "废物利用机", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.vitality_amulet", "活力护符", ContentRarity.Blue, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.dragon_scale_armor", "龙鳞甲", ContentRarity.Gold, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.craving", "渴望", ContentRarity.Gold, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.phoenix_feather", "凤凰羽毛", ContentRarity.Gold, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.junk_slot_machine", "废物老虎机", ContentRarity.Gold, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.lucky_coin", "幸运硬币", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.throwing_knife_bag", "飞刀袋", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.potion_bag", "药水袋", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.junk_launcher", "废物发射器", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.junk_coating", "废物涂层", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.sling", "弹弓", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.shield_knife", "打盾刀", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.gold_knife", "打金刀", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.heavy_armor", "重盔甲", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.gold_armor", "金币盔甲", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.wood_armor", "木甲", ContentRarity.White, "t"));

            catalog.Rewards.AddPool(new RewardPoolDefinition("help.choice", 3)
                .Add("help.healing_potion", CardKind.HelpCard, 1)
                .Add("help.throwing_knife", CardKind.HelpCard, 1)
                .Add("help.brutality_card", CardKind.HelpCard, 1)
                .Add("help.sturdy_shield", CardKind.HelpCard, 1)
                .Add("help.bomb", CardKind.HelpCard, 1)
                .Add("help.gold_card", CardKind.HelpCard, 1)
                .Add("help.food_card", CardKind.HelpCard, 1)
                .Add("help.watchtower", CardKind.HelpCard, 1)
                .Add("help.stat_boost_card", CardKind.HelpCard, 1));

            catalog.Rewards.AddPool(new RewardPoolDefinition("relic.common_chest", 3)
                .Add("relic.wood_shield", CardKind.Relic, 1)
                .Add("relic.wood_sword", CardKind.Relic, 1)
                .Add("relic.wood_armor", CardKind.Relic, 1)
                .Add("relic.junk_recycler", CardKind.Relic, 1)
                .Add("relic.lucky_coin", CardKind.Relic, 1)
                .Add("relic.throwing_knife_bag", CardKind.Relic, 1)
                .Add("relic.potion_bag", CardKind.Relic, 1)
                .Add("relic.junk_launcher", CardKind.Relic, 1)
                .Add("relic.junk_coating", CardKind.Relic, 1)
                .Add("relic.sling", CardKind.Relic, 1)
                .Add("relic.shield_knife", CardKind.Relic, 1)
                .Add("relic.gold_knife", CardKind.Relic, 1)
                .Add("relic.heavy_armor", CardKind.Relic, 1)
                .Add("relic.gold_armor", CardKind.Relic, 1)
                .Add("relic.vitality_amulet", CardKind.Relic, 1)
                .Add("relic.dragon_scale_armor", CardKind.Relic, 1)
                .Add("relic.phoenix_feather", CardKind.Relic, 1)
                .Add("relic.craving", CardKind.Relic, 1)
                .Add("relic.junk_slot_machine", CardKind.Relic, 1));

            return catalog;
        }
    }
}
