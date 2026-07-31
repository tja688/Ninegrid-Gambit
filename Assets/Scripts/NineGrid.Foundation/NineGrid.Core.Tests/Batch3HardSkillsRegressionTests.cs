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
    /// 剩技硬骨头批次3：刺客领袖 / 天涯若比邻。
    /// </summary>
    public sealed class Batch3HardSkillsRegressionTests
    {
        private static readonly SlotId sHostSlot = SlotId.Board(2);
        private static readonly SlotId sFarSlot = SlotId.Board(9);
        private static readonly SlotId sMidSlot = SlotId.Board(4);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IContentSystem mContent;
        private IEffectSystem mEffects;
        private IStatSystem mStats;
        private IBoardSystem mBoard;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 41UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mContent = mArch.GetSystem<IContentSystem>();
            mEffects = mArch.GetSystem<IEffectSystem>();
            mStats = mArch.GetSystem<IStatSystem>();
            mBoard = mArch.GetSystem<IBoardSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Catalog_Batch3Skills_HaveResolvedEffectMounts()
        {
            AssertSkillMounted("skill.assassin_leader", "skill.assassin_leader.rule");
            AssertSkillMounted("skill.world_as_neighbors", "skill.world_as_neighbors.rule");
        }

        [Test]
        public void AssassinLeader_FaceUp_NewMonsterDealtFaceDown()
        {
            StartEmptyNode();
            var leaderUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var registry = mArch.GetModel<CardRegistry>();
            ActivateSkillAndFlush(registry.Get(leaderUid), "skill.assassin_leader");
            Assert.IsTrue(registry.Get(leaderUid).FaceUp);

            var eventStart = mPipeline.EventLog.Entries.Count;
            var dealtUid = SpawnOnBoardReturnUid("monster.skull_head", CardKind.Monster, sFarSlot);
            Assert.IsFalse(registry.Get(dealtUid).FaceUp, "正面刺客领袖在场时新怪应经 OnDeal→Flip 翻到背面");
            AssertEventLogContainsAfter(eventStart, CoreEventType.EffectTriggered, "skill.assassin_leader.rule");
            AssertEventLogContainsAfter(eventStart, CoreEventType.CardFaceChanged, null);
        }

        [Test]
        public void AssassinLeader_FillEmptySlots_AlsoFaceDown()
        {
            StartEmptyNode();
            var leaderUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            ActivateSkillAndFlush(registry.Get(leaderUid), "skill.assassin_leader");

            var draft = mContent.CreateDraft("monster.skull_head");
            var pending = draft.Create(registry);
            mContent.ApplyContentToCard(pending);
            deck.AddToDrawPile(pending, false);

            var eventStart = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new FillEmptySlotsAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var board = mArch.GetModel<BoardModel>();
            var foundFaceDown = false;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                if (uid == 0 || uid == leaderUid)
                {
                    continue;
                }

                Assert.AreEqual("monster.skull_head", registry.Get(uid).DefId);
                Assert.IsFalse(registry.Get(uid).FaceUp, "FillEmptySlots 也应经 Flip 翻到背面");
                foundFaceDown = true;
            }

            Assert.IsTrue(foundFaceDown, "应至少补出一张新怪");
            AssertEventLogContainsAfter(eventStart, CoreEventType.EffectTriggered, "skill.assassin_leader.rule");
            AssertEventLogContainsAfter(eventStart, CoreEventType.CardFaceChanged, null);
        }

        [Test]
        public void AssassinLeader_LeaderFaceDownOrRemoved_NewMonsterDealtFaceUp()
        {
            StartEmptyNode();
            var leaderUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var registry = mArch.GetModel<CardRegistry>();
            ActivateSkillAndFlush(registry.Get(leaderUid), "skill.assassin_leader");

            mPipeline.Enqueue(new FlipCardAction(leaderUid));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            mEffects.SyncOwnerFaceSuppression(leaderUid);
            Assert.IsFalse(registry.Get(leaderUid).FaceUp);

            var dealtWhileDown = SpawnOnBoardReturnUid("monster.skull_head", CardKind.Monster, sFarSlot);
            Assert.IsTrue(registry.Get(dealtWhileDown).FaceUp, "领袖背面时新怪应默认正面");

            mPipeline.Enqueue(new RemoveCardAction(leaderUid, ZoneId.Removed, "test.removeLeader"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var dealtAfterGone = SpawnOnBoardReturnUid("monster.melee_3", CardKind.Monster, sMidSlot);
            Assert.IsTrue(registry.Get(dealtAfterGone).FaceUp, "领袖离场后新怪应默认正面");
        }

        [Test]
        public void AssassinLeader_ExistingFaceDown_NotAutoFlippedBack()
        {
            StartEmptyNode();
            var leaderUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var otherUid = SpawnOnBoardReturnUid("monster.skull_head", CardKind.Monster, sFarSlot);
            var registry = mArch.GetModel<CardRegistry>();
            registry.Get(otherUid).FaceUp = false;
            ActivateSkillAndFlush(registry.Get(leaderUid), "skill.assassin_leader");

            mPipeline.Enqueue(new FlipCardAction(leaderUid));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            mEffects.SyncOwnerFaceSuppression(leaderUid);

            Assert.IsFalse(registry.Get(otherUid).FaceUp, "已在场背面怪不得因领袖失效自动翻正");
        }

        [Test]
        public void AssassinLeader_TrapSpawn_NotForcedFaceDown()
        {
            StartEmptyNode();
            var leaderUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var registry = mArch.GetModel<CardRegistry>();
            ActivateSkillAndFlush(registry.Get(leaderUid), "skill.assassin_leader");

            var stoneUid = SpawnOnBoardReturnUid("trap.revive_stone", CardKind.Monster, sFarSlot);
            Assert.IsTrue(registry.Get(stoneUid).FaceUp, "机关卡不应被刺客领袖强制翻面");
        }

        [Test]
        public void WorldAsNeighbors_NonAdjacentMonsters_AreAdjacentForSkills()
        {
            StartEmptyNode();
            var hostUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var farUid = SpawnOnBoardReturnUid("monster.skull_head", CardKind.Monster, sFarSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var host = registry.Get(hostUid);
            var far = registry.Get(farUid);

            Assert.IsFalse(host.Slot.Value.IsAdjacentTo(far.Slot.Value), "几何上应不相邻");
            Assert.IsFalse(mBoard.AreAdjacent(host, far), "无天涯时不应虚拟相邻");

            ActivateSkillAndFlush(host, "skill.world_as_neighbors");
            Assert.IsTrue(mBoard.AreAdjacent(host, far), "天涯应使非邻接怪物彼此相邻（技能邻接）");
        }

        [Test]
        public void WorldAsNeighbors_LinkTacticsAura_AppliesToFarMonster()
        {
            StartEmptyNode();
            var worldUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var tacticsUid = SpawnOnBoardReturnUid("monster.skull_head", CardKind.Monster, sMidSlot);
            var farUid = SpawnOnBoardReturnUid("monster.melee_3", CardKind.Monster, sFarSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var far = registry.Get(farUid);
            far.Stats.SetBase(StatId.Attack, 2);

            ActivateSkillAndFlush(registry.Get(worldUid), "skill.world_as_neighbors");
            ActivateSkillAndFlush(registry.Get(tacticsUid), "skill.link_tactics");

            Assert.IsFalse(registry.Get(tacticsUid).Slot.Value.IsAdjacentTo(far.Slot.Value));
            Assert.AreEqual(3, mStats.GetEffectiveInt(far, StatId.Attack), "天涯下链接战术应对非几何邻接怪生效");
        }

        [Test]
        public void WorldAsNeighbors_DoesNotChangeAttackPatternRange()
        {
            StartEmptyNode();
            var hostUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var registry = mArch.GetModel<CardRegistry>();
            ActivateSkillAndFlush(registry.Get(hostUid), "skill.world_as_neighbors");

            var board = mArch.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            // 中心格对全盘要么正交要么对角；把玩家挪到格1，怪在格9 → 几何八向皆不相邻。
            board.SetAvatar(avatar, SlotId.Board(1));
            var avatarSlot = board.AvatarSlot.Value;
            var farSlot = SlotId.Board(9);
            Assert.IsFalse(farSlot.IsAdjacentTo(avatarSlot), "格1↔格9 正交不相邻");
            Assert.IsFalse(farSlot.IsDiagonallyAdjacentTo(avatarSlot), "格1↔格9 对角不相邻");

            Assert.IsFalse(
                AttackPatternRules.MeetsPositionRequirement(AttackPattern.OrthogonalMelee, farSlot, avatarSlot),
                "天涯不得放宽普通近战齐射位置条件");
            Assert.IsFalse(
                AttackPatternRules.MeetsPositionRequirement(AttackPattern.DiagonalMelee, farSlot, avatarSlot),
                "天涯不得放宽斜角近战位置条件");
            Assert.IsFalse(
                AttackPatternRules.MeetsPositionRequirement(AttackPattern.OmnidirectionalMelee, farSlot, avatarSlot),
                "天涯不得放宽全向近战齐射位置条件");
        }

        [Test]
        public void WorldAsNeighbors_DoesNotMakeAvatarMonsterAdjacentViaBoardSystemSlots()
        {
            StartEmptyNode();
            var hostUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sFarSlot);
            var registry = mArch.GetModel<CardRegistry>();
            ActivateSkillAndFlush(registry.Get(hostUid), "skill.world_as_neighbors");

            var board = mArch.GetModel<BoardModel>();
            Assert.IsFalse(
                mBoard.AreAdjacent(board.AvatarSlot.Value, sFarSlot),
                "天涯不得扩展玩家互动邻接（Avatar 非怪物）");
        }

        private void AssertEventLogContainsAfter(int startIndex, CoreEventType type, string messageOrNull)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type != type)
                {
                    continue;
                }

                if (messageOrNull == null || entry.Message == messageOrNull || entry.Cause == messageOrNull)
                {
                    return;
                }
            }

            Assert.Fail("EventLog after " + startIndex + " missing " + type
                + (messageOrNull == null ? string.Empty : (" message/cause=" + messageOrNull)));
        }

        private void StartEmptyNode()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);
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

        private int SpawnOnBoardReturnUid(string defId, CardKind kind, SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, kind, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            return mArch.GetModel<BoardModel>().GetCardUid(slot);
        }
    }
}
