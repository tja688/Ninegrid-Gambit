using System;
using System.Collections.Generic;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// One currently playing source observed on MMSoundManager's Music track.
    /// The source id is diagnostic-only; it is never a persisted binding key.
    /// </summary>
    public readonly struct MusicTrackSourceSnapshot
    {
        public MusicTrackSourceSnapshot(
            string sourceId,
            string clipKey,
            double playbackPositionSeconds,
            bool isPlaying = true)
        {
            SourceId = sourceId ?? string.Empty;
            ClipKey = clipKey ?? string.Empty;
            PlaybackPositionSeconds = Math.Max(0d, playbackPositionSeconds);
            IsPlaying = isPlaying;
        }

        public string SourceId { get; }
        public string ClipKey { get; }
        public double PlaybackPositionSeconds { get; }
        public bool IsPlaying { get; }

        public string DisplayName => string.IsNullOrEmpty(ClipKey)
            ? SourceId
            : SourceId + " · " + ClipKey;
    }

    /// <summary>
    /// Result of one Music-track audit. Unknown sources are reported, never stopped by the audit itself.
    /// </summary>
    public sealed class MusicAuditResult
    {
        internal MusicAuditResult(
            string trigger,
            IReadOnlyList<MusicTrackSourceSnapshot> actualSources,
            IReadOnlyList<string> claimedSourceIds,
            IReadOnlyList<MusicTrackSourceSnapshot> unknownSources,
            MusicOverlapAnomaly anomaly)
        {
            Trigger = trigger ?? string.Empty;
            ActualSources = actualSources ?? Array.Empty<MusicTrackSourceSnapshot>();
            ClaimedSourceIds = claimedSourceIds ?? Array.Empty<string>();
            UnknownSources = unknownSources ?? Array.Empty<MusicTrackSourceSnapshot>();
            Anomaly = anomaly;
        }

        public string Trigger { get; }
        public IReadOnlyList<MusicTrackSourceSnapshot> ActualSources { get; }
        public IReadOnlyList<string> ClaimedSourceIds { get; }
        public IReadOnlyList<MusicTrackSourceSnapshot> UnknownSources { get; }
        public MusicOverlapAnomaly Anomaly { get; }
        public bool HasUnknownSources => UnknownSources.Count > 0;
    }

    /// <summary>
    /// Traceable ghost-BGM finding emitted when a Music-track source is not claimed by
    /// current music, retiring music, or the single Editor Preview owner.
    /// </summary>
    public sealed class MusicOverlapAnomaly
    {
        internal MusicOverlapAnomaly(
            string trigger,
            long musicGeneration,
            string desiredState,
            string finalBindingClipKey,
            string currentStableSource,
            string retiringStableSource,
            string requestSource,
            string sceneName,
            int chainId,
            int batchId,
            double time,
            IReadOnlyList<MusicTrackSourceSnapshot> actualSources,
            IReadOnlyList<string> claimedSourceIds,
            IReadOnlyList<MusicTrackSourceSnapshot> unknownSources)
        {
            Trigger = trigger ?? string.Empty;
            MusicGeneration = musicGeneration;
            DesiredState = desiredState ?? string.Empty;
            FinalBindingClipKey = finalBindingClipKey ?? string.Empty;
            CurrentStableSource = currentStableSource ?? string.Empty;
            RetiringStableSource = retiringStableSource ?? string.Empty;
            RequestSource = requestSource ?? string.Empty;
            SceneName = sceneName ?? string.Empty;
            ChainId = chainId;
            BatchId = batchId;
            Time = time;
            ActualSources = actualSources ?? Array.Empty<MusicTrackSourceSnapshot>();
            ClaimedSourceIds = claimedSourceIds ?? Array.Empty<string>();
            UnknownSources = unknownSources ?? Array.Empty<MusicTrackSourceSnapshot>();
        }

        public string Trigger { get; }
        public long MusicGeneration { get; }
        public string DesiredState { get; }
        public string FinalBindingClipKey { get; }
        public string CurrentStableSource { get; }
        public string RetiringStableSource { get; }
        public string RequestSource { get; }
        public string SceneName { get; }
        public int ChainId { get; }
        public int BatchId { get; }
        public double Time { get; }
        public IReadOnlyList<MusicTrackSourceSnapshot> ActualSources { get; }
        public IReadOnlyList<string> ClaimedSourceIds { get; }
        public IReadOnlyList<MusicTrackSourceSnapshot> UnknownSources { get; }
    }
}
