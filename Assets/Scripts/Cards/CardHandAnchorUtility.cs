using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 从 CardHandAnchors 子节点名（handcardN）解析并排序槽位 Transform。
    /// </summary>
    public static class CardHandAnchorUtility
    {
        private static readonly Regex HandCardNamePattern = new(
            @"^handcard(\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly HashSet<string> ExcludedChildNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "HandcardApplyZone",
        };

        /// <summary>
        /// 收集 root 下按 handcard 编号升序排列的槽位（1-based 编号映射到 0-based 列表索引）。
        /// </summary>
        public static List<Transform> GetSortedHandAnchors(Transform root, int maxSlots)
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

                if (!TryParseHandCardIndex(childName, out var slotIndex))
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

        public static bool TryParseHandCardIndex(string nodeName, out int slotIndex)
        {
            slotIndex = 0;
            if (string.IsNullOrWhiteSpace(nodeName))
            {
                return false;
            }

            var match = HandCardNamePattern.Match(nodeName.Trim());
            if (!match.Success)
            {
                return false;
            }

            return int.TryParse(match.Groups[1].Value, out slotIndex);
        }
    }
}
