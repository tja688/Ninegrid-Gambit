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
    /// #132：烈焰套（deck.stone_legion）怪物技能权威链路核对——effectAssemblies → skillIds。
    /// 技能效果模板统一由 skill_*.json 持有；怪物 JSON 不再直挂 assemblies。
    /// 只断言稳定 ID / 技能挂载 / 序列与层主标志；禁止断言可变 displayName。
    /// </summary>
    public sealed class StoneLegionSkillIdsContractTests
    {
        private static readonly string[] StoneLegionMonsterIds =
        {
            "monster.dragon_cult_leader",
            "monster.salamander",
            "monster.void_lost",
            "monster.dragon_follower",
            "monster.shelter_stone",
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
        public void StoneLegionMonsters_SkillsViaSkillIds_NoDirectAssemblies()
        {
            AssertSkillIds("monster.dragon_cult_leader", "skill.offer_fire");
            AssertSkillIds("monster.salamander", "skill.ranged_weapon", "skill.flame_boiling");
            AssertSkillIds("monster.void_lost", "skill.call_melee6");
            AssertSkillIds("monster.dragon_follower", "skill.intense_burning");
            AssertSkillIds("monster.shelter_stone", "skill.cloud_breath", "skill.endless_flame");
        }

        [Test]
        public void StoneLegionSkills_ResolveSameEffectMountsAsBeforeMigration()
        {
            AssertSkillMounts("skill.offer_fire", "skill.offer_fire.remove");
            AssertSkillMounts("skill.ranged_weapon", "skill.ranged_weapon.activate");
            AssertSkillMounts("skill.flame_boiling", "skill.flame_boiling.rule");
            AssertSkillMounts("skill.call_melee6", "skill.call_melee6.interact");
            AssertSkillMounts("skill.intense_burning", "skill.intense_burning.flame_deal");
            AssertSkillMounts("skill.cloud_breath", "skill.cloud_breath.interact");
            AssertSkillMounts("skill.endless_flame", "skill.endless_flame.remove");
        }

        [Test]
        public void StoneLegionDeck_SlotsAndLayerLeaderFlag()
        {
            var issues = ThemeDeckMappingVerifier.VerifyDeck(mContent.Catalog, "deck.stone_legion");
            Assert.AreEqual(0, issues.Count, FormatIssues(issues));

            Assert.IsTrue(mContent.Catalog.TryGetCard("monster.shelter_stone", out var shelterStone));
            Assert.AreEqual(5, shelterStone.Sequence, "层主必须是 sequence 5");
            Assert.IsTrue(shelterStone.IsBoss, "层主卡须挂 isBoss");
            Assert.AreEqual(MonsterRank.FloorBoss, shelterStone.Rank, "层主卡等级须投影为 FloorBoss");
        }

        [Test]
        public void StoneLegionMonsters_ValidateCatalog_IsValid()
        {
            var report = mContent.ValidateCatalog();
            Assert.IsTrue(report.IsValid, FormatIssues(report));
            Assert.AreEqual(0, report.PendingEffectIds.Count, "Pending: " + string.Join(", ", report.PendingEffectIds));
        }

        [Test]
        public void StoneLegionCardJson_AuthoringMatchesStreaming()
        {
            for (var i = 0; i < StoneLegionMonsterIds.Length; i++)
            {
                AssertAreEqualFiles(
                    CardPresentationJsonIO.GetAuthoringAbsolutePath(StoneLegionMonsterIds[i]),
                    CardPresentationJsonIO.GetStreamingAbsolutePath(StoneLegionMonsterIds[i]),
                    StoneLegionMonsterIds[i] + " 卡面 JSON 双侧不一致（Authoring 与 Streaming 必须同内容）");
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
