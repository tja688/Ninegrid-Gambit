using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 仅保留 legacy Assets/...#spriteName 解码，供 Catalog SO 一次性迁移使用。
    /// </summary>
    public static class ContentVisualSpriteKeyCodec
    {
        public static bool TryDecodeLegacy(string key, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            var separatorIndex = key.IndexOf('#');
            if (separatorIndex > 0 && separatorIndex < key.Length - 1)
            {
                var assetPath = key.Substring(0, separatorIndex);
                var spriteName = key.Substring(separatorIndex + 1);
                return TryLoadSubSprite(assetPath, spriteName, out sprite);
            }

            return TryFindSpriteByName(key, out sprite);
        }

        public static bool TryDecode(string key, out Sprite sprite)
        {
            return TryDecodeLegacy(key, out sprite);
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
}
