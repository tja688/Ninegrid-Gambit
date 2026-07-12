using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 金币盔甲：受到伤害时用金币抵消当前护甲伤害。
    /// </summary>
    public sealed class GoldArmorRegressionTests
    {
        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IStatSystem mStats;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, TableNineContentCatalog.CreateDefault());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 11UL, AvatarArmor = 0 });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mStats = mArch.GetSystem<IStatSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void GoldArmor_BaseArmor_AppliesOnNodeStart()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.gold_armor");
            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);

            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            Assert.AreEqual(1, mStats.GetEffectiveInt(avatar, StatId.Armor), "金币盔甲应提供基础护甲+1");
            Assert.AreEqual(1, StatArmorUtility.GetCurrentArmor(avatar), "关卡开始应重置当前护甲=有效护甲");
        }

        [Test]
        public void GoldArmor_AbsorbsArmorDamageWithGold_WhenArmorAndCoinsAvailable()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.gold_armor");
            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);

            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(50);
            StatArmorUtility.SetCurrentArmor(avatar, 3);

            var coinsBefore = player.Coins.Value;
            var armorBefore = StatArmorUtility.GetCurrentArmor(avatar);
            var hpBefore = (int)avatar.Stats.GetBase(StatId.Hp);

            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 2, "test"));
            mPipeline.RunToCompletion();

            Assert.AreEqual(armorBefore, StatArmorUtility.GetCurrentArmor(avatar), "金币应抵消护甲伤害");
            Assert.AreEqual(hpBefore, (int)avatar.Stats.GetBase(StatId.Hp), "护甲被金币抵消时不应扣血");
            Assert.AreEqual(coinsBefore - 10, player.Coins.Value, "每点护甲伤害应消耗5金币");
        }

        [Test]
        public void GoldArmor_DoesNotAbsorb_WhenNoArmor()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.gold_armor");
            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);

            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(50);
            StatArmorUtility.SetCurrentArmor(avatar, 0);

            var coinsBefore = player.Coins.Value;
            var hpBefore = (int)avatar.Stats.GetBase(StatId.Hp);

            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 2, "test"));
            mPipeline.RunToCompletion();

            Assert.AreEqual(coinsBefore, player.Coins.Value, "无护甲时不应消耗金币");
            Assert.AreEqual(hpBefore - 2, (int)avatar.Stats.GetBase(StatId.Hp), "无护甲时应扣血");
        }

        [Test]
        public void GoldArmor_GrantRelicAction_ActivatesRuleModifier()
        {
            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);

            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(50);
            StatArmorUtility.SetCurrentArmor(avatar, 2);

            mPipeline.Enqueue(new GrantRelicAction("relic.gold_armor"));
            mPipeline.RunToCompletion();

            var coinsBefore = player.Coins.Value;
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 2, "test"));
            mPipeline.RunToCompletion();

            Assert.AreEqual(2, StatArmorUtility.GetCurrentArmor(avatar), "获得遗物后金币应抵消护甲伤害");
            Assert.AreEqual(coinsBefore - 10, player.Coins.Value);
        }
    }
}
