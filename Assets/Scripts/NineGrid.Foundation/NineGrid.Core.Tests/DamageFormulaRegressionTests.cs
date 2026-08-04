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
    /// ADR-0028：标准伤害公式 — 伤害减免、无视护甲、与金甲交互。
    /// </summary>
    public sealed class DamageFormulaRegressionTests
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
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 28UL, AvatarArmor = 0 });
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
        public void DamageReduction_SubtractsBeforeArmorAbsorb()
        {
            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);
            var avatar = GetAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 3);
            GrantDamageReduction(avatar.Uid, 1);
            var hpBefore = (int)avatar.Stats.GetBase(StatId.Hp);

            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 3, "test"));
            mPipeline.RunToCompletion();

            // 原始伤害 = 3 - 1 = 2；甲吸收 2，血不变
            Assert.AreEqual(1, StatArmorUtility.GetCurrentArmor(avatar));
            Assert.AreEqual(hpBefore, (int)avatar.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void DamageReduction_GteAttack_YieldsZeroRawDamage()
        {
            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);
            var avatar = GetAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 5);
            GrantDamageReduction(avatar.Uid, 4);
            var hpBefore = (int)avatar.Stats.GetBase(StatId.Hp);

            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 3, "test"));
            mPipeline.RunToCompletion();

            Assert.AreEqual(5, StatArmorUtility.GetCurrentArmor(avatar), "减免≥攻击时甲不变");
            Assert.AreEqual(hpBefore, (int)avatar.Stats.GetBase(StatId.Hp), "减免≥攻击时血不变");
        }

        [Test]
        public void IgnoreArmor_DealsFullHpLoss_LeavesArmorIntact()
        {
            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);
            var avatar = GetAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 5);
            var hpBefore = (int)avatar.Stats.GetBase(StatId.Hp);

            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 3, "test", null, ignoreArmor: true));
            mPipeline.RunToCompletion();

            Assert.AreEqual(5, StatArmorUtility.GetCurrentArmor(avatar), "无视护甲不应扣甲");
            Assert.AreEqual(hpBefore - 3, (int)avatar.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void IgnoreArmor_WithDamageReduction_StillSkipsArmor()
        {
            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);
            var avatar = GetAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 5);
            GrantDamageReduction(avatar.Uid, 1);
            var hpBefore = (int)avatar.Stats.GetBase(StatId.Hp);

            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 3, "test", null, ignoreArmor: true));
            mPipeline.RunToCompletion();

            // 原始 = 3 - 1 = 2；无视甲 → 只掉 2 血
            Assert.AreEqual(5, StatArmorUtility.GetCurrentArmor(avatar));
            Assert.AreEqual(hpBefore - 2, (int)avatar.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void GoldArmor_AbsorbsOnlyArmorSegment_NotOverflowHp()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.gold_armor");
            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);
            var avatar = GetAvatar();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(50);
            StatArmorUtility.SetCurrentArmor(avatar, 2);
            var coinsBefore = player.Coins.Value;
            var hpBefore = (int)avatar.Stats.GetBase(StatId.Hp);

            // 原始 4：拟甲伤 2（金抵）、溢出 2 打血
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 4, "test"));
            mPipeline.RunToCompletion();

            Assert.AreEqual(2, StatArmorUtility.GetCurrentArmor(avatar), "金甲应抵掉拟甲伤段");
            Assert.AreEqual(hpBefore - 2, (int)avatar.Stats.GetBase(StatId.Hp), "溢出段应扣血");
            Assert.AreEqual(coinsBefore - 10, player.Coins.Value, "仅甲伤段耗金：2×5");
        }

        [Test]
        public void IgnoreArmor_DoesNotSpendGoldArmor()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.gold_armor");
            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);
            var avatar = GetAvatar();
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(50);
            StatArmorUtility.SetCurrentArmor(avatar, 3);
            var coinsBefore = player.Coins.Value;
            var hpBefore = (int)avatar.Stats.GetBase(StatId.Hp);

            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 2, "test", null, ignoreArmor: true));
            mPipeline.RunToCompletion();

            Assert.AreEqual(3, StatArmorUtility.GetCurrentArmor(avatar));
            Assert.AreEqual(hpBefore - 2, (int)avatar.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(coinsBefore, player.Coins.Value, "无视护甲路径不花金甲");
        }

        [Test]
        public void DamageFlatDelta_ThenDamageReduction_Order()
        {
            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);
            var avatar = GetAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 0);
            // Flat -1 then Reduction 1：Amount 5 → afterRules 4 → raw 3
            mStats.RuleModifiers.Add(new RuleModifier(
                RuleId.DamageFlatDelta,
                ModifierOp.Add,
                -1f,
                ModifierLayer.Persistent,
                new ModifierSource("test:flat"),
                ModifierScope.Permanent,
                new TargetUidCondition(avatar.Uid)));
            GrantDamageReduction(avatar.Uid, 1);
            var hpBefore = (int)avatar.Stats.GetBase(StatId.Hp);

            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 5, "test"));
            mPipeline.RunToCompletion();

            Assert.AreEqual(hpBefore - 3, (int)avatar.Stats.GetBase(StatId.Hp));
        }

        private CardInstance GetAvatar()
        {
            return mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
        }

        private void GrantDamageReduction(int targetUid, int amount)
        {
            mStats.RuleModifiers.Add(new RuleModifier(
                RuleId.DamageReduction,
                ModifierOp.Add,
                amount,
                ModifierLayer.Persistent,
                new ModifierSource("test:damage_reduction:" + targetUid),
                ModifierScope.Permanent,
                new TargetUidCondition(targetUid)));
        }
    }
}
