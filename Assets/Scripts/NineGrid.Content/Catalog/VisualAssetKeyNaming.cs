namespace NineGrid.Content
{
    public enum VisualAssetSlot
    {
        Icon,
        Face
    }

    public static class VisualAssetKeyNaming
    {
        public const string ResourcesRoot = "Sprites/Content";

        public static string FromConvention(ContentVisualKind kind, VisualAssetSlot slot, string contentId)
        {
            if (string.IsNullOrEmpty(contentId) || kind == ContentVisualKind.Unknown)
            {
                return string.Empty;
            }

            return "content/" + slot.ToString().ToLowerInvariant() + "/" + kind + "/" + contentId;
        }

        public static string FromConventionFaceDeck(string deckId)
        {
            if (string.IsNullOrEmpty(deckId))
            {
                return string.Empty;
            }

            return "content/face/MonsterDeck/" + deckId;
        }

        public static string MissingSpriteKey()
        {
            return "content/fallback/missing_card";
        }

        public static string ToResourcesPath(string assetKey)
        {
            if (string.IsNullOrEmpty(assetKey))
            {
                return string.Empty;
            }

            return ResourcesRoot + "/" + assetKey;
        }
    }
}
