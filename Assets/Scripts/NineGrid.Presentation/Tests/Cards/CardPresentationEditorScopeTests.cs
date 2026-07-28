using System;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Content.Editor;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// 表现层配置器条目门槛与解耦装配 IA：Skill 不进窗；空装配清 effectIds；挂装配投影进 Catalog。
    /// </summary>
    public sealed class CardPresentationEditorScopeTests
    {
        [Test]
        public void IsCardLikeKind_ExcludesSkill_KeepsHelpAndItemKinds()
        {
            Assert.IsFalse(CardPresentationMigration.IsCardLikeKind("Skill"));
            Assert.IsTrue(CardPresentationMigration.IsCardLikeKind("HelpCard"));
            Assert.IsTrue(CardPresentationMigration.IsCardLikeKind("Item"));
            Assert.IsTrue(CardPresentationMigration.IsCardLikeKind("PlayerCard"));
            Assert.IsTrue(CardPresentationMigration.IsCardLikeKind("Monster"));
            Assert.IsTrue(CardPresentationMigration.IsCardLikeKind("Relic"));
            Assert.IsTrue(CardPresentationMigration.IsCardLikeKind("Avatar"));
        }

        [Test]
        public void IsPresentationEditorEntry_BlocksSkillPrefixEvenIfKindLooksLikeHelp()
        {
            Assert.IsFalse(
                CardPresentationMigration.IsPresentationEditorEntry("Skill", "skill.breathe_fire"));
            Assert.IsFalse(
                CardPresentationMigration.IsPresentationEditorEntry("HelpCard", "skill.breathe_fire"));
            Assert.IsTrue(
                CardPresentationMigration.IsPresentationEditorEntry("HelpCard", "help.bomb"));
            Assert.IsTrue(
                CardPresentationMigration.IsPresentationEditorEntry("Item", "player.gold_card"));
        }

        [Test]
        public void MapSidebarCategory_SkillIsNotItem_HelpCardStillIs()
        {
            Assert.AreEqual(
                CardPresentationSidebarCategory.Other,
                CardPresentationEditorSession.MapSidebarCategory("Skill"));
            Assert.AreEqual(
                CardPresentationSidebarCategory.Item,
                CardPresentationEditorSession.MapSidebarCategory("HelpCard"));
            Assert.AreEqual(
                CardPresentationSidebarCategory.Item,
                CardPresentationEditorSession.MapSidebarCategory("Item"));
            Assert.AreEqual(
                CardPresentationSidebarCategory.Item,
                CardPresentationEditorSession.MapSidebarCategory("PlayerCard"));
        }

        [Test]
        public void IsItemLikeKind_AndCombatStats_MatchPresentationRoles()
        {
            Assert.IsTrue(CardPresentationEditorSession.IsItemLikeKind("HelpCard"));
            Assert.IsTrue(CardPresentationEditorSession.IsItemLikeKind("PlayerCard"));
            Assert.IsFalse(CardPresentationEditorSession.IsItemLikeKind("Monster"));
            Assert.IsTrue(CardPresentationEditorSession.IsCombatStatsKind("Avatar"));
            Assert.IsTrue(CardPresentationEditorSession.IsCombatStatsKind("Monster"));
            Assert.IsFalse(CardPresentationEditorSession.IsCombatStatsKind("Relic"));
            Assert.IsTrue(CardPresentationEditorSession.IsMonsterKind("Monster"));
            Assert.IsFalse(CardPresentationEditorSession.IsMonsterKind("HelpCard"));
        }

        [Test]
        public void NormalizeLogicMounts_EmptyAssembliesClearsLegacyEffectIds()
        {
            var dto = new CardPresentationConfigDto
            {
                effectAssemblies = Array.Empty<EffectAssemblyDto>(),
                effectIds = new[] { "legacy.fx" },
                skillIds = null,
            };

            CardPresentationEditorSession.NormalizeLogicMounts(dto);

            Assert.IsNotNull(dto.effectAssemblies);
            Assert.AreEqual(0, dto.effectAssemblies.Length);
            Assert.IsNotNull(dto.effectIds);
            Assert.AreEqual(0, dto.effectIds.Length);
            Assert.IsNotNull(dto.skillIds);
            Assert.AreEqual(0, dto.skillIds.Length);
        }

        [Test]
        public void NormalizeLogicMounts_KeepsEffectIdsWhenAssembliesPresent()
        {
            var dto = new CardPresentationConfigDto
            {
                effectAssemblies = new[]
                {
                    new EffectAssemblyDto
                    {
                        id = "help.demo.use",
                        templateId = "tpl.demo",
                        containerType = "HelpCard",
                        argsJson = "{}",
                    },
                },
                effectIds = new[] { "should.remain.until.projection" },
            };

            CardPresentationEditorSession.NormalizeLogicMounts(dto);

            Assert.AreEqual(1, dto.effectAssemblies.Length);
            Assert.AreEqual(1, dto.effectIds.Length);
            Assert.AreEqual("should.remain.until.projection", dto.effectIds[0]);
        }

        [Test]
        public void ProjectCard_EmptyAssembliesAndEffectIds_YieldsNoEffectIds()
        {
            var dto = new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "help.blank_board",
                kind = "HelpCard",
                displayName = "白板",
                deckId = "deck.help",
                effectAssemblies = Array.Empty<EffectAssemblyDto>(),
                effectIds = Array.Empty<string>(),
            };

            var catalog = new GameContentCatalog();
            Assert.IsTrue(ContentJsonCatalogProjector.TryProjectCard(dto, catalog, out var card));
            Assert.AreEqual(0, card.EffectIds.Count);
            Assert.IsFalse(catalog.Effects.ContainsKey("help.blank_board.use"));
        }

        [Test]
        public void FormatEffectTemplateChoiceLabel_AppendsTransitionalOriginSuffix()
        {
            Assert.AreEqual(
                "造成伤害（原帮助卡效果）",
                CardPresentationEditorSession.FormatEffectTemplateChoiceLabel(
                    "tpl.help.fireball.use",
                    "造成伤害"));
            Assert.AreEqual(
                "掉甲加攻（原怪物技能效果）",
                CardPresentationEditorSession.FormatEffectTemplateChoiceLabel(
                    "tpl.skill.stone_lover.armor_lost",
                    "掉甲加攻"));
            Assert.AreEqual(
                "基础护甲+1（原遗物效果）",
                CardPresentationEditorSession.FormatEffectTemplateChoiceLabel(
                    "tpl.relic.wood_shield.stat",
                    "基础护甲+1"));
            Assert.AreEqual(
                "其它（原效果）",
                CardPresentationEditorSession.FormatEffectTemplateChoiceLabel("tpl.misc.foo", "其它"));
            Assert.AreEqual(
                "（原帮助卡效果）",
                CardPresentationEditorSession.ResolveEffectOriginSuffix("tpl.help.x"));
        }

        [Test]
        public void TryExpandSkillIdsIntoAssemblies_FlattensStoneLoverOntoMonster()
        {
            var authoring = CardPresentationJsonIO.GetAuthoringAbsolutePath("skill.stone_lover");
            if (!System.IO.File.Exists(authoring)
                && !System.IO.File.Exists(CardPresentationJsonIO.GetStreamingAbsolutePath("skill.stone_lover")))
            {
                Assert.Ignore("skill.stone_lover JSON 不在磁盘");
            }

            var session = new CardPresentationEditorSession();
            var dto = new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "monster.blank_expand_demo",
                kind = "Monster",
                displayName = "展开演示",
                effectAssemblies = Array.Empty<EffectAssemblyDto>(),
                skillIds = new[] { "skill.stone_lover" },
            };

            Assert.IsTrue(session.TryExpandSkillIdsIntoAssemblies(dto));
            Assert.AreEqual(0, dto.skillIds.Length);
            Assert.GreaterOrEqual(dto.effectAssemblies.Length, 1);
            Assert.AreEqual("tpl.skill.stone_lover.armor_lost", dto.effectAssemblies[0].templateId);
            Assert.AreEqual("MonsterSkill", dto.effectAssemblies[0].containerType);
        }

        [Test]
        public void ProjectMonster_WithDirectAssembly_ResolvesWithoutSkillIds()
        {
            EffectTemplateCatalog.Invalidate();
            if (!EffectTemplateCatalog.TryGet("tpl.skill.stone_lover.armor_lost", out var template)
                || template == null)
            {
                Assert.Ignore("生产 effect_templates.json 未加载 tpl.skill.stone_lover.armor_lost");
            }

            var dto = new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "monster.editor_direct_mount",
                kind = "Monster",
                displayName = "直挂演示",
                deckId = "deck.monster",
                effectAssemblies = new[]
                {
                    new EffectAssemblyDto
                    {
                        id = "monster.editor_direct_mount.armor_lost",
                        templateId = "tpl.skill.stone_lover.armor_lost",
                        containerType = "MonsterSkill",
                        argsJson = "{\"delta\":1,\"reason\":\"direct\"}",
                    },
                },
                effectIds = Array.Empty<string>(),
                skillIds = Array.Empty<string>(),
            };

            var catalog = new GameContentCatalog();
            Assert.IsTrue(ContentJsonCatalogProjector.TryProjectCard(dto, catalog, out var card));
            Assert.AreEqual(1, card.EffectIds.Count);
            Assert.AreEqual(0, card.SkillIds.Count);
            Assert.IsTrue(catalog.TryGetEffect("monster.editor_direct_mount.armor_lost", out var effect));
            Assert.AreEqual(EffectContainerType.MonsterSkill, effect.ContainerType);
        }

        [Test]
        public void SuggestArgsJsonForTemplate_StrayCubSlot6_ReusesPeerValue2()
        {
            CardPresentationConfigCatalog.Invalidate();
            EffectTemplateCatalog.Invalidate();
            if (!EffectTemplateCatalog.TryGet("tpl.skill.stray_cub.slot6", out _)
                || (!System.IO.File.Exists(CardPresentationJsonIO.GetAuthoringAbsolutePath("skill.stray_cub"))
                    && !System.IO.File.Exists(CardPresentationJsonIO.GetStreamingAbsolutePath("skill.stray_cub"))))
            {
                Assert.Ignore("生产 stray_cub 模板/技能 JSON 未加载");
            }

            var session = new CardPresentationEditorSession();
            var args = session.SuggestArgsJsonForTemplate("tpl.skill.stray_cub.slot6");
            Assert.IsTrue(args.Contains("\"value\""), args);
            Assert.IsTrue(args.Contains("2"), args);
            Assert.AreNotEqual("{}", args);
        }

        [Test]
        public void ProjectStoneMan_StrayCubSlot6_ResolvesAttackValue2()
        {
            CardPresentationConfigCatalog.Invalidate();
            EffectTemplateCatalog.Invalidate();
            if (!CardPresentationConfigCatalog.TryGet("monster.stone_man", out var dto) || dto == null)
            {
                Assert.Ignore("monster.stone_man JSON 未加载");
            }

            Assert.IsNotNull(dto.effectAssemblies);
            Assert.GreaterOrEqual(dto.effectAssemblies.Length, 1);
            Assert.AreEqual("tpl.skill.stray_cub.slot6", dto.effectAssemblies[0].templateId);
            Assert.IsTrue(dto.effectAssemblies[0].argsJson.Contains("2"), dto.effectAssemblies[0].argsJson);

            var catalog = ContentCatalogBootstrap.Load();
            Assert.IsTrue(catalog.TryGetCard("monster.stone_man", out var card));
            Assert.AreEqual(1, card.EffectIds.Count);
            Assert.IsTrue(catalog.TryGetEffect(card.EffectIds[0], out var effect));
            var parsed = EffectJson.Parse(effect.Json);
            Assert.AreEqual(2, parsed.Get("modifier").Get("value").AsInt(0), effect.Json);
        }
    }
}
