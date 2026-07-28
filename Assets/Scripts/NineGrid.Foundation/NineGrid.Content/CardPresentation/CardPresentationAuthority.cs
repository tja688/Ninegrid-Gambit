namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// CardPresentation JSON 与 Luban/SO 重叠字段的权威契约（#68：生产内容 schema≥2 一卡一文件进 Catalog）。
    /// <para>
    /// 有 CardPresentation JSON 且字段有效时，下列字段以 JSON 为唯一真相；无 JSON / 字段空时才允许旧源 fallback：
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>displayName</c> — JSON；schema≥2 经投影写入 Catalog，已投影卡种不再 Overlay</description></item>
    /// <item><description><c>description</c> — JSON；旧：TbContentVisual.description</description></item>
    /// <item><description><c>gold</c> → KillGold/Price — JSON；schema≥2 投影，未投影卡种 Overlay</description></item>
    /// <item><description><c>stats.hp/attack/armor/recovery</c> — JSON；schema≥2 投影；未投影卡种 Overlay（recovery 旧属 Luban）</description></item>
    /// <item><description>sprites / animations / mainVisual — JSON；旧：ContentVisual SO（仅缺 JSON）</description></item>
    /// </list>
    /// <para>
    /// schema≥2：rarity / tags / effectIds / skillIds / 牌组与房间结构字段亦属 JSON，经
    /// <c>ContentJsonCatalogProjector</c> 投影；效果 DSL 本体与奖励池/经济/节点规则仍可 Luban（#69）。
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
