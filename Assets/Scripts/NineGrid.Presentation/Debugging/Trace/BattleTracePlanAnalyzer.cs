using System.Collections.Generic;
using System.Text;
using NineGrid.Presentation.Orchestration;

namespace NineGrid.Presentation.Debugging.Trace
{
    internal static class BattleTracePlanAnalyzer
    {
        private static readonly HashSet<FlowId> sMotionFlows = new()
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

        public static void RecordPlan(PresentationPlan plan, BattleTraceSession session)
        {
            if (plan == null || session == null)
            {
                return;
            }

            for (var groupIndex = 0; groupIndex < plan.Groups.Count; groupIndex++)
            {
                ActionPlanGroup group = plan.Groups[groupIndex];
                int parallelGroupSize = group.Steps.Count;
                var motionFlows = new List<FlowId>();

                for (var stepIndex = 0; stepIndex < group.Steps.Count; stepIndex++)
                {
                    PlanStep step = group.Steps[stepIndex];
                    session.RecordPlanStep(
                        group.ActionId,
                        step.Id.ToString(),
                        step.FlowId,
                        step.Payload,
                        parallelGroupSize);

                    if (step.Payload != null && sMotionFlows.Contains(step.FlowId))
                    {
                        motionFlows.Add(step.FlowId);
                    }
                }

                if (motionFlows.Count > 1)
                {
                    session.RecordWarning(
                        "PARALLEL_MOTION",
                        "Action#" + group.ActionId + " has " + motionFlows.Count
                        + " motion flows (" + string.Join(", ", motionFlows) + ")",
                        group.ActionId);
                }
            }
        }
    }
}
