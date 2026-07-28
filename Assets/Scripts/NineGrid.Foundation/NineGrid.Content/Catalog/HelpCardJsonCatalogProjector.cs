using System;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Content
{
    /// <summary>
    /// 帮助卡投影入口（#67）；实现委托 <see cref="ContentJsonCatalogProjector"/>（#68 起含怪物/遗物/技能等）。
    /// </summary>
    public static class HelpCardJsonCatalogProjector
    {
        public const int MinSchemaVersion = ContentJsonCatalogProjector.MinSchemaVersion;

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
            if (!ContentJsonCatalogProjector.TryProjectCard(dto, out card))
            {
                return false;
            }

            if (card.Kind != CardKind.HelpCard)
            {
                card = null;
                return false;
            }

            return true;
        }
    }
}
