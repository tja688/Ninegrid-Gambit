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

    public enum ReactionAnchorKind
    {
        Immediate,
        BatchStart,
        BatchEnd,
        StepStart,
        StepEnd,
        StepMarker,
    }

    public sealed class ReactionAnchor
    {
        public ReactionAnchorKind Kind { get; set; } = ReactionAnchorKind.Immediate;
        public PlanStepId StepId { get; set; }
        public string Marker { get; set; } = string.Empty;
        public float OffsetSeconds { get; set; }
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

    public sealed class PlannedReaction
    {
        public ReactionId ReactionId { get; set; }
        public FlowPayload Payload { get; set; }
        public ReactionAnchor Anchor { get; set; } = new ReactionAnchor();
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
            IReadOnlyList<PlannedReaction> reactions,
            CoreViewSnapshot snapshot)
        {
            BatchId = batchId;
            Groups = groups ?? new ActionPlanGroup[0];
            Reactions = reactions ?? new PlannedReaction[0];
            Snapshot = snapshot;
        }

        public int BatchId { get; }
        public IReadOnlyList<ActionPlanGroup> Groups { get; }
        public IReadOnlyList<PlannedReaction> Reactions { get; }
        public CoreViewSnapshot Snapshot { get; }
    }
}
