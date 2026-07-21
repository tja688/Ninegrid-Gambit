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
    /// 阶段三：MaxHp ModifyBaseStat 管线与治疗封顶回归。
    /// </summary>
    public sealed class MaxHpRegressionTests
    {
        private IArchitecture mArch;
        private IStatSystem mStats;
        private IActionPipelineSystem mPipeline;
        private IContentSystem mContent;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, TableNineContentCatalog.CreateDefault());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 13UL, AvatarMaxHp = 20 });
            mStats = mArch.GetSystem<IStatSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mContent = mArch.GetSystem<IContentSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void VitalityAmulet_GrantsBaseMaxHpAndHp_OnActivate()
        {
            var avatar = Avatar();
            var hpBefore = (int)avatar.Stats.GetBase(StatId.Hp);
            var maxBefore = (int)avatar.Stats.GetBase(StatId.MaxHp);

            mContent.ActivateRelic("relic.vitality_amulet");
            mPipeline.RunToCompletion();

            avatar = Avatar();
            Assert.AreEqual(maxBefore + 6, (int)avatar.Stats.GetBase(StatId.MaxHp));
            Assert.AreEqual(hpBefore + 6, (int)avatar.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void HardSkin_GrantsBaseMaxHpAndHp_OnRelicGrant()
        {
            var avatar = Avatar();
            var hpBefore = (int)avatar.Stats.GetBase(StatId.Hp);
            var maxBefore = (int)avatar.Stats.GetBase(StatId.MaxHp);

            mContent.ActivateRelic("relic.hard_skin");
            mPipeline.RunToCompletion();

            avatar = Avatar();
            Assert.AreEqual(maxBefore + 10, (int)avatar.Stats.GetBase(StatId.MaxHp));
            Assert.AreEqual(hpBefore + 10, (int)avatar.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void Heal_CapsAtEffectiveMaxHp()
        {
            var avatar = Avatar();
            avatar.Stats.SetBase(StatId.Hp, 18);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            mStats.AddModifier(avatar, new StatModifier(
                StatId.MaxHp,
                ModifierOp.Add,
                5f,
                ModifierLayer.Persistent,
                new ModifierSource("test.bonus"),
                ModifierScope.Permanent));

            mPipeline.Enqueue(new HealAction(avatar.Uid, avatar.Uid, 10, "test"));
            mPipeline.RunToCompletion();

            avatar = Avatar();
            Assert.AreEqual(25, (int)avatar.Stats.GetBase(StatId.Hp), "应封顶在有效上限 25，而非基础 20+10");
        }

        private CardInstance Avatar()
        {
            return mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
        }
    }
}
