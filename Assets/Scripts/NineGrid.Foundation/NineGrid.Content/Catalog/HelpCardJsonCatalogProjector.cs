using System;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Content
{
    /// <summary>
    /// 将帮助卡一卡一文件 JSON 投影进 <see cref="GameContentCatalog"/>（覆盖同 DefId）。
    /// 效果 DSL 本体仍可由 Luban 提供；本投影只写卡身份 / 数值 / 效果挂载。
    /// </summary>
    public static class HelpCardJsonCatalogProjector
    {
        public const int MinSchemaVersion = 2;

        public static int ApplyToCatalog(GameContentCatalog catalog)
        {
            if (catalog == null)
            {
                return 0;
            }

            var applied = 0;
            foreach (var contentId in CardPresentationConfigCatalog.AllContentIds)
            {
                if (!CardPresentationConfigCatalog.TryGet(contentId, out var dto) || dto == null)
                {
                    continue;
                }

                if (!TryProject(dto, out var card))
                {
                    continue;
                }

                catalog.AddCard(card);
                applied++;
            }

            return applied;
        }

        public static bool TryProject(CardPresentationConfigDto dto, out CardContentDefinition card)
        {
            card = null;
            if (dto == null || string.IsNullOrWhiteSpace(dto.contentId))
            {
                return false;
            }

            if (dto.schemaVersion < MinSchemaVersion)
            {
                return false;
            }

            if (!IsHelpCardKind(dto.kind))
            {
                return false;
            }

            var displayName = string.IsNullOrWhiteSpace(dto.displayName)
                ? dto.contentId.Trim()
                : dto.displayName.Trim();

            card = new CardContentDefinition(dto.contentId.Trim(), displayName, CardKind.HelpCard)
                .WithRarity(ParseRarity(dto.rarity))
                .WithPrice(Math.Max(0, dto.gold));

            if (!string.IsNullOrWhiteSpace(dto.deckId))
            {
                card.InDeck(dto.deckId.Trim());
            }

            var stats = dto.stats;
            if (stats != null)
            {
                card.WithStats(
                    Math.Max(0, stats.hp),
                    Math.Max(0, stats.attack),
                    Math.Max(0, stats.armor));
            }

            var projected = card;
            AddTokens(dto.tags, value => projected.AddTag(value));
            AddTokens(dto.effectIds, value => projected.AddEffect(value));
            return true;
        }

        private static bool IsHelpCardKind(string kind)
        {
            return string.Equals(kind, nameof(CardKind.HelpCard), StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "Help", StringComparison.OrdinalIgnoreCase);
        }

        private static ContentRarity ParseRarity(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return ContentRarity.None;
            }

            return Enum.TryParse(raw.Trim(), ignoreCase: true, out ContentRarity rarity)
                ? rarity
                : ContentRarity.None;
        }

        private static void AddTokens(string[] values, Action<string> add)
        {
            if (values == null || add == null)
            {
                return;
            }

            for (var i = 0; i < values.Length; i++)
            {
                var value = values[i];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    add(value.Trim());
                }
            }
        }
    }
}
