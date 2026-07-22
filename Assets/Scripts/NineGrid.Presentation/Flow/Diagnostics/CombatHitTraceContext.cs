namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 交战命中诊断上下文：下一次 ApplyCombatHit 消费的 BattleTrace reason。
    /// Field 写入，Flow 读取并清空。例：PlayerAttack / CounterAttack。
    /// </summary>
    public static class CombatHitTraceContext
    {
        public static string PendingReason;
    }
}
