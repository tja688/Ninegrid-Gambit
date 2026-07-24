using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 按 Unity 资产路径加载 Sprite。
    /// 首版：Editor / Play Mode in Editor 走 AssetDatabase；
    /// 路径含 /Resources/ 时尝试 Resources.Load；正式包体加载后续再接 Addressables。
    /// </summary>
    public static class CardPresentationSpritePath
    {
        public static Sprite LoadSprite(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var normalized = assetPath.Replace('\\', '/').Trim();

#if UNITY_EDITOR
            var editorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(normalized);
            if (editorSprite != null)
            {
                return editorSprite;
            }
#endif

            if (TryLoadFromResources(normalized, out var resourcesSprite))
            {
                return resourcesSprite;
            }

            return null;
        }

        private static bool TryLoadFromResources(string assetPath, out Sprite sprite)
        {
            sprite = null;
            const string marker = "/Resources/";
            var index = assetPath.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var relative = assetPath.Substring(index + marker.Length);
            if (relative.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
            {
                relative = relative.Substring(0, relative.LastIndexOf('.'));
            }

            sprite = Resources.Load<Sprite>(relative);
            return sprite != null;
        }
    }
}
