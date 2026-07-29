using System;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 在 Tick 时按条件向主线追加后续步骤（一次性）。
    /// </summary>
    public sealed class TimelineBranchStep : ITimelineStep
    {
        private readonly BattleTimeline mTimeline;
        private readonly Func<bool> mCondition;
        private readonly Action<BattleTimeline> mWhenTrue;
        private readonly Action<BattleTimeline> mWhenFalse;
        private bool mDone;

        public TimelineBranchStep(
            BattleTimeline timeline,
            Func<bool> condition,
            Action<BattleTimeline> whenTrue,
            Action<BattleTimeline> whenFalse = null)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException("timeline");
            }

            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            mTimeline = timeline;
            mCondition = condition;
            mWhenTrue = whenTrue;
            mWhenFalse = whenFalse;
        }

        public TimelineStepStatus Tick(float deltaTime)
        {
            if (mDone)
            {
                return TimelineStepStatus.Finished;
            }

            if (mCondition())
            {
                if (mWhenTrue != null)
                {
                    mWhenTrue(mTimeline);
                }
            }
            else if (mWhenFalse != null)
            {
                mWhenFalse(mTimeline);
            }

            mDone = true;
            return TimelineStepStatus.Finished;
        }
    }
}
