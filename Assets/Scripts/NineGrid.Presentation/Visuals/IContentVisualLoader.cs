using System.Collections.Generic;
using NineGrid.Content;
using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
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
                VisualAssetDefinition definition;
                if (mCatalog != null && mCatalog.TryGet(visualId, out definition))
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
