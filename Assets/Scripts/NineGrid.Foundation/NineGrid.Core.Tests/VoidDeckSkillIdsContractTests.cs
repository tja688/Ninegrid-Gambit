using System.IO;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #133：决斗套（deck.void）怪物技能权威链路核对——effectAssemblies → skillIds。
    /// 技能效果模板统一由 skill_*.json 持有；怪物 JSON 不再直挂 assemblies。
    /// 五张卡 attackPattern 均为「无」（显式合法取值，ADR-0011），技能经 skillIds
    /// 挂载——不得把「无主动攻击」误判为「无技能」。
    /// 只断言稳定 ID / 技能挂载 / 序列与层主标志；禁止断言可变 displayName。
    /// </summary>
    public sealed class VoidDeckSkillIdsContractTests
    {
        private static readonly string[] VoidDeckMonsterIds =
        {
            "monster.bone_club_skeleton",
            "monster.bone_courier",
            "monster.fire_priest",
            "monster.fire_swallower",
            "monster.fire_bather",
        };

        private IArchitecture mArch;
        private IContentSystem mContent;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            mContent = mArch.GetSystem<IContentSystem>();
            mContent.TryReloadFromConfig();
        }

        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
            EffectTemplateCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void VoidDeckMonsters_SkillsViaSkillIds_NoDirectAssemblies()
        {
            AssertSkillIds("monster.bone_club_skeleton", "skill.holy_duel");
            AssertSkillIds("monster.bone_courier", "skill.holy_duel", "skill.evade");
            AssertSkillIds("monster.fire_priest", "skill.holy_duel", "skill.battle_hardened");
            AssertSkillIds("monster.fire_swallower", "skill.holy_duel", "skill.taunt");
            AssertSkillIds("monster.fire_bather", "skill.holy_duel", "skill.attrition");
        }

        [Test]
        public void VoidDeckMonsters_NoneAttackPattern_StillHaveSkills()
        {
            for (var i = 0; i < VoidDeckMonsterIds.Length; i++)
            {
                Assert.IsTrue(mContent.Catalog.TryGetCard(VoidDeckMonsterIds[i], out var card), "missing " + VoidDeckMonsterIds[i]);
                Assert.AreEqual(AttackPattern.None, card.AttackPattern,
                    VoidDeckMonsterIds[i] + " attackPattern 须为显式「无」");
                Assert.Greater(card.SkillIds.Count, 0,
                    VoidDeckMonsterIds[i] + " 无主动攻击 ≠ 无技能，须经 skillIds 挂技能");
            }
        }

        [Test]
        public void VoidDeckSkills_ResolveSameEffectMountsAsBeforeMigration()
        {
            AssertSkillMounts("skill.holy_duel", "skill.holy_duel.activate");
            AssertSkillMounts("skill.evade", "skill.evade.battle");
            AssertSkillMounts("skill.battle_hardened", "skill.battle_hardened.damage");
            AssertSkillMounts("skill.taunt", "skill.taunt.rule");
            AssertSkillMounts("skill.attrition", "skill.attrition.remove");
        }

        [Test]
        public void VoidDeck_SlotsAndLayerLeaderFlag()
        {
            var issues = ThemeDeckMappingVerifier.VerifyDeck(mContent.Catalog, "deck.void");
            Assert.AreEqual(0, issues.Count, FormatIssues(issues));

            Assert.IsTrue(mContent.Catalog.TryGetCard("monster.fire_bather", out var fireBather));
            Assert.AreEqual(5, fireBather.Sequence, "层主必须是 sequence 5");
            Assert.IsTrue(fireBather.IsBoss, "层主卡须挂 isBoss");
            Assert.AreEqual(MonsterRank.FloorBoss, fireBather.Rank, "层主卡等级须投影为 FloorBoss");
        }

        [Test]
        public void VoidDeckMonsters_ValidateCatalog_IsValid()
        {
            var report = mContent.ValidateCatalog();
            Assert.IsTrue(report.IsValid, FormatIssues(report));
            Assert.AreEqual(0, report.PendingEffectIds.Count, "Pending: " + string.Join(", ", report.PendingEffectIds));
        }

        [Test]
        public void VoidDeckCardJson_AuthoringMatchesStreaming()
        {
            for (var i = 0; i < VoidDeckMonsterIds.Length; i++)
            {
                AssertAreEqualFiles(
                    CardPresentationJsonIO.GetAuthoringAbsolutePath(VoidDeckMonsterIds[i]),
                    CardPresentationJsonIO.GetStreamingAbsolutePath(VoidDeckMonsterIds[i]),
                    VoidDeckMonsterIds[i] + " 卡面 JSON 双侧不一致（Authoring 与 Streaming 必须同内容）");
            }
        }

        private void AssertSkillIds(string monsterId, params string[] expectedSkillIds)
        {
            Assert.IsTrue(mContent.Catalog.TryGetCard(monsterId, out var card), "missing " + monsterId);
            Assert.AreEqual(0, card.EffectIds.Count, monsterId + " 应通过 skillIds 挂技能，不得直挂 assemblies");
            Assert.AreEqual(expectedSkillIds.Length, card.SkillIds.Count, monsterId + " skillIds 数量不符");
            for (var i = 0; i < expectedSkillIds.Length; i++)
            {
                Assert.AreEqual(expectedSkillIds[i], card.SkillIds[i], monsterId + " skillIds[" + i + "] 不符");
            }
        }

        private void AssertSkillMounts(string skillId, string effectId)
        {
            Assert.IsTrue(mContent.Catalog.TryGetSkill(skillId, out var skill), "missing " + skillId);
            CollectionAssert.Contains(skill.EffectIds, effectId, skillId + " should mount " + effectId);
            Assert.IsTrue(mContent.Catalog.TryGetEffect(effectId, out var fx), "missing effect " + effectId);
            Assert.AreEqual(ContentImplementationState.Implemented, fx.State, effectId);
        }

        private static void AssertAreEqualFiles(string first, string second, string message)
        {
            Assert.IsTrue(File.Exists(first), "文件不存在：" + first);
            Assert.IsTrue(File.Exists(second), "文件不存在：" + second);
            Assert.AreEqual(File.ReadAllBytes(first), File.ReadAllBytes(second), message);
        }

        private static string FormatIssues(System.Collections.Generic.List<ThemeDeckMappingVerifier.Issue> issues)
        {
            if (issues.Count == 0)
            {
                return "no issues";
            }

            return string.Join("; ", issues);
        }

        private static string FormatIssues(ContentValidationReport report)
        {
            if (report.Issues.Count == 0)
            {
                return "no issues";
            }

            return string.Join("; ", report.Issues);
        }
    }
}
