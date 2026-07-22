using System;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 触发脉冲：发即完成，不占长期控制权，可降级（sink 失败/关闭不拆主线）。
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
                try
                {
                    mSink.Pulse(mTriggerId);
                }
                catch (Exception)
                {
                    // 关闭/失败 FX 不拆主时间线；卡时序靠显式 Delay，不靠 await FX。
                }

                mFired = true;
            }

            return TimelineStepStatus.Finished;
        }
    }
}
