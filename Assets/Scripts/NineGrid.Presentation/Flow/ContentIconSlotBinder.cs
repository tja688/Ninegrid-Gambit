using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 把 content defId 列表刷到锚点子物体上的 SpriteRenderer（按兄弟序占槽）。
    /// </summary>
    internal static class ContentIconSlotBinder
    {
        public static bool TryGetSlotTransform(Transform anchorsRoot, int slotIndex, out Transform slot)
        {
            slot = null;
            if (anchorsRoot == null || slotIndex < 0 || slotIndex >= anchorsRoot.childCount)
            {
                return false;
            }

            slot = anchorsRoot.GetChild(slotIndex);
            return slot != null;
        }

        public static bool TryGetSlotTransformByDefId(
            Transform anchorsRoot,
            IReadOnlyList<string> displayedDefIds,
            string defId,
            out Transform slot)
        {
            slot = null;
            if (anchorsRoot == null || string.IsNullOrEmpty(defId) || displayedDefIds == null)
            {
                return false;
            }

            for (var i = 0; i < displayedDefIds.Count; i++)
            {
                if (displayedDefIds[i] != defId)
                {
                    continue;
                }

                return TryGetSlotTransform(anchorsRoot, i, out slot);
            }

            return false;
        }

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
            ApplySlots(slots, defIds, catalog);
        }

        /// <summary>#69：从一卡一文件 JSON 解析主图标（生产路径）。</summary>
        public static void ApplyFromPresentationJson(
            SpriteRenderer[] slots,
            IReadOnlyList<string> defIds)
        {
            ApplySlots(slots, defIds, catalog: null);
        }

        private static void ApplySlots(
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

                var sprite = ResolveSprite(defIds[i], catalog);
                if (sprite == null)
                {
                    ClearSlot(slot);
                    continue;
                }

                slot.sprite = sprite;
                slot.enabled = true;
                BindHitProxy(slot, defIds[i]);
            }
        }

        private static Sprite ResolveSprite(string defId, ContentVisualSpriteCatalogSO catalog)
        {
            if (catalog != null && catalog.TryGet(defId, out var icon, out var face))
            {
                return icon != null ? icon : face;
            }

            if (!CardPresentationConfigCatalog.TryGet(defId, out var dto)
                || dto?.sprites == null
                || string.IsNullOrWhiteSpace(dto.sprites.mainIcon))
            {
                return null;
            }

            return CardPresentationSpritePath.LoadSprite(dto.sprites.mainIcon);
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
            // 空槽保持 enabled，避免 Sync 间隙出现图标栏“断口”。
            slot.enabled = true;
            BindHitProxy(slot, string.Empty);
        }

        private static void BindHitProxy(SpriteRenderer slot, string defId)
        {
            if (slot == null)
            {
                return;
            }

            var proxy = slot.GetComponent<ContentIconSlotHitProxy>();
            if (proxy == null)
            {
                return;
            }

            proxy.DefId = defId ?? string.Empty;
        }
    }
}
