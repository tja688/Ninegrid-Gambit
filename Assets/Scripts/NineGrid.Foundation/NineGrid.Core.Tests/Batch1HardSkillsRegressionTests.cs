using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 剩技硬骨头批次1：休养 / 起来 / 逃避 / 历战 / 链接战术 / 烈焰沸腾 / 快递 装配与冒烟。
    /// </summary>
    public sealed class Batch1HardSkillsRegressionTests
    {
        private static readonly SlotId sHostSlot = SlotId.Board(5);
        private static readonly SlotId sOtherSlot = SlotId.Board(2);
        private static readonly SlotId sCornerSlot = SlotId.Board(1);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IContentSystem mContent;
        private IEffectSystem mEffects;
        private IStatSystem mStats;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 31UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mContent = mArch.GetSystem<IContentSystem>();
            mEffects = mArch.GetSystem<IEffectSystem>();
            mStats = mArch.GetSystem<IStatSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Catalog_Batch1Skills_HaveResolvedEffectMounts()
        {
            AssertSkillMounted("skill.recuperate", "skill.recuperate.flip");
            AssertSkillMounted("skill.rise_up", "skill.rise_up.damage");
            AssertSkillMounted("skill.evade", "skill.evade.battle");
            AssertSkillMounted("skill.link_tactics", "skill.link_tactics.activate");
            AssertSkillMounted("skill.link_tactics", "skill.link_tactics.refresh");
            AssertSkillMounted("skill.delivery", "skill.delivery.move");
            AssertSkillMounted("skill.flame_boiling", "skill.flame_boiling.rule");
            AssertSkillMounted("skill.battle_hardened", "skill.battle_hardened.damage");
        }

        [Test]
        public void ActivateSkillsOnCard_Recuperate_OnFlipFaceUp_AddsAttackAndArmor()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            var hostUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var host = registry.Get(hostUid);
            host.Stats.SetBase(StatId.Attack, 2);
            host.Stats.SetBase(StatId.Armor, 1);

            var mounted = mContent.ActivateSkillsOnCard(host, new[] { "skill.recuperate" });
            Assert.Greater(mounted.Count, 0);

            host.FaceUp = false;
            mEffects.SyncOwnerFaceSuppression(hostUid);

            mPipeline.Enqueue(new FlipCardAction(hostUid));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.IsTrue(host.FaceUp);
            Assert.AreEqual(3, (int)host.Stats.GetBase(StatId.Attack), "休养应攻+1");
            Assert.AreEqual(3, (int)host.Stats.GetBase(StatId.Armor), "休养应甲+2");
        }

        [Test]
        public void ActivateSkillsOnCard_RiseUp_DamageToPlayer_FlipsOtherMonster()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            var hostUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var otherUid = SpawnOnBoardReturnUid("monster.skull_head", CardKind.Monster, sOtherSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var host = registry.Get(hostUid);
            var other = registry.Get(otherUid);
            Assert.IsTrue(other.FaceUp);

            var mounted = mContent.ActivateSkillsOnCard(host, new[] { "skill.rise_up" });
            Assert.Greater(mounted.Count, 0);

            var avatarUid = mArch.GetModel<BoardModel>().AvatarUid.Value;
            mPipeline.Enqueue(new DealDamageAction(hostUid, avatarUid, 1, "skill.rise_up", "test.rise_up"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.IsFalse(other.FaceUp, "起来应对其他怪执行 Flip");
        }

        [Test]
        public void ActivateSkillsOnCard_LinkTactics_OnActivate_BuffsAdjacentMonsterOnce()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            var hostUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var allyUid = SpawnOnBoardReturnUid("monster.skull_head", CardKind.Monster, sOtherSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var host = registry.Get(hostUid);
            var ally = registry.Get(allyUid);
            ally.Stats.SetBase(StatId.Attack, 2);

            var mounted = mContent.ActivateSkillsOnCard(host, new[] { "skill.link_tactics" });
            Assert.Greater(mounted.Count, 0);
            mPipeline.RunToCompletion();

            Assert.AreEqual(3, mStats.GetEffectiveInt(ally, StatId.Attack), "邻接光环应攻+1");

            // CardMoved 刷新路径：离开邻接后光环应失效
            mPipeline.Enqueue(new SwapBoardSlotsAction(sOtherSlot, sCornerSlot));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            ally = registry.Get(allyUid);
            Assert.AreEqual(2, mStats.GetEffectiveInt(ally, StatId.Attack), "离开邻接后光环应失效");
        }

        private void AssertSkillMounted(string skillId, string effectId)
        {
            Assert.IsTrue(mContent.Catalog.TryGetSkill(skillId, out var skill), "missing " + skillId);
            CollectionAssert.Contains(skill.EffectIds, effectId, skillId + " should mount " + effectId);
            Assert.IsTrue(mContent.Catalog.TryGetEffect(effectId, out var fx), "missing effect " + effectId);
            Assert.AreEqual(ContentImplementationState.Implemented, fx.State, effectId);
        }

        private int SpawnOnBoardReturnUid(string defId, CardKind kind, SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, kind, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            return mArch.GetModel<BoardModel>().GetCardUid(slot);
        }
    }
}
