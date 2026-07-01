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
                return new PresentationPlan(0, new ActionPlanGroup[0], new PlannedReaction[0], null);
            }

            var groupedSteps = new Dictionary<int, List<PlanStep>>();
            var reactions = new List<PlannedReaction>();
            var nextStepId = 1;
            var groupIndex = 0;
            var lastStepByAction = new Dictionary<int, PlanStepId>();

            for (var i = 0; i < batch.Instructions.Count; i++)
            {
                var instruction = batch.Instructions[i];
                if (!InstructionKindFlowRouter.TryRoute(instruction, out var route))
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

                if (route.Kind == InstructionRouteKind.Flow)
                {
                    var step = CreateStep(ref nextStepId, actionId, groupIndex, route.FlowId, route.Payload, source);
                    steps.Add(step);
                    lastStepByAction[actionId] = step.Id;
                    continue;
                }

                if (route.SynthesizeAttackFlow)
                {
                    var attackPayload = FlowPayload.FromEvent(instruction.Event);
                    var attackStep = CreateStep(
                        ref nextStepId,
                        actionId,
                        groupIndex,
                        FlowId.CardAttack,
                        attackPayload,
                        source);
                    steps.Add(attackStep);
                    lastStepByAction[actionId] = attackStep.Id;

                    var damageReaction = new PlannedReaction
                    {
                        ReactionId = route.ReactionId,
                        Payload = route.Payload,
                        Source = source,
                        Anchor = new ReactionAnchor
                        {
                            Kind = ReactionAnchorKind.StepMarker,
                            StepId = attackStep.Id,
                            Marker = FlowMarkers.Impact,
                        },
                    };
                    reactions.Add(damageReaction);
                    continue;
                }

                var anchor = route.SuggestedAnchor ?? new ReactionAnchor();
                if (anchor.Kind == ReactionAnchorKind.StepMarker
                    || anchor.Kind == ReactionAnchorKind.StepStart
                    || anchor.Kind == ReactionAnchorKind.StepEnd)
                {
                    if (lastStepByAction.TryGetValue(actionId, out var stepId))
                    {
                        anchor.StepId = stepId;
                    }
                }

                reactions.Add(new PlannedReaction
                {
                    ReactionId = route.ReactionId,
                    Payload = route.Payload,
                    Source = source,
                    Anchor = anchor,
                });
            }

            var groups = new List<ActionPlanGroup>();
            foreach (var pair in groupedSteps)
            {
                groups.Add(new ActionPlanGroup(pair.Key, pair.Value));
                groupIndex++;
            }

            groups.Sort((left, right) => left.ActionId.CompareTo(right.ActionId));
            return new PresentationPlan(batch.BatchId, groups, reactions, batch.Snapshot);
        }

        private static PlanStep CreateStep(
            ref int nextStepId,
            int actionId,
            int groupIndex,
            FlowId flowId,
            FlowPayload payload,
            SourceRef source)
        {
            var step = new PlanStep
            {
                Id = new PlanStepId(nextStepId++),
                ActionId = actionId,
                GroupIndex = groupIndex,
                FlowId = flowId,
                Payload = payload,
                Source = source,
            };
            return step;
        }
    }
}
