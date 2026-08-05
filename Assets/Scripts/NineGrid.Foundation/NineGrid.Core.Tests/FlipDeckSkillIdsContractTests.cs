using System.Collections.Generic;
using System.IO;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #131：翻面套（deck.skeleton_legion）怪物技能权威链路迁移——effectAssemblies → skillIds。
    /// 技能效果模板统一由 skill_*.json 持有；怪物 JSON 不再直挂 assemblies。
    /// 翻面惰性契约：常规/被动技能效果必须带 IsFaceUp 条件或天然不可在背面触发；
    /// 策划显式声明的 OnFlip（跳杀/休养/盗取）与 OnDeal（刺客领袖）翻面边沿仍生效。
    /// 只断言稳定 ID / 技能挂载 / 触发语义；禁止断言可变 displayName。
    /// </summary>
    public sealed class FlipDeckSkillIdsContractTests
    {
        private static readonly string[] FlipMonsterIds =
        {
            "monster.smart_orc",
            "monster.brainless_orc",
            "monster.young_orc",
            "monster.veteran_orc",
            "monster.orc_commander",
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
        public void FlipDeckMonsters_SkillsViaSkillIds_NoDirectAssemblies()
        {
            AssertSkillIds("monster.smart_orc", "skill.ambush_melee", "skill.leap_kill");
            AssertSkillIds("monster.brainless_orc", "skill.ranged_weapon", "skill.rise_up");
            AssertSkillIds("monster.young_orc", "skill.ambush_melee", "skill.recuperate");
            AssertSkillIds("monster.veteran_orc", "skill.ambush_melee", "skill.steal");
            AssertSkillIds("monster.orc_commander", "skill.assassin_leader", "skill.rise_up");
        }

        [Test]
        public void FlipDeckSkills_ResolveSameEffectMountsAsBeforeMigration()
        {
            AssertSkillMounts("skill.ambush_melee", "skill.ambush_melee.interact");
            AssertSkillMounts("skill.leap_kill", "skill.leap_kill.flip");
            AssertSkillMounts("skill.recuperate", "skill.recuperate.flip");
            AssertSkillMounts("skill.steal", "skill.steal.flip");
            AssertSkillMounts("skill.ranged_weapon", "skill.ranged_weapon.activate");
            AssertSkillMounts("skill.rise_up", "skill.rise_up.damage");
            AssertSkillMounts("skill.assassin_leader", "skill.assassin_leader.rule");
        }

        [Test]
        public void FlipDeckSkills_FaceDownLazyTriggerContract()
        {
            // 背面默认惰性：常规/被动效果要么带 IsFaceUp 条件，要么其触发点天然不可在背面发生。
            AssertTriggerAtom("skill.ambush_melee.interact", "OnInteract", requireIsFaceUp: true);
            AssertTriggerAtom("skill.rise_up.damage", "OnSelfDamageDealtToPlayerCumulative", requireIsFaceUp: false);
            AssertTriggerAtom("skill.ranged_weapon.activate", "OnActivate", requireIsFaceUp: false);

            // 策划显式声明的翻面边沿触发：OnFlip 类仅在翻面时生效，且仍需翻至正面（IsFaceUp）。
            AssertTriggerAtom("skill.leap_kill.flip", "OnFlip", requireIsFaceUp: true);
            AssertTriggerAtom("skill.recuperate.flip", "OnFlip", requireIsFaceUp: true);
            AssertTriggerAtom("skill.steal.flip", "OnFlip", requireIsFaceUp: true);

            // 刺客领袖：仅领袖本人正面时，发牌触发翻面（其翻面动作施加于被发牌的怪）。
            AssertTriggerAtom("skill.assassin_leader.rule", "OnDeal", requireIsFaceUp: true);
        }

        [Test]
        public void FlipDeckMonsters_ValidateCatalog_IsValid()
        {
            var report = mContent.ValidateCatalog();
            Assert.IsTrue(report.IsValid, FormatIssues(report));
            Assert.AreEqual(0, report.PendingEffectIds.Count, "Pending: " + string.Join(", ", report.PendingEffectIds));
        }

        [Test]
        public void FlipDeckCardJson_AuthoringMatchesStreaming()
        {
            for (var i = 0; i < FlipMonsterIds.Length; i++)
            {
                AssertAreEqualFiles(
                    CardPresentationJsonIO.GetAuthoringAbsolutePath(FlipMonsterIds[i]),
                    CardPresentationJsonIO.GetStreamingAbsolutePath(FlipMonsterIds[i]),
                    FlipMonsterIds[i] + " 卡面 JSON 双侧不一致（Authoring 与 Streaming 必须同内容）");
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

        private void AssertTriggerAtom(string effectId, string expectedTriggerAtom, bool requireIsFaceUp)
        {
            Assert.IsTrue(mContent.Catalog.TryGetEffect(effectId, out var fx), "missing effect " + effectId);

            var body = EffectJson.Parse(fx.Json);
            var trigger = body.Get("trigger").Get("atom").AsString(string.Empty);
            Assert.AreEqual(expectedTriggerAtom, trigger, effectId + " 触发原子不符");

            if (requireIsFaceUp)
            {
                AssertHasIsFaceUpCondition(effectId, body);
            }
        }

        private static void AssertHasIsFaceUpCondition(string effectId, EffectDslNode body)
        {
            var conditions = body.Get("conditions").AsArray();
            Assert.Greater(conditions.Count, 0, effectId + " 无 conditions 声明，无法锁背面惰性");
            for (var i = 0; i < conditions.Count; i++)
            {
                if (string.Equals(
                    conditions[i].Get("atom").AsString(string.Empty),
                    "IsFaceUp",
                    System.StringComparison.Ordinal))
                {
                    return;
                }
            }

            Assert.Fail(effectId + " 应带 IsFaceUp 条件（背面默认惰性，不得在背面触发）");
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
