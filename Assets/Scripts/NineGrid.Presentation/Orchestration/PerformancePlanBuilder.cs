using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Orchestration.Combat;

namespace NineGrid.Presentation.Orchestration
{
    public sealed class PerformancePlanBuilder
    {
        public PresentationPlan Build(PresentationBatch batch)
        {
            if (batch == null)
            {
                return new PresentationPlan(0, new ActionPlanGroup[0], null, new PresentationInstruction[0]);
            }

            var outputGroups = new List<ActionPlanGroup>();
            var combatSegment = new List<RoutedCombatInstruction>();
            var pendingNonCombatSteps = new List<PlanStep>();
            var pendingNonCombatActionId = -1;
            var nextStepId = 1;
            var groupIndex = 0;

            for (var i = 0; i < batch.Instructions.Count; i++)
            {
                PresentationInstruction instruction = batch.Instructions[i];
                if (!TryCreateRoute(instruction, batch.Snapshot, out InstructionRoute route))
                {
                    continue;
                }

                if (CombatExchangeFolder.IsCombatInstruction(instruction, route, batch.Snapshot))
                {
                    FlushPendingNonCombat(outputGroups, pendingNonCombatSteps, ref pendingNonCombatActionId);

                    var source = new SourceRef(instruction.Sequence, instruction.Event.ActionId, instruction.Kind);
                    combatSegment.Add(new RoutedCombatInstruction(instruction, route, source));
                    continue;
                }

                if (combatSegment.Count > 0)
                {
                    FlushCombatSegment(combatSegment, batch.Snapshot, outputGroups, ref nextStepId, ref groupIndex);
                    combatSegment.Clear();
                }

                AppendNonCombatStep(
                    outputGroups,
                    pendingNonCombatSteps,
                    ref pendingNonCombatActionId,
                    instruction,
                    route,
                    ref nextStepId,
                    groupIndex);
            }

            if (combatSegment.Count > 0)
            {
                FlushCombatSegment(combatSegment, batch.Snapshot, outputGroups, ref nextStepId, ref groupIndex);
            }

            FlushPendingNonCombat(outputGroups, pendingNonCombatSteps, ref pendingNonCombatActionId);

            var groups = PlanGroupNormalizer.Normalize(outputGroups);
            return new PresentationPlan(batch.BatchId, groups, batch.Snapshot, batch.Instructions);
        }

        private static void FlushCombatSegment(
            List<RoutedCombatInstruction> segment,
            CoreViewSnapshot snapshot,
            List<ActionPlanGroup> outputGroups,
            ref int nextStepId,
            ref int groupIndex)
        {
            CombatExchange exchange = CombatExchangeFolder.Fold(segment, snapshot);
            outputGroups.AddRange(StrikeFlowResolver.ResolveGroups(exchange, ref nextStepId, ref groupIndex));
        }

        private static void AppendNonCombatStep(
            List<ActionPlanGroup> outputGroups,
            List<PlanStep> pendingSteps,
            ref int pendingActionId,
            PresentationInstruction instruction,
            InstructionRoute route,
            ref int nextStepId,
            int groupIndex)
        {
            int actionId = instruction.Event.ActionId;
            if (pendingSteps.Count > 0 && pendingActionId != actionId)
            {
                FlushPendingNonCombat(outputGroups, pendingSteps, ref pendingActionId);
            }

            if (pendingSteps.Count == 0)
            {
                pendingActionId = actionId;
            }

            var source = new SourceRef(instruction.Sequence, actionId, instruction.Kind);
            pendingSteps.Add(CreateStep(
                ref nextStepId,
                actionId,
                groupIndex,
                route.FlowId,
                route.Payload,
                source));
        }

        private static void FlushPendingNonCombat(
            List<ActionPlanGroup> outputGroups,
            List<PlanStep> pendingSteps,
            ref int pendingActionId)
        {
            if (pendingSteps.Count == 0 || pendingActionId < 0)
            {
                return;
            }

            outputGroups.Add(new ActionPlanGroup(pendingActionId, pendingSteps.ToArray()));
            pendingSteps.Clear();
            pendingActionId = -1;
        }

        private static bool TryCreateRoute(
            PresentationInstruction instruction,
            CoreViewSnapshot snapshot,
            out InstructionRoute route)
        {
            route = null;
            if (!InstructionKindFlowRouter.TryRoute(instruction, out route)
                || route.Kind != InstructionRouteKind.Flow)
            {
                return false;
            }

            route.FlowId = ResolvePlaybackFlow(route.FlowId, route.Payload, snapshot);
            if (IsBattleFlow(route.FlowId))
            {
                AttackDirectionResolver.ApplyBoardDirection(route.Payload, snapshot);
            }

            return true;
        }

        private static FlowId ResolvePlaybackFlow(FlowId flowId, FlowPayload payload, CoreViewSnapshot snapshot)
        {
            switch (flowId)
            {
                case FlowId.CardAttack:
                    return AttackFlowResolver.ResolveDamageFlow(payload, snapshot);
                case FlowId.CardKill:
                    return AttackFlowResolver.ResolveKillFlow(payload, snapshot);
                default:
                    return flowId;
            }
        }

        private static bool IsBattleFlow(FlowId flowId)
        {
            return flowId == FlowId.CardAttack
                   || flowId == FlowId.Counterattack
                   || flowId == FlowId.CardKill
                   || flowId == FlowId.CounterattackKill;
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
    }
}
