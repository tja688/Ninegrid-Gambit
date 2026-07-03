using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Presentation.Orchestration
{
    public readonly struct PlanStepId
    {
        public PlanStepId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public override string ToString() => "Step#" + Value;
    }

    public readonly struct SourceRef
    {
        public SourceRef(long sequence, int actionId, PresentationInstructionKind instructionKind)
        {
            Sequence = sequence;
            ActionId = actionId;
            InstructionKind = instructionKind;
        }

        public long Sequence { get; }
        public int ActionId { get; }
        public PresentationInstructionKind InstructionKind { get; }
    }

    public sealed class PlanStep
    {
        public PlanStepId Id { get; set; }
        public int ActionId { get; set; }
        public int GroupIndex { get; set; }
        public FlowId FlowId { get; set; }
        public FlowPayload Payload { get; set; }
        public SourceRef Source { get; set; }
    }

    public sealed class ActionPlanGroup
    {
        public ActionPlanGroup(int actionId, IReadOnlyList<PlanStep> steps)
        {
            ActionId = actionId;
            Steps = steps ?? new PlanStep[0];
        }

        public int ActionId { get; }
        public IReadOnlyList<PlanStep> Steps { get; }
    }

    public sealed class PresentationPlan
    {
        public PresentationPlan(
            int batchId,
            IReadOnlyList<ActionPlanGroup> groups,
            CoreViewSnapshot snapshot,
            IReadOnlyList<PresentationInstruction> instructions = null)
        {
            BatchId = batchId;
            Groups = groups ?? new ActionPlanGroup[0];
            Snapshot = snapshot;
            Instructions = instructions ?? new PresentationInstruction[0];
        }

        public int BatchId { get; }
        public IReadOnlyList<ActionPlanGroup> Groups { get; }
        public CoreViewSnapshot Snapshot { get; }
        public IReadOnlyList<PresentationInstruction> Instructions { get; }
    }
}
