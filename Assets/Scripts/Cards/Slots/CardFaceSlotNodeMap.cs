using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NineGrid.Cards.Slots
{
    /// <summary>
    /// 槽代号 → 卡面模板节点名候选（公开装配契约）。
    /// 用于编辑器终态预览、模板默认抽取，以及运行时 Binder 节点解析。
    /// </summary>
    public static class CardFaceSlotNodeMap
    {
        private static readonly Dictionary<string, string[]> SpriteCandidates =
            new Dictionary<string, string[]>
            {
                {
                    CardFaceSlotCodes.MainIcon,
                    new[] { "主图标", "核心图标", "遗物主图标", "MainIcon", "Main Icon" }
                },
                { CardFaceSlotCodes.FaceBackground, new[] { "背景", "背景图", "Card Background", "CardBackground" } },
                { CardFaceSlotCodes.BackBorder, new[] { "边框", "Card_Border_rectangle_bronze", "Card_Border_rectangle_dark" } },
                { CardFaceSlotCodes.BackShirt, new[] { "CardShirts_8", "CardShirts", "背纹" } },
                { CardFaceSlotCodes.BackLogo, new[] { "logo", "Logo" } },
            };

        private static readonly Dictionary<string, string[]> TextCandidates =
            new Dictionary<string, string[]>
            {
                {
                    CardFaceSlotCodes.Name,
                    new[] { "名字", "名字横幅", "遗物名字", "标准世界文字", "Name" }
                },
                { CardFaceSlotCodes.Attack, new[] { "攻击数值", "Attack" } },
                { CardFaceSlotCodes.Armor, new[] { "护甲数值", "Armor" } },
                { CardFaceSlotCodes.Hp, new[] { "血量数值", "Hp", "Life" } },
                { CardFaceSlotCodes.ActionCount, new[] { "行动计数", "ActionCount", "Action_Count" } },
                { CardFaceSlotCodes.BasicDescription, new[] { "描述", "介绍区域", "描述面板", "BasicDescription" } },
            };

        public static bool TryFindRenderer(Transform faceRoot, string slotCode, out SpriteRenderer renderer)
        {
            renderer = null;
            if (faceRoot == null || string.IsNullOrEmpty(slotCode))
            {
                return false;
            }

            if (!SpriteCandidates.TryGetValue(slotCode, out var names))
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

        /// <summary>
        /// 查找槽对应 TMP：节点自身或子树内第一个 TextMeshPro。
        /// 怪物「行动计数」同时存在 Sprite 与 TMP 同名节点时，优先带 TMP 的节点。
        /// </summary>
        public static bool TryFindText(Transform faceRoot, string slotCode, out TMP_Text text)
        {
            text = null;
            if (faceRoot == null || string.IsNullOrEmpty(slotCode))
            {
                return false;
            }

            if (!TextCandidates.TryGetValue(slotCode, out var names))
            {
                return false;
            }

            for (var i = 0; i < names.Length; i++)
            {
                if (TryFindTextByName(faceRoot, names[i], out text))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>读取槽文本（供 EditMode 测试等不直接依赖 TMP 类型的调用方）。</summary>
        public static bool TryReadText(Transform faceRoot, string slotCode, out string value)
        {
            value = null;
            if (!TryFindText(faceRoot, slotCode, out var text) || text == null)
            {
                return false;
            }

            value = text.text;
            return true;
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
                if (!TryFindRenderer(faceRoot, pair.Key, out var renderer) || pair.Value == null)
                {
                    continue;
                }

                renderer.sprite = pair.Value;
            }
        }

        private static bool TryFindTextByName(Transform faceRoot, string name, out TMP_Text text)
        {
            text = null;
            var matches = new List<Transform>();
            CollectDeep(faceRoot, name, matches);
            for (var i = 0; i < matches.Count; i++)
            {
                var tmp = matches[i].GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    text = tmp;
                    return true;
                }

                tmp = matches[i].GetComponentInChildren<TMP_Text>(true);
                if (tmp != null)
                {
                    text = tmp;
                    return true;
                }
            }

            return false;
        }

        private static void Capture(Transform faceRoot, string code, IDictionary<string, Sprite> map)
        {
            if (TryFindRenderer(faceRoot, code, out var renderer) && renderer.sprite != null)
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

        private static void CollectDeep(Transform root, string name, List<Transform> results)
        {
            if (root == null || string.IsNullOrEmpty(name) || results == null)
            {
                return;
            }

            if (root.name == name)
            {
                results.Add(root);
            }

            for (var i = 0; i < root.childCount; i++)
            {
                CollectDeep(root.GetChild(i), name, results);
            }
        }
    }
}
