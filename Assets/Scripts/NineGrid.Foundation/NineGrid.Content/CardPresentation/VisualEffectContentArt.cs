using System;

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 像素特效精灵表的 Resources 根（与 <see cref="CardPresentationContentArt"/> 同属 ContentArt，ADR-0008）。
    /// </summary>
    public static class VisualEffectContentArt
    {
        public const string LegacyArtsEffectsFolder = "Assets/Arts/Images/Multiple/Effects";
        public const string RootAssetFolder = "Assets/Resources/ContentArt/Multiple/Effects";
        public const string ResourcesRelativeRoot = "ContentArt/Multiple/Effects";

        public static bool IsUnderEffectsRoot(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            var normalized = assetPath.Replace('\\', '/').Trim();
            return normalized.StartsWith(RootAssetFolder + "/", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalized, RootAssetFolder, StringComparison.OrdinalIgnoreCase);
        }

        public static string RewriteLegacyEffectsPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return assetPath ?? string.Empty;
            }

            var normalized = assetPath.Replace('\\', '/').Trim();
            if (IsUnderEffectsRoot(normalized))
            {
                return normalized;
            }

            if (normalized.StartsWith(LegacyArtsEffectsFolder + "/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, LegacyArtsEffectsFolder, StringComparison.OrdinalIgnoreCase))
            {
                var tail = normalized.Length > LegacyArtsEffectsFolder.Length
                    ? normalized.Substring(LegacyArtsEffectsFolder.Length).TrimStart('/')
                    : string.Empty;
                return string.IsNullOrEmpty(tail)
                    ? RootAssetFolder
                    : RootAssetFolder + "/" + tail;
            }

            return normalized;
        }
    }
}
