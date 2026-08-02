using System.Collections.Generic;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;

namespace NineGrid.Flow.RoomIcons
{
    /// <summary>
    /// 场地图标落格：读表现 JSON <c>boardSlot</c>，双选项撞格时回退到 1/3/2。
    /// </summary>
    public static class RoomIconBoardSlotResolver
    {
        public const int DefaultSoloSlot = 2;
        public static readonly int[] DualFallbackSlots = { 1, 3, 2 };

        /// <summary>读取 contentId 配置的格位；未配或非法返回 0。</summary>
        public static int ReadConfiguredSlot(string contentId)
        {
            if (string.IsNullOrWhiteSpace(contentId))
            {
                return 0;
            }

            if (!CardPresentationConfigCatalog.TryGet(contentId, out var dto) || dto == null)
            {
                return 0;
            }

            return NormalizeSlot(dto.boardSlot);
        }

        /// <summary>
        /// 为选项列表分配落格。每项优先用自身 boardSlot；未配时单图标→2、双图标按序 1/3；
        /// 撞格则落到 DualFallback 中仍空的格。
        /// </summary>
        public static int[] ResolveSlots(IReadOnlyList<string> contentIds)
        {
            if (contentIds == null || contentIds.Count == 0)
            {
                return new int[0];
            }

            var result = new int[contentIds.Count];
            var used = new HashSet<int>();
            for (var i = 0; i < contentIds.Count; i++)
            {
                var configured = ReadConfiguredSlot(contentIds[i]);
                var slot = configured;
                if (slot == 0)
                {
                    slot = contentIds.Count == 1
                        ? DefaultSoloSlot
                        : (i == 0 ? DualFallbackSlots[0] : DualFallbackSlots[1]);
                }

                if (used.Contains(slot))
                {
                    slot = FirstFreeFallback(used);
                }

                result[i] = slot;
                used.Add(slot);
            }

            return result;
        }

        public static int NormalizeSlot(int boardSlot)
        {
            if (boardSlot < SlotId.MinBoardIndex || boardSlot > SlotId.MaxBoardIndex)
            {
                return 0;
            }

            return boardSlot;
        }

        private static int FirstFreeFallback(HashSet<int> used)
        {
            for (var i = 0; i < DualFallbackSlots.Length; i++)
            {
                if (!used.Contains(DualFallbackSlots[i]))
                {
                    return DualFallbackSlots[i];
                }
            }

            for (var s = SlotId.MinBoardIndex; s <= SlotId.MaxBoardIndex; s++)
            {
                if (!used.Contains(s))
                {
                    return s;
                }
            }

            return DualFallbackSlots[0];
        }
    }
}
