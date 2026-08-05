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
    /// #129：旋转套（deck.orc_legion）怪物技能权威链路迁移——effectAssemblies → skillIds。
    /// 技能效果模板统一由 skill_*.json 持有；怪物 JSON 不再直挂 assemblies。
    /// 只断言稳定 ID / 技能挂载 / 攻击模式；禁止断言可变 displayName。
    /// </summary>
    public sealed class RotationDeckSkillIdsContractTests
    {
        private static readonly string[] RotationMonsterIds =
        {
            "monster.big_stone",
            "monster.sky_eye",
            "monster.fire_dragon",
            "monster.skeleton_taunter",
            "monster.skeleton_king",
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
        public void RotationDeckMonsters_SkillsViaSkillIds_NoDirectAssemblies()
        {
            AssertSkillIds("monster.sky_eye", "skill.ranged_weapon", "skill.evade");
            AssertSkillIds("monster.fire_dragon", "skill.delivery");
            AssertSkillIds("monster.skeleton_taunter", "skill.turn_world");
            AssertSkillIds("monster.skeleton_king", "skill.space_mastery");
        }

        [Test]
        public void BigStone_NoSkills_AttackPatternOnly_NotFlaggedAsMissingSkill()
        {
            Assert.IsTrue(mContent.Catalog.TryGetCard("monster.big_stone", out var bigStone));
            Assert.AreEqual(0, bigStone.SkillIds.Count, "big_stone 策划只有斜角攻击模式，不得凭空加技能");
            Assert.AreEqual(0, bigStone.EffectIds.Count, "big_stone 不得直挂任何效果装配");
            Assert.AreEqual(AttackPattern.DiagonalMelee, bigStone.AttackPattern, "big_stone 仅靠 attackPattern 表现斜角近战");
        }

        [Test]
        public void RotationDeckSkills_ResolveSameEffectMountsAsBeforeMigration()
        {
            AssertSkillMounts("skill.ranged_weapon", "skill.ranged_weapon.activate");
            AssertSkillMounts("skill.evade", "skill.evade.battle");
            AssertSkillMounts("skill.delivery", "skill.delivery.move");
            AssertSkillMounts("skill.turn_world", "skill.turn_world.enter");
            AssertSkillMounts("skill.space_mastery", "skill.space_mastery.battle");
        }

        [Test]
        public void RotationDeckMonsters_ValidateCatalog_IsValid()
        {
            var report = mContent.ValidateCatalog();
            Assert.IsTrue(report.IsValid, FormatIssues(report));
            Assert.AreEqual(0, report.PendingEffectIds.Count, "Pending: " + string.Join(", ", report.PendingEffectIds));
        }

        [Test]
        public void RotationDeckCardJson_AuthoringMatchesStreaming()
        {
            for (var i = 0; i < RotationMonsterIds.Length; i++)
            {
                AssertAreEqualFiles(
                    CardPresentationJsonIO.GetAuthoringAbsolutePath(RotationMonsterIds[i]),
                    CardPresentationJsonIO.GetStreamingAbsolutePath(RotationMonsterIds[i]),
                    RotationMonsterIds[i] + " 卡面 JSON 双侧不一致（Authoring 与 Streaming 必须同内容）");
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
