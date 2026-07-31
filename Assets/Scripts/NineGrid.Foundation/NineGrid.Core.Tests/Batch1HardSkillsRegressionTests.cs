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
            Assert.Greater(mPipeline.PendingCount, 0, "OnActivate 应入队 AddStatModifier，调用方必须冲刷");
            Assert.AreEqual(2, mStats.GetEffectiveInt(ally, StatId.Attack), "未冲刷前邻接光环不应生效");

            var startIndex = mPipeline.EventLog.Entries.Count;
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(3, mStats.GetEffectiveInt(ally, StatId.Attack), "邻接光环应攻+1");
            Assert.IsTrue(
                HasBaseStatModifiedAttackSince(startIndex, allyUid, 3),
                "Permanent Attack 光环应提交 BaseStatModified(ResultValue=有效攻) 供卡面");

            // CardMoved 刷新路径：离开邻接后光环应失效
            startIndex = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new SwapBoardSlotsAction(sOtherSlot, sCornerSlot));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            ally = registry.Get(allyUid);
            Assert.AreEqual(2, mStats.GetEffectiveInt(ally, StatId.Attack), "离开邻接后光环应失效");
            Assert.IsTrue(
                HasBaseStatModifiedAttackSince(startIndex, allyUid, 2),
                "离开邻接后应再提交有效攻=基值到卡面");

            // 宿主移除：拓扑旁路应让邻怪卡面攻回落（不依赖 refresh 再 Apply）
            mPipeline.Enqueue(new SwapBoardSlotsAction(sCornerSlot, sOtherSlot));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            ally = registry.Get(allyUid);
            Assert.AreEqual(3, mStats.GetEffectiveInt(ally, StatId.Attack), "回到邻接应再+1");

            startIndex = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new RemoveCardAction(hostUid, ZoneId.Removed, "test.removeHost"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            ally = registry.Get(allyUid);
            Assert.AreEqual(2, mStats.GetEffectiveInt(ally, StatId.Attack), "宿主移除后光环应失效");
            Assert.IsTrue(
                HasBaseStatModifiedAttackSince(startIndex, allyUid, 2),
                "宿主移除后应提交有效攻回基值");
        }

        [Test]
        public void ActivateSkillsOnCard_Evade_NonLethalHit_SwapsWithCornerCard()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            var hostUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sCornerSlot);
            var partnerUid = SpawnOnBoardReturnUid("monster.skull_head", CardKind.Monster, SlotId.Board(3));
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var host = registry.Get(hostUid);
            host.Stats.SetBase(StatId.MaxHp, 9);
            host.Stats.SetBase(StatId.Hp, 9);
            host.Stats.SetBase(StatId.Armor, 0);

            Assert.Greater(mContent.ActivateSkillsOnCard(host, new[] { "skill.evade" }).Count, 0);
            PrepareAvatarAttack(1);

            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, hostUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            Assert.AreEqual(partnerUid, board.GetCardUid(sCornerSlot), "非致命逃避应与四角卡 Swap");
            Assert.AreEqual(hostUid, board.GetCardUid(SlotId.Board(3)));
            Assert.AreEqual(ZoneId.Board, registry.Get(hostUid).Zone.Value);
        }

        [Test]
        public void ActivateSkillsOnCard_Evade_LethalHit_DoesNotSwap()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            var hostUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sCornerSlot);
            var partnerUid = SpawnOnBoardReturnUid("monster.skull_head", CardKind.Monster, SlotId.Board(3));
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var host = registry.Get(hostUid);
            host.Stats.SetBase(StatId.MaxHp, 9);
            host.Stats.SetBase(StatId.Hp, 1);
            host.Stats.SetBase(StatId.Armor, 0);

            Assert.Greater(mContent.ActivateSkillsOnCard(host, new[] { "skill.evade" }).Count, 0);
            PrepareAvatarAttack(5);

            var startIndex = mPipeline.EventLog.Entries.Count;
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, hostUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            Assert.AreEqual(ZoneId.Graveyard, registry.Get(hostUid).Zone.Value, "致命一击应击杀宿主");
            Assert.AreEqual(partnerUid, board.GetCardUid(SlotId.Board(3)), "致死后不得 Swap，伙伴应仍在原四角格");
            Assert.IsFalse(
                ContainsCardSwappedSince(startIndex),
                "致死命中不得产生 CardSwapped（逃避）");
        }

        private void PrepareAvatarAttack(int attack)
        {
            var board = mArch.GetModel<BoardModel>();
            var avatar = mArch.GetModel<CardRegistry>().Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, 99);
            avatar.Stats.SetBase(StatId.Hp, 99);
            avatar.Stats.SetBase(StatId.Armor, 0);
            avatar.Stats.SetBase(StatId.Attack, attack);
        }

        private bool ContainsCardSwappedSince(int startIndex)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.CardSwapped)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasBaseStatModifiedAttackSince(int startIndex, int cardUid, int expectedAttack)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type != CoreEventType.BaseStatModified)
                {
                    continue;
                }

                var uid = e.CardUid > 0 ? e.CardUid : e.TargetUid;
                if (uid == cardUid
                    && (StatId)e.Amount == StatId.Attack
                    && e.ResultValue == expectedAttack)
                {
                    return true;
                }
            }

            return false;
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
