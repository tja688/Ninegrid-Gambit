using System.Collections.Generic;
using NineGrid.Content;
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
    /// 批次 B：跳杀 / 盗取 — OnFlip 模板解析装配 + ActivateSkillsOnCard 冒烟。
    /// </summary>
    public sealed class FlipSkillOnFlipRegressionTests
    {
        private static readonly SlotId sHostSlot = SlotId.Board(2);
        private static readonly SlotId sHelpSlot = SlotId.Board(1);

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
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 17UL });
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
        public void Catalog_LeapKillAndSteal_HaveResolvedEffectMounts()
        {
            Assert.IsTrue(mContent.Catalog.TryGetSkill("skill.leap_kill", out var leap), "catalog missing skill.leap_kill");
            Assert.Greater(leap.EffectIds.Count, 0);
            Assert.IsTrue(mContent.Catalog.TryGetEffect("skill.leap_kill.flip", out var leapFx));
            Assert.AreEqual(ContentImplementationState.Implemented, leapFx.State);

            Assert.IsTrue(mContent.Catalog.TryGetSkill("skill.steal", out var steal), "catalog missing skill.steal");
            Assert.Greater(steal.EffectIds.Count, 0);
            Assert.IsTrue(mContent.Catalog.TryGetEffect("skill.steal.flip", out var stealFx));
            Assert.AreEqual(ContentImplementationState.Implemented, stealFx.State);
        }

        [Test]
        public void ActivateSkillsOnCard_LeapKill_OnFlipFaceUpAdjacent_DamagesPlayerByAttack()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            var hostUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var host = registry.Get(hostUid);

            var mounted = mContent.ActivateSkillsOnCard(host, new[] { "skill.leap_kill" });
            Assert.Greater(mounted.Count, 0, "ActivateSkillsOnCard(skill.leap_kill) 应激活至少 1 个实例");

            host.FaceUp = false;
            mArch.GetSystem<IEffectSystem>().SyncOwnerFaceSuppression(hostUid);
            host.Stats.SetBase(StatId.Attack, 3);
            var expectedDamage = (int)System.Math.Round(
                mArch.GetSystem<IStatSystem>().GetEffectiveValue(host, StatId.Attack));
            Assert.AreEqual(3, expectedDamage, "宿主有效攻击应被设为 3");

            var board = mArch.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, 99);
            avatar.Stats.SetBase(StatId.Hp, 99);
            avatar.Stats.SetBase(StatId.Armor, 0);
            StatArmorUtility.SetCurrentArmor(avatar, 0);
            var hpBefore = (int)avatar.Stats.GetBase(StatId.Hp);

            var startIndex = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new FlipCardAction(hostUid));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.IsTrue(host.FaceUp);
            Assert.IsTrue(
                ContainsEffectTriggeredSince(startIndex, "skill.leap_kill"),
                "跳杀翻至正面且邻玩家应产生 EffectTriggered(skill.leap_kill)");
            Assert.AreEqual(
                hpBefore - expectedDamage,
                (int)avatar.Stats.GetBase(StatId.Hp),
                "玩家应受到等同宿主攻击的伤害");
        }

        [Test]
        public void ActivateSkillsOnCard_Steal_OnFlipFaceUp_RemovesOneAdjacentHelpCard()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            var hostUid = SpawnOnBoardReturnUid("monster.headless_skeleton", CardKind.Monster, sHostSlot);
            var helpUid = SpawnOnBoardReturnUid("help.flame", CardKind.HelpCard, sHelpSlot);
            var registry = mArch.GetModel<CardRegistry>();
            var host = registry.Get(hostUid);

            var mounted = mContent.ActivateSkillsOnCard(host, new[] { "skill.steal" });
            Assert.Greater(mounted.Count, 0, "ActivateSkillsOnCard(skill.steal) 应激活至少 1 个实例");

            host.FaceUp = false;
            mArch.GetSystem<IEffectSystem>().SyncOwnerFaceSuppression(hostUid);

            Assert.AreEqual(sHelpSlot, registry.Get(helpUid).Slot.Value);

            var startIndex = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new FlipCardAction(hostUid));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.IsTrue(host.FaceUp);
            Assert.IsTrue(
                ContainsEffectTriggeredSince(startIndex, "skill.steal"),
                "盗取翻至正面应产生 EffectTriggered(skill.steal)");
            Assert.AreEqual(
                ZoneId.Removed,
                registry.Get(helpUid).Zone.Value,
                "邻接帮助卡应被移除");
            Assert.AreEqual(0, mArch.GetModel<BoardModel>().GetCardUid(sHelpSlot));
        }

        private int SpawnOnBoardReturnUid(string defId, CardKind kind, SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, kind, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            return mArch.GetModel<BoardModel>().GetCardUid(slot);
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
