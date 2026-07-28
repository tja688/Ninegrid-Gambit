namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// CardPresentation JSON 权威契约（ADR-0008 / #69）：一卡一文件为内容唯一权威。
    /// <para>
    /// schema≥2 经 <c>ContentJsonCatalogProjector</c> 写入 Catalog；无 BusinessOverlay。
    /// 表现层读 sprites / animations / description / displayName；数值只经 Catalog 造卡。
    /// </para>
    /// </summary>
    public static class CardPresentationAuthority
    {
        /// <summary>该 contentId 已有 CardPresentation JSON 文件。</summary>
        public static bool HasConfig(string contentId)
        {
            return CardPresentationConfigCatalog.TryGet(contentId, out var dto) && dto != null;
        }

        /// <summary>JSON 拥有有效描述（非空白）。</summary>
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
