using System;
using System.Collections.Generic;
using NineGrid.Content.Audio;
using NineGrid.Content.Editor;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class AudioSystemBehaviorTests
    {
        private const string CatalogJson =
            "{\"schemaVersion\":2,\"ticket\":\"#173\",\"bindings\":["
            + "{\"cueId\":\"ui.test\",\"enabled\":true,\"clipKey\":\"audio/SFX/click\",\"volumeDb\":0,\"startOffsetSeconds\":0.25,\"bindingDelaySeconds\":0.4,\"minimumIntervalSeconds\":1},"
            + "{\"cueId\":\"skill.test\",\"enabled\":true,\"clipKey\":\"\",\"volumeDb\":-2,\"bindingDelaySeconds\":0.8,\"variants\":["
            + "{\"variantId\":\"a\",\"clipKey\":\"audio/SFX/a\",\"weight\":1,\"volumeTrimDb\":-1,\"startOffsetSeconds\":0.1},"
            + "{\"variantId\":\"broken\",\"clipKey\":\"\",\"weight\":99,\"volumeTrimDb\":0,\"startOffsetSeconds\":0},"
            + "{\"variantId\":\"b\",\"clipKey\":\"audio/SFX/b\",\"weight\":1,\"volumeTrimDb\":2,\"startOffsetSeconds\":0.3}]}]}";

        [Test]
        public void RequestCue_SeparatesStartOffsetBindingDelayAndCooldown()
        {
            var clock = new FakeClock();
            var playback = new FakePlayback();
            var system = CreateSystem(clock, playback);

            var first = system.RequestCue(AudioCueRequest.Simple("ui.test", "test"));
            var second = system.RequestCue(AudioCueRequest.Simple("ui.test", "test"));

            Assert.AreEqual(AudioCueOutcome.Played, first.Outcome);
            Assert.AreEqual(0.25f, playback.Requests[0].StartOffsetSeconds, 0.001f);
            Assert.AreEqual(0.4f, playback.Requests[0].BindingDelaySeconds, 0.001f);
            Assert.AreEqual(AudioCueOutcome.Cooldown, second.Outcome);
            Assert.AreEqual(1, playback.Requests.Count);
            Assert.AreEqual(AudioHistoryOutcome.Cooldown, system.History[system.History.Count - 1].Outcome);
        }

        [Test]
        public void ScheduleCue_RequiresExplicitCancellationAndDoesNotSubscribeToEmitterLifetime()
        {
            var scheduler = new FakeScheduler();
            var playback = new FakePlayback();
            var system = new AudioSystem(
                AudioBindingCatalog.FromJson(CatalogJson),
                playback,
                new FakeClock(),
                scheduler: scheduler,
                randomValue: () => 0d);

            var kept = system.ScheduleCue(AudioCueRequest.Simple("ui.test", "kept"), 2f);
            var cancelled = system.ScheduleCue(AudioCueRequest.Simple("ui.test", "cancelled"), 3f);

            Assert.IsTrue(kept.IsValid);
            Assert.IsTrue(system.CancelScheduledCue(cancelled));
            scheduler.Fire(kept);
            Assert.AreEqual(1, playback.Requests.Count);
            Assert.IsFalse(system.CancelScheduledCue(cancelled));
        }

        [Test]
        public void VariantPool_SkipsInvalidRowsAvoidsImmediateRepeatAndRecordsActualVariant()
        {
            var playback = new FakePlayback();
            var system = CreateSystem(new FakeClock(), playback);

            var first = system.RequestCue(AudioCueRequest.Simple("skill.test", "test"));
            var second = system.RequestCue(AudioCueRequest.Simple("skill.test", "test"));

            Assert.AreEqual("a", first.VariantId);
            Assert.AreEqual("audio/SFX/a", first.ActualClipKey);
            Assert.AreEqual("b", second.VariantId);
            Assert.AreEqual("audio/SFX/b", second.ActualClipKey);
            Assert.AreEqual(0.1f, playback.Requests[0].StartOffsetSeconds, 0.001f);
            Assert.AreEqual(0.8f, playback.Requests[0].BindingDelaySeconds, 0.001f);
            Assert.AreEqual("b", system.History[system.History.Count - 1].VariantId);
        }

        [Test]
        public void MissingMaterial_ReturnsBackendFailureWithChosenVariant()
        {
            var playback = new FakePlayback { Fail = true };
            var system = CreateSystem(new FakeClock(), playback);

            var result = system.RequestCue(AudioCueRequest.Simple("skill.test", "test"));

            Assert.AreEqual(AudioCueOutcome.BackendFailure, result.Outcome);
            Assert.AreEqual("a", result.VariantId);
            Assert.AreEqual("missing material", result.FailureReason);
        }
        [Test]
        public void CueDeclarationScan_ReportsDuplicateAndUnboundDeclarations()
        {
            var catalog = AudioBindingCatalog.FromJson(
                "{\"schemaVersion\":2,\"bindings\":[{\"cueId\":\"scan.bound\",\"enabled\":true,\"clipKey\":\"audio/SFX/click\"}]}");

            var result = AudioCueDeclarationScanner.Scan(
                catalog,
                typeof(AudioSystemBehaviorTests).Assembly);

            Assert.That(result.Findings, Has.Some.Matches<AudioCueDeclarationFinding>(finding =>
                finding.CueId == "scan.duplicate"
                && finding.Message.Contains("重复")));
            Assert.That(result.Findings, Has.Some.Matches<AudioCueDeclarationFinding>(finding =>
                finding.CueId == "scan.unbound"
                && finding.Message.Contains("未绑定")));
            Assert.That(result.Findings, Has.None.Matches<AudioCueDeclarationFinding>(finding =>
                finding.CueId == "scan.bound"
                && finding.Message.Contains("未绑定")));
        }

        [AudioCue("scan.bound", "扫描器已绑定测试", "Tests", "AudioSystemBehaviorTests", AudioCueContexts.None)]
        private const string ScanBoundCue = "scan.bound";

        [AudioCue("scan.unbound", "扫描器未绑定测试", "Tests", "AudioSystemBehaviorTests", AudioCueContexts.None)]
        private const string ScanUnboundCue = "scan.unbound";

        [AudioCue("scan.duplicate", "扫描器重复测试一", "Tests", "AudioSystemBehaviorTests", AudioCueContexts.None)]
        private const string ScanDuplicateCueA = "scan.duplicate";

        [AudioCue("scan.duplicate", "扫描器重复测试二", "Tests", "AudioSystemBehaviorTests", AudioCueContexts.None)]
        private const string ScanDuplicateCueB = "scan.duplicate";

        private static AudioSystem CreateSystem(FakeClock clock, FakePlayback playback)
        {
            return new AudioSystem(
                AudioBindingCatalog.FromJson(CatalogJson),
                playback,
                clock,
                scheduler: new FakeScheduler(),
                randomValue: () => 0d);
        }

        private sealed class FakeClock : IAudioClock
        {
            public double UnscaledTime { get; set; } = 10d;
        }

        private sealed class FakePlayback : IAudioPlaybackAdapter
        {
            public readonly List<AudioPlaybackRequest> Requests = new List<AudioPlaybackRequest>();
            public bool Fail { get; set; }

            public AudioBackendResult Play(AudioPlaybackRequest request)
            {
                Requests.Add(request);
                return Fail
                    ? AudioBackendResult.Failure("missing material")
                    : AudioBackendResult.Success(request.ClipKey);
            }
        }

        private sealed class FakeScheduler : IAudioCueScheduler
        {
            private readonly Dictionary<long, Action> pending = new Dictionary<long, Action>();
            private long nextKey;

            public AudioScheduleKey Schedule(float delaySeconds, Action callback)
            {
                var key = new AudioScheduleKey(++nextKey);
                pending[key.Value] = callback;
                return key;
            }

            public bool Cancel(AudioScheduleKey key)
            {
                return pending.Remove(key.Value);
            }

            public void Fire(AudioScheduleKey key)
            {
                if (pending.TryGetValue(key.Value, out var callback))
                {
                    pending.Remove(key.Value);
                    callback();
                }
            }
        }
    }
}
