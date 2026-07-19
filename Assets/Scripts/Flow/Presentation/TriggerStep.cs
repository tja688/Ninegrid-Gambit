using System;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 触发脉冲：发即完成，不占长期控制权，可降级。
    /// </summary>
    public interface ITriggerPulseSink
    {
        void Pulse(string triggerId);
    }

    public sealed class TriggerStep : ITimelineStep
    {
        private readonly ITriggerPulseSink mSink;
        private readonly string mTriggerId;
        private bool mFired;

        public TriggerStep(ITriggerPulseSink sink, string triggerId)
        {
            if (sink == null)
            {
                throw new ArgumentNullException("sink");
            }

            if (string.IsNullOrEmpty(triggerId))
            {
                throw new ArgumentException("triggerId is required.", "triggerId");
            }

            mSink = sink;
            mTriggerId = triggerId;
        }

        public TimelineStepStatus Tick(float deltaTime)
        {
            if (!mFired)
            {
                mSink.Pulse(mTriggerId);
                mFired = true;
            }

            return TimelineStepStatus.Finished;
        }
    }
}
