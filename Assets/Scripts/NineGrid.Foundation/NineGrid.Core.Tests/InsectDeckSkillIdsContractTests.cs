using System.Collections.Generic;
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
    /// #130：召唤套（deck.insect）怪物技能权威链路迁移——effectAssemblies → skillIds。
    /// 技能效果模板统一由 skill_*.json 持有；怪物 JSON 不再直挂 assemblies。
    /// 只断言稳定 ID / 技能挂载 / 攻击模式；禁止断言可变 displayName。
    /// </summary>
    public sealed class InsectDeckSkillIdsContractTests
    {
        private static readonly string[] InsectMonsterIds =
        {
            "monster.wandering_child",
            "monster.smuggler",
            "monster.big_skeleton",
            "monster.world_turning_hand",
            "monster.rogue",
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
        public void InsectDeckMonsters_SkillsViaSkillIds_NoDirectAssemblies()
        {
            AssertSkillIds("monster.wandering_child", "skill.death_summon");
            AssertSkillIds("monster.smuggler", "skill.ranged_weapon", "skill.speed_up");
            AssertSkillIds("monster.world_turning_hand", "skill.chant");
            AssertSkillIds("monster.rogue", "skill.lord_of_death");
        }

        [Test]
        public void BigSkeleton_NoSkills_AttackPatternOnly_NotFlaggedAsMissingSkill()
        {
            Assert.IsTrue(mContent.Catalog.TryGetCard("monster.big_skeleton", out var bigSkeleton));
            Assert.AreEqual(0, bigSkeleton.SkillIds.Count, "big_skeleton 策划只有全向近战，不得凭空加技能");
            Assert.AreEqual(0, bigSkeleton.EffectIds.Count, "big_skeleton 不得直挂任何效果装配");
            Assert.AreEqual(AttackPattern.OmnidirectionalMelee, bigSkeleton.AttackPattern, "big_skeleton 仅靠 attackPattern 表现全向近战");
        }

        [Test]
        public void InsectDeckSkills_ResolveSameEffectMountsAsBeforeMigration()
        {
            AssertSkillMounts("skill.death_summon", "skill.death_summon.remove");
            AssertSkillMounts("skill.ranged_weapon", "skill.ranged_weapon.activate");
            AssertSkillMounts("skill.speed_up", "skill.speed_up.damage");
            AssertSkillMounts("skill.chant", "skill.chant.interact");
            AssertSkillMounts("skill.lord_of_death", "skill.lord_of_death.remove");
        }

        [Test]
        public void InsectDeckMonsters_ValidateCatalog_IsValid()
        {
            var report = mContent.ValidateCatalog();
            Assert.IsTrue(report.IsValid, FormatIssues(report));
            Assert.AreEqual(0, report.PendingEffectIds.Count, "Pending: " + string.Join(", ", report.PendingEffectIds));
        }

        [Test]
        public void InsectDeckCardJson_AuthoringMatchesStreaming()
        {
            for (var i = 0; i < InsectMonsterIds.Length; i++)
            {
                AssertAreEqualFiles(
                    CardPresentationJsonIO.GetAuthoringAbsolutePath(InsectMonsterIds[i]),
                    CardPresentationJsonIO.GetStreamingAbsolutePath(InsectMonsterIds[i]),
                    InsectMonsterIds[i] + " 卡面 JSON 双侧不一致（Authoring 与 Streaming 必须同内容）");
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
