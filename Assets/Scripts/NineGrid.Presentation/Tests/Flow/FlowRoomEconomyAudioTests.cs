using System.Collections.Generic;
using NineGrid.Content.Audio;
using NineGrid.Content.Editor;
using NineGrid.Flow.Presentation;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #177：房间 / 经济 / 跑图流程声音提示的公开 seam——声明唯一性、金币方向、过场区分与余额不足识别。
    /// </summary>
    public sealed class FlowRoomEconomyAudioTests
    {
        [TearDown]
        public void TearDown()
        {
            TriggerPulseHub.ResetToNull();
        }

        [Test]
        public void GoldPresentation_PulsesGainOrSpendOnceByDirection()
        {
            var sink = CaptureAudio();

            FlowRoomEconomyAudioCues.PulseGoldPresentation(isSpend: false, "test");
            FlowRoomEconomyAudioCues.PulseGoldPresentation(isSpend: true, "test");

            CollectionAssert.AreEqual(
                new[]
                {
                    FlowRoomEconomyAudioCues.GoldGain,
                    FlowRoomEconomyAudioCues.GoldSpend,
                },
                sink.CueIds);
        }

        [Test]
        public void Transition_PulsesFloorCrossOrSameFloorDistinctly()
        {
            var sink = CaptureAudio();

            FlowRoomEconomyAudioCues.PulseTransition(crossFloor: true, "test");
            FlowRoomEconomyAudioCues.PulseTransition(crossFloor: false, "test");

            CollectionAssert.AreEqual(
                new[]
                {
                    FlowRoomEconomyAudioCues.FloorCross,
                    FlowRoomEconomyAudioCues.RunTransition,
                },
                sink.CueIds);
        }

        [Test]
        public void InsufficientGoldReason_MatchesCoreRejectMessage()
        {
            Assert.IsTrue(FlowRoomEconomyAudioCues.IsInsufficientGoldReason("Not enough gold"));
            Assert.IsFalse(FlowRoomEconomyAudioCues.IsInsufficientGoldReason("Busy"));
            Assert.IsFalse(FlowRoomEconomyAudioCues.IsInsufficientGoldReason(null));
        }

        [Test]
        public void RoomEnter_PulsesWithRoomContext()
        {
            var sink = CaptureAudio();

            FlowRoomEconomyAudioCues.PulseRoom(
                FlowRoomEconomyAudioCues.RoomEnter,
                "test",
                "Shop");

            Assert.AreEqual(1, sink.Requests.Count);
            Assert.AreEqual(FlowRoomEconomyAudioCues.RoomEnter, sink.Requests[0].CueId);
            Assert.AreEqual("Shop", sink.Requests[0].RoomId);
        }

        [Test]
        public void FlowRoomEconomyCueDeclarations_AreUniqueAndPresent()
        {
            var catalog = AudioBindingCatalog.FromJson(
                "{\"schemaVersion\":2,\"bindings\":["
                + "{\"cueId\":\"flow.room.enter\",\"enabled\":true,\"clipKey\":\"audio/SFX/商店门打开\"},"
                + "{\"cueId\":\"shop.buy\",\"enabled\":true,\"clipKey\":\"audio/SFX/购买物品\"}]}");

            var result = AudioCueDeclarationScanner.Scan(
                catalog,
                typeof(FlowRoomEconomyAudioCues).Assembly);

            Assert.That(result.Findings, Has.None.Matches<AudioCueDeclarationFinding>(finding =>
                finding.CueId == FlowRoomEconomyAudioCues.RoomEnter
                && finding.Message.Contains("重复")));
            Assert.That(result.Findings, Has.None.Matches<AudioCueDeclarationFinding>(finding =>
                finding.CueId == FlowRoomEconomyAudioCues.ShopBuy
                && finding.Message.Contains("重复")));
            Assert.That(result.Findings, Has.Some.Matches<AudioCueDeclarationFinding>(finding =>
                finding.CueId == FlowRoomEconomyAudioCues.Victory
                && finding.Message.Contains("未绑定")));
        }

        private static CaptureSink CaptureAudio()
        {
            var sink = new CaptureSink();
            TriggerPulseHub.Configure(NullTriggerPulseSink.Instance, sink);
            return sink;
        }

        private sealed class CaptureSink : ITriggerPulseSink, IAudioCuePulseSink
        {
            public readonly List<string> CueIds = new List<string>();
            public readonly List<AudioCueRequest> Requests = new List<AudioCueRequest>();

            public void Pulse(string triggerId)
            {
                CueIds.Add(triggerId ?? string.Empty);
            }

            public void Pulse(AudioCueRequest request)
            {
                CueIds.Add(request.CueId ?? string.Empty);
                Requests.Add(request);
            }
        }
    }
}
