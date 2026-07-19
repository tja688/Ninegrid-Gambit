using System;

namespace NineGrid.Flow.Presentation
{
    /// <summary>按开关降级：关闭时丢弃脉冲，不抛、不占时间线。</summary>
    public sealed class GatedTriggerPulseSink : ITriggerPulseSink
    {
        private readonly ITriggerPulseSink mInner;
        private readonly Func<bool> mEnabled;

        public GatedTriggerPulseSink(ITriggerPulseSink inner, Func<bool> enabled)
        {
            if (inner == null)
            {
                throw new ArgumentNullException("inner");
            }

            if (enabled == null)
            {
                throw new ArgumentNullException("enabled");
            }

            mInner = inner;
            mEnabled = enabled;
        }

        public void Pulse(string triggerId)
        {
            if (!mEnabled())
            {
                return;
            }

            mInner.Pulse(triggerId);
        }
    }
}
