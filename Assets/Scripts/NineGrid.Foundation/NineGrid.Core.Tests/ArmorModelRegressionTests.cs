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
    /// 阶段三：基础/有效/当前护甲三层模型回归。
    /// </summary>
    public sealed class ArmorModelRegressionTests
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
        public void WoodShieldModifier_AbsorbsDamage_AfterNodeStartReset()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.wood_shield");
            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);

            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            Assert.AreEqual(1, mStats.GetEffectiveInt(avatar, StatId.Armor), "木盾应 +1 有效护甲");
            Assert.AreEqual(1, StatArmorUtility.GetCurrentArmor(avatar), "关卡开始应重置当前护甲=有效护甲");

            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 1, "test"));
            mPipeline.RunToCompletion();

            Assert.AreEqual(0, StatArmorUtility.GetBaseArmor(avatar), "木盾是有效护甲修正，不应改基础护甲");
            Assert.AreEqual(0, StatArmorUtility.GetCurrentArmor(avatar), "伤害应扣当前护甲");
            Assert.AreEqual(mStats.GetEffectiveInt(avatar, StatId.MaxHp), (int)avatar.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void NodeStart_RestoresCurrentArmor_AfterPriorDepletion()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.wood_shield");
            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);

            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 1, "test"));
            mPipeline.RunToCompletion();
            Assert.AreEqual(0, StatArmorUtility.GetCurrentArmor(avatar));

            mPipeline.Enqueue(new ResetCurrentArmorAction());
            mPipeline.RunToCompletion();
            avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            Assert.AreEqual(1, StatArmorUtility.GetCurrentArmor(avatar), "新关卡应恢复当前护甲");
        }

        [Test]
        public void HeavyArmor_NodeStart_BonusUsesBaseArmor_NotEffective()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.heavy_armor");
            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);

            mStats.AddModifier(avatar, new StatModifier(
                StatId.Armor,
                ModifierOp.Add,
                4f,
                ModifierLayer.Persistent,
                new ModifierSource("test.extra"),
                ModifierScope.Permanent));

            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);
            avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);

            // 有效护甲 = 0 基础 + 1 重盔甲 + 4 测试 = 5；基础护甲 = 0。
            // 节点开始：当前 = 5；重盔甲 floor(0*0.5)=0 额外，不应按有效 5 给 +2。
            Assert.AreEqual(5, StatArmorUtility.GetCurrentArmor(avatar));
        }

        [Test]
        public void GainArmor_AddsToCurrent_NotBase()
        {
            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);
            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Armor, 2);
            StatArmorUtility.SetCurrentArmor(avatar, 1);

            mPipeline.Enqueue(new GainArmorAction(avatar.Uid, 3, "test"));
            mPipeline.RunToCompletion();

            Assert.AreEqual(2, StatArmorUtility.GetBaseArmor(avatar));
            Assert.AreEqual(4, StatArmorUtility.GetCurrentArmor(avatar));
        }

        [Test]
        public void ArmorBreakingHammer_ReducesMonsterCurrentArmor_NotBaseOnly()
        {
            var node = new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster)
            {
                MaxHp = 20,
                Attack = 0,
                Armor = 5
            });
            Assert.IsTrue(mPhase.StartNode(node).Accepted);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var monsterUid = 0;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == board.AvatarSlot.Value)
                {
                    continue;
                }

                monsterUid = board.GetCardUid(slot);
                if (monsterUid != 0)
                {
                    break;
                }
            }

            Assert.Greater(monsterUid, 0);
            var monster = registry.Get(monsterUid);
            Assert.AreEqual(5, StatArmorUtility.GetBaseArmor(monster));
            Assert.AreEqual(5, StatArmorUtility.GetCurrentArmor(monster));

            mPipeline.Enqueue(new SpawnCardAction("help.armor_breaking_hammer", CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var hammerUid = mArch.GetModel<DeckModel>().ItemSlotUids[mArch.GetModel<DeckModel>().ItemSlotUids.Count - 1];

            var use = mPhase.ApplyUseItem(hammerUid, new System.Collections.Generic.List<int> { monsterUid }, null);
            Assert.IsTrue(use.Accepted, use.Reason);

            monster = registry.Get(monsterUid);
            Assert.AreEqual(5, StatArmorUtility.GetBaseArmor(monster), "破击锤应扣当前护甲，不改基础护甲");
            Assert.AreEqual(0, StatArmorUtility.GetCurrentArmor(monster), "5 护甲怪物被破击锤应归零");
        }
    }
}
