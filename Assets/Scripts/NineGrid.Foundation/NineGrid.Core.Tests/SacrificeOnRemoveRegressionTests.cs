using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 献身：宿主被移除时，场上其他怪物攻击 +1（QuickTest ActivateSkillsOnCard 路径）。
    /// </summary>
    public sealed class SacrificeOnRemoveRegressionTests
    {
        private static readonly SlotId sVictimSlot = SlotId.Board(4);
        private static readonly SlotId sAllySlot = SlotId.Board(5);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IContentSystem mContent;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            EffectTemplateCatalog.Invalidate();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 11UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mContent = mArch.GetSystem<IContentSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            EffectTemplateCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Catalog_SkillSacrifice_HasResolvedEffectMount()
        {
            Assert.IsTrue(mContent.Catalog.TryGetSkill("skill.sacrifice", out var skill), "catalog missing skill.sacrifice");
            Assert.Greater(skill.EffectIds.Count, 0, "skill.sacrifice.EffectIds 应含装配投影 mountId");
            Assert.IsTrue(
                mContent.Catalog.TryGetEffect("skill.sacrifice.remove", out var effect),
                "catalog missing effect skill.sacrifice.remove");
            Assert.AreEqual(ContentImplementationState.Implemented, effect.State);
        }

        [Test]
        public void ActivateSkillsOnCard_Sacrifice_OnKill_BuffsOtherBoardMonstersAttack()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            var victimUid = SpawnOnBoardReturnUid("monster.headless_skeleton", sVictimSlot);
            var allyUid = SpawnOnBoardReturnUid("monster.dragon_follower", sAllySlot);
            var registry = mArch.GetModel<CardRegistry>();

            registry.Get(victimUid).Stats.SetBase(StatId.Hp, 1);
            registry.Get(victimUid).Stats.SetBase(StatId.MaxHp, 1);
            registry.Get(victimUid).Stats.SetBase(StatId.Armor, 0);

            var mounted = mContent.ActivateSkillsOnCard(registry.Get(victimUid), new[] { "skill.sacrifice" });
            Assert.Greater(mounted.Count, 0, "ActivateSkillsOnCard(skill.sacrifice) 应激活至少 1 个实例");

            var allyAtkBefore = (int)registry.Get(allyUid).Stats.GetBase(StatId.Attack);
            PrepareAvatarAttack(10);

            var board = mArch.GetModel<BoardModel>();
            var startIndex = mPipeline.EventLog.Entries.Count;
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, victimUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            Assert.IsTrue(
                ContainsEffectTriggeredSince(startIndex, "skill.sacrifice"),
                "献身宿主被移除应产生 EffectTriggered(skill.sacrifice)");
            Assert.AreEqual(
                allyAtkBefore + 1,
                (int)registry.Get(allyUid).Stats.GetBase(StatId.Attack),
                "场上其他怪物应攻击 +1");
        }

        private int SpawnOnBoardReturnUid(string defId, SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.Monster, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            return mArch.GetModel<BoardModel>().GetCardUid(slot);
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

        private bool ContainsEffectTriggeredSince(int startIndex, string sourceDefId)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type == CoreEventType.EffectTriggered
                    && string.Equals(entry.SourceDefId, sourceDefId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
