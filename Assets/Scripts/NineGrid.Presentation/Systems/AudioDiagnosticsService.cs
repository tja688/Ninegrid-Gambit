#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using NineGrid.Flow.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// Development-only Sfx-track audit + idle repeat detection.
    /// Observes MMSoundManager without stopping sources.
    /// </summary>
    public sealed class AudioDiagnosticsService
    {
        private const int PersistAuditThreshold = 3;

        private readonly IAudioPlaybackDiagnosticsAdapter mAdapter;
        private readonly Dictionary<string, PersistTracker> mPersistBySource =
            new Dictionary<string, PersistTracker>(StringComparer.Ordinal);
        private AudioDiagnosticsTicker mTicker;

        private AudioDiagnosticsService(IAudioPlaybackDiagnosticsAdapter adapter)
        {
            mAdapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        public static AudioDiagnosticsService Install(IAudioPlaybackDiagnosticsAdapter adapter)
        {
            if (adapter == null)
            {
                return null;
            }

            var service = Create(adapter);
            service.mTicker = AudioDiagnosticsTicker.Install(service);
            return service;
        }

        public static AudioDiagnosticsService Create(IAudioPlaybackDiagnosticsAdapter adapter)
        {
            return adapter == null ? null : new AudioDiagnosticsService(adapter);
        }

        public void Dispose()
        {
            mTicker?.Dispose();
            mTicker = null;
            mPersistBySource.Clear();
        }

        public void AuditSfxTrack(string trigger)
        {
            IReadOnlyList<SfxTrackSourceSnapshot> observed;
            try
            {
                observed = mAdapter.GetPlayingSfxSources() ?? Array.Empty<SfxTrackSourceSnapshot>();
            }
            catch (Exception exception)
            {
                RecordSnapshot(trigger, Array.Empty<SfxTrackSourceSnapshot>(), "audit-failed:" + exception.Message);
                return;
            }

            var playing = new List<SfxTrackSourceSnapshot>(observed.Count);
            for (var i = 0; i < observed.Count; i++)
            {
                if (observed[i].IsPlaying)
                {
                    playing.Add(observed[i]);
                }
            }

            if (playing.Count > 0)
            {
                RecordSnapshot(trigger, playing, null);
            }

            UpdatePersistTrackers(trigger, playing);
        }

        private void UpdatePersistTrackers(string trigger, IReadOnlyList<SfxTrackSourceSnapshot> playing)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var directorIdle = !DirectorTrace.DirectorMainlineBusy
                && !DirectorTrace.DirectorBypassBusy;

            for (var i = 0; i < playing.Count; i++)
            {
                var snapshot = playing[i];
                if (string.IsNullOrEmpty(snapshot.SourceId))
                {
                    continue;
                }

                seen.Add(snapshot.SourceId);
                if (!mPersistBySource.TryGetValue(snapshot.SourceId, out var tracker))
                {
                    tracker = new PersistTracker();
                    mPersistBySource[snapshot.SourceId] = tracker;
                }

                tracker.Advance(snapshot);
                if (tracker.ConsecutiveAudits >= PersistAuditThreshold
                    && (directorIdle || snapshot.Loop))
                {
                    RecordPersistAnomaly(trigger, snapshot, tracker, directorIdle);
                    tracker.ResetAfterReport();
                }
            }

            if (mPersistBySource.Count == 0)
            {
                return;
            }

            var stale = new List<string>();
            foreach (var pair in mPersistBySource)
            {
                if (!seen.Contains(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }

            for (var i = 0; i < stale.Count; i++)
            {
                mPersistBySource.Remove(stale[i]);
            }
        }

        private static void RecordSnapshot(
            string trigger,
            IReadOnlyList<SfxTrackSourceSnapshot> playing,
            string note)
        {
            var payload = new Dictionary<string, string>
            {
                ["trigger"] = trigger ?? string.Empty,
                ["playingCount"] = playing.Count.ToString(CultureInfo.InvariantCulture),
                ["scene"] = SceneManager.GetActiveScene().name ?? string.Empty,
            };

            if (!string.IsNullOrEmpty(note))
            {
                payload["note"] = note;
            }

            for (var i = 0; i < playing.Count && i < 8; i++)
            {
                var row = playing[i];
                var prefix = "src" + i.ToString(CultureInfo.InvariantCulture);
                payload[prefix + "Id"] = row.SourceId;
                payload[prefix + "Clip"] = row.ClipKey;
                if (!string.IsNullOrEmpty(row.CueId))
                {
                    payload[prefix + "Cue"] = row.CueId;
                }

                payload[prefix + "Pos"] = row.PlaybackPositionSeconds.ToString(
                    "R",
                    CultureInfo.InvariantCulture);
                payload[prefix + "Loop"] = row.Loop ? "true" : "false";
            }

            DirectorTrace.AppendBusyFields(payload);
            payload["batchId"] = DirectorTrace.ActiveBatchId.ToString(CultureInfo.InvariantCulture);
            payload["sessionId"] = DiagTraceShared.CurrentSessionId;
            payload["runTag"] = DiagTraceShared.RunTag;
            PerfTraceRecorder.Record(
                PerfTraceKinds.AudioSfxTrackSnapshot,
                uid: -1,
                PerfTraceSites.AudioSystemCue,
                payload);
        }

        private static void RecordPersistAnomaly(
            string trigger,
            SfxTrackSourceSnapshot snapshot,
            PersistTracker tracker,
            bool directorIdle)
        {
            var payload = new Dictionary<string, string>
            {
                ["trigger"] = trigger ?? string.Empty,
                ["sourceId"] = snapshot.SourceId,
                ["clipKey"] = snapshot.ClipKey,
                ["cueId"] = snapshot.CueId ?? string.Empty,
                ["loop"] = snapshot.Loop ? "true" : "false",
                ["consecutiveAudits"] = tracker.ConsecutiveAudits.ToString(CultureInfo.InvariantCulture),
                ["directorIdle"] = directorIdle ? "true" : "false",
                ["scene"] = SceneManager.GetActiveScene().name ?? string.Empty,
                ["reason"] = directorIdle
                    ? "Sfx 来源在导演空闲时连续多秒仍在播放。"
                    : "Sfx 来源开启 loop 并持续多秒播放。",
            };

            DirectorTrace.AppendBusyFields(payload);
            payload["batchId"] = DirectorTrace.ActiveBatchId.ToString(CultureInfo.InvariantCulture);
            payload["sessionId"] = DiagTraceShared.CurrentSessionId;
            payload["runTag"] = DiagTraceShared.RunTag;
            PerfTraceRecorder.Record(
                PerfTraceKinds.AudioSfxPersistAnomaly,
                uid: -1,
                PerfTraceSites.AudioSystemCue,
                payload);
        }

        private sealed class PersistTracker
        {
            private string mClipKey = string.Empty;

            public int ConsecutiveAudits { get; private set; }

            public void Advance(SfxTrackSourceSnapshot snapshot)
            {
                if (string.Equals(mClipKey, snapshot.ClipKey, StringComparison.Ordinal))
                {
                    ConsecutiveAudits++;
                    return;
                }

                mClipKey = snapshot.ClipKey ?? string.Empty;
                ConsecutiveAudits = 1;
            }

            public void ResetAfterReport()
            {
                ConsecutiveAudits = 0;
            }
        }
    }
}
#endif
