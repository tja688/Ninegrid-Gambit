using System.Collections;
using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Presentation.Orchestration
{
    /// <summary>
    /// 共享接缝：<see cref="Play"/> 消费真实 <see cref="PresentationBatch"/>，经 PlanBuilder → 四池 → Reconcile。
    /// </summary>
    public sealed class PresentationBatchPlayer
    {
        private readonly PerformancePlanBuilder mPlanBuilder = new PerformancePlanBuilder();
        private readonly PresentationPlanExecutor mExecutor;
        private readonly IInputLockGate mInputLockGate;

        public PresentationBatchPlayer(
            IViewRegistry viewRegistry,
            FlowRegistry flowRegistry,
            ReactionRegistry reactionRegistry,
            IReadOnlyList<IReconcilable> reconcilables,
            IInputLockGate inputLockGate = null)
        {
            mExecutor = new PresentationPlanExecutor(viewRegistry, flowRegistry, reactionRegistry, reconcilables);
            mInputLockGate = inputLockGate ?? new LocalInputLockGate();
        }

        public IInputLockGate InputLockGate => mInputLockGate;

        public PresentationPlan BuildPlan(PresentationBatch batch)
        {
            return mPlanBuilder.Build(batch);
        }

        public string DumpPlan(PresentationPlan plan)
        {
            if (plan == null)
            {
                return string.Empty;
            }

            var lines = new List<string>
            {
                "Batch#" + plan.BatchId,
            };

            for (var groupIndex = 0; groupIndex < plan.Groups.Count; groupIndex++)
            {
                var group = plan.Groups[groupIndex];
                lines.Add("  Action#" + group.ActionId);
                for (var stepIndex = 0; stepIndex < group.Steps.Count; stepIndex++)
                {
                    var step = group.Steps[stepIndex];
                    lines.Add("    " + step.Id + " Flow=" + step.FlowId + " card=" + step.Payload.CardUid);
                }
            }

            for (var reactionIndex = 0; reactionIndex < plan.Reactions.Count; reactionIndex++)
            {
                var reaction = plan.Reactions[reactionIndex];
                var anchor = reaction.Anchor;
                lines.Add(
                    "  Reaction " + reaction.ReactionId
                    + " anchor=" + anchor.Kind
                    + " step=" + anchor.StepId
                    + " marker=" + anchor.Marker);
            }

            return string.Join("\n", lines);
        }

        public PresentationPlanPlayResult PlaySync(PresentationBatch batch, PresentationPlan plan = null)
        {
            plan ??= BuildPlan(batch);
            mInputLockGate.Acquire(batch.BatchId);
            try
            {
                return mExecutor.PlaySync(plan);
            }
            finally
            {
                mInputLockGate.Release(batch.BatchId);
            }
        }

        public IEnumerator PlayCoroutine(
            PresentationBatch batch,
            PresentationPlan plan = null,
            bool deferParallelStartOneFrame = true)
        {
            plan ??= BuildPlan(batch);
            mInputLockGate.Acquire(batch.BatchId);
            yield return mExecutor.PlayCoroutine(plan, deferParallelStartOneFrame);
            mInputLockGate.Release(batch.BatchId);
        }
    }
}
