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
    /// 剩技硬骨头批次2：提速 / 远程武器 / 死亡召唤 / 死亡之主 / 神圣决斗 / 潜伏近战
    /// + 复活石机关内容（复活亡者：互动6自移除 → 特5同格）。
    /// </summary>
    public sealed class Batch2HardSkillsRegressionTests
    {
        private static readonly SlotId sHostSlot = SlotId.Board(2);
        private static readonly SlotId sOtherSlot = SlotId.Board(4);
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
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 37UL });
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
        public void Catalog_Batch2Skills_HaveResolvedEffectMounts()
        {
            AssertSkillMounted("skill.speed_up", "skill.speed_up.damage");
            AssertSkillMounted("skill.ranged_weapon", "skill.ranged_weapon.activate");
            AssertSkillMounted("skill.death_summon", "skill.death_summon.remove");
            AssertSkillMounted("skill.lord_of_death", "skill.lord_of_death.remove");
            AssertSkillMounted("skill.holy_duel", "skill.holy_duel.activate");
            AssertSkillMounted("skill.ambush_melee", "skill.ambush_melee.interact");
            AssertTrapMounted("trap.revive_stone", "trap.revive_stone.interact");
            AssertTrapMounted("trap.revive_stone", "trap.revive_stone.remove");
            Assert.IsTrue(
                mContent.Catalog.TryGetCard("monster.summon.special_omni", out var special5),
                "missing monster.summon.special_omni（复活亡者打出目标）");
            Assert.AreEqual(2, special5.Stats.Attack);
        }

        [Test]
        public void SpeedUp_HostDamagesPlayer_OtherMonsterCountdownMinusOne()
        {
            StartEmptyNode();
            var hostUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var otherUid = SpawnOnBoardReturnUid("monster.skull_head", CardKind.Monster, sOtherSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var other = registry.Get(otherUid);

            Assert.Greater(mContent.ActivateSkillsOnCard(registry.Get(hostUid), new[] { "skill.speed_up" }).Count, 0);
            Assert.AreEqual(3, other.Counters.Get(CoreCounterKeys.AttackPatternCountdown), "初始倒计时=频率3");

            var avatarUid = mArch.GetModel<BoardModel>().AvatarUid.Value;
            mPipeline.Enqueue(new DealDamageAction(hostUid, avatarUid, 1, "skill.speed_up", "test.speed_up"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(2, other.Counters.Get(CoreCounterKeys.AttackPatternCountdown), "提速应使其他怪倒计时-1");

            // 背面目标不加速（ADR-0016 冻结）
            other.FaceUp = false;
            mEffects.SyncOwnerFaceSuppression(otherUid);
            mPipeline.Enqueue(new DealDamageAction(hostUid, avatarUid, 1, "skill.speed_up", "test.speed_up"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(2, other.Counters.Get(CoreCounterKeys.AttackPatternCountdown), "背面怪倒计时冻结");

            // 非本卡伤害不触发
            other.FaceUp = true;
            mEffects.SyncOwnerFaceSuppression(otherUid);
            var avatar = registry.Get(avatarUid);
            mPipeline.Enqueue(new DealDamageAction(avatar.Uid, otherUid, 1, "test", "test.avatar"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(2, other.Counters.Get(CoreCounterKeys.AttackPatternCountdown), "非本卡伤害不得触发提速");
        }

        [Test]
        public void RangedWeapon_PlayerAttacksHost_NoCounterNoStrikeFirst()
        {
            StartEmptyNode();
            var hostUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var host = registry.Get(hostUid);
            host.Stats.SetBase(StatId.Attack, 5);
            ActivateSkillAndFlush(host, "skill.ranged_weapon");

            var board = mArch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            PrepareAvatar(99, 1, 0);

            Assert.IsFalse(mPhase.MonsterStrikesFirst(avatarUid, hostUid), "远程武器怪不先手");

            var avatarBefore = (int)registry.Get(avatarUid).Stats.GetBase(StatId.Hp);
            var hit = mPhase.ApplyCombatHit(avatarUid, hostUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            // 反击段（表现层单独 ApplyCombatHit(monster→avatar)）应短路
            var counter = mPhase.ApplyCombatHit(hostUid, avatarUid);
            Assert.IsTrue(counter.Accepted, counter.Reason);
            Assert.AreEqual(avatarBefore, (int)registry.Get(avatarUid).Stats.GetBase(StatId.Hp), "反击段不得造成伤害");
            Assert.AreEqual(ZoneId.Board, registry.Get(hostUid).Zone.Value, "玩家命中未致死");
        }

        [Test]
        public void RangedWeapon_WithoutSkill_CounterDamages()
        {
            StartEmptyNode();
            var hostUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var registry = mArch.GetModel<CardRegistry>();
            registry.Get(hostUid).Stats.SetBase(StatId.Attack, 5);
            var board = mArch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            PrepareAvatar(99, 1, 0);

            var counter = mPhase.ApplyCombatHit(hostUid, avatarUid);
            Assert.IsTrue(counter.Accepted, counter.Reason);
            Assert.AreEqual(94, (int)registry.Get(avatarUid).Stats.GetBase(StatId.Hp), "无远程武器时应正常反击");
        }

        [Test]
        public void DeathSummon_Removed_SpawnsReviveStoneAtSameSlot()
        {
            StartEmptyNode();
            var hostUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var registry = mArch.GetModel<CardRegistry>();
            ActivateSkillAndFlush(registry.Get(hostUid), "skill.death_summon");

            mPipeline.Enqueue(new RemoveCardAction(hostUid, ZoneId.Removed, "test.remove"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var board = mArch.GetModel<BoardModel>();
            var stoneUid = board.GetCardUid(sHostSlot);
            Assert.AreNotEqual(0, stoneUid, "原槽应被复活石占据");
            Assert.AreEqual("trap.revive_stone", registry.Get(stoneUid).DefId, "死亡召唤应打出复活石到同格");
            Assert.AreEqual(6, (int)registry.Get(stoneUid).Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void LordOfDeath_OtherRemoved_SpawnsStone_OwnDeathDoesNot()
        {
            StartEmptyNode();
            var lordUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var otherUid = SpawnOnBoardReturnUid("monster.skull_head", CardKind.Monster, sOtherSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            ActivateSkillAndFlush(registry.Get(lordUid), "skill.lord_of_death");

            // 他怪移除 → 复活石占其原槽
            mPipeline.Enqueue(new RemoveCardAction(otherUid, ZoneId.Removed, "test.removeOther"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var stoneUid = board.GetCardUid(sOtherSlot);
            Assert.AreNotEqual(0, stoneUid, "他怪原槽应被复活石占据");
            Assert.AreEqual("trap.revive_stone", registry.Get(stoneUid).DefId);

            // 自己死不补复活石
            mPipeline.Enqueue(new RemoveCardAction(lordUid, ZoneId.Removed, "test.removeLord"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.IsTrue(board.IsEmpty(sHostSlot), "死亡之主自己死不补复活石");
        }

        [Test]
        public void LordOfDeath_DoesNotRetriggerOnReviveStone_Special5WinsSlot()
        {
            StartEmptyNode();
            var lordUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var stoneUid = SpawnOnBoardReturnUid("trap.revive_stone", CardKind.Monster, sOtherSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            ActivateSkillAndFlush(registry.Get(lordUid), "skill.lord_of_death");

            // 复活石移除 → 只出特5，不得再被死亡之主盖成新复活石（否则循环）
            mPipeline.Enqueue(new RemoveCardAction(stoneUid, ZoneId.Removed, "test.removeStone"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var spawnedUid = board.GetCardUid(sOtherSlot);
            Assert.AreNotEqual(0, spawnedUid);
            Assert.AreEqual("monster.summon.special_omni", registry.Get(spawnedUid).DefId);
        }

        [Test]
        public void DeathSummon_AndLord_SameRemoval_SpawnsOneStoneWithoutThrow()
        {
            StartEmptyNode();
            var summonUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var lordUid = SpawnOnBoardReturnUid("monster.skull_head", CardKind.Monster, sOtherSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            ActivateSkillAndFlush(registry.Get(summonUid), "skill.death_summon");
            ActivateSkillAndFlush(registry.Get(lordUid), "skill.lord_of_death");

            Assert.DoesNotThrow(() =>
            {
                mPipeline.Enqueue(new RemoveCardAction(summonUid, ZoneId.Removed, "test.removeBoth"));
                Assert.Greater(mPipeline.RunToCompletion(), 0);
            });

            var stoneUid = board.GetCardUid(sHostSlot);
            Assert.AreNotEqual(0, stoneUid);
            Assert.AreEqual("trap.revive_stone", registry.Get(stoneUid).DefId);
        }

        [Test]
        public void HolyDuel_BattleHolderThenOther_PlayerTakes2_FlipClearsMark()
        {
            StartEmptyNode();
            var holderUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var otherUid = SpawnOnBoardReturnUid("monster.skull_head", CardKind.Monster, sOtherSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            PrepareAvatar(99, 1, 0);
            ActivateSkillAndFlush(registry.Get(holderUid), "skill.holy_duel");

            // 与持有者交战 → 标记
            var first = mPhase.ApplyCombatHit(avatarUid, holderUid);
            Assert.IsTrue(first.Accepted, first.Reason);
            var player = mArch.GetModel<PlayerModel>();
            Assert.AreEqual(holderUid, player.DuelMarkMonsterUid, "交战后应记录持有者标记");

            // 打其他怪 → 玩家 2 伤
            var second = mPhase.ApplyCombatHit(avatarUid, otherUid);
            Assert.IsTrue(second.Accepted, second.Reason);
            Assert.AreEqual(97, (int)registry.Get(avatarUid).Stats.GetBase(StatId.Hp), "决斗惩罚应扣玩家2血");

            // 持有者翻面 → 清标记 → 不再惩罚
            mPipeline.Enqueue(new FlipCardAction(holderUid));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(0, player.DuelMarkMonsterUid, "持有者翻面应清标记");
            var hpBefore = (int)registry.Get(avatarUid).Stats.GetBase(StatId.Hp);
            var third = mPhase.ApplyCombatHit(avatarUid, otherUid);
            Assert.IsTrue(third.Accepted, third.Reason);
            Assert.AreEqual(hpBefore, (int)registry.Get(avatarUid).Stats.GetBase(StatId.Hp), "清标记后不得再惩罚");
        }

        [Test]
        public void AmbushMelee_FiveInteractsAdjacent_DamagesPlayerAndFlipsSelf()
        {
            StartEmptyNode();
            var hostUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var host = registry.Get(hostUid);
            host.Stats.SetBase(StatId.Attack, 3);
            ActivateSkillAndFlush(host, "skill.ambush_melee");

            var avatarUid = mArch.GetModel<BoardModel>().AvatarUid.Value;
            PrepareAvatar(99, 1, 0);

            for (var i = 0; i < 4; i++)
            {
                Assert.IsTrue(mPhase.AdvanceInteractionCount().Accepted);
            }

            Assert.AreEqual(99, (int)registry.Get(avatarUid).Stats.GetBase(StatId.Hp), "未满5拍不得伤人");
            Assert.IsTrue(host.FaceUp, "未满5拍不得翻面");

            Assert.IsTrue(mPhase.AdvanceInteractionCount().Accepted);
            Assert.AreEqual(96, (int)registry.Get(avatarUid).Stats.GetBase(StatId.Hp), "第5拍应造成等同攻击的伤害");
            Assert.IsFalse(host.FaceUp, "第5拍后本卡应翻面");

            // 翻到背面后停止计数/触发，等外部翻正
            var hpBefore = (int)registry.Get(avatarUid).Stats.GetBase(StatId.Hp);
            for (var i = 0; i < 6; i++)
            {
                Assert.IsTrue(mPhase.AdvanceInteractionCount().Accepted);
            }

            Assert.AreEqual(hpBefore, (int)registry.Get(avatarUid).Stats.GetBase(StatId.Hp), "背面期间不得再触发");
        }

        [Test]
        public void ReviveStone_SixInteracts_RemovesSelfAndSpawnsSpecial5AtSameSlot()
        {
            StartEmptyNode();
            var stoneUid = SpawnOnBoardReturnUid("trap.revive_stone", CardKind.Monster, sHostSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();

            for (var i = 0; i < 5; i++)
            {
                Assert.IsTrue(mPhase.AdvanceInteractionCount().Accepted);
            }

            Assert.AreEqual(stoneUid, board.GetCardUid(sHostSlot), "未满6拍复活石应仍在场");

            Assert.IsTrue(mPhase.AdvanceInteractionCount().Accepted);
            var spawnedUid = board.GetCardUid(sHostSlot);
            Assert.AreNotEqual(0, spawnedUid, "原槽应有特5");
            Assert.AreNotEqual(stoneUid, spawnedUid, "复活石应被移除");
            Assert.AreEqual("monster.summon.special_omni", registry.Get(spawnedUid).DefId, "复活亡者应打出特5到同格");
        }

        private void StartEmptyNode()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);
        }

        private void PrepareAvatar(int hp, int attack, int armor)
        {
            var board = mArch.GetModel<BoardModel>();
            var avatar = mArch.GetModel<CardRegistry>().Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, hp);
            avatar.Stats.SetBase(StatId.Hp, hp);
            avatar.Stats.SetBase(StatId.Armor, armor);
            avatar.Stats.SetBase(StatId.CurrentArmor, armor);
            avatar.Stats.SetBase(StatId.Attack, attack);
        }

        private void ActivateSkillAndFlush(CardInstance card, string skillId)
        {
            Assert.Greater(mContent.ActivateSkillsOnCard(card, new[] { skillId }).Count, 0);
            if (mPipeline.PendingCount > 0)
            {
                Assert.Greater(mPipeline.RunToCompletion(), 0);
            }
        }

        private void AssertSkillMounted(string skillId, string effectId)
        {
            Assert.IsTrue(mContent.Catalog.TryGetSkill(skillId, out var skill), "missing " + skillId);
            CollectionAssert.Contains(skill.EffectIds, effectId, skillId + " should mount " + effectId);
            Assert.IsTrue(mContent.Catalog.TryGetEffect(effectId, out var fx), "missing effect " + effectId);
            Assert.AreEqual(ContentImplementationState.Implemented, fx.State, effectId);
        }

        private void AssertTrapMounted(string trapId, string effectId)
        {
            Assert.IsTrue(mContent.Catalog.TryGetCard(trapId, out var trap), "missing " + trapId);
            CollectionAssert.Contains(trap.EffectIds, effectId, trapId + " should mount " + effectId);
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
