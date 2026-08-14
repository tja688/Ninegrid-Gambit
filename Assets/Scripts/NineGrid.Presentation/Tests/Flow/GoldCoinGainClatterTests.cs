using System.Collections.Generic;
using NineGrid.Content.Audio;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>金币入账哗啦啦：开关分流、数量→响数换算、排期延迟与降级单声。</summary>
    public sealed class GoldCoinGainClatterTests
    {
        [TearDown]
        public void TearDown()
        {
            GoldCoinGainClatter.Mode = GoldCoinGainClatterMode.AllGains;
            GoldCoinGainClatter.AudioSystemResolver = null;
            TriggerPulseHub.ResetToNull();
        }

        [Test]
        public void ShouldClatter_AllGains_AlwaysClattersPositiveDelta()
        {
            GoldCoinGainClatter.Mode = GoldCoinGainClatterMode.AllGains;
            Assert.IsTrue(GoldCoinGainClatter.ShouldClatter(1, false));
            Assert.IsTrue(GoldCoinGainClatter.ShouldClatter(10, false));
            Assert.IsTrue(GoldCoinGainClatter.ShouldClatter(10, true));
            Assert.IsFalse(GoldCoinGainClatter.ShouldClatter(0, false));
            Assert.IsFalse(GoldCoinGainClatter.ShouldClatter(-3, true));
        }

        [Test]
        public void ShouldClatter_VictorySettlementOnly_OnlyVictorySettlement()
        {
            GoldCoinGainClatter.Mode = GoldCoinGainClatterMode.VictorySettlementOnly;
            Assert.IsTrue(GoldCoinGainClatter.ShouldClatter(10, true));
            Assert.IsFalse(GoldCoinGainClatter.ShouldClatter(10, false));
            Assert.IsFalse(GoldCoinGainClatter.ShouldClatter(1, false));
        }

        [Test]
        public void ResolvePlayCount_ScalesWithAmount_AndClamps()
        {
            Assert.AreEqual(2, GoldCoinGainClatter.ResolvePlayCount(1));
            Assert.AreEqual(2, GoldCoinGainClatter.ResolvePlayCount(2));
            Assert.AreEqual(3, GoldCoinGainClatter.ResolvePlayCount(5));
            Assert.AreEqual(5, GoldCoinGainClatter.ResolvePlayCount(10));
            Assert.AreEqual(10, GoldCoinGainClatter.ResolvePlayCount(100));
            Assert.AreEqual(0, GoldCoinGainClatter.ResolvePlayCount(0));
            Assert.AreEqual(0, GoldCoinGainClatter.ResolvePlayCount(-5));
        }

        [Test]
        public void BuildDelays_AreNonNegative_AndStrictlyIncreasing()
        {
            var delays = GoldCoinGainClatter.BuildDelays(6);
            Assert.AreEqual(6, delays.Length);
            for (var i = 0; i < delays.Length; i++)
            {
                Assert.GreaterOrEqual(delays[i], 0f);
                if (i > 0)
                {
                    Assert.Greater(delays[i], delays[i - 1]);
                }
            }
        }

        [Test]
        public void Pulse_VictorySettlementOnly_NonVictory_DegradesToSingleDing()
        {
            var sink = CaptureAudio();
            GoldCoinGainClatter.Mode = GoldCoinGainClatterMode.VictorySettlementOnly;

            GoldCoinGainClatter.Pulse(10, isVictorySettlement: false, "test");

            CollectionAssert.AreEqual(
                new[] { FlowRoomEconomyAudioCues.GoldGain },
                sink.CueIds);
        }

        [Test]
        public void Pulse_WithoutAudioSystem_ClatterDegradesToSingleDing()
        {
            var sink = CaptureAudio();
            GoldCoinGainClatter.AudioSystemResolver = () => null;

            GoldCoinGainClatter.Pulse(10, isVictorySettlement: false, "test");

            CollectionAssert.AreEqual(
                new[] { FlowRoomEconomyAudioCues.GoldGain },
                sink.CueIds);
        }

        [Test]
        public void Pulse_WithAudioSystem_SchedulesRepeatedPlaysByAmount()
        {
            var scheduler = new FakeScheduler();
            var playback = new FakePlayback();
            var audio = new AudioSystem(
                AudioBindingCatalog.FromJson(GoldBindingJson),
                playback,
                new FakeClock(),
                scheduler: scheduler,
                randomValue: () => 0d);
            GoldCoinGainClatter.AudioSystemResolver = () => audio;

            GoldCoinGainClatter.Pulse(10, isVictorySettlement: false, "test");

            // 只断言排期，不真正播：真实运行时各响相隔 PlayIntervalSeconds(0.07) > 最短间隔(0.05)，
            // 逐个落点都能出声；这里用固定时钟同时触发会撞冷却，不能反映真实行为。
            var expectedPlays = GoldCoinGainClatter.ResolvePlayCount(10);
            Assert.AreEqual(expectedPlays, scheduler.PendingCount);
            Assert.AreEqual(0, playback.Requests.Count);

            var delays = scheduler.PeekDelays();
            Assert.AreEqual(expectedPlays, delays.Count);
            for (var i = 0; i < delays.Count; i++)
            {
                Assert.GreaterOrEqual(delays[i], 0f);
                if (i > 0)
                {
                    Assert.Greater(delays[i], delays[i - 1]);
                }
            }

            // 相邻排期间隔必须大于 economy.gold_gain 的最短间隔，否则真实冷却会把后续响吞掉。
            for (var i = 1; i < delays.Count; i++)
            {
                Assert.Greater(delays[i] - delays[i - 1], 0.05f);
            }
        }

        [Test]
        public void Pulse_ZeroDelta_DoesNothing()
        {
            var sink = CaptureAudio();
            GoldCoinGainClatter.Pulse(0, isVictorySettlement: false, "test");
            Assert.IsEmpty(sink.CueIds);
        }

        private static CaptureSink CaptureAudio()
        {
            var sink = new CaptureSink();
            TriggerPulseHub.Configure(NullTriggerPulseSink.Instance, sink);
            return sink;
        }

        private const string GoldBindingJson =
            "{\"schemaVersion\":2,\"bindings\":["
            + "{\"cueId\":\"economy.gold_gain\",\"enabled\":true,\"clipKey\":\"\","
            + "\"volumeDb\":-3,\"minimumIntervalSeconds\":0.05,\"variants\":["
            + "{\"variantId\":\"ding-a\",\"clipKey\":\"audio/SFX/硬币叮当\",\"weight\":1,\"volumeTrimDb\":0,\"startOffsetSeconds\":0},"
            + "{\"variantId\":\"ding-b\",\"clipKey\":\"audio/SFX/硬币叮当\",\"weight\":1,\"volumeTrimDb\":-1,\"startOffsetSeconds\":0}"
            + "]}]}";

        private sealed class CaptureSink : ITriggerPulseSink, IAudioCuePulseSink
        {
            public readonly List<string> CueIds = new List<string>();

            public void Pulse(string triggerId)
            {
                CueIds.Add(triggerId ?? string.Empty);
            }

            public void Pulse(AudioCueRequest request)
            {
                CueIds.Add(request.CueId ?? string.Empty);
            }
        }

        private sealed class FakeClock : IAudioClock
        {
            public double UnscaledTime { get; set; } = 10d;
        }

        private sealed class FakePlayback : IAudioPlaybackAdapter
        {
            public readonly List<AudioPlaybackRequest> Requests = new List<AudioPlaybackRequest>();

            public AudioBackendResult Play(AudioPlaybackRequest request)
            {
                Requests.Add(request);
                return AudioBackendResult.Success(request.ClipKey);
            }
        }

        private sealed class FakeScheduler : IAudioCueScheduler
        {
            private readonly Dictionary<long, (float Delay, System.Action Callback)> pending =
                new Dictionary<long, (float, System.Action)>();
            private long nextKey;

            public int PendingCount => pending.Count;

            public AudioScheduleKey Schedule(float delaySeconds, System.Action callback)
            {
                var key = new AudioScheduleKey(++nextKey);
                pending[key.Value] = (delaySeconds, callback);
                return key;
            }

            public bool Cancel(AudioScheduleKey key)
            {
                return pending.Remove(key.Value);
            }

            public List<float> PeekDelays()
            {
                var delays = new List<float>();
                foreach (var entry in pending.Values)
                {
                    delays.Add(entry.Delay);
                }

                delays.Sort();
                return delays;
            }
        }
    }
}
