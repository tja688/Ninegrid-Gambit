using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 区域归属只读桥：Cards 不引 Core/Presentation，由表现层接线到 Query。
    /// </summary>
    public static class CardZoneOwnershipHook
    {
        /// <summary>uid 在 Core ItemSlots 时为 true；禁止将该视图插入卡组。</summary>
        public static Func<int, bool> IsCoreItemSlots;

        /// <summary>uid 在 Core DrawPile 时为 true。</summary>
        public static Func<int, bool> IsCoreDrawPile;

        public static void Reset()
        {
            IsCoreItemSlots = null;
            IsCoreDrawPile = null;
        }

        public static bool CoreSaysItemSlots(int uid)
        {
            return uid > 0 && IsCoreItemSlots != null && IsCoreItemSlots(uid);
        }

        public static bool CoreSaysDrawPile(int uid)
        {
            return uid > 0 && IsCoreDrawPile != null && IsCoreDrawPile(uid);
        }
    }
}
