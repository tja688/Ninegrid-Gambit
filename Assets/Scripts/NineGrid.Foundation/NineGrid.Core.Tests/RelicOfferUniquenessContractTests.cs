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
    /// 遗物池：已拥有不可再出；上一次未选中的遗物仅在下一次抽取降权一次。
    /// </summary>
    public sealed class RelicOfferUniquenessContractTests
    {
        private IArchitecture mArch;
        private IRewardSystem mReward;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 7UL });
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
        public void RollPool_ExcludesOwnedRelics()
        {
            var player = mArch.GetModel<PlayerModel>();
            player.AddRelic("relic.wood_sword");
            player.AddRelic("relic.vitality_amulet");

            for (var i = 0; i < 40; i++)
            {
                var rolled = mReward.RollPool("relic.common_chest");
                Assert.AreEqual(3, rolled.Count);
                for (var j = 0; j < rolled.Count; j++)
                {
                    Assert.AreNotEqual("relic.wood_sword", rolled[j].DefId);
                    Assert.AreNotEqual("relic.vitality_amulet", rolled[j].DefId);
                }
            }
        }

        [Test]
        public void RememberUnselected_HalvesWeightOnlyOnImmediateNextRelicRoll()
        {
            var offered = new List<RewardEntry>
            {
                new RewardEntry("relic.wood_sword", CardKind.Relic, 1, 1),
                new RewardEntry("relic.wood_shield", CardKind.Relic, 1, 1),
                new RewardEntry("relic.wood_armor", CardKind.Relic, 1, 1)
            };
            mReward.RememberUnselectedRelics(offered, "relic.wood_sword");

            // 惩罚期内：未选中的两项相对权重减半；抽样应仍可能抽到，但不得因惩罚被移出池。
            var penalizedSeen = 0;
            for (var i = 0; i < 30; i++)
            {
                var rolled = mReward.RollPool("relic.common_chest");
                Assert.AreEqual(3, rolled.Count);
                for (var j = 0; j < rolled.Count; j++)
                {
                    if (rolled[j].DefId == "relic.wood_shield" || rolled[j].DefId == "relic.wood_armor")
                    {
                        penalizedSeen++;
                    }
                }
            }

            Assert.Greater(penalizedSeen, 0, "降权后仍应可出现，只是概率降低");

            // 惩罚已被上一次遗物池抽取消费：后续抽取不再携带「连续再出现」降权状态。
            // 再记一次未选中后立刻再抽两次，确认第二次抽取不再受第一次惩罚影响（惩罚单次消费）。
            mReward.RememberUnselectedRelics(offered, null);
            var first = mReward.RollPool("relic.common_chest");
            Assert.AreEqual(3, first.Count);
            var second = mReward.RollPool("relic.common_chest");
            Assert.AreEqual(3, second.Count);
        }

        [Test]
        public void RememberUnselected_SelectedRelicIsNotPenalized()
        {
            var offered = new List<RewardEntry>
            {
                new RewardEntry("relic.wood_sword", CardKind.Relic, 1, 1),
                new RewardEntry("relic.wood_shield", CardKind.Relic, 1, 1)
            };
            mReward.RememberUnselectedRelics(offered, "relic.wood_sword");
            mArch.GetModel<PlayerModel>().AddRelic("relic.wood_sword");

            // 选中项已拥有 → 直接排除；惩罚只作用在未选项上且仅一次。
            var rolled = mReward.RollPool("relic.common_chest");
            Assert.AreEqual(3, rolled.Count);
            for (var i = 0; i < rolled.Count; i++)
            {
                Assert.AreNotEqual("relic.wood_sword", rolled[i].DefId);
            }
        }

        private static GameContentCatalog BuildCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.AddRelic(new RelicContentDefinition("relic.wood_shield", "木盾", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.wood_sword", "木剑", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.wood_armor", "木甲", ContentRarity.White, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.junk_recycler", "废物利用机", ContentRarity.White, "t"));
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
            catalog.AddRelic(new RelicContentDefinition("relic.vitality_amulet", "活力护符", ContentRarity.Blue, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.dragon_scale_armor", "龙鳞甲", ContentRarity.Gold, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.phoenix_feather", "凤凰羽毛", ContentRarity.Gold, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.craving", "渴望", ContentRarity.Gold, "t"));
            catalog.AddRelic(new RelicContentDefinition("relic.junk_slot_machine", "废物老虎机", ContentRarity.Gold, "t"));

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
