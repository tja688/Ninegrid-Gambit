using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地就绪后通知表现层装配探索 Controller，并承接探索提交（由 NineGrid.Presentation 注册）。
    /// 避免 Cards→Presentation 程序集环；非 CombatHitSink 业务委托。
    /// </summary>
    public static class ExploreInputHook
    {
        public static Action<GroundFieldManagerSingleton> WireController;

        /// <summary>空槽探索提交；返回是否接纳（含忙时缓冲）。</summary>
        public static Func<int, bool> TrySubmitExplore;

        public static void RequestWire(GroundFieldManagerSingleton field)
        {
            WireController?.Invoke(field);
        }
    }
}
