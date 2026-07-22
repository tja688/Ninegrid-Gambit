using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地拾取就绪后通知表现层装配 Controller，并承接拾取写 Core（由 NineGrid.Presentation 注册）。
    /// 避免 Cards→Presentation 程序集环；非 CombatHitSink 业务委托。
    /// </summary>
    public static class PickupInputHook
    {
        public static Action<CardHandManagerSingleton> WireController;

        /// <summary>场地格拾取写 Core；返回表现摘要。</summary>
        public static Func<int, PickupItemPresentationResult> TryApplyPickup;

        public static void RequestWire(CardHandManagerSingleton hand)
        {
            WireController?.Invoke(hand);
        }
    }
}
