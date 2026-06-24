using System.Collections.Generic;
using NineGrid.Content;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 运行时按视觉表 key 解析 Sprite（编辑器走 AssetDatabase，发布走 Resources 约定路径）。
    /// </summary>
    public static class ContentVisualSpriteLoader
    {
        public const char SubSpriteSeparator = '#';

        private static readonly Dictionary<string, Sprite> Cache = new();

        public static bool TryLoad(string key, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (Cache.TryGetValue(key, out sprite) && sprite != null)
            {
                return true;
            }

#if UNITY_EDITOR
            if (TryLoadFromEncodedAssetPath(key, out sprite)
                || TryFindSpriteByName(key, out sprite))
            {
                Cache[key] = sprite;
                return true;
            }
#else
            if (TryLoadFromResources(key, out sprite))
            {
                Cache[key] = sprite;
                return true;
            }
#endif

            return false;
        }

        public static void ClearCache()
        {
            Cache.Clear();
        }

#if UNITY_EDITOR
        private static bool TryLoadFromEncodedAssetPath(string key, out Sprite sprite)
        {
            sprite = null;
            var separatorIndex = key.IndexOf(SubSpriteSeparator);
            if (separatorIndex <= 0 || separatorIndex >= key.Length - 1)
            {
                return false;
            }

            var assetPath = key.Substring(0, separatorIndex);
            var spriteName = key.Substring(separatorIndex + 1);
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
#else
        private static bool TryLoadFromResources(string key, out Sprite sprite)
        {
            sprite = null;
            var separatorIndex = key.IndexOf(SubSpriteSeparator);
            if (separatorIndex > 0 && separatorIndex < key.Length - 1)
            {
                var assetPath = key.Substring(0, separatorIndex);
                var spriteName = key.Substring(separatorIndex + 1);
                var resourcesPath = ToResourcesPath(assetPath);
                if (!string.IsNullOrEmpty(resourcesPath))
                {
                    var sprites = Resources.LoadAll<Sprite>(resourcesPath);
                    for (var i = 0; i < sprites.Length; i++)
                    {
                        if (sprites[i] != null && sprites[i].name == spriteName)
                        {
                            sprite = sprites[i];
                            return true;
                        }
                    }
                }
            }

            string[] candidates =
            {
                key,
                ContentVisualResolver.IconConventionRoot + "/" + key,
            };

            for (var i = 0; i < candidates.Length; i++)
            {
                sprite = Resources.Load<Sprite>(candidates[i]);
                if (sprite != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ToResourcesPath(string assetPath)
        {
            const string resourcesToken = "/Resources/";
            int index = assetPath.IndexOf(resourcesToken);
            if (index < 0)
            {
                return string.Empty;
            }

            string path = assetPath.Substring(index + resourcesToken.Length);
            int extension = path.LastIndexOf('.');
            if (extension > 0)
            {
                path = path.Substring(0, extension);
            }

            return path;
        }
#endif
    }
}
