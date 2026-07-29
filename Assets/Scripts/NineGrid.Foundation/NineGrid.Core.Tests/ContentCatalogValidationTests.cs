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
    /// 内容门禁：小型夹具与生产 JSON Catalog 均须 ValidateCatalog 绿（ADR-0008 / ADR-0009 / #70）。
    /// </summary>
    public sealed class ContentCatalogValidationTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, TableNineContentCatalog.CreateDefault());
        }

        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
            EffectTemplateCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void DefaultCatalog_ValidateCatalog_IsValidWithNoPendingEffects()
        {
            var report = mArch.GetSystem<IContentSystem>().ValidateCatalog();

            Assert.IsTrue(report.IsValid, FormatIssues(report));
            Assert.AreEqual(0, report.PendingEffectIds.Count, "Pending effect ids: " + string.Join(", ", report.PendingEffectIds));
            Assert.Greater(report.ImplementedEffectIds.Count, 0, "Expected implemented effects in default catalog.");
        }

        [Test]
        public void BootstrapCatalog_ValidateCatalog_IsValidWithProductionJsonProjection()
        {
            CardPresentationConfigCatalog.Invalidate();
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            var report = mArch.GetSystem<IContentSystem>().ValidateCatalog();

            Assert.IsTrue(report.IsValid, FormatIssues(report));
            Assert.AreEqual(0, report.PendingEffectIds.Count, "Pending effect ids: " + string.Join(", ", report.PendingEffectIds));

            Assert.IsTrue(catalog.Cards.TryGetValue("help.healing_spring", out var spring));
            Assert.AreEqual(CardKind.HelpCard, spring.Kind);
            Assert.AreEqual(3, spring.EffectIds.Count, "healing_spring mounts must come from JSON projection");
            Assert.AreEqual("help.healing_spring.board_adjacent", spring.EffectIds[0]);
            Assert.AreEqual("help.healing_spring.item_battle", spring.EffectIds[1]);
            Assert.AreEqual("help.healing_spring.use", spring.EffectIds[2]);
            Assert.AreEqual("治疗圣光", spring.DisplayName);

            Assert.IsTrue(catalog.Cards.TryGetValue("help.doubling_tower", out var tower));
            Assert.AreEqual(2, tower.EffectIds.Count);
            Assert.AreEqual("help.doubling_tower.board_monster", tower.EffectIds[0]);
            Assert.AreEqual("help.doubling_tower.item_player", tower.EffectIds[1]);

            Assert.IsTrue(catalog.Relics.TryGetValue("relic.arsenal", out var arsenal));
            Assert.AreEqual("军械库", arsenal.DisplayName);
            Assert.AreEqual(1, arsenal.EffectIds.Count);
            Assert.AreEqual("relic.arsenal.node_end", arsenal.EffectIds[0]);

            Assert.IsTrue(catalog.Skills.TryGetValue("skill.absorb_bone", out var absorb));
            Assert.Greater(absorb.EffectIds.Count, 0);

            Assert.IsTrue(catalog.Cards.TryGetValue("monster.beggar", out var beggar));
            Assert.AreEqual(CardKind.Monster, beggar.Kind);
            Assert.AreEqual(AttackPattern.None, beggar.AttackPattern);
            Assert.AreEqual(0, beggar.Stats.Action);
            // 技能仍以独立 Skill JSON 存在；怪物挂载可为空（当前乞丐无 skillIds）。
            Assert.IsTrue(catalog.Skills.ContainsKey("skill.beggar_bond"));
            Assert.AreEqual(0, beggar.SkillIds.Count);

            Assert.IsTrue(catalog.MonsterDecks.TryGetValue("deck.dragon", out var dragon));
            Assert.Greater(dragon.MonsterDefIds.Count, 0);

            Assert.IsTrue(catalog.Rewards.TryGetRoom(RoomKind.Fountain, out var fountain));
            Assert.IsTrue(fountain.HealToFull);

            Assert.IsTrue(catalog.Effects.ContainsKey("help.healing_potion.use"));
            Assert.IsTrue(catalog.Effects.ContainsKey("relic.junk_recycler.use"));
            var potion = catalog.Effects["help.healing_potion.use"];
            var recycler = catalog.Effects["relic.junk_recycler.use"];
            Assert.AreEqual(10, EffectDefinitionParser.ParseJson(potion.Json).Action.Get("amount").AsInt(0));
            Assert.AreEqual(2, EffectDefinitionParser.ParseJson(recycler.Json).Action.Get("amount").AsInt(0));
            Assert.AreEqual(EffectContainerType.HelpCard, EffectDefinitionParser.ParseJson(potion.Json).ContainerType);
            Assert.AreEqual(EffectContainerType.Relic, EffectDefinitionParser.ParseJson(recycler.Json).ContainerType);
            Assert.Greater(EffectTemplateCatalog.Count, 0, "effect templates must load");
            Assert.IsTrue(EffectTemplateCatalog.TryGet("tpl.heal_player_on_use_help_card", out _));

            Assert.AreEqual(5, catalog.Economy.MonsterRemovedGold);
            Assert.Greater(catalog.Rewards.NodeDeckRules.Count, 0);
        }

        private static string FormatIssues(ContentValidationReport report)
        {
            if (report.Issues.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("; ", report.Issues);
        }
    }
}
