using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地交战就绪后通知表现层装配攻击 Controller，并承接攻击提交（由 NineGrid.Presentation 注册）。
    /// 避免 Cards→Presentation 程序集环；非 CombatHitSink 业务委托。
    /// </summary>
    public static class AttackInputHook
    {
        public static Action<FieldBattleView> WireController;

        /// <summary>怪物格攻击提交；返回是否接纳（含忙时缓冲）。</summary>
        public static Func<int, bool> TrySubmitAttack;

        public static void RequestWire(FieldBattleView battle)
        {
            WireController?.Invoke(battle);
        }
    }
}
