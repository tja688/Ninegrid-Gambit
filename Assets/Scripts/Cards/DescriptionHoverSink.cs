using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 描述展示路由：Hover 为默认悬停；Drag 预留给拖拽专属文案（当前默认与 Hover 相同）。
    /// </summary>
    public enum DescriptionShowRoute : byte
    {
        Hover = 0,
        Drag = 1,
    }

    /// <summary>
    /// Cards → Flow 描述悬停桥：Cards 不引用 Flow，由 DescriptionManagerSingleton 在 Awake 注册。
    /// </summary>
    public static class DescriptionHoverSink
    {
        /// <summary>Show(defId, route)；由 DescriptionManager 注册。</summary>
        public static Action<string, DescriptionShowRoute> Show;

        /// <summary>Clear(route)；仅清除匹配路由的当前描述，避免 hover/drag 互相踩。</summary>
        public static Action<DescriptionShowRoute> Clear;

        public static void RequestShow(string defId, DescriptionShowRoute route = DescriptionShowRoute.Hover)
        {
            if (string.IsNullOrEmpty(defId))
            {
                RequestClear(route);
                return;
            }

            Show?.Invoke(defId, route);
        }

        public static void RequestClear(DescriptionShowRoute route = DescriptionShowRoute.Hover)
        {
            Clear?.Invoke(route);
        }
    }
}
