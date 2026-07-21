using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Cards.Slots
{
    /// <summary>
    /// 槽代号 → 卡面模板节点名候选（按序查找第一个 SpriteRenderer）。
    /// 用于编辑器终态预览灌图与模板默认 Sprite 抽取；正式 Binder 属后续切片。
    /// </summary>
    public static class CardFaceSlotNodeMap
    {
        private static readonly Dictionary<string, string[]> Candidates =
            new Dictionary<string, string[]>
            {
                { CardFaceSlotCodes.MainIcon, new[] { "主图标", "核心图标", "MainIcon", "Main Icon" } },
                { CardFaceSlotCodes.FaceBackground, new[] { "背景", "Card Background", "CardBackground" } },
                { CardFaceSlotCodes.BackBorder, new[] { "边框", "Card_Border_rectangle_bronze", "Card_Border_rectangle_dark" } },
                { CardFaceSlotCodes.BackShirt, new[] { "CardShirts_8", "CardShirts", "背纹" } },
                { CardFaceSlotCodes.BackLogo, new[] { "logo", "Logo" } },
            };

        public static bool TryFindRenderer(Transform faceRoot, string slotCode, out SpriteRenderer renderer)
        {
            renderer = null;
            if (faceRoot == null || string.IsNullOrEmpty(slotCode))
            {
                return false;
            }

            string[] names;
            if (!Candidates.TryGetValue(slotCode, out names))
            {
                return false;
            }

            for (var i = 0; i < names.Length; i++)
            {
                var node = FindDeep(faceRoot, names[i]);
                if (node == null)
                {
                    continue;
                }

                renderer = node.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    return true;
                }
            }

            return false;
        }

        public static Dictionary<string, Sprite> CaptureTemplateDefaults(Transform faceRoot)
        {
            var map = new Dictionary<string, Sprite>();
            Capture(faceRoot, CardFaceSlotCodes.MainIcon, map);
            Capture(faceRoot, CardFaceSlotCodes.FaceBackground, map);
            Capture(faceRoot, CardFaceSlotCodes.BackBorder, map);
            Capture(faceRoot, CardFaceSlotCodes.BackShirt, map);
            Capture(faceRoot, CardFaceSlotCodes.BackLogo, map);
            return map;
        }

        public static void ApplyResolvedSprites(Transform faceRoot, IReadOnlyDictionary<string, Sprite> resolvedByCode)
        {
            if (faceRoot == null || resolvedByCode == null)
            {
                return;
            }

            foreach (var pair in resolvedByCode)
            {
                SpriteRenderer renderer;
                if (!TryFindRenderer(faceRoot, pair.Key, out renderer) || pair.Value == null)
                {
                    continue;
                }

                renderer.sprite = pair.Value;
            }
        }

        private static void Capture(Transform faceRoot, string code, IDictionary<string, Sprite> map)
        {
            SpriteRenderer renderer;
            if (TryFindRenderer(faceRoot, code, out renderer) && renderer.sprite != null)
            {
                map[code] = renderer.sprite;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
