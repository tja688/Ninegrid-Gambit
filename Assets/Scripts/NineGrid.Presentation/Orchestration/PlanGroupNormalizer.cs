using System.Collections.Generic;

namespace NineGrid.Presentation.Orchestration
{
    /// <summary>
    /// 规范化 Action 组：旋转组去重、交战 Flow 串行拆分。
    /// </summary>
    internal static class PlanGroupNormalizer
    {
        public static List<ActionPlanGroup> Normalize(IReadOnlyList<ActionPlanGroup> groups)
        {
            if (groups == null || groups.Count == 0)
            {
                return new List<ActionPlanGroup>();
            }

            var normalized = new List<ActionPlanGroup>();
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                ActionPlanGroup group = groups[groupIndex];
                var steps = CoalesceCardDealSteps(SuppressRotationMoveSteps(group.Steps));
                AppendSplitBattleGroups(normalized, group.ActionId, steps);
            }

            return normalized;
        }

        private static List<PlanStep> SuppressRotationMoveSteps(IReadOnlyList<PlanStep> steps)
        {
            if (steps == null || steps.Count == 0)
            {
                return new List<PlanStep>();
            }

            var hasBoardRotate = false;
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].FlowId == FlowId.BoardRotate)
                {
                    hasBoardRotate = true;
                    break;
                }
            }

            if (!hasBoardRotate)
            {
                return new List<PlanStep>(steps);
            }

            var filtered = new List<PlanStep>(steps.Count);
            for (var i = 0; i < steps.Count; i++)
            {
                PlanStep step = steps[i];
                if (step.FlowId != FlowId.MoveCard)
                {
                    filtered.Add(step);
                }
            }

            return filtered;
        }

        private static List<PlanStep> CoalesceCardDealSteps(IReadOnlyList<PlanStep> steps)
        {
            if (steps == null || steps.Count == 0)
            {
                return new List<PlanStep>();
            }

            var normalized = new List<PlanStep>(steps.Count);
            var pendingDeals = new List<PlanStep>();

            for (var i = 0; i < steps.Count; i++)
            {
                PlanStep step = steps[i];
                if (step.FlowId == FlowId.CardDeal)
                {
                    pendingDeals.Add(step);
                    continue;
                }

                FlushPendingCardDeals(normalized, pendingDeals);
                normalized.Add(step);
            }

            FlushPendingCardDeals(normalized, pendingDeals);
            return normalized;
        }

        private static void FlushPendingCardDeals(List<PlanStep> output, List<PlanStep> pendingDeals)
        {
            if (pendingDeals.Count == 0)
            {
                return;
            }

            if (pendingDeals.Count == 1)
            {
                output.Add(pendingDeals[0]);
            }
            else
            {
                output.Add(MergeCardDealSteps(pendingDeals));
            }

            pendingDeals.Clear();
        }

        private static PlanStep MergeCardDealSteps(IReadOnlyList<PlanStep> dealSteps)
        {
            PlanStep first = dealSteps[0];
            var batchedPayloads = new List<FlowPayload>(dealSteps.Count);
            for (var i = 0; i < dealSteps.Count; i++)
            {
                FlowPayload payload = dealSteps[i].Payload;
                if (payload != null)
                {
                    batchedPayloads.Add(payload);
                }
            }

            var mergedPayload = new FlowPayload
            {
                Amount = batchedPayloads.Count,
                BatchedDeals = batchedPayloads,
            };

            if (batchedPayloads.Count > 0)
            {
                FlowPayload lead = batchedPayloads[0];
                mergedPayload.CardUid = lead.CardUid;
                mergedPayload.ToSlot = lead.ToSlot;
                mergedPayload.FromSlot = lead.FromSlot;
                mergedPayload.Message = lead.Message;
                mergedPayload.Cause = lead.Cause;
            }

            return new PlanStep
            {
                Id = first.Id,
                ActionId = first.ActionId,
                GroupIndex = first.GroupIndex,
                FlowId = FlowId.CardDeal,
                Payload = mergedPayload,
                Source = first.Source,
            };
        }

        private static void AppendSplitBattleGroups(
            List<ActionPlanGroup> output,
            int actionId,
            IReadOnlyList<PlanStep> steps)
        {
            if (steps == null || steps.Count == 0)
            {
                return;
            }

            var exclusiveCount = 0;
            for (var i = 0; i < steps.Count; i++)
            {
                if (IsExclusiveBattleFlow(steps[i].FlowId))
                {
                    exclusiveCount++;
                }
            }

            if (exclusiveCount <= 1)
            {
                output.Add(new ActionPlanGroup(actionId, steps));
                return;
            }

            var current = new List<PlanStep>();
            for (var i = 0; i < steps.Count; i++)
            {
                PlanStep step = steps[i];
                if (IsExclusiveBattleFlow(step.FlowId))
                {
                    if (current.Count > 0)
                    {
                        output.Add(new ActionPlanGroup(actionId, current));
                        current = new List<PlanStep>();
                    }

                    output.Add(new ActionPlanGroup(actionId, new[] { step }));
                    continue;
                }

                current.Add(step);
            }

            if (current.Count > 0)
            {
                output.Add(new ActionPlanGroup(actionId, current));
            }
        }

        private static bool IsExclusiveBattleFlow(FlowId flowId)
        {
            return flowId == FlowId.CardAttack
                   || flowId == FlowId.Counterattack
                   || flowId == FlowId.CardKill
                   || flowId == FlowId.CounterattackKill;
        }
    }
}
