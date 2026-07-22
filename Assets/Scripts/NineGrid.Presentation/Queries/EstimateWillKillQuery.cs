using System;
using NineGrid.Core;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Presentation.Queries
{
    /// <summary>
    /// 预估 attacker 对 target 是否足以击杀（选 Lethal Profile 用）；只读，不改 Core 状态。
    /// </summary>
    public sealed class EstimateWillKillQuery : AbstractQuery<bool>
    {
        private readonly int mAttackerUid;
        private readonly int mTargetUid;

        public EstimateWillKillQuery(int attackerUid, int targetUid)
        {
            mAttackerUid = attackerUid;
            mTargetUid = targetUid;
        }

        protected override bool OnDo()
        {
            var registry = this.GetModel<CardRegistry>();
            if (!registry.TryGet(mAttackerUid, out var attacker)
                || !registry.TryGet(mTargetUid, out var target))
            {
                return false;
            }

            var stats = this.GetSystem<IStatSystem>();
            var attack = Math.Max(0, stats.GetEffectiveInt(attacker, StatId.Attack));
            if (attacker.Kind == CardKind.Monster)
            {
                attack += (int)Math.Round(
                    stats.EvaluateRule(RuleId.EnemyAttackDelta, 0f, stats.CreateContext(attacker)));
            }

            var armor = Math.Max(0, stats.GetEffectiveInt(target, StatId.Armor));
            var hp = Math.Max(0, stats.GetEffectiveInt(target, StatId.Hp));
            var hpLoss = Math.Min(hp, Math.Max(0, attack - armor));
            return hp - hpLoss <= 0;
        }
    }
}
