namespace NineGrid.Presentation.Orchestration.Combat
{
    internal enum StrikeRole
    {
        PrimaryAttack,
        CounterAttack,
    }

    /// <summary>
    /// 表现层折叠单元：一次 Strike（主攻击或反击）及其可选击杀。
    /// </summary>
    internal sealed class StrikeStep
    {
        public StrikeRole Role { get; set; }
        public int AttackerUid { get; set; }
        public int TargetUid { get; set; }
        public FlowPayload DamagePayload { get; set; }
        public FlowPayload KillPayload { get; set; }
        public bool TargetKilled { get; set; }
        public int ActionId { get; set; }
        public SourceRef DamageSource { get; set; }
        public SourceRef KillSource { get; set; }
    }

    internal sealed class CombatExchange
    {
        public CombatExchange(System.Collections.Generic.List<StrikeStep> strikes)
        {
            Strikes = strikes ?? new System.Collections.Generic.List<StrikeStep>();
        }

        public System.Collections.Generic.List<StrikeStep> Strikes { get; }
    }
}
