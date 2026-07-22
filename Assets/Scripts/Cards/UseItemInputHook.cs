using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 手牌用牌就绪后通知表现层装配 Controller，并承接用牌提交（由 NineGrid.Presentation 注册）。
    /// 避免 Cards→Presentation 程序集环；非 CombatHitSink 业务委托。
    /// </summary>
    public static class UseItemInputHook
    {
        public static Action<CardHandManagerSingleton> WireController;

        /// <summary>用牌意图提交；返回是否接纳（含忙时缓冲）。</summary>
        public static Func<int, int[], string, bool> TrySubmitUseItem;

        public static void RequestWire(CardHandManagerSingleton hand)
        {
            WireController?.Invoke(hand);
        }
    }
}
