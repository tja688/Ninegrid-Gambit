using System;
using NineGrid.Flow.Diagnostics;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 局内 FX/音效脉冲唯一 hub。脉冲发即完成；可关 FX；音效默认 debounce。
    /// </summary>
    public static class TriggerPulseHub
    {
        public const float DefaultAudioDebounceSeconds = 0.05f;

        private static ITriggerPulseSink sFx = NullTriggerPulseSink.Instance;
        private static ITriggerPulseSink sAudio = NullTriggerPulseSink.Instance;
        private static bool sFxEnabled = true;
        private static bool sAudioEnabled = true;

        public static bool FxEnabled
        {
            get { return sFxEnabled; }
            set { sFxEnabled = value; }
        }

        public static bool AudioEnabled
        {
            get { return sAudioEnabled; }
            set { sAudioEnabled = value; }
        }

        public static ITriggerPulseSink Fx
        {
            get { return sFx; }
        }

        public static ITriggerPulseSink Audio
        {
            get { return sAudio; }
        }

        /// <summary>生产装配：FX + 带 debounce 的音效 sink。</summary>
        public static void Configure(ITriggerPulseSink fx, ITriggerPulseSink audio)
        {
            sFx = fx ?? NullTriggerPulseSink.Instance;
            sAudio = audio ?? NullTriggerPulseSink.Instance;
        }

        public static void ResetToNull()
        {
            sFx = NullTriggerPulseSink.Instance;
            sAudio = NullTriggerPulseSink.Instance;
            sFxEnabled = true;
            sAudioEnabled = true;
        }

        public static void PulseFx(string triggerId)
        {
            if (!sFxEnabled)
            {
                DirectorTrace.TriggerPulse(triggerId, "fx", degraded: true);
                return;
            }

            SafePulse(sFx, triggerId, "fx");
        }

        public static void PulseAudio(string triggerId)
        {
            if (!sAudioEnabled)
            {
                DirectorTrace.TriggerPulse(triggerId, "audio", degraded: true);
                return;
            }

            SafePulse(sAudio, triggerId, "audio");
        }

        private static void SafePulse(ITriggerPulseSink sink, string triggerId, string channel)
        {
            try
            {
                sink.Pulse(triggerId);
                DirectorTrace.TriggerPulse(triggerId, channel, degraded: false);
            }
            catch (Exception)
            {
                DirectorTrace.TriggerPulse(triggerId, channel, degraded: true);
            }
        }
    }
}
