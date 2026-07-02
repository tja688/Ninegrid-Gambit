using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Presentation.Orchestration
{
    public sealed class PerformancePlanBuilder
    {
        public PresentationPlan Build(PresentationBatch batch)
        {
            if (batch == null)
            {
                return new PresentationPlan(0, new ActionPlanGroup[0], null);
            }

            var groupedSteps = new Dictionary<int, List<PlanStep>>();
            var nextStepId = 1;
            var groupIndex = 0;

            for (var i = 0; i < batch.Instructions.Count; i++)
            {
                var instruction = batch.Instructions[i];
                if (!InstructionKindFlowRouter.TryRoute(instruction, out var route)
                    || route.Kind != InstructionRouteKind.Flow)
                {
                    continue;
                }

                var actionId = instruction.Event.ActionId;
                if (!groupedSteps.TryGetValue(actionId, out var steps))
                {
                    steps = new List<PlanStep>();
                    groupedSteps.Add(actionId, steps);
                }

                var source = new SourceRef(instruction.Sequence, actionId, instruction.Kind);
                if (route.FlowId == FlowId.CardAttack || route.FlowId == FlowId.CardKill)
                {
                    AttackDirectionResolver.ApplyBoardDirection(route.Payload, batch.Snapshot);
                }

                steps.Add(CreateStep(ref nextStepId, actionId, groupIndex, route.FlowId, route.Payload, source));
            }

            var groups = new List<ActionPlanGroup>();
            foreach (var pair in groupedSteps)
            {
                groups.Add(new ActionPlanGroup(pair.Key, pair.Value));
                groupIndex++;
            }

            groups.Sort((left, right) => left.ActionId.CompareTo(right.ActionId));
            return new PresentationPlan(batch.BatchId, groups, batch.Snapshot);
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
