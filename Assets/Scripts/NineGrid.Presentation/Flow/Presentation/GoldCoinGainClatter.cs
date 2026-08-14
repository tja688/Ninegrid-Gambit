using NineGrid.Content.Audio;
using NineGrid.Presentation.Systems;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>金币入账哗啦啦开关：全局 vs 仅胜利结算变卖。</summary>
    public enum GoldCoinGainClatterMode
    {
        /// <summary>默认：所有金币入账都播哗啦啦。</summary>
        AllGains,
        /// <summary>只打破机关清关、变卖残留道具卡入账时播哗啦啦；其余入账降级单声。</summary>
        VictorySettlementOnly,
    }

    /// <summary>
    /// 金币入账哗啦啦调度：按收到的金币数量高频重复硬币叮当（配小随机池），模拟哗啦啦入账。
    /// 开关 <see cref="Mode"/>：默认全局哗啦啦，可切到只留胜利结算（变卖残留道具卡）哗啦啦。
    /// 架构不可用时降级单声，不抛异常。
    /// </summary>
    public static class GoldCoinGainClatter
    {
        /// <summary>体验开关：默认全局哗啦啦；切 VictorySettlementOnly 体验「只留结算哗啦啦」。</summary>
        public static GoldCoinGainClatterMode Mode = GoldCoinGainClatterMode.AllGains;

        /// <summary>每多少金币响一下（向上取整）。</summary>
        public const int GoldPerClatterPlay = 2;

        public const int MinClatterPlays = 2;
        public const int MaxClatterPlays = 10;

        /// <summary>首响延迟（秒），给金币飞入起点留点感觉。</summary>
        public const float FirstPlayDelaySeconds = 0.05f;

        /// <summary>相邻响间隔（秒）；减去最大抖动后仍须大于 economy.gold_gain 最短间隔(0.05)，否则被冷却吞掉。</summary>
        public const float PlayIntervalSeconds = 0.08f;

        public const float JitterSeconds = 0.012f;

        /// <summary>测试/装配缝：可注入 IAudioSystem 替身，缺省走 AudioSystem.EnsureRegistered。</summary>
        internal static System.Func<IAudioSystem> AudioSystemResolver;

        public static bool ShouldClatter(int delta, bool isVictorySettlement)
        {
            if (delta <= 0)
            {
                return false;
            }

            return Mode == GoldCoinGainClatterMode.AllGains || isVictorySettlement;
        }

        /// <summary>按数量换算响数：每 GoldPerClatterPlay 金币一下，夹在 [Min, Max]。</summary>
        public static int ResolvePlayCount(int delta)
        {
            if (delta <= 0)
            {
                return 0;
            }

            var raw = Mathf.CeilToInt(delta / (float)GoldPerClatterPlay);
            return Mathf.Clamp(raw, MinClatterPlays, MaxClatterPlays);
        }

        /// <summary>哗啦啦排期延迟序列（首响 + i*间隔 + 轻微抖动，保证非负且整体有先后）。</summary>
        internal static float[] BuildDelays(int playCount)
        {
            var delays = new float[playCount];
            for (var i = 0; i < playCount; i++)
            {
                var delay = FirstPlayDelaySeconds
                    + i * PlayIntervalSeconds
                    + Random.Range(-JitterSeconds, JitterSeconds);
                delays[i] = Mathf.Max(0f, delay);
            }

            return delays;
        }

        /// <summary>
        /// 金币入账声音入口：命中开关则高频排期哗啦啦，否则/架构不可用降级单声。
        /// 经 AudioSystem.ScheduleCue 排期，绕开 TriggerPulseHub 的 0.05s debounce（否则快速重复会被吞）。
        /// </summary>
        public static void Pulse(int delta, bool isVictorySettlement, string diagnosticSource)
        {
            if (delta <= 0)
            {
                return;
            }

            if (!ShouldClatter(delta, isVictorySettlement))
            {
                FlowRoomEconomyAudioCues.Pulse(FlowRoomEconomyAudioCues.GoldGain, diagnosticSource);
                return;
            }

            var audio = TryResolveAudioSystem();
            if (audio == null)
            {
                FlowRoomEconomyAudioCues.Pulse(FlowRoomEconomyAudioCues.GoldGain, diagnosticSource);
                return;
            }

            var playCount = ResolvePlayCount(delta);
            var delays = BuildDelays(playCount);
            for (var i = 0; i < delays.Length; i++)
            {
                audio.ScheduleCue(
                    AudioCueRequest.Simple(FlowRoomEconomyAudioCues.GoldGain, diagnosticSource),
                    delays[i]);
            }
        }

        private static IAudioSystem TryResolveAudioSystem()
        {
            if (AudioSystemResolver != null)
            {
                try
                {
                    return AudioSystemResolver();
                }
                catch
                {
                    return null;
                }
            }

            try
            {
                return AudioSystem.EnsureRegistered();
            }
            catch
            {
                return null;
            }
        }
    }
}
