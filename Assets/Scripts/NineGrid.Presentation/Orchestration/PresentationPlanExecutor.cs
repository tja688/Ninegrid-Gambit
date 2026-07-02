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
            bool reconciled)
        {
            Plan = plan;
            PlayedFlowCount = playedFlowCount;
            Reconciled = reconciled;
        }

        public PresentationPlan Plan { get; }
        public int PlayedFlowCount { get; }
        public bool Reconciled { get; }
    }

    public sealed class PresentationPlanExecutor
    {
        private readonly IViewRegistry mViewRegistry;
        private readonly FlowRegistry mFlowRegistry;
        private readonly IReadOnlyList<IReconcilable> mReconcilables;
        private readonly TableNineActorFactory mActorFactory;

        public PresentationPlanExecutor(
            IViewRegistry viewRegistry,
            FlowRegistry flowRegistry,
            IReadOnlyList<IReconcilable> reconcilables,
            TableNineActorFactory actorFactory = null)
        {
            mViewRegistry = viewRegistry;
            mFlowRegistry = flowRegistry;
            mReconcilables = reconcilables ?? new IReconcilable[0];
            mActorFactory = actorFactory;
        }

        public PresentationPlanPlayResult PlaySync(PresentationPlan plan)
        {
            if (plan == null)
            {
                return new PresentationPlanPlayResult(null, 0, false);
            }

            using (CreatePlaybackScope(plan))
            {
                var playedFlows = 0;

                for (var i = 0; i < plan.Groups.Count; i++)
                {
                    var group = plan.Groups[i];
                    var handles = new List<FlowHandle>(group.Steps.Count);
                    for (var stepIndex = 0; stepIndex < group.Steps.Count; stepIndex++)
                    {
                        var step = group.Steps[stepIndex];
                        if (!mFlowRegistry.TryGet(step.FlowId, out var binding))
                        {
                            continue;
                        }

                        var handle = binding.Play(mViewRegistry, step.Payload);
                        handles.Add(handle);
                        playedFlows++;
                    }

                    while (IsAnyPlaying(handles))
                    {
                    }
                }

                Reconcile(plan);
                return new PresentationPlanPlayResult(plan, playedFlows, true);
            }
        }

        public IEnumerator PlayCoroutine(PresentationPlan plan, bool deferParallelStartOneFrame = true)
        {
            if (plan == null)
            {
                yield break;
            }

            using (CreatePlaybackScope(plan))
            {
                for (var groupIndex = 0; groupIndex < plan.Groups.Count; groupIndex++)
                {
                    var group = plan.Groups[groupIndex];
                    var handles = new List<FlowHandle>(group.Steps.Count);

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

                        var handle = binding.Play(mViewRegistry, step.Payload);
                        handles.Add(handle);
                    }

                    while (IsAnyPlaying(handles))
                    {
                        yield return null;
                    }
                }

                Reconcile(plan);
            }
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

        private FlowPlaybackScope CreatePlaybackScope(PresentationPlan plan)
        {
            var scope = FlowPlaybackScope.Push(plan?.Snapshot, mActorFactory);
            scope.Activate();
            return scope;
        }
    }
}
