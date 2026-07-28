using NUnit.Framework;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// CardPresentation 权威契约：数值/显示名只经投影进 Catalog，无 BusinessOverlay。
    /// </summary>
    public sealed class CardPresentationAuthorityTests
    {
        [SetUp]
        public void SetUp()
        {
            CardPresentationConfigCatalog.Invalidate();
        }

        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
        }

        [Test]
        public void Authority_TryGetOwnedDescription_RequiresNonEmpty()
        {
            Assert.IsFalse(CardPresentationAuthority.HasConfig("monster.authority_demo"));
            Assert.IsFalse(CardPresentationAuthority.TryGetOwnedDescription("monster.authority_demo", out _));

            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "monster.authority_demo",
                kind = "Monster",
                description = "   ",
            });
            Assert.IsTrue(CardPresentationAuthority.HasConfig("monster.authority_demo"));
            Assert.IsFalse(CardPresentationAuthority.TryGetOwnedDescription("monster.authority_demo", out _));

            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "monster.authority_demo",
                kind = "Monster",
                description = "from-json",
                displayName = "JsonName",
            });
            Assert.IsTrue(CardPresentationAuthority.TryGetOwnedDescription("monster.authority_demo", out var desc));
            Assert.AreEqual("from-json", desc);
            Assert.IsTrue(CardPresentationAuthority.TryGetOwnedDisplayName("monster.authority_demo", out var name));
            Assert.AreEqual("JsonName", name);
        }

        [Test]
        public void Projector_WritesDisplayNameGoldStats_FromSchema2Json()
        {
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "monster.overlay_demo",
                kind = "Monster",
                displayName = "JsonMonster",
                gold = 99,
                stats = new CardPresentationStatsDto { hp = 20, attack = 7, armor = 4 },
            });

            var catalog = new GameContentCatalog();
            Assert.AreEqual(1, ContentJsonCatalogProjector.ApplyToCatalog(catalog));

            Assert.AreEqual("JsonMonster", catalog.Cards["monster.overlay_demo"].DisplayName);
            Assert.AreEqual(99, catalog.Cards["monster.overlay_demo"].KillGold);
            Assert.AreEqual(20, catalog.Cards["monster.overlay_demo"].Stats.MaxHp);
            Assert.AreEqual(20, catalog.Cards["monster.overlay_demo"].Stats.Hp);
            Assert.AreEqual(7, catalog.Cards["monster.overlay_demo"].Stats.Attack);
            Assert.AreEqual(4, catalog.Cards["monster.overlay_demo"].Stats.Armor);
        }

        [Test]
        public void PresentationJson_DoesNotMutateCatalog_WithoutProjector()
        {
            var catalog = new GameContentCatalog();
            catalog.AddCard(
                new CardContentDefinition("help.overlay_demo", "HelpSeed", CardKind.HelpCard)
                    .WithPrice(5)
                    .WithStats(1, 1, 0));

            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "help.overlay_demo",
                kind = "HelpCard",
                displayName = "JsonHelp",
                gold = 42,
                stats = new CardPresentationStatsDto { hp = 9, attack = 8, armor = 7 },
            });

            // #69：无 BusinessOverlay；未再跑投影时 Catalog 保持原值。
            Assert.AreEqual("HelpSeed", catalog.Cards["help.overlay_demo"].DisplayName);
            Assert.AreEqual(5, catalog.Cards["help.overlay_demo"].Price);
            Assert.AreEqual(1, catalog.Cards["help.overlay_demo"].Stats.MaxHp);
            Assert.AreEqual(1, catalog.Cards["help.overlay_demo"].Stats.Attack);
        }
    }
}
