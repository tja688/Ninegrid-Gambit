using System;
using System.Collections;
using System.Collections.Generic;

namespace NineGrid.Presentation.Orchestration
{
    public sealed class PresentationPlanPlayResult
    {
        public PresentationPlanPlayResult(
            PresentationPlan plan,
            int playedFlowCount,
            int playedReactionCount,
            bool reconciled)
        {
            Plan = plan;
            PlayedFlowCount = playedFlowCount;
            PlayedReactionCount = playedReactionCount;
            Reconciled = reconciled;
        }

        public PresentationPlan Plan { get; }
        public int PlayedFlowCount { get; }
        public int PlayedReactionCount { get; }
        public bool Reconciled { get; }
    }

    public sealed class PresentationPlanExecutor
    {
        private readonly IViewRegistry mViewRegistry;
        private readonly FlowRegistry mFlowRegistry;
        private readonly ReactionRegistry mReactionRegistry;
        private readonly IReadOnlyList<IReconcilable> mReconcilables;

        public PresentationPlanExecutor(
            IViewRegistry viewRegistry,
            FlowRegistry flowRegistry,
            ReactionRegistry reactionRegistry,
            IReadOnlyList<IReconcilable> reconcilables)
        {
            mViewRegistry = viewRegistry;
            mFlowRegistry = flowRegistry;
            mReactionRegistry = reactionRegistry;
            mReconcilables = reconcilables ?? new IReconcilable[0];
        }

        public PresentationPlanPlayResult PlaySync(PresentationPlan plan)
        {
            if (plan == null)
            {
                return new PresentationPlanPlayResult(null, 0, 0, false);
            }

            var playedFlows = 0;
            var playedReactions = 0;
            FireAnchoredReactions(plan, ReactionAnchorKind.BatchStart, ref playedReactions, null, string.Empty);

            for (var i = 0; i < plan.Groups.Count; i++)
            {
                var group = plan.Groups[i];
                var handles = new List<FlowHandle>(group.Steps.Count);
                for (var stepIndex = 0; stepIndex < group.Steps.Count; stepIndex++)
                {
                    var step = group.Steps[stepIndex];
                    FireAnchoredReactions(plan, ReactionAnchorKind.StepStart, ref playedReactions, step.Id, string.Empty);

                    if (!mFlowRegistry.TryGet(step.FlowId, out var binding))
                    {
                        continue;
                    }

                    var handle = binding.Play(
                        mViewRegistry,
                        step.Payload,
                        marker => FireAnchoredReactions(plan, ReactionAnchorKind.StepMarker, ref playedReactions, step.Id, marker));
                    handles.Add(handle);
                    playedFlows++;
                }

                for (var stepIndex = 0; stepIndex < group.Steps.Count; stepIndex++)
                {
                    var step = group.Steps[stepIndex];
                    FireAnchoredReactions(plan, ReactionAnchorKind.StepEnd, ref playedReactions, step.Id, string.Empty);
                }

                FireImmediateReactionsForAction(plan, group.ActionId, ref playedReactions);
            }

            FireAnchoredReactions(plan, ReactionAnchorKind.BatchEnd, ref playedReactions, null, string.Empty);

            Reconcile(plan);
            return new PresentationPlanPlayResult(plan, playedFlows, playedReactions, true);
        }

