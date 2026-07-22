using NineGrid.Flow.Presentation;

namespace NineGrid.Presentation.Tests.Fixtures
{
    public sealed class ScriptedTimelineStep : ITimelineStep
    {
        private readonly int mContinueTicks;
        private int mTicksSeen;

        public ScriptedTimelineStep(int continueTicks)
        {
            mContinueTicks = continueTicks;
        }

        public int TickCount
        {
            get { return mTicksSeen; }
        }

        public TimelineStepStatus Tick(float deltaTime)
        {
            mTicksSeen++;
            return mTicksSeen <= mContinueTicks
                ? TimelineStepStatus.Continue
                : TimelineStepStatus.Finished;
        }
    }

}
