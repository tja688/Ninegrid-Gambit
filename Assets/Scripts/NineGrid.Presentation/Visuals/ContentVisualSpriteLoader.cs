using System.Collections.Generic;
using NineGrid.Content;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 按 visual_id 经注册表解析并加载 Sprite。
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
#if UNITY_EDITOR
            if (sEditorCache.TryGetValue(cacheKey, out sprite) && sprite != null)
            {
                return true;
            }

            if (TryLoadEditor(visualId, conventionAssetKey, out sprite))
            {
                sEditorCache[cacheKey] = sprite;
                return true;
            }
#endif
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

#if UNITY_EDITOR
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

            VisualAssetDefinition definition;
            if (!sCatalog.TryGet(visualId, out definition)
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
#endif
    }

#if UNITY_EDITOR
    internal static class ContentVisualSpriteKeyCodec
    {
        public static bool TryDecodeLegacy(string key, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            var separatorIndex = key.IndexOf(ContentVisualSpriteLoader.SubSpriteSeparator);
            if (separatorIndex > 0 && separatorIndex < key.Length - 1)
            {
                var assetPath = key.Substring(0, separatorIndex);
                var spriteName = key.Substring(separatorIndex + 1);
                return TryLoadSubSprite(assetPath, spriteName, out sprite);
            }

            return TryFindSpriteByName(key, out sprite);
        }

        private static bool TryLoadSubSprite(string assetPath, string spriteName, out Sprite sprite)
        {
            sprite = null;
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (var i = 0; i < assets.Length; i++)
            {
                var candidate = assets[i] as Sprite;
                if (candidate != null && candidate.name == spriteName)
                {
                    sprite = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindSpriteByName(string spriteName, out Sprite sprite)
        {
            sprite = null;
            var guids = AssetDatabase.FindAssets(spriteName + " t:Sprite");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                for (var j = 0; j < assets.Length; j++)
                {
                    var candidate = assets[j] as Sprite;
                    if (candidate != null && candidate.name == spriteName)
                    {
                        sprite = candidate;
                        return true;
                    }
                }
            }

            return false;
        }
    }
#endif
}
