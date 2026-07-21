using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards.Slots
{
    /// <summary>
    /// 通层 sortingOrder 校验：同一 SortingLayer + 同一卡级 SortingGroup 作用域内禁止重复 order。
    /// </summary>
    public static class CardFaceSortingOrderValidator
    {
        public readonly struct DuplicateHit
        {
            public readonly string SortingLayerName;
            public readonly int SortingOrder;
            public readonly string FirstPath;
            public readonly string SecondPath;

            public DuplicateHit(string sortingLayerName, int sortingOrder, string firstPath, string secondPath)
            {
                SortingLayerName = sortingLayerName;
                SortingOrder = sortingOrder;
                FirstPath = firstPath;
                SecondPath = secondPath;
            }

            public override string ToString()
            {
                return $"[{SortingLayerName}] order={SortingOrder}: {FirstPath} ↔ {SecondPath}";
            }
        }

        /// <summary>
        /// 扫描根下全部 SpriteRenderer；按「所属卡级 SortingGroup 实例 + SortingLayer」分桶检测重复 order。
        /// </summary>
        public static List<DuplicateHit> FindDuplicates(Transform root)
        {
            var hits = new List<DuplicateHit>();
            if (root == null)
            {
                return hits;
            }

            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            // key: groupInstanceId|layerId|order → first path
            var seen = new Dictionary<string, string>();

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var group = renderer.GetComponentInParent<SortingGroup>();
                var groupId = group != null ? group.GetInstanceID() : 0;
                var layerId = renderer.sortingLayerID;
                var order = renderer.sortingOrder;
                var key = groupId + "|" + layerId + "|" + order;
                var path = BuildPath(root, renderer.transform);

                string existing;
                if (seen.TryGetValue(key, out existing))
                {
                    hits.Add(new DuplicateHit(
                        renderer.sortingLayerName,
                        order,
                        existing,
                        path));
                }
                else
                {
                    seen[key] = path;
                }
            }

            return hits;
        }

        private static string BuildPath(Transform root, Transform node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            if (node == root)
            {
                return root.name;
            }

            var parts = new List<string>();
            var current = node;
            while (current != null)
            {
                parts.Add(current.name);
                if (current == root)
                {
                    break;
                }

                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
