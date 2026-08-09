using System;
using System.Collections.Generic;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// One currently playing source observed on MMSoundManager's Sfx track.
    /// </summary>
    public readonly struct SfxTrackSourceSnapshot
    {
        public SfxTrackSourceSnapshot(
            string sourceId,
            string clipKey,
            string cueId,
            double playbackPositionSeconds,
            bool loop,
            bool isPlaying = true)
        {
            SourceId = sourceId ?? string.Empty;
            ClipKey = clipKey ?? string.Empty;
            CueId = cueId ?? string.Empty;
            PlaybackPositionSeconds = Math.Max(0d, playbackPositionSeconds);
            Loop = loop;
            IsPlaying = isPlaying;
        }

        public string SourceId { get; }
        public string ClipKey { get; }
        public string CueId { get; }
        public double PlaybackPositionSeconds { get; }
        public bool Loop { get; }
        public bool IsPlaying { get; }

        public string DisplayName => string.IsNullOrEmpty(ClipKey)
            ? SourceId
            : SourceId + " · " + ClipKey;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public interface IAudioPlaybackDiagnosticsAdapter
    {
        IReadOnlyList<SfxTrackSourceSnapshot> GetPlayingSfxSources();
    }
#endif
}
