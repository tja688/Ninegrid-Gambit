using System;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public static class ContentVisualSpriteKeyCodec
    {
        public const char SubSpriteSeparator = '#';

        public static string Encode(Sprite sprite)
        {
            if (sprite == null)
            {
                return string.Empty;
            }

            var assetPath = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(assetPath))
            {
                return string.Empty;
            }

            return assetPath + SubSpriteSeparator + sprite.name;
        }

        public static bool TryDecode(string key, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            var separatorIndex = key.IndexOf(SubSpriteSeparator);
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
}
