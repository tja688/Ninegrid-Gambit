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
                var steps = SuppressRotationMoveSteps(group.Steps);
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
