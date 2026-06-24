namespace NineGrid.Content
{
    public static class VisualIdNaming
    {
        public const string MissingSpriteId = "visual.missing.sprite";

        public static string ForIcon(string contentId)
        {
            return Build("icon", contentId);
        }

        public static string ForFace(string contentId)
        {
            return Build("face", contentId);
        }

        public static bool IsVisualId(string key)
        {
            return !string.IsNullOrEmpty(key) && key.StartsWith("visual.");
        }

        public static bool IsLegacyPathKey(string key)
        {
            return !string.IsNullOrEmpty(key)
                && (key.StartsWith("Assets/") || key.Contains("#"));
        }

        private static string Build(string slot, string contentId)
        {
            if (string.IsNullOrEmpty(contentId))
            {
                return string.Empty;
            }

            return "visual." + slot + "." + contentId;
        }
    }
}
