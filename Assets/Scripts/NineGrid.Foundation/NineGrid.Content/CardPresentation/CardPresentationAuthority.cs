namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// CardPresentation JSON 与 Luban/SO 重叠字段的权威契约（不拆 Luban；仅消双重真相）。
    /// <para>
    /// 有 CardPresentation JSON 且字段有效时，下列字段以 JSON 为唯一真相；无 JSON / 字段空时才允许旧源 fallback：
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>displayName</c> — JSON；旧：TbCard.display_name / ContentVisual resolver</description></item>
    /// <item><description><c>description</c> — JSON；旧：TbContentVisual.description</description></item>
    /// <item><description><c>gold</c> → KillGold/Price — JSON；旧：TbCard.price / Economy 默认击杀金</description></item>
    /// <item><description><c>stats.hp/attack/armor</c> — JSON；旧：TbCard max_hp/attack/armor（recovery 仍 Luban）</description></item>
    /// <item><description>sprites / animations / mainVisual — JSON；旧：ContentVisual SO（仅缺 JSON）</description></item>
    /// </list>
    /// <para>
    /// 玩法身份仍属 Luban：kind/deckId（JSON 仅镜像）、effect_ids、skill_ids、rarity、elite/boss/reserve、
    /// tags、effects DSL、rewards/rooms/economy/decks、<c>TbCardFrameStyle</c>。
    /// </para>
    /// </summary>
    public static class CardPresentationAuthority
    {
        /// <summary>该 contentId 已有 CardPresentation JSON 文件（重叠表现字段归 JSON）。</summary>
        public static bool HasConfig(string contentId)
        {
            return CardPresentationConfigCatalog.TryGet(contentId, out var dto) && dto != null;
        }

        /// <summary>JSON 拥有有效描述（非空白）；调用方应优先于 ContentVisual。</summary>
        public static bool TryGetOwnedDescription(string contentId, out string description)
        {
            description = string.Empty;
            if (!CardPresentationConfigCatalog.TryGet(contentId, out var dto) || dto == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(dto.description))
            {
                return false;
            }

            description = dto.description;
            return true;
        }

        /// <summary>JSON 拥有有效显示名。</summary>
        public static bool TryGetOwnedDisplayName(string contentId, out string displayName)
        {
            displayName = string.Empty;
            if (!CardPresentationConfigCatalog.TryGet(contentId, out var dto) || dto == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(dto.displayName))
            {
                return false;
            }

            displayName = dto.displayName;
            return true;
        }
    }
}
