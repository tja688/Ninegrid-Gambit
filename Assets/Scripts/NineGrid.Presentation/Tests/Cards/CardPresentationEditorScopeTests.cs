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
        public void ProjectCard_WithAssembly_ResolvesIntoCatalogEffects()
        {
            EffectTemplateCatalog.Invalidate();
            if (!EffectTemplateCatalog.TryGet("tpl.heal_player_on_use_help_card", out var template)
                || template == null)
            {
                Assert.Ignore("生产 effect_templates.json 未加载 tpl.heal_player_on_use_help_card");
            }

            var dto = new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "help.editor_mount_demo",
                kind = "HelpCard",
                displayName = "装配演示",
                deckId = "deck.help",
                effectAssemblies = new[]
                {
                    new EffectAssemblyDto
                    {
                        id = "help.editor_mount_demo.use",
                        templateId = "tpl.heal_player_on_use_help_card",
                        containerType = "HelpCard",
                        argsJson = "{\"amount\":3}",
                    },
                },
                effectIds = Array.Empty<string>(),
            };

            var catalog = new GameContentCatalog();
            Assert.IsTrue(ContentJsonCatalogProjector.TryProjectCard(dto, catalog, out var card));
            Assert.AreEqual(1, card.EffectIds.Count);
            Assert.AreEqual("help.editor_mount_demo.use", card.EffectIds[0]);
            Assert.IsTrue(catalog.TryGetEffect("help.editor_mount_demo.use", out var effect));
            Assert.IsNotNull(effect);
            Assert.AreEqual(EffectContainerType.HelpCard, effect.ContainerType);
        }
    }
}
