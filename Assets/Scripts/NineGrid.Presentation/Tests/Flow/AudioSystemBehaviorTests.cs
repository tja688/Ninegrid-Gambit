using System;
using System.Collections.Generic;
using System.Linq;
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
            Assert.AreEqual(AudioHistoryOutcome.Scheduled, system.History[0].Outcome);
            Assert.AreEqual(kept.Value, system.History[0].ScheduleKey);
            Assert.AreEqual(2f, system.History[0].ScheduleDelaySeconds, 0.001f);
            Assert.IsTrue(system.CancelScheduledCue(cancelled));
            Assert.AreEqual(AudioHistoryOutcome.Cancelled, system.History[system.History.Count - 1].Outcome);
            Assert.AreEqual(cancelled.Value, system.History[system.History.Count - 1].ScheduleKey);
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
        public void TryFromJson_RejectsInvalidOrNullBindings_WithoutReplacingCatalogOnApply()
        {
            var playback = new FakePlayback();
            var system = CreateSystem(new FakeClock(), playback);
            var before = system.ApplyWorkbenchCatalog(
                "{\"schemaVersion\":2,\"bindings\":[{\"cueId\":\"ui.test\",\"enabled\":true,\"clipKey\":\"audio/SFX/click\",\"minimumIntervalSeconds\":0}]}");
            Assert.IsTrue(before.Succeeded);
            Assert.AreEqual(1L, before.Revision);

            var invalid = system.ApplyWorkbenchCatalog("{not-json");
            Assert.IsFalse(invalid.Succeeded);
            Assert.AreEqual(1L, invalid.Revision);

            var nullBindings = system.ApplyWorkbenchCatalog("{\"schemaVersion\":2}");
            Assert.IsFalse(nullBindings.Succeeded);
            Assert.AreEqual(1L, nullBindings.Revision);

            var empty = system.ApplyWorkbenchCatalog("   ");
            Assert.IsFalse(empty.Succeeded);
            Assert.AreEqual(1L, empty.Revision);

            var played = system.RequestCue(AudioCueRequest.Simple("ui.test", "still-old-catalog"));
            Assert.AreEqual(AudioCueOutcome.Played, played.Outcome);
            Assert.AreEqual(1, playback.Requests.Count);
        }

        [Test]
        public void ApplyWorkbenchCatalog_IncrementsRevisionAndAffectsFutureRequests()
        {
            var playback = new FakePlayback();
            var system = CreateSystem(new FakeClock(), playback);
            Assert.AreEqual(AudioCueOutcome.Played, system.RequestCue(AudioCueRequest.Simple("ui.test", "a")).Outcome);

            var applied = system.ApplyWorkbenchCatalog(
                "{\"schemaVersion\":2,\"bindings\":[{\"cueId\":\"ui.test\",\"enabled\":true,\"clipKey\":\"audio/SFX/hot\",\"volumeDb\":-3,\"startOffsetSeconds\":0.5,\"bindingDelaySeconds\":0,\"minimumIntervalSeconds\":0}]}");
            Assert.IsTrue(applied.Succeeded);
            Assert.AreEqual(1L, applied.Revision);

            var after = system.RequestCue(AudioCueRequest.Simple("ui.test", "b"));
            Assert.AreEqual(AudioCueOutcome.Played, after.Outcome);
            Assert.AreEqual("audio/SFX/hot", playback.Requests[playback.Requests.Count - 1].ClipKey);
            Assert.AreEqual(0.5f, playback.Requests[playback.Requests.Count - 1].StartOffsetSeconds, 0.001f);
            Assert.AreEqual(1L, system.GetWorkbenchSnapshot().Revision);
        }

        [Test]
        public void DisabledBinding_RecordsSuppressedWithBindingKeyAndContext()
        {
            var catalogJson =
                "{\"schemaVersion\":2,\"bindings\":["
                + "{\"cueId\":\"ui.mute\",\"enabled\":false,\"clipKey\":\"audio/SFX/click\",\"note\":\"临时静音\","
                + "\"selectorCardDefId\":\"card.a\"}]}";
            var playback = new FakePlayback();
            var system = new AudioSystem(
                AudioBindingCatalog.FromJson(catalogJson),
                playback,
                new FakeClock(),
                scheduler: new FakeScheduler(),
                randomValue: () => 0d);

            var request = new AudioCueRequest(
                "ui.mute",
                "workbench",
                "card.a",
                "skill.1",
                "room.1",
                "item.1",
                "content.1",
                99);
            var result = system.RequestCue(request);

            Assert.AreEqual(AudioCueOutcome.Suppressed, result.Outcome);
            Assert.AreEqual(0, playback.Requests.Count);
            var suppressed = system.History[system.History.Count - 1];
            Assert.AreEqual(AudioHistoryOutcome.Suppressed, suppressed.Outcome);
            Assert.AreEqual("临时静音", suppressed.CueNote);
            Assert.AreEqual(result.BindingKey, suppressed.BindingKey);
            Assert.IsFalse(string.IsNullOrEmpty(suppressed.BindingKey));
            Assert.AreEqual("card.a", suppressed.CardDefId);
            Assert.AreEqual("skill.1", suppressed.SkillId);
            Assert.AreEqual("room.1", suppressed.RoomId);
            Assert.AreEqual("item.1", suppressed.ItemDefId);
            Assert.AreEqual("content.1", suppressed.ContentId);
            Assert.AreEqual(99, suppressed.DiagnosticCardUid);
            Assert.AreEqual("workbench binding disabled", suppressed.FailureReason);
            Assert.IsTrue(string.IsNullOrEmpty(suppressed.SourceId));
        }

        [Test]
        public void History_AssignsMonotonicSequenceAndCopiesRequestContext()
        {
            var playback = new FakePlayback { EmitSourceId = true };
            var system = CreateSystem(new FakeClock(), playback);
            var request = new AudioCueRequest(
                "ui.test",
                "seq-test",
                "c",
                "s",
                "r",
                "i",
                "x",
                7);

            system.RequestCue(request);
            system.RequestCue(request);

            Assert.AreEqual(1L, system.History[0].Sequence);
            Assert.Greater(system.History[system.History.Count - 1].Sequence, system.History[0].Sequence);
            var played = system.History[system.History.Count - 1];
            Assert.AreEqual(AudioHistoryOutcome.Cooldown, played.Outcome);
            Assert.AreEqual("c", played.CardDefId);
            Assert.AreEqual(7, played.DiagnosticCardUid);

            var firstPlayed = system.History.First(record => record.Outcome == AudioHistoryOutcome.Played);
            Assert.That(firstPlayed.SourceId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void WorkbenchSnapshot_IncludesHistoryPlayingSourcesAndAggregates()
        {
            var playback = new FakeDiagnosticsPlayback();
            var clock = new FakeClock();
            var system = new AudioSystem(
                AudioBindingCatalog.FromJson(
                    "{\"schemaVersion\":2,\"bindings\":[{\"cueId\":\"ui.test\",\"enabled\":true,\"clipKey\":\"audio/SFX/click\",\"minimumIntervalSeconds\":0}]}"),
                playback,
                clock,
                randomValue: () => 0d);

            system.RequestCue(AudioCueRequest.Simple("ui.test", "agg"));
            clock.UnscaledTime = 11d;
            system.RequestCue(AudioCueRequest.Simple("ui.test", "agg"));

            var snapshot = system.GetWorkbenchSnapshot();
            Assert.AreEqual(0L, snapshot.Revision);
            Assert.GreaterOrEqual(snapshot.History.Count, 4);
            Assert.AreEqual(2, snapshot.PlayingSources.Count);
            Assert.That(snapshot.Aggregates, Has.Some.Matches<AudioCueAggregate>(agg =>
                agg.Requested >= 2
                && agg.Played == 2
                && agg.RecentTimestamps != null
                && agg.RecentTimestamps.Count == 4));
        }

        [Test]
        public void PreviewWorkbenchBinding_IgnoresEnabledAndDoesNotTouchCooldownOrHistory()
        {
            var catalogJson =
                "{\"schemaVersion\":2,\"bindings\":["
                + "{\"cueId\":\"ui.test\",\"enabled\":false,\"clipKey\":\"audio/SFX/click\",\"volumeDb\":0,"
                + "\"startOffsetSeconds\":0.2,\"bindingDelaySeconds\":0.5,\"minimumIntervalSeconds\":10}]}";
            var playback = new FakePlayback { EmitSourceId = true };
            var system = new AudioSystem(
                AudioBindingCatalog.FromJson(catalogJson),
                playback,
                new FakeClock(),
                randomValue: () => 0d);
            var bindingKey = system.History.Count >= 0
                ? AudioBindingCatalog.FromJson(catalogJson).Bindings[0].BindingKey
                : string.Empty;
            var historyBefore = system.History.Count;

            var preview = system.PreviewWorkbenchBinding(bindingKey, includeBindingDelay: true);
            Assert.IsTrue(preview.Succeeded);
            Assert.AreEqual("audio/SFX/click", preview.ActualClipKey);
            Assert.That(preview.SourceId, Is.Not.Null.And.Not.Empty);
            Assert.AreEqual(1, playback.Requests.Count);
            Assert.AreEqual(0.5f, playback.Requests[0].BindingDelaySeconds, 0.001f);
            Assert.AreEqual(historyBefore, system.History.Count);

            Assert.AreEqual(AudioCueOutcome.Suppressed, system.RequestCue(AudioCueRequest.Simple("ui.test", "after-preview")).Outcome);
            Assert.AreEqual(1, playback.Requests.Count);
        }

        [Test]
        public void ApplyWorkbenchCatalog_ClearsBindingCooldownMemory_PreservesCueBurstWindows()
        {
            var playback = new FakePlayback();
            var clock = new FakeClock { UnscaledTime = 10d };
            var system = CreateSystem(clock, playback);

            Assert.AreEqual(AudioCueOutcome.Played, system.RequestCue(AudioCueRequest.Simple("ui.test", "1")).Outcome);
            Assert.AreEqual(AudioCueOutcome.Cooldown, system.RequestCue(AudioCueRequest.Simple("ui.test", "2")).Outcome);

            var applied = system.ApplyWorkbenchCatalog(
                "{\"schemaVersion\":2,\"bindings\":[{\"cueId\":\"ui.test\",\"enabled\":true,\"clipKey\":\"audio/SFX/click\",\"minimumIntervalSeconds\":1}]}");
            Assert.IsTrue(applied.Succeeded);

            Assert.AreEqual(AudioCueOutcome.Played, system.RequestCue(AudioCueRequest.Simple("ui.test", "3")).Outcome);
            Assert.AreEqual(2, playback.Requests.Count);
        }

        [Test]
        public void ScheduledCue_ResolvesAgainstNewestCatalogOnFire()
        {
            var scheduler = new FakeScheduler();
            var playback = new FakePlayback();
            var system = new AudioSystem(
                AudioBindingCatalog.FromJson(CatalogJson),
                playback,
                new FakeClock(),
                scheduler: scheduler,
                randomValue: () => 0d);

            var key = system.ScheduleCue(AudioCueRequest.Simple("ui.test", "scheduled"), 1f);
            var applied = system.ApplyWorkbenchCatalog(
                "{\"schemaVersion\":2,\"bindings\":[{\"cueId\":\"ui.test\",\"enabled\":true,\"clipKey\":\"audio/SFX/scheduled\",\"minimumIntervalSeconds\":0}]}");
            Assert.IsTrue(applied.Succeeded);
            scheduler.Fire(key);

            Assert.AreEqual(1, playback.Requests.Count);
            Assert.AreEqual("audio/SFX/scheduled", playback.Requests[0].ClipKey);
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
            public bool EmitSourceId { get; set; }
            private int mNextSourceId = 1;

            public AudioBackendResult Play(AudioPlaybackRequest request)
            {
                Requests.Add(request);
                if (Fail)
                {
                    return AudioBackendResult.Failure("missing material");
                }

                return EmitSourceId
                    ? AudioBackendResult.Success(request.ClipKey, "sfx-source:" + mNextSourceId++)
                    : AudioBackendResult.Success(request.ClipKey);
            }
        }

        private sealed class FakeDiagnosticsPlayback : IAudioPlaybackAdapter, IAudioPlaybackDiagnosticsAdapter
        {
            public readonly List<SfxTrackSourceSnapshot> Sources = new List<SfxTrackSourceSnapshot>();
            private int mNextSourceId = 1;

            public AudioBackendResult Play(AudioPlaybackRequest request)
            {
                var sourceId = "sfx-source:" + mNextSourceId++;
                Sources.Add(new SfxTrackSourceSnapshot(
                    sourceId,
                    request.ClipKey,
                    request.CueId,
                    0d,
                    loop: false));
                return AudioBackendResult.Success(request.ClipKey, sourceId);
            }

            public IReadOnlyList<SfxTrackSourceSnapshot> GetPlayingSfxSources()
            {
                return Sources;
            }

            public bool StopSfxSource(string sourceId)
            {
                if (string.IsNullOrEmpty(sourceId))
                {
                    return false;
                }

                for (var i = 0; i < Sources.Count; i++)
                {
                    if (string.Equals(Sources[i].SourceId, sourceId, StringComparison.Ordinal))
                    {
                        Sources.RemoveAt(i);
                        return true;
                    }
                }

                return false;
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
