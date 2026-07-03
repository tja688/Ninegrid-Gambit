using System.Collections;
using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Presentation.Orchestration
{
    /// <summary>
    /// 共享接缝：<see cref="Play"/> 消费真实 <see cref="PresentationBatch"/>，经 PlanBuilder → Flow → 数值投影。
    /// </summary>
    public sealed class PresentationBatchPlayer
    {
        private readonly PerformancePlanBuilder mPlanBuilder = new PerformancePlanBuilder();
        private readonly PresentationPlanExecutor mExecutor;
        private readonly IInputLockGate mInputLockGate;

        public PresentationBatchPlayer(
            IViewRegistry viewRegistry,
            FlowRegistry flowRegistry,
            IInputLockGate inputLockGate = null,
            TableNineActorFactory actorFactory = null,
            StatEventProjection statEventProjection = null)
        {
            mExecutor = new PresentationPlanExecutor(viewRegistry, flowRegistry, actorFactory, statEventProjection);
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
                    FlowPayload flowPayload = step.Payload;
                    lines.Add(
                        "    " + step.Id
                        + " Flow=" + step.FlowId
                        + " " + FormatFlowPayload(flowPayload));
                }
            }

            return string.Join("\n", lines);
        }

        private static string FormatFlowPayload(FlowPayload payload)
        {
            if (payload == null)
            {
                return "payload=null";
            }

            return "actor=" + payload.ActorUid
                + " target=" + payload.TargetUid
                + " card=" + payload.CardUid
                + " from=" + payload.FromSlot
                + " to=" + payload.ToSlot
                + " amount=" + payload.Amount
                + " dir=" + FormatDirection(payload.Direction);
        }

        private static string FormatDirection(UnityEngine.Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return "auto";
            }

            return direction.x.ToString("0.##") + "," + direction.y.ToString("0.##");
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
