using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 描述展示路由（遗留枚举，供编译兼容）。
    /// 动态 HUD 描述 TMP 管道已退役；卡面基础描述权威在 Presentation Commit（Basic_Description）。
    /// </summary>
    public enum DescriptionShowRoute : byte
    {
        Hover = 0,
        Drag = 1,
        BoardSelect = 2,
    }

    /// <summary>
    /// 描述输出入口（已退役）：不再驱动 Card Info Text / NoticeText 等动态 TMP。
    /// 保留类型与空实现，避免场景与旧调用点编译断裂；卡面静态描述走 Basic_Description Commit。
    /// </summary>
    public static class DescriptionDisplayHook
    {
        /// <summary>遗留接线位；退役后恒为 null。</summary>
        public static Action EnsureWired;

        public static Action<string, DescriptionShowRoute> Show;
        public static Action<string, DescriptionShowRoute> ShowText;
        public static Action<DescriptionShowRoute> Clear;

        public static void RequestShow(string defId, DescriptionShowRoute route = DescriptionShowRoute.Hover)
        {
            // no-op：动态描述 TMP 已砍；勿在此恢复 Card Info Text 写入。
        }

        public static void RequestShowText(string text, DescriptionShowRoute route)
        {
            // no-op
        }

        public static void RequestClear(DescriptionShowRoute route = DescriptionShowRoute.Hover)
        {
            // no-op
        }
    }
}
