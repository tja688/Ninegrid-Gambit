using System.Collections.Generic;
using NineGrid.Content;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 把 content defId 列表刷到锚点子物体上的 SpriteRenderer（按兄弟序占槽）。
    /// </summary>
    internal static class ContentIconSlotBinder
    {
        public static SpriteRenderer[] CollectChildRenderers(Transform anchorsRoot)
        {
            if (anchorsRoot == null)
            {
                return System.Array.Empty<SpriteRenderer>();
            }

            var list = new List<SpriteRenderer>(anchorsRoot.childCount);
            for (var i = 0; i < anchorsRoot.childCount; i++)
            {
                var child = anchorsRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                var renderer = child.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    list.Add(renderer);
                }
            }

            return list.ToArray();
        }

        public static void Apply(
            SpriteRenderer[] slots,
            IReadOnlyList<string> defIds,
            ContentVisualSpriteCatalogSO catalog)
        {
            if (slots == null || slots.Length == 0)
            {
                return;
            }

            var count = defIds != null ? defIds.Count : 0;
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                if (i >= count || string.IsNullOrEmpty(defIds[i]))
                {
                    ClearSlot(slot);
                    continue;
                }

                Sprite icon = null;
                Sprite face = null;
                if (catalog != null)
                {
                    catalog.TryGet(defIds[i], out icon, out face);
                }

                var sprite = icon != null ? icon : face;
                if (sprite == null)
                {
                    ClearSlot(slot);
                    continue;
                }

                slot.sprite = sprite;
                slot.enabled = true;
            }
        }

        public static void ClearAll(SpriteRenderer[] slots)
        {
            if (slots == null)
            {
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    ClearSlot(slots[i]);
                }
            }
        }

        private static void ClearSlot(SpriteRenderer slot)
        {
            slot.sprite = null;
            slot.enabled = false;
        }
    }
}
