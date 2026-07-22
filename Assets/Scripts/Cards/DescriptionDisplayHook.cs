using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 描述展示路由：Hover 为默认悬停；Drag 预留给拖拽专属文案；BoardSelect 为多选提示。
    /// </summary>
    public enum DescriptionShowRoute : byte
    {
        Hover = 0,
        Drag = 1,
        BoardSelect = 2,
    }

    /// <summary>
    /// 描述输出统一入口：由 NineGrid.Presentation Controller 接线为 QF Event。
    /// 卡面基础描述权威仍在 Presentation Commit（Basic_Description）；本 Hook 只驱动遗留 HUD/提示文案。
    /// </summary>
    public static class DescriptionDisplayHook
    {
        /// <summary>由 Presentation Controller 注册，确保 Hook→Event 接线已安装。</summary>
        public static Action EnsureWired;

        public static Action<string, DescriptionShowRoute> Show;
        public static Action<string, DescriptionShowRoute> ShowText;
        public static Action<DescriptionShowRoute> Clear;

        public static void RequestShow(string defId, DescriptionShowRoute route = DescriptionShowRoute.Hover)
        {
            EnsureWired?.Invoke();
            if (string.IsNullOrEmpty(defId))
            {
                RequestClear(route);
                return;
            }

            Show?.Invoke(defId, route);
        }

        public static void RequestShowText(string text, DescriptionShowRoute route)
        {
            EnsureWired?.Invoke();
            if (string.IsNullOrEmpty(text))
            {
                RequestClear(route);
                return;
            }

            ShowText?.Invoke(text, route);
        }

        public static void RequestClear(DescriptionShowRoute route = DescriptionShowRoute.Hover)
        {
            EnsureWired?.Invoke();
            Clear?.Invoke(route);
        }
    }
}
