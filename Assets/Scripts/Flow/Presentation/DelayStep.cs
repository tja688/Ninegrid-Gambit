using System;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 显式卡时序的 Delay Step：累计墙钟至 duration 后 Finished。
    /// </summary>
    public sealed class DelayStep : ITimelineStep
    {
        private readonly float mDurationSeconds;
        private float mElapsed;

        public DelayStep(float durationSeconds)
        {
            if (durationSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException("durationSeconds");
            }

            mDurationSeconds = durationSeconds;
        }

        public TimelineStepStatus Tick(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                mElapsed += deltaTime;
            }

            return mElapsed >= mDurationSeconds
                ? TimelineStepStatus.Finished
                : TimelineStepStatus.Continue;
        }
    }
}
