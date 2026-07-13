using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 表现层容器归属与 Core 区对照（由 Flow 在战斗启动时接线）。
    /// Cards 层不直接依赖 Core，避免 asmdef 环依赖。
    /// </summary>
    public static class CardZoneOwnershipSink
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
