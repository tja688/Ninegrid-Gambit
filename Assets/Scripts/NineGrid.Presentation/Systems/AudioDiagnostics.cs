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
            bool isPlaying = true,
            bool claimed = true,
            bool workbenchPreview = false)
        {
            SourceId = sourceId ?? string.Empty;
            ClipKey = clipKey ?? string.Empty;
            CueId = cueId ?? string.Empty;
            PlaybackPositionSeconds = Math.Max(0d, playbackPositionSeconds);
            Loop = loop;
            IsPlaying = isPlaying;
            Claimed = claimed;
            WorkbenchPreview = workbenchPreview;
        }

        public string SourceId { get; }
        public string ClipKey { get; }
        public string CueId { get; }
        public double PlaybackPositionSeconds { get; }
        public bool Loop { get; }
        public bool IsPlaying { get; }

        /// <summary>
        /// True when this source was started via the project Adapter Play path.
        /// False means MMSoundManager Sfx-track playback that Adapter did not own (escape hatch).
        /// </summary>
        public bool Claimed { get; }

        /// <summary>
        /// True when this source was started by Audio Workbench preview (not gameplay).
        /// </summary>
        public bool WorkbenchPreview { get; }

        public string DisplayName => string.IsNullOrEmpty(ClipKey)
            ? SourceId
            : SourceId + " · " + ClipKey;
    }

    /// <summary>
    /// Scene AudioSource that is playing outside MMSoundManager's Sfx/Music pools.
    /// Report-only; workbench must not auto-kill these.
    /// </summary>
    public readonly struct SceneAudioOrphanSnapshot
    {
        public SceneAudioOrphanSnapshot(
            string gameObjectPath,
            string clipName,
            double playbackPositionSeconds,
            bool loop)
        {
            GameObjectPath = gameObjectPath ?? string.Empty;
            ClipName = clipName ?? string.Empty;
            PlaybackPositionSeconds = Math.Max(0d, playbackPositionSeconds);
            Loop = loop;
        }

        public string GameObjectPath { get; }
        public string ClipName { get; }
        public double PlaybackPositionSeconds { get; }
        public bool Loop { get; }
    }

    /// <summary>Recent Sfx persist anomaly surfaced to the workbench (not only PerfTrace).</summary>
    public readonly struct AudioPersistAnomalySnapshot
    {
        public AudioPersistAnomalySnapshot(
            string sourceId,
            string clipKey,
            string cueId,
            bool loop,
            string reason,
            double time)
        {
            SourceId = sourceId ?? string.Empty;
            ClipKey = clipKey ?? string.Empty;
            CueId = cueId ?? string.Empty;
            Loop = loop;
            Reason = reason ?? string.Empty;
            Time = time;
        }

        public string SourceId { get; }
        public string ClipKey { get; }
        public string CueId { get; }
        public bool Loop { get; }
        public string Reason { get; }
        public double Time { get; }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public interface IAudioPlaybackDiagnosticsAdapter
    {
        IReadOnlyList<SfxTrackSourceSnapshot> GetPlayingSfxSources();

        /// <summary>
        /// 停止指定 SFX 源。空/未知/已死 ID 返回 false；MMSoundManager 访问仅允许在 Adapter 内。
        /// </summary>
        bool StopSfxSource(string sourceId);

        /// <summary>停止当前 Sfx 轨上全部在播源；返回成功停掉的数量。</summary>
        int StopAllSfxSources();

        /// <summary>
        /// 场景中正在播放、且不在 MMSoundManager Sfx/Music 池内的 AudioSource（只报不停）。
        /// </summary>
        IReadOnlyList<SceneAudioOrphanSnapshot> GetSceneAudioOrphans();
    }
#endif
}
