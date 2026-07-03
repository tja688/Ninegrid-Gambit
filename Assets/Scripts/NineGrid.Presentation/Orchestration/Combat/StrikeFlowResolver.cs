using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Presentation.Orchestration.Combat
{
    /// <summary>
    /// 将 <see cref="StrikeStep"/> 映射为串行 Plan 组；变体扩展入口。
    /// </summary>
    internal static class StrikeFlowResolver
    {
        public static List<ActionPlanGroup> ResolveGroups(
            CombatExchange exchange,
            ref int nextStepId,
            ref int groupIndex)
        {
            var groups = new List<ActionPlanGroup>();
            if (exchange?.Strikes == null)
            {
                return groups;
            }

            for (var i = 0; i < exchange.Strikes.Count; i++)
            {
                AppendStrikeGroups(groups, exchange.Strikes[i], ref nextStepId, ref groupIndex);
            }

            return groups;
        }

        private static void AppendStrikeGroups(
            List<ActionPlanGroup> groups,
            StrikeStep strike,
            ref int nextStepId,
            ref int groupIndex)
        {
            if (strike == null)
            {
                return;
            }

            if (strike.Role == StrikeRole.PrimaryAttack && strike.TargetKilled)
            {
                groups.Add(CreateGroup(
                    strike.ActionId,
                    CreateStep(
                        ref nextStepId,
                        strike.ActionId,
                        groupIndex++,
                        FlowId.CardKill,
                        BuildPrimaryKillPayload(strike),
                        strike.KillSource.Sequence > 0 ? strike.KillSource : strike.DamageSource)));
                return;
            }

            if (strike.Role == StrikeRole.PrimaryAttack)
            {
                groups.Add(CreateGroup(
                    strike.ActionId,
                    CreateStep(
                        ref nextStepId,
                        strike.ActionId,
                        groupIndex++,
                        FlowId.CardAttack,
                        strike.DamagePayload,
                        strike.DamageSource)));
                return;
            }

            if (strike.Role == StrikeRole.CounterAttack && strike.DamagePayload != null)
            {
                groups.Add(CreateGroup(
                    strike.ActionId,
                    CreateStep(
                        ref nextStepId,
                        strike.ActionId,
                        groupIndex++,
                        FlowId.Counterattack,
                        strike.DamagePayload,
                        strike.DamageSource)));
            }

            if (strike.Role == StrikeRole.CounterAttack && strike.TargetKilled)
            {
                int killActionId = strike.KillPayload != null
                    ? strike.KillSource.ActionId
                    : strike.ActionId;
                groups.Add(CreateGroup(
                    killActionId,
                    CreateStep(
                        ref nextStepId,
                        killActionId,
                        groupIndex++,
                        FlowId.CounterattackKill,
                        BuildCounterKillPayload(strike),
                        strike.KillSource)));
            }
        }

        private static FlowPayload BuildPrimaryKillPayload(StrikeStep strike)
        {
            FlowPayload payload = ClonePayload(strike.KillPayload ?? strike.DamagePayload);
            if (strike.DamagePayload != null)
            {
                payload.ActorUid = strike.DamagePayload.ActorUid;
                payload.TargetUid = strike.DamagePayload.TargetUid;
                payload.Amount = strike.DamagePayload.Amount;
                payload.Direction = strike.DamagePayload.Direction;
            }

            if (strike.KillPayload != null)
            {
                payload.CardUid = strike.KillPayload.CardUid;
                payload.FromSlot = strike.KillPayload.FromSlot;
                payload.ToSlot = strike.KillPayload.ToSlot;
            }

            payload.IncludeStrike = true;
            return payload;
        }

        private static FlowPayload BuildCounterKillPayload(StrikeStep strike)
        {
            FlowPayload payload = ClonePayload(strike.KillPayload ?? strike.DamagePayload);
            payload.IncludeStrike = false;
            return payload;
        }

        private static FlowPayload ClonePayload(FlowPayload source)
        {
            if (source == null)
            {
                return new FlowPayload();
            }

            return new FlowPayload
            {
                ActorUid = source.ActorUid,
                TargetUid = source.TargetUid,
                CardUid = source.CardUid,
                FromSlot = source.FromSlot,
                ToSlot = source.ToSlot,
                Amount = source.Amount,
                Delta = source.Delta,
                Index = source.Index,
                Total = source.Total,
                Direction = source.Direction,
                SourceDefId = source.SourceDefId,
                Cause = source.Cause,
                Message = source.Message,
                RemainingHp = source.RemainingHp,
                RemainingArmor = source.RemainingArmor,
            };
        }

        private static ActionPlanGroup CreateGroup(int actionId, PlanStep step)
        {
            return new ActionPlanGroup(actionId, new[] { step });
        }

        private static PlanStep CreateStep(
            ref int nextStepId,
            int actionId,
            int groupIndex,
            FlowId flowId,
            FlowPayload payload,
            SourceRef source)
        {
            return new PlanStep
            {
                Id = new PlanStepId(nextStepId++),
                ActionId = actionId,
                GroupIndex = groupIndex,
                FlowId = flowId,
                Payload = payload,
                Source = source,
            };
        }

        // 预留：按 DefId / Tag 解析变体 Flow，本 PR 不填表。
        // internal static FlowId ResolveVariant(StrikeStep step, CoreViewSnapshot snapshot) => FlowId.None;
    }
}
