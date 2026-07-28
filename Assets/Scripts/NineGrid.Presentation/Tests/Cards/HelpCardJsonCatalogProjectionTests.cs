using NUnit.Framework;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// #67：帮助卡 JSON → GameContentCatalog 投影；Overlay 不再盖写帮助卡业务字段。
    /// </summary>
    public sealed class HelpCardJsonCatalogProjectionTests
    {
        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
        }

        [Test]
        public void TryProject_MapsIdentityStatsMounts_FromHelpJson()
        {
            var dto = new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "help.proj_demo",
                kind = "HelpCard",
                displayName = "投影演示",
                gold = 80,
                rarity = "Blue",
                tags = new[] { "恢复" },
                effectIds = new[]
                {
                    "help.proj_demo.board_adjacent",
                    "help.proj_demo.item_battle",
                    "help.proj_demo.use",
                },
                stats = new CardPresentationStatsDto { hp = 0, attack = 0, armor = 0 },
            };

            Assert.IsTrue(HelpCardJsonCatalogProjector.TryProject(dto, out var card));
            Assert.AreEqual("help.proj_demo", card.DefId);
            Assert.AreEqual(CardKind.HelpCard, card.Kind);
            Assert.AreEqual("投影演示", card.DisplayName);
            Assert.AreEqual(ContentRarity.Blue, card.Rarity);
            Assert.AreEqual(80, card.Price);
            Assert.AreEqual(1, card.Tags.Count);
            Assert.AreEqual("恢复", card.Tags[0]);
            Assert.AreEqual(3, card.EffectIds.Count);
            Assert.AreEqual("help.proj_demo.board_adjacent", card.EffectIds[0]);
            Assert.AreEqual("help.proj_demo.item_battle", card.EffectIds[1]);
            Assert.AreEqual("help.proj_demo.use", card.EffectIds[2]);
        }

        [Test]
        public void ApplyToCatalog_OverwritesLubanHelpCard_WithJson()
        {
            var catalog = new GameContentCatalog();
            catalog.AddCard(
                new CardContentDefinition("help.proj_overwrite", "LubanName", CardKind.HelpCard)
                    .WithRarity(ContentRarity.White)
                    .WithPrice(1)
                    .AddTag("旧")
                    .AddEffect("help.proj_overwrite.old"));

            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "help.proj_overwrite",
                kind = "HelpCard",
                displayName = "JsonName",
                gold = 99,
                rarity = "Gold",
                tags = new[] { "特殊" },
                effectIds = new[] { "help.proj_overwrite.use" },
                stats = new CardPresentationStatsDto(),
            });

            Assert.AreEqual(1, HelpCardJsonCatalogProjector.ApplyToCatalog(catalog));

            var card = catalog.Cards["help.proj_overwrite"];
            Assert.AreEqual("JsonName", card.DisplayName);
            Assert.AreEqual(99, card.Price);
            Assert.AreEqual(ContentRarity.Gold, card.Rarity);
            Assert.AreEqual(1, card.Tags.Count);
            Assert.AreEqual("特殊", card.Tags[0]);
            Assert.AreEqual(1, card.EffectIds.Count);
            Assert.AreEqual("help.proj_overwrite.use", card.EffectIds[0]);
        }

        [Test]
        public void TryProject_SkipsNonHelpOrOldSchema()
        {
            Assert.IsFalse(HelpCardJsonCatalogProjector.TryProject(new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "monster.x",
                kind = "Monster",
                displayName = "怪",
            }, out _));

            Assert.IsFalse(HelpCardJsonCatalogProjector.TryProject(new CardPresentationConfigDto
            {
                schemaVersion = 1,
                contentId = "help.old_schema",
                kind = "HelpCard",
                displayName = "旧版",
                effectIds = new[] { "help.old_schema.use" },
            }, out _));
        }

        [Test]
        public void Overlay_DoesNotOverwriteHelpCard_DisplayNameGoldStats()
        {
            var catalog = new GameContentCatalog();
            catalog.AddCard(
                new CardContentDefinition("help.overlay_skip", "FromJson", CardKind.HelpCard)
                    .WithPrice(80)
                    .WithStats(0, 0, 0));

            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "help.overlay_skip",
                kind = "HelpCard",
                displayName = "OverlayWouldChange",
                gold = 1,
                stats = new CardPresentationStatsDto { hp = 9, attack = 8, armor = 7 },
            });

            CardPresentationBusinessOverlay.ApplyToCatalog(catalog);

            var card = catalog.Cards["help.overlay_skip"];
            Assert.AreEqual("FromJson", card.DisplayName);
            Assert.AreEqual(80, card.Price);
            Assert.AreEqual(0, card.Stats.MaxHp);
            Assert.AreEqual(0, card.Stats.Attack);
            Assert.AreEqual(0, card.Stats.Armor);
        }
    }
}
