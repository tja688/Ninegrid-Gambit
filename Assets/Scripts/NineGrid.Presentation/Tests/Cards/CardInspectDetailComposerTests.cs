using System.Collections.Generic;
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

        [Test]
        public void Compose_PrefersLiveMountedSkills_OverEmptyMonsterJson()
        {
            // QuickTest 白板怪：JSON skillIds 为空，但局内已注入 skill.offer_fire 类技能。
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "monster.blank_host",
                kind = "Monster",
                displayName = "白板宿主",
                description = "卡面已被注入技能短描述",
                faceIntro = "白板小传",
                skillIds = System.Array.Empty<string>(),
                effectAssemblies = System.Array.Empty<EffectAssemblyDto>(),
            });
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "skill.live_inject",
                kind = "Skill",
                displayName = "注入技",
                description = "注入简要勿进详述",
                effectAssemblies = new[]
                {
                    new EffectAssemblyDto
                    {
                        id = "skill.live_inject.rule",
                        templateId = "tpl.live_inject",
                        containerType = "MonsterSkill",
                        argsJson = "{\"amount\":7}",
                    },
                },
            });

            var catalog = new GameContentCatalog();
            catalog.AddSkill(new SkillContentDefinition(
                "skill.live_inject",
                "注入技",
                EffectContainerType.MonsterSkill,
                "注入简要勿进详述")
                .AddEffect("skill.live_inject.rule"));
            catalog.AddEffect(new ContentEffectDefinition(
                "skill.live_inject.rule",
                EffectContainerType.MonsterSkill,
                "{\"kind\":\"Triggered\",\"action\":{\"atom\":\"DealDamage\",\"amount\":7}}",
                ContentImplementationState.Implemented,
                "[场上] 造成{amount}点伤害"));

            var live = new List<CardInspectDetailComposer.LiveEffectMount>
            {
                new CardInspectDetailComposer.LiveEffectMount(
                    "skill.live_inject",
                    "skill.live_inject.rule"),
            };

            var result = CardInspectDetailComposer.Compose(
                "monster.blank_host",
                new CardPresentationSnapshot
                {
                    DefId = "monster.blank_host",
                    BasicDescription = "卡面已被注入技能短描述",
                    Attack = 5,
                    Hp = 99,
                },
                catalog,
                live);

            StringAssert.Contains("【注入技】", result.SkillDetails);
            StringAssert.Contains("[场上] 造成7点伤害", result.SkillDetails);
            StringAssert.DoesNotContain("注入简要勿进详述", result.SkillDetails);
            StringAssert.DoesNotContain("卡面已被注入技能短描述", result.SkillDetails);
        }

        [Test]
        public void Compose_LiveMounts_IgnorePresetJsonSkillIds()
        {
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "monster.has_preset",
                kind = "Monster",
                displayName = "预设怪",
                skillIds = new[] { "skill.preset_only" },
                effectAssemblies = System.Array.Empty<EffectAssemblyDto>(),
            });
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "skill.preset_only",
                kind = "Skill",
                displayName = "预设技",
                effectAssemblies = new[]
                {
                    new EffectAssemblyDto
                    {
                        id = "skill.preset_only.rule",
                        templateId = "tpl.preset",
                        containerType = "MonsterSkill",
                        argsJson = "{}",
                    },
                },
            });
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "skill.runtime_only",
                kind = "Skill",
                displayName = "运行时技",
                effectAssemblies = new[]
                {
                    new EffectAssemblyDto
                    {
                        id = "skill.runtime_only.rule",
                        templateId = "tpl.runtime",
                        containerType = "MonsterSkill",
                        argsJson = "{}",
                    },
                },
            });

            var catalog = new GameContentCatalog();
            catalog.AddSkill(new SkillContentDefinition(
                "skill.preset_only",
                "预设技",
                EffectContainerType.MonsterSkill,
                string.Empty));
            catalog.AddSkill(new SkillContentDefinition(
                "skill.runtime_only",
                "运行时技",
                EffectContainerType.MonsterSkill,
                string.Empty));
            catalog.AddEffect(new ContentEffectDefinition(
                "skill.preset_only.rule",
                EffectContainerType.MonsterSkill,
                "{}",
                ContentImplementationState.Implemented,
                "[场上] 预设效果"));
            catalog.AddEffect(new ContentEffectDefinition(
                "skill.runtime_only.rule",
                EffectContainerType.MonsterSkill,
                "{}",
                ContentImplementationState.Implemented,
                "[场上] 运行时效果"));

            var live = new List<CardInspectDetailComposer.LiveEffectMount>
            {
                new CardInspectDetailComposer.LiveEffectMount(
                    "skill.runtime_only",
                    "skill.runtime_only.rule"),
            };

            var result = CardInspectDetailComposer.Compose(
                "monster.has_preset",
                new CardPresentationSnapshot { DefId = "monster.has_preset" },
                catalog,
                live);

            StringAssert.Contains("【运行时技】", result.SkillDetails);
            StringAssert.Contains("[场上] 运行时效果", result.SkillDetails);
            StringAssert.DoesNotContain("预设技", result.SkillDetails);
            StringAssert.DoesNotContain("预设效果", result.SkillDetails);
        }
    }
}
