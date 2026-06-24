using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 从 pixel_art_card_pack 数字条（144–153）解析 0–9 精灵，供滚筒数字复用。
    /// </summary>
    public static class TableNineDigitSpriteLibrary
    {
        private const string CardPackPath = "Assets/Arts/Images/Multiple/pixel_art_card_pack.png";

        public static Sprite[] LoadDigitsFromSameSheet(Sprite reference)
        {
            if (reference == null)
            {
                return null;
            }

#if UNITY_EDITOR
            string path = AssetDatabase.GetAssetPath(reference);
            if (string.IsNullOrEmpty(path))
            {
                path = CardPackPath;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var byName = new Dictionary<string, Sprite>();
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    byName[sprite.name] = sprite;
                }
            }

            var digits = new Sprite[10];
            for (var d = 0; d < 10; d++)
            {
                string key = $"pixel_art_card_pack_{144 + d}";
                if (!byName.TryGetValue(key, out digits[d]))
                {
                    return null;
                }
            }

            return digits;
#else
            return null;
#endif
        }

        public static Sprite[] LoadDefaultDigits()
        {
#if UNITY_EDITOR
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(CardPackPath);
            var byName = new Dictionary<string, Sprite>();
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    byName[sprite.name] = sprite;
                }
            }

            var digits = new Sprite[10];
            for (var d = 0; d < 10; d++)
            {
                string key = $"pixel_art_card_pack_{144 + d}";
                if (!byName.TryGetValue(key, out digits[d]))
                {
                    return null;
                }
            }

            return digits;
#else
            return null;
#endif
        }
    }
}
