using System;
using System.Collections.Generic;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 单 Step 内 fork 的并行子流：全部子步 Finished 后父步 Finished。
    /// </summary>
    public sealed class ParallelForkStep : ITimelineStep
    {
        private readonly ITimelineStep[] mChildren;
        private readonly bool[] mFinished;

        public ParallelForkStep(IReadOnlyList<ITimelineStep> children)
        {
            if (children == null || children.Count == 0)
            {
                throw new ArgumentException("ParallelForkStep requires at least one child.", "children");
            }

            mChildren = new ITimelineStep[children.Count];
            mFinished = new bool[children.Count];
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i] == null)
                {
                    throw new ArgumentException("ParallelForkStep child cannot be null.", "children");
                }

                mChildren[i] = children[i];
            }
        }

        public TimelineStepStatus Tick(float deltaTime)
        {
            var allFinished = true;
            for (var i = 0; i < mChildren.Length; i++)
            {
                if (mFinished[i])
                {
                    continue;
                }

                if (mChildren[i].Tick(deltaTime) == TimelineStepStatus.Finished)
                {
                    mFinished[i] = true;
                }
                else
                {
                    allFinished = false;
                }
            }

            return allFinished ? TimelineStepStatus.Finished : TimelineStepStatus.Continue;
        }
    }
}
