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

            public AudioBackendResult Play(AudioPlaybackRequest request)
            {
                return AudioBackendResult.Success(request.ClipKey);
            }

            public IReadOnlyList<SfxTrackSourceSnapshot> GetPlayingSfxSources()
            {
                return Sources;
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
