using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 统一音效/触发脉冲 debounce：同 triggerId 在窗口内只放行首次。
    /// EditMode 可注入 nowSeconds；默认墙钟 realtimeSinceStartup。
    /// </summary>
    public sealed class DebouncingTriggerPulseSink : ITriggerPulseSink
    {
        private readonly ITriggerPulseSink mInner;
        private readonly float mWindowSeconds;
        private readonly Func<float> mNowSeconds;
        private readonly Dictionary<string, float> mLastPulseAt = new Dictionary<string, float>();

        public DebouncingTriggerPulseSink(
            ITriggerPulseSink inner,
            float windowSeconds,
            Func<float> nowSeconds = null)
        {
            if (inner == null)
            {
                throw new ArgumentNullException("inner");
            }

            if (windowSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException("windowSeconds");
            }

            mInner = inner;
            mWindowSeconds = windowSeconds;
            mNowSeconds = nowSeconds ?? (() => Time.realtimeSinceStartup);
        }

        public void Pulse(string triggerId)
        {
            if (string.IsNullOrEmpty(triggerId))
            {
                return;
            }

            var now = mNowSeconds();
            float last;
            if (mLastPulseAt.TryGetValue(triggerId, out last) && now - last < mWindowSeconds)
            {
                return;
            }

            mLastPulseAt[triggerId] = now;
            mInner.Pulse(triggerId);
        }
    }
}
