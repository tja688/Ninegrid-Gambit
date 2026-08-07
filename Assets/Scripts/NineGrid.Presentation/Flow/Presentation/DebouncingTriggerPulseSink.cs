using System;
using System.Collections.Generic;
using NineGrid.Content.Audio;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 统一音效/触发脉冲 debounce：同 cue ID 在窗口内只放行首次。
    /// EditMode 可注入 nowSeconds；默认墙钟 realtimeSinceStartup。
    /// </summary>
    public sealed class DebouncingTriggerPulseSink : ITriggerPulseSink, IAudioCuePulseSink
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
            TryPulse(triggerId, () => mInner.Pulse(triggerId));
        }

        public void Pulse(AudioCueRequest request)
        {
            TryPulse(request.CueId, () =>
            {
                var typedInner = mInner as IAudioCuePulseSink;
                if (typedInner != null)
                {
                    typedInner.Pulse(request);
                }
                else
                {
                    mInner.Pulse(request.CueId);
                }
            });
        }

        private void TryPulse(string triggerId, Action pulse)
        {
            if (string.IsNullOrEmpty(triggerId))
            {
                return;
            }

            var now = mNowSeconds();
            if (mLastPulseAt.TryGetValue(triggerId, out var last)
                && now - last < mWindowSeconds)
            {
                return;
            }

            mLastPulseAt[triggerId] = now;
            pulse();
        }
    }
}
