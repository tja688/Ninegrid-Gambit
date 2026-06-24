namespace NineGrid.Content
{
    public sealed class VisualAssetDefinition
    {
        public VisualAssetDefinition(
            string visualId,
            VisualAssetKind kind,
            string assetKey,
            string fallbackId)
        {
            VisualId = visualId ?? string.Empty;
            Kind = kind;
            AssetKey = assetKey ?? string.Empty;
            FallbackId = fallbackId ?? string.Empty;
        }

        public string VisualId { get; }
        public VisualAssetKind Kind { get; }
        public string AssetKey { get; }
        public string FallbackId { get; }
    }
}
