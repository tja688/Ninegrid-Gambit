using System.Collections.Generic;
using System.Text;
using NineGrid.Core;
using NineGrid.Presentation.Orchestration;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Slices
{
    /// <summary>
    /// Tier1 观察器：对 <see cref="PresentationBatch"/> 就地 Build plan，按 ActionId 组打印步骤并告警同组多运动 Flow。
    /// </summary>
    public static class PlaybackTrace
    {
        private static readonly HashSet<FlowId> sMotionFlows = new HashSet<FlowId>
        {
            FlowId.CardAttack,
            FlowId.CardKill,
            FlowId.BoardRotate,
            FlowId.MoveCard,
            FlowId.CardDeal,
            FlowId.FillSlots,
            FlowId.UseItem,
            FlowId.CardDeckEntry,
            FlowId.CardAcquisition,
            FlowId.Counterattack,
            FlowId.CounterattackKill,
            FlowId.RoomChoiceIn,
            FlowId.RoomChoiceOut,
            FlowId.InGameUiEntrance,
            FlowId.InGameUiExit,
            FlowId.PlayerAppear,
        };

        public static string Dump(PresentationBatch batch, bool logParallelWarnings = true)
        {
            if (batch == null)
            {
                return string.Empty;
            }

            var builder = new PerformancePlanBuilder();
            PresentationPlan plan = builder.Build(batch);
            var text = FormatPlan(plan, logParallelWarnings);
            Debug.Log(text);
            return text;
        }

        private static string FormatPlan(PresentationPlan plan, bool logParallelWarnings)
        {
            var lines = new List<string>
            {
                "[PlaybackTrace] Batch#" + plan.BatchId + " groups=" + plan.Groups.Count,
            };

            for (var groupIndex = 0; groupIndex < plan.Groups.Count; groupIndex++)
            {
                ActionPlanGroup group = plan.Groups[groupIndex];
                lines.Add("  Action#" + group.ActionId + " steps=" + group.Steps.Count);

                var motionFlowsInGroup = new List<FlowId>();
                for (var stepIndex = 0; stepIndex < group.Steps.Count; stepIndex++)
                {
                    PlanStep step = group.Steps[stepIndex];
                    FlowPayload payload = step.Payload;
                    lines.Add(
                        "    " + step.Id
                        + " Flow=" + step.FlowId
                        + " card=" + (payload?.CardUid ?? 0)
                        + " amount=" + (payload?.Amount ?? 0)
                        + " msg=" + (payload?.Message ?? string.Empty)
                        + " to=" + (payload?.ToSlot.ToString() ?? "None"));

                    if (payload != null && sMotionFlows.Contains(step.FlowId))
                    {
                        motionFlowsInGroup.Add(step.FlowId);
                    }
                }

                if (logParallelWarnings && motionFlowsInGroup.Count > 1)
                {
                    lines.Add(
                        "    ⚠ PARALLEL MOTION WARNING: Action#"
                        + group.ActionId
                        + " has "
                        + motionFlowsInGroup.Count
                        + " motion flows ("
                        + string.Join(", ", motionFlowsInGroup)
                        + ")");
                }
            }

            return string.Join("\n", lines);
        }
    }
}
