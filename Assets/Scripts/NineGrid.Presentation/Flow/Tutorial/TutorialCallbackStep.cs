using System;
using NineGrid.Flow.Presentation;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>教学导演时间线回调 Step（换阶段 / 重开收尾）。</summary>
    internal sealed class TutorialCallbackStep : ITimelineStep
    {
        private readonly Action mCallback;
        private bool mInvoked;

        public TutorialCallbackStep(Action callback)
        {
            mCallback = callback;
        }

        public TimelineStepStatus Tick(float deltaTime)
        {
            _ = deltaTime;
            if (mInvoked)
            {
                return TimelineStepStatus.Finished;
            }

            mInvoked = true;
            mCallback?.Invoke();
            return TimelineStepStatus.Finished;
        }
    }
}
