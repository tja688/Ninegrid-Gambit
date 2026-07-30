using System;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Content
{
    /// <summary>
    /// 将遭遇表 + 卡面 <c>deckId</c> 归属装配进 <see cref="GameContentCatalog.MonsterDecks"/>。
    /// 须在 <see cref="ContentJsonCatalogProjector"/> 投影卡面之后调用。
    /// </summary>
    public static class MonsterDeckCatalogBuilder
    {
        public static int ApplyToCatalog(GameContentCatalog catalog)
        {
            if (catalog == null)
            {
                return 0;
            }

            MonsterDeckTableCatalog.EnsureLoaded();
            var applied = 0;
            for (var i = 0; i < MonsterDeckTableCatalog.Rows.Count; i++)
            {
                var row = MonsterDeckTableCatalog.Rows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.deck_id))
                {
                    continue;
                }

                var deckId = row.deck_id.Trim();
                var kind = ParseEnum(row.deck_kind, MonsterDeckKind.Unknown);
                var displayName = !string.IsNullOrWhiteSpace(row.display_name)
                    ? row.display_name.Trim()
                    : ResolvePresentationDisplayName(deckId);

                var deck = new MonsterDeckDefinition(deckId, displayName, kind);
                AddMembersFromCardDeckIds(catalog, deckId, deck);
                catalog.AddMonsterDeck(deck);
                applied++;
            }

            return applied;
        }

        private static void AddMembersFromCardDeckIds(
            GameContentCatalog catalog,
            string deckId,
            MonsterDeckDefinition deck)
        {
            foreach (var pair in catalog.Cards)
            {
                var card = pair.Value;
                if (card == null
                    || card.Kind != CardKind.Monster
                    || string.IsNullOrWhiteSpace(card.DeckId)
                    || !string.Equals(card.DeckId, deckId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                deck.AddMonster(card.DefId);
            }
        }

        private static string ResolvePresentationDisplayName(string deckId)
        {
            if (CardPresentationConfigCatalog.TryGet(deckId, out var dto)
                && dto != null
                && !string.IsNullOrWhiteSpace(dto.displayName))
            {
                return dto.displayName.Trim();
            }

            return deckId;
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            if (string.IsNullOrEmpty(value))
            {
                return fallback;
            }

            try
            {
                return (T)Enum.Parse(typeof(T), value, true);
            }
            catch (ArgumentException)
            {
                return fallback;
            }
        }
    }
}
