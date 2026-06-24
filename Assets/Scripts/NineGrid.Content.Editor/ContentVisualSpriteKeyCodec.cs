using NineGrid.Content;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public static class ContentVisualSpriteKeyCodec
    {
        public static string EncodeIcon(string contentId, Sprite sprite)
        {
            return Encode(ContentVisualKeySlot.Icon, contentId, ContentVisualKind.Unknown, sprite);
        }

        public static string EncodeFace(string contentId, ContentVisualKind kind, Sprite sprite)
        {
            return Encode(ContentVisualKeySlot.Face, contentId, kind, sprite);
        }

        public static string Encode(ContentVisualKeySlot slot, string contentId, ContentVisualKind kind, Sprite sprite)
        {
            if (sprite == null || string.IsNullOrEmpty(contentId))
            {
                return string.Empty;
            }

            return slot == ContentVisualKeySlot.Icon
                ? VisualIdNaming.ForIcon(contentId)
                : VisualIdNaming.ForFace(contentId);
        }

        public static VisualAssetXlsxRow BuildAssetUpsert(
            ContentVisualKeySlot slot,
            string contentId,
            ContentVisualKind kind,
            Sprite sprite)
        {
            if (sprite == null || string.IsNullOrEmpty(contentId))
            {
                return null;
            }

            var visualId = Encode(slot, contentId, kind, sprite);
            return new VisualAssetXlsxRow
            {
                VisualId = visualId,
                Kind = "sprite",
                AssetKey = ResolveAssetKey(slot, contentId, kind, sprite),
                FallbackId = string.Empty
            };
        }

        public static bool TryDecode(string key, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (VisualIdNaming.IsLegacyPathKey(key))
            {
                return TryDecodeLegacy(key, out sprite);
            }

            if (VisualIdNaming.IsVisualId(key))
            {
                return NineGrid.Presentation.Visuals.ContentVisualSpriteLoader.TryLoad(key, string.Empty, out sprite);
            }

            return TryFindSpriteByName(key, out sprite);
        }

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

        private static string ResolveAssetKey(
            ContentVisualKeySlot slot,
            string contentId,
            ContentVisualKind kind,
            Sprite sprite)
        {
            var editorAssetKey = TryBuildEditorAssetKey(sprite);
            if (!string.IsNullOrEmpty(editorAssetKey))
            {
                return editorAssetKey;
            }

            if (slot == ContentVisualKeySlot.Icon)
            {
                return VisualAssetKeyNaming.FromConvention(kind, VisualAssetSlot.Icon, contentId);
            }

            return VisualAssetKeyNaming.FromConvention(kind, VisualAssetSlot.Face, contentId);
        }

        private static string TryBuildEditorAssetKey(Sprite sprite)
        {
            if (sprite == null)
            {
                return string.Empty;
            }

            var path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            return path + "#" + sprite.name;
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
