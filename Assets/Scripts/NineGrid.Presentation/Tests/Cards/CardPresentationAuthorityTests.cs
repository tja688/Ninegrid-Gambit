using NUnit.Framework;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// CardPresentation 重叠字段单真相：BusinessOverlay + Authority 契约。
    /// </summary>
    public sealed class CardPresentationAuthorityTests
    {
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
        public void Overlay_AppliesDisplayNameGoldStats_WhenPositive()
        {
            var catalog = new GameContentCatalog();
            var monster = new CardContentDefinition("monster.overlay_demo", "LubanName", CardKind.Monster)
                .WithPrice(1)
                .WithStats(10, 2, 1);
            monster.KillGold = 3;
            catalog.AddCard(monster);

            var help = new CardContentDefinition("help.overlay_demo", "HelpLuban", CardKind.HelpCard)
                .WithPrice(5)
                .WithStats(1, 1, 0);
            catalog.AddCard(help);

            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "monster.overlay_demo",
                kind = "Monster",
                displayName = "JsonMonster",
                gold = 99,
                stats = new CardPresentationStatsDto { hp = 20, attack = 7, armor = 4 },
            });
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "help.overlay_demo",
                kind = "HelpCard",
                displayName = "JsonHelp",
                gold = 42,
                stats = new CardPresentationStatsDto { hp = 0, attack = 0, armor = 0 },
            });

            CardPresentationBusinessOverlay.ApplyToCatalog(catalog);

            Assert.AreEqual("JsonMonster", catalog.Cards["monster.overlay_demo"].DisplayName);
            Assert.AreEqual(99, catalog.Cards["monster.overlay_demo"].KillGold);
            Assert.AreEqual(20, catalog.Cards["monster.overlay_demo"].Stats.MaxHp);
            Assert.AreEqual(20, catalog.Cards["monster.overlay_demo"].Stats.Hp);
            Assert.AreEqual(7, catalog.Cards["monster.overlay_demo"].Stats.Attack);
            Assert.AreEqual(4, catalog.Cards["monster.overlay_demo"].Stats.Armor);

            Assert.AreEqual("JsonHelp", catalog.Cards["help.overlay_demo"].DisplayName);
            Assert.AreEqual(42, catalog.Cards["help.overlay_demo"].Price);
            Assert.AreEqual(1, catalog.Cards["help.overlay_demo"].Stats.MaxHp);
            Assert.AreEqual(1, catalog.Cards["help.overlay_demo"].Stats.Attack);
        }

        [Test]
        public void Overlay_EmptyOrZero_DoesNotClobberLuban()
        {
            var catalog = new GameContentCatalog();
            var card = new CardContentDefinition("monster.keep_luban", "KeepName", CardKind.Monster)
                .WithStats(8, 3, 2);
            card.KillGold = 11;
            catalog.AddCard(card);

            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "monster.keep_luban",
                kind = "Monster",
                displayName = "  ",
                gold = 0,
                stats = new CardPresentationStatsDto { hp = 0, attack = 0, armor = 0 },
            });

            CardPresentationBusinessOverlay.ApplyToCatalog(catalog);

            Assert.AreEqual("KeepName", catalog.Cards["monster.keep_luban"].DisplayName);
            Assert.AreEqual(11, catalog.Cards["monster.keep_luban"].KillGold);
            Assert.AreEqual(8, catalog.Cards["monster.keep_luban"].Stats.MaxHp);
            Assert.AreEqual(3, catalog.Cards["monster.keep_luban"].Stats.Attack);
            Assert.AreEqual(2, catalog.Cards["monster.keep_luban"].Stats.Armor);
        }
    }
}
