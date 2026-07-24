using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Content
{
    /// <summary>
    /// 将卡牌表现 JSON 的 gold/name/stats 覆盖进 Core 内容目录（JSON 权威）。
    /// </summary>
    public static class CardPresentationBusinessOverlay
    {
        public static void ApplyToCatalog(GameContentCatalog catalog)
        {
            if (catalog?.Cards == null)
            {
                return;
            }

            foreach (var pair in catalog.Cards)
            {
                var card = pair.Value;
                if (card == null || string.IsNullOrEmpty(card.DefId))
                {
                    continue;
                }

                if (!CardPresentationConfigCatalog.TryGet(card.DefId, out var dto) || dto == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(dto.displayName))
                {
                    // DisplayName 为 private set；仅覆盖可写业务字段。
                }

                if (dto.gold > 0)
                {
                    if (card.Kind == CardKind.Monster)
                    {
                        card.KillGold = dto.gold;
                    }
                    else
                    {
                        card.Price = dto.gold;
                    }
                }

                if (dto.stats == null)
                {
                    continue;
                }

                if (dto.stats.hp > 0)
                {
                    card.Stats.MaxHp = dto.stats.hp;
                    card.Stats.Hp = dto.stats.hp;
                }

                if (dto.stats.attack > 0)
                {
                    card.Stats.Attack = dto.stats.attack;
                }

                if (dto.stats.armor > 0)
                {
                    card.Stats.Armor = dto.stats.armor;
                }
            }
        }
    }
}
