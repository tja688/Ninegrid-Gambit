using NineGrid.Cards.Presentation;
using NineGrid.Content.CardPresentation;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Cards
{
    public sealed class CardInspectDetailComposerTests
    {
        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
        }

        [Test]
        public void Compose_UsesFaceIntro_AndDeckDisplayName_WithoutRepeatingBrief()
        {
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "monster.inspect_demo",
                kind = "Monster",
                deckId = "deck.inspect_demo",
                displayName = "试作怪",
                description = "卡面概括",
                faceIntro = "背景小传",
                effectAssemblies = System.Array.Empty<EffectAssemblyDto>(),
                skillIds = System.Array.Empty<string>(),
            });
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "deck.inspect_demo",
                kind = "Deck",
                displayName = "试作牌组",
                description = "牌组说明",
            });

            var snapshot = new CardPresentationSnapshot
            {
                DefId = "monster.inspect_demo",
                BasicDescription = "卡面概括",
                DetailDescription = "卡面概括",
                FaceIntro = "背景小传",
            };

            var result = CardInspectDetailComposer.Compose(
                "monster.inspect_demo",
                snapshot,
                catalog: null);

            Assert.AreEqual("背景小传", result.FaceIntro);
            StringAssert.Contains("试作牌组", result.DeckIntro);
            StringAssert.Contains("牌组说明", result.DeckIntro);
            // 真卡面已展示简要，详述区不应再抄一遍 description。
            Assert.AreEqual(string.Empty, result.SkillDetails);
        }

        [Test]
        public void Compose_ExpandsSkillDesignText_NotBriefDescription()
        {
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "monster.inspect_skills",
                kind = "Monster",
                deckId = "deck.inspect_demo",
                displayName = "多技能怪",
                description = "卡面概括勿重复",
                faceIntro = "intro",
                skillIds = new[] { "skill.inspect_a", "skill.inspect_b" },
                effectAssemblies = System.Array.Empty<EffectAssemblyDto>(),
            });
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "skill.inspect_a",
                kind = "Skill",
                displayName = "技能甲",
                description = "甲的简要（卡面用）",
                effectAssemblies = new[]
                {
                    new EffectAssemblyDto
                    {
                        id = "skill.inspect_a.rule",
                        templateId = "tpl.inspect_a",
                        containerType = "MonsterSkill",
                        argsJson = "{}",
                    },
                },
            });
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "skill.inspect_b",
                kind = "Skill",
                displayName = "技能乙",
                description = "乙的简要（卡面用）",
                effectAssemblies = new[]
                {
                    new EffectAssemblyDto
                    {
                        id = "skill.inspect_b.rule",
                        templateId = "tpl.inspect_b",
                        containerType = "MonsterSkill",
                        argsJson = "{}",
                    },
                },
            });

            var catalog = new GameContentCatalog();
            catalog.AddSkill(new SkillContentDefinition(
                "skill.inspect_a",
                "技能甲",
                EffectContainerType.MonsterSkill,
                "甲的简要（卡面用）"));
            catalog.AddSkill(new SkillContentDefinition(
                "skill.inspect_b",
                "技能乙",
                EffectContainerType.MonsterSkill,
                "乙的简要（卡面用）"));
            catalog.AddEffect(new ContentEffectDefinition(
                "skill.inspect_a.rule",
                EffectContainerType.MonsterSkill,
                "{}",
                ContentImplementationState.Implemented,
                "[场上] 甲的详细效果"));
            catalog.AddEffect(new ContentEffectDefinition(
                "skill.inspect_b.rule",
                EffectContainerType.MonsterSkill,
                "{}",
                ContentImplementationState.Implemented,
                "[使用时] 乙的详细效果"));

            var result = CardInspectDetailComposer.Compose(
                "monster.inspect_skills",
                new CardPresentationSnapshot
                {
                    DefId = "monster.inspect_skills",
                    BasicDescription = "卡面概括勿重复",
                },
                catalog);

            StringAssert.Contains("【技能甲】", result.SkillDetails);
            StringAssert.Contains("[场上] 甲的详细效果", result.SkillDetails);
            StringAssert.Contains("【技能乙】", result.SkillDetails);
            StringAssert.Contains("[使用时] 乙的详细效果", result.SkillDetails);
            StringAssert.DoesNotContain("甲的简要（卡面用）", result.SkillDetails);
            StringAssert.DoesNotContain("乙的简要（卡面用）", result.SkillDetails);
            StringAssert.DoesNotContain("卡面概括勿重复", result.SkillDetails);
        }
    }
}
