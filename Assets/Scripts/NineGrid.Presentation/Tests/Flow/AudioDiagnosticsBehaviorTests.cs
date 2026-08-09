#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using NineGrid.Content.Audio;
using NineGrid.Flow.Diagnostics;
using NineGrid.Presentation.Systems;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class AudioDiagnosticsBehaviorTests
    {
        private const string CatalogJson =
            "{\"schemaVersion\":2,\"ticket\":\"#173\",\"bindings\":["
            + "{\"cueId\":\"ui.test\",\"enabled\":true,\"clipKey\":\"audio/SFX/click\",\"volumeDb\":0,\"minimumIntervalSeconds\":0}]}";

        [Test]
        public void RequestCue_RapidRepeats_EmitsBurstAnomalyAfterThreshold()
        {
            ResetPerfTrace();
            var clock = new FakeClock { Now = 10d };
            var playback = new FakeDiagnosticsPlayback();
            var system = new AudioSystem(
                AudioBindingCatalog.FromJson(CatalogJson),
                playback,
                clock,
                randomValue: () => 0d);

            for (var i = 0; i < 4; i++)
            {
                system.RequestCue(AudioCueRequest.Simple("ui.test", "burst-test"));
                clock.Now += 0.1d;
            }

            Assert.That(CountKind(PerfTraceKinds.AudioCueBurstAnomaly), Is.EqualTo(1));
        }

        [Test]
        public void AuditSfxTrack_IdlePersistentSource_EmitsPersistAnomaly()
        {
            ResetPerfTrace();
            var playback = new FakeDiagnosticsPlayback();
            playback.Sources.Add(new SfxTrackSourceSnapshot(
                "sfx-source:42",
                "audio/SFX/ghost",
                "ghost.cue",
                0.5d,
                loop: false));

            var service = AudioDiagnosticsService.Create(playback);
            service.AuditSfxTrack("t1");
            service.AuditSfxTrack("t2");
            service.AuditSfxTrack("t3");

            Assert.That(CountKind(PerfTraceKinds.AudioSfxPersistAnomaly), Is.EqualTo(1));
        }

        [Test]
        public void AuditSfxTrack_NoPlayingSources_DoesNotEmitSnapshot()
        {
            ResetPerfTrace();
            var service = AudioDiagnosticsService.Create(new FakeDiagnosticsPlayback());
            service.AuditSfxTrack("empty");

            Assert.That(CountKind(PerfTraceKinds.AudioSfxTrackSnapshot), Is.EqualTo(0));
        }

        [Test]
        public void StopSfxSource_StopsKnownSourceAndRejectsEmptyUnknownOrDead()
        {
            var playback = new FakeDiagnosticsPlayback();
            var system = new AudioSystem(
                AudioBindingCatalog.FromJson(CatalogJson),
                playback,
                new FakeClock(),
                randomValue: () => 0d);

            system.RequestCue(AudioCueRequest.Simple("ui.test", "stop-test"));
            var sourceId = system.History[system.History.Count - 1].SourceId;
            Assert.That(sourceId, Is.Not.Null.And.Not.Empty);
            Assert.That(system.GetWorkbenchSnapshot().PlayingSources.Count, Is.EqualTo(1));

            Assert.IsTrue(system.StopSfxSource(sourceId));
            Assert.IsFalse(system.StopSfxSource(sourceId));
            Assert.IsFalse(system.StopSfxSource(string.Empty));
            Assert.IsFalse(system.StopSfxSource("sfx-source:missing"));
            Assert.That(system.GetWorkbenchSnapshot().PlayingSources.Count, Is.EqualTo(0));
        }

        [Test]
        public void DisabledBinding_EmitsAudioCueSuppressedTraceWithReason()
        {
            ResetPerfTrace();
            var catalogJson =
                "{\"schemaVersion\":2,\"bindings\":["
                + "{\"cueId\":\"ui.test\",\"enabled\":false,\"clipKey\":\"audio/SFX/click\",\"note\":\"静音测试\"}]}";
            var system = new AudioSystem(
                AudioBindingCatalog.FromJson(catalogJson),
                new FakeDiagnosticsPlayback(),
                new FakeClock(),
                randomValue: () => 0d);

            var result = system.RequestCue(new AudioCueRequest(
                "ui.test",
                "suppressed-trace",
                "card.x",
                "skill.y",
                "room.z",
                "item.w",
                "content.v",
                42));

            Assert.AreEqual(AudioCueOutcome.Suppressed, result.Outcome);
            Assert.That(CountKind(PerfTraceKinds.AudioCueSuppressed), Is.EqualTo(1));
            var session = PerfTraceRecorder.CurrentSession;
            var ev = session.events.First(e => e.kind == PerfTraceKinds.AudioCueSuppressed);
            Assert.That(ev.payload["reason"], Is.EqualTo("workbench binding disabled"));
            Assert.That(ev.payload["cardDefId"], Is.EqualTo("card.x"));
            Assert.That(ev.payload.ContainsKey("bindingKey"), Is.True);
            Assert.That(ev.payload["bindingKey"], Is.Not.Null.And.Not.Empty);
        }

        private static void ResetPerfTrace()
        {
            PerfTraceRecorder.Clear();
            PerfTraceRecorder.Enabled = true;
            PerfTraceRecorder.BeginSessionIfNeeded(1UL);
        }

        private static int CountKind(string kind)
        {
            var session = PerfTraceRecorder.CurrentSession;
            if (session?.events == null)
            {
                return 0;
            }

            return session.events.Count(ev => ev.kind == kind);
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
                    if (string.Equals(Sources[i].SourceId, sourceId, System.StringComparison.Ordinal))
                    {
                        Sources.RemoveAt(i);
                        return true;
                    }
                }

                return false;
            }
        }

        private sealed class FakeClock : IAudioClock
        {
            public double Now { get; set; }
            public double UnscaledTime => Now;
        }
    }
}
#endif
