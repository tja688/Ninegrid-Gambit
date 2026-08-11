using System;
using NineGrid.Content.Audio;
using NineGrid.Content.Vfx;
using NineGrid.Flow.Diagnostics;
using NineGrid.Presentation.Systems;

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
        private static IVfxCuePulseSink sVfx = NullVfxCuePulseSink.Instance;
        private static bool sFxEnabled = true;
        private static bool sAudioEnabled = true;
        private static bool sVfxEnabled = true;

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

        public static bool VfxEnabled
        {
            get { return sVfxEnabled; }
            set { sVfxEnabled = value; }
        }

        public static ITriggerPulseSink Fx
        {
            get { return sFx; }
        }

        public static ITriggerPulseSink Audio
        {
            get { return sAudio; }
        }

        public static IVfxCuePulseSink Vfx
        {
            get { return sVfx; }
        }

        /// <summary>生产装配：旧 FX + 带 debounce 的音效 sink + 类型化 VFX。</summary>
        public static void Configure(
            ITriggerPulseSink fx,
            ITriggerPulseSink audio,
            IVfxCuePulseSink vfx = null)
        {
            sFx = fx ?? NullTriggerPulseSink.Instance;
            sAudio = audio ?? NullTriggerPulseSink.Instance;
            sVfx = vfx ?? NullVfxCuePulseSink.Instance;
        }

        public static void ResetToNull()
        {
            sFx = NullTriggerPulseSink.Instance;
            sAudio = NullTriggerPulseSink.Instance;
            sVfx = NullVfxCuePulseSink.Instance;
            sFxEnabled = true;
            sAudioEnabled = true;
            sVfxEnabled = true;
        }

        /// <summary>
        /// 仅重置 FX 通道（战斗间清理）。音频通道属于主菜单到跑图结束的应用会话，
        /// 不随局内战斗装配启停（ADR-0036）。
        /// </summary>
        public static void ResetFxToNull()
        {
            sFx = NullTriggerPulseSink.Instance;
            sFxEnabled = true;
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

        /// <summary>保留无上下文入口；简单 cue 由音频模块解析。</summary>
        public static void PulseAudio(string triggerId)
        {
            PulseAudio(AudioCueRequest.Simple(triggerId, "TriggerPulseHub.PulseAudio"));
        }

        /// <summary>类型化声音提示入口；Hub 不选择素材、不保存绑定。</summary>
        public static void PulseAudio(AudioCueRequest request)
        {
            if (!sAudioEnabled)
            {
                DirectorTrace.TriggerPulse(request.CueId, "audio", degraded: true);
                return;
            }

            SafePulse(sAudio, request);
        }

        public static VfxCueResult PulseVfx(VfxCueRequest request)
        {
            return PulseVfx(request, VfxSpatialContext.Empty);
        }

        public static VfxCueResult PulseVfx(VfxCueRequest request, VfxSpatialContext spatialContext)
        {
            if (!sVfxEnabled)
            {
                DirectorTrace.TriggerPulse(request.CueId, "vfx", degraded: true);
                return new VfxCueResult
                {
                    Outcome = VfxCueOutcome.Suppressed,
                    CueId = request.CueId,
                    FailureReason = "vfx disabled",
                    PresentationPlan = VfxPresentationPlan.None,
                };
            }

            return SafePulseVfx(sVfx, request, spatialContext);
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

        private static void SafePulse(ITriggerPulseSink sink, AudioCueRequest request)
        {
            try
            {
                var typedSink = sink as IAudioCuePulseSink;
                if (typedSink != null)
                {
                    typedSink.Pulse(request);
                }
                else
                {
                    sink.Pulse(request.CueId);
                }

                DirectorTrace.TriggerPulse(request.CueId, "audio", degraded: false);
            }
            catch (Exception)
            {
                DirectorTrace.TriggerPulse(request.CueId, "audio", degraded: true);
            }
        }

        private static VfxCueResult SafePulseVfx(
            IVfxCuePulseSink sink,
            VfxCueRequest request,
            VfxSpatialContext spatialContext)
        {
            try
            {
                var result = sink.Pulse(request, spatialContext) ?? new VfxCueResult
                {
                    Outcome = VfxCueOutcome.BackendFailure,
                    CueId = request.CueId,
                    FailureReason = "vfx sink returned null",
                    PresentationPlan = VfxPresentationPlan.None,
                };
                DirectorTrace.TriggerPulse(request.CueId, "vfx", degraded: false);
                return result;
            }
            catch (Exception ex)
            {
                DirectorTrace.TriggerPulse(request.CueId, "vfx", degraded: true);
                return new VfxCueResult
                {
                    Outcome = VfxCueOutcome.BackendFailure,
                    CueId = request.CueId,
                    FailureReason = ex.Message,
                    PresentationPlan = VfxPresentationPlan.None,
                };
            }
        }
    }
}
