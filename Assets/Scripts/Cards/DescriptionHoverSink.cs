using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// Cards → Flow 描述悬停桥：Cards 不引用 Flow，由 DescriptionManagerSingleton 在 Awake 注册。
    /// </summary>
    public static class DescriptionHoverSink
    {
        public static Action<string> Show;
        public static Action Clear;

        public static void RequestShow(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                RequestClear();
                return;
            }

            Show?.Invoke(defId);
        }

        public static void RequestClear()
        {
            Clear?.Invoke();
        }
    }
}