        public IEnumerator PlayCoroutine(PresentationPlan plan, bool deferParallelStartOneFrame = true)
        {
            if (plan == null)
            {
                yield break;
            }

            var playedReactions = 0;
            FireAnchoredReactions(plan, ReactionAnchorKind.BatchStart, ref playedReactions, null, string.Empty);

            for (var groupIndex = 0; groupIndex < plan.Groups.Count; groupIndex++)
            {
                var group = plan.Groups[groupIndex];
                var handles = new List<FlowHandle>(group.Steps.Count);

                for (var stepIndex = 0; stepIndex < group.Steps.Count; stepIndex++)
                {
                    var step = group.Steps[stepIndex];
                    FireAnchoredReactions(plan, ReactionAnchorKind.StepStart, ref playedReactions, step.Id, string.Empty);
                }

                if (deferParallelStartOneFrame && group.Steps.Count > 0)
                {
                    yield return null;
                }

                for (var stepIndex = 0; stepIndex < group.Steps.Count; stepIndex++)
                {
                    var step = group.Steps[stepIndex];
                    if (!mFlowRegistry.TryGet(step.FlowId, out var binding))
                    {
                        continue;
                    }

                    var stepId = step.Id;
                    var handle = binding.Play(
                        mViewRegistry,
                        step.Payload,
                        marker => FireAnchoredReactions(plan, ReactionAnchorKind.StepMarker, ref playedReactions, stepId, marker));
                    handles.Add(handle);
                }

                while (IsAnyPlaying(handles))
                {
                    yield return null;
                }

                for (var stepIndex = 0; stepIndex < group.Steps.Count; stepIndex++)
                {
                    var step = group.Steps[stepIndex];
                    FireAnchoredReactions(plan, ReactionAnchorKind.StepEnd, ref playedReactions, step.Id, string.Empty);
                }

                FireImmediateReactionsForAction(plan, group.ActionId, ref playedReactions);
            }

            FireAnchoredReactions(plan, ReactionAnchorKind.BatchEnd, ref playedReactions, null, string.Empty);
            Reconcile(plan);
        }

        public void Reconcile(PresentationPlan plan)
        {
            if (plan?.Snapshot == null)
            {
                return;
            }

            for (var i = 0; i < mReconcilables.Count; i++)
            {
                mReconcilables[i]?.ApplySnapshot(plan.Snapshot);
            }
        }

        private void FireImmediateReactionsForAction(PresentationPlan plan, int actionId, ref int playedReactions)
        {
            for (var i = 0; i < plan.Reactions.Count; i++)
            {
                var reaction = plan.Reactions[i];
                if (reaction.Anchor.Kind != ReactionAnchorKind.Immediate
                    || reaction.Source.ActionId != actionId)
                {
                    continue;
                }

                if (!mReactionRegistry.TryGet(reaction.ReactionId, out var binding))
                {
                    continue;
                }

                binding.Play(mViewRegistry, reaction.Payload);
                playedReactions++;
            }
        }

        private void FireAnchoredReactions(
            PresentationPlan plan,
            ReactionAnchorKind anchorKind,
            ref int playedReactions,
            PlanStepId? stepId,
            string marker)
        {
            for (var i = 0; i < plan.Reactions.Count; i++)
            {
                var reaction = plan.Reactions[i];
                if (!MatchesAnchor(reaction.Anchor, anchorKind, stepId, marker))
                {
                    continue;
                }

                if (!mReactionRegistry.TryGet(reaction.ReactionId, out var binding))
                {
                    continue;
                }

                binding.Play(mViewRegistry, reaction.Payload);
                playedReactions++;
            }
        }

        private static bool MatchesAnchor(
            ReactionAnchor anchor,
            ReactionAnchorKind anchorKind,
            PlanStepId? stepId,
            string marker)
        {
            if (anchor == null || anchor.Kind != anchorKind)
            {
                return false;
            }

            if (anchorKind == ReactionAnchorKind.StepMarker)
            {
                return stepId.HasValue
                       && anchor.StepId.Value == stepId.Value.Value
                       && string.Equals(anchor.Marker, marker, StringComparison.Ordinal);
            }

            if (anchorKind == ReactionAnchorKind.StepStart || anchorKind == ReactionAnchorKind.StepEnd)
            {
                return stepId.HasValue && anchor.StepId.Value == stepId.Value.Value;
            }

            return true;
        }

        private static bool IsAnyPlaying(IReadOnlyList<FlowHandle> handles)
        {
            for (var i = 0; i < handles.Count; i++)
            {
                if (handles[i] != null && handles[i].IsPlaying)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
