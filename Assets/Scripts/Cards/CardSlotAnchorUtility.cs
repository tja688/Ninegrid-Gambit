using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 从场景锚点节点名（slotN / slotN_Player）解析并排序槽位 Transform。
    /// </summary>
    public static class CardSlotAnchorUtility
    {
        private static readonly Regex SlotNamePattern = new(
            @"^slot(\d+)(?:_Player)?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly HashSet<string> ExcludedChildNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CardDeckAddAnchors",
            "DeckEntryPreparationSlot",
        };

        /// <summary>
        /// 收集 root 下按 slot 编号升序排列的槽位（1-based 编号映射到 0-based 列表索引）。
        /// </summary>
        public static List<Transform> GetSortedSlotTransforms(Transform root, int maxSlots)
        {
            if (root == null || maxSlots <= 0)
            {
                return new List<Transform>();
            }

            var byIndex = new Dictionary<int, Transform>();
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                var childName = child.name.Trim();
                if (ExcludedChildNames.Contains(childName))
                {
                    continue;
                }

                if (!TryParseSlotIndex(childName, out var slotIndex))
                {
                    continue;
                }

                if (slotIndex < 1 || slotIndex > maxSlots)
                {
                    continue;
                }

                byIndex[slotIndex] = child;
            }

            var result = new List<Transform>(maxSlots);
            for (var slot = 1; slot <= maxSlots; slot++)
            {
                if (byIndex.TryGetValue(slot, out var anchor))
                {
                    result.Add(anchor);
                }
                else
                {
                    result.Add(null);
                }
            }

            return result;
        }

        /// <summary>
        /// 按 Ground 开局发牌顺序（1,2,3,6,9,8,7,4）返回 0-based 槽位索引列表。
        /// </summary>
        public static IReadOnlyList<int> GetOpeningRingSlotIndices(int maxSlots = 9)
        {
            return new[] { 0, 1, 2, 5, 8, 7, 6, 3 };
        }

        public static bool TryParseSlotIndex(string nodeName, out int slotIndex)
        {
            slotIndex = 0;
            if (string.IsNullOrWhiteSpace(nodeName))
            {
                return false;
            }

            var match = SlotNamePattern.Match(nodeName.Trim());
            if (!match.Success)
            {
                return false;
            }

            return int.TryParse(match.Groups[1].Value, out slotIndex);
        }
    }
}
