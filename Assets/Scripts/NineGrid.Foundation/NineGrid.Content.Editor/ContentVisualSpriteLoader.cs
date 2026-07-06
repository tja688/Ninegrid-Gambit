using System.Collections.Generic;
using NineGrid.Content;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 编辑器内按 visual_id 经注册表解析并加载 Sprite（内容视觉工具专用）。
    /// </summary>
    public static class ContentVisualSpriteLoader
    {
        public const char SubSpriteSeparator = '#';

        private static VisualAssetCatalog sCatalog;
        private static IContentVisualLoader sLoader;
        private static readonly Dictionary<string, Sprite> sEditorCache = new Dictionary<string, Sprite>();

        public static void Configure(VisualAssetCatalog catalog)
        {
            sCatalog = catalog;
            sLoader = catalog == null ? null : new ResourcesVisualLoader(catalog);
            ClearCache();
        }

        public static bool TryLoad(string visualId, string conventionAssetKey, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(visualId) && string.IsNullOrEmpty(conventionAssetKey))
            {
                return false;
            }

            var cacheKey = (visualId ?? string.Empty) + "|" + (conventionAssetKey ?? string.Empty);
            if (sEditorCache.TryGetValue(cacheKey, out sprite) && sprite != null)
            {
                return true;
            }

            if (TryLoadEditor(visualId, conventionAssetKey, out sprite))
            {
                sEditorCache[cacheKey] = sprite;
                return true;
            }

            if (sLoader != null
                && sLoader.TryLoadSprite(visualId, conventionAssetKey, out sprite)
                && sprite != null)
            {
                return true;
            }

            return false;
        }

        public static void ClearCache()
        {
            sEditorCache.Clear();
        }

        private static bool TryLoadEditor(string visualId, string conventionAssetKey, out Sprite sprite)
        {
            sprite = null;
            if (VisualIdNaming.IsLegacyPathKey(visualId))
            {
                return ContentVisualSpriteKeyCodec.TryDecodeLegacy(visualId, out sprite);
            }

            var loader = sLoader ?? new ResourcesVisualLoader(sCatalog);
            if (loader.TryLoadSprite(visualId, conventionAssetKey, out sprite) && sprite != null)
            {
                return true;
            }

            if (TryLoadCatalogLegacyAssetKey(visualId, out sprite))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(conventionAssetKey))
            {
                if (VisualIdNaming.IsLegacyPathKey(conventionAssetKey)
                    && ContentVisualSpriteKeyCodec.TryDecodeLegacy(conventionAssetKey, out sprite))
                {
                    return true;
                }

                return TryLoadAssetPathInEditor(conventionAssetKey, out sprite);
            }

            return false;
        }

        private static bool TryLoadCatalogLegacyAssetKey(string visualId, out Sprite sprite)
        {
            sprite = null;
            if (sCatalog == null || string.IsNullOrEmpty(visualId))
            {
                return false;
            }

            if (!sCatalog.TryGet(visualId, out VisualAssetDefinition definition)
                || string.IsNullOrEmpty(definition.AssetKey)
                || !VisualIdNaming.IsLegacyPathKey(definition.AssetKey))
            {
                return false;
            }

            return ContentVisualSpriteKeyCodec.TryDecodeLegacy(definition.AssetKey, out sprite);
        }

        private static bool TryLoadAssetPathInEditor(string assetKey, out Sprite sprite)
        {
            sprite = null;
            var resourcesPath = VisualAssetKeyNaming.ToResourcesPath(assetKey);
            const string resourcesToken = "Resources/";
            var index = resourcesPath.IndexOf(resourcesToken);
            if (index < 0)
            {
                return false;
            }

            var relative = resourcesPath.Substring(index + resourcesToken.Length);
            var guids = AssetDatabase.FindAssets(relative + " t:Sprite");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var loaded = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (loaded != null)
                {
                    sprite = loaded;
                    return true;
                }
            }

            return false;
        }
    }

    public interface IContentVisualLoader
    {
        bool TryLoadSprite(string visualId, string conventionAssetKey, out Sprite sprite);
    }

    public sealed class ResourcesVisualLoader : IContentVisualLoader
    {
        private readonly VisualAssetCatalog mCatalog;
        private readonly Dictionary<string, Sprite> mCache = new Dictionary<string, Sprite>();

        public ResourcesVisualLoader(VisualAssetCatalog catalog)
        {
            mCatalog = catalog;
        }

        public bool TryLoadSprite(string visualId, string conventionAssetKey, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(visualId))
            {
                return TryLoadAssetKey(conventionAssetKey, out sprite);
            }

            var cacheKey = visualId + "|" + (conventionAssetKey ?? string.Empty);
            if (mCache.TryGetValue(cacheKey, out sprite) && sprite != null)
            {
                return true;
            }

            var visited = new HashSet<string>();
            if (TryLoadWithFallback(visualId, conventionAssetKey, visited, out sprite))
            {
                mCache[cacheKey] = sprite;
                return true;
            }

            return false;
        }

        private bool TryLoadWithFallback(
            string visualId,
            string conventionAssetKey,
            HashSet<string> visited,
            out Sprite sprite)
        {
            sprite = null;
            if (!string.IsNullOrEmpty(visualId))
            {
                if (visited.Contains(visualId))
                {
                    return false;
                }

                visited.Add(visualId);
                if (mCatalog != null && mCatalog.TryGet(visualId, out VisualAssetDefinition definition))
                {
                    if (TryLoadAssetKey(definition.AssetKey, out sprite))
                    {
                        return true;
                    }

                    if (!string.IsNullOrEmpty(definition.FallbackId))
                    {
                        return TryLoadWithFallback(definition.FallbackId, string.Empty, visited, out sprite);
                    }
                }
            }

            if (TryLoadAssetKey(conventionAssetKey, out sprite))
            {
                return true;
            }

            if (mCatalog != null)
            {
                return TryLoadWithFallback(VisualIdNaming.MissingSpriteId, string.Empty, visited, out sprite);
            }

            return false;
        }

        private static bool TryLoadAssetKey(string assetKey, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(assetKey))
            {
                return false;
            }

            var resourcesPath = VisualAssetKeyNaming.ToResourcesPath(assetKey);
            sprite = Resources.Load<Sprite>(resourcesPath);
            return sprite != null;
        }
    }
}
