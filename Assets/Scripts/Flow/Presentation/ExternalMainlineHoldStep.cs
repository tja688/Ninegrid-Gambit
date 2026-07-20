using System;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 主线租约：外部薄适配（Pickup / RewardDrain 等）挂在导演主线上持忙，直至显式释放。
    /// </summary>
    public sealed class ExternalMainlineHoldStep : ITimelineStep
    {
        private readonly Func<bool> mIsReleased;

        public ExternalMainlineHoldStep(Func<bool> isReleased)
        {
            if (isReleased == null)
            {
                throw new ArgumentNullException("isReleased");
            }

            mIsReleased = isReleased;
        }

        public TimelineStepStatus Tick(float deltaTime)
        {
            return mIsReleased()
                ? TimelineStepStatus.Finished
                : TimelineStepStatus.Continue;
        }
    }
}
