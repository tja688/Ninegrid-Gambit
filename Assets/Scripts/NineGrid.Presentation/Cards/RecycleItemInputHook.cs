using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 道具卡格拖入回收区的静态 Hook：CompositionRoot / Hand 装配 Controller。
    /// </summary>
    public static class RecycleItemInputHook
    {
        public static Action<CardHandManagerSingleton> WireController;

        /// <summary>提交回收意图；Allow 或 Buffer 时返回 true。</summary>
        public static Func<int, bool> TrySubmitRecycleItem;

        public static void RequestWire(CardHandManagerSingleton hand)
        {
            WireController?.Invoke(hand);
        }
    }
}
