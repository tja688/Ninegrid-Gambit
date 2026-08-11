using System;
using System.Collections.Generic;
using MoreMountains.Tools;
using NineGrid.Content.Audio;
using UnityEngine;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 项目唯一音频播放 Adapter：SFX 与 BGM 均由深模块给出正式 Resources 键，分别进入 MMSoundManager Sfx/Music 轨。
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public sealed class MMSoundManagerAudioPlaybackAdapter
        : IAudioPlaybackAdapter,
            IMusicPlaybackDiagnosticsAdapter,
            IAudioPlaybackDiagnosticsAdapter,
            IAudioSfxTailAdapter
#else
    public sealed class MMSoundManagerAudioPlaybackAdapter : IAudioPlaybackAdapter, IMusicPlaybackAdapter, IAudioSfxTailAdapter
#endif
    {
        private readonly Dictionary<string, AudioSource> sfxSources =
            new Dictionary<string, AudioSource>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> sfxClipKeys =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> sfxCueIds =
            new Dictionary<string, string>(StringComparer.Ordinal);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly Dictionary<string, AudioSource> musicSources =
            new Dictionary<string, AudioSource>(StringComparer.Ordinal);
        private readonly Dictionary<AudioClip, string> musicClipKeys =
            new Dictionary<AudioClip, string>();
        private readonly HashSet<string> sfxOwnedIds = new HashSet<string>(StringComparer.Ordinal);
#endif

        public AudioBackendResult Play(AudioPlaybackRequest request)
        {
            var resourcesKey = AudioAssetManifestLoader.NormalizeKey(request.ClipKey);
            if (!AudioAssetManifestLoader.IsKnownFormalKey(resourcesKey))
            {
                return AudioBackendResult.Failure("正式音频 manifest 未登记素材键：" + resourcesKey);
            }

            var clip = Resources.Load<AudioClip>(resourcesKey);
            if (clip == null)
            {
                return AudioBackendResult.Failure("Resources 音频素材不存在：" + resourcesKey);
            }

            var manager = MMSoundManager.Instance;
            if (manager == null)
            {
                return AudioBackendResult.Failure("MMSoundManager 未就绪：" + resourcesKey);
            }

            var options = MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
            options.Volume = Mathf.Clamp(request.Volume, 0f, 2f);
            options.PlaybackTime = Mathf.Clamp(request.StartOffsetSeconds, 0f, Mathf.Max(0f, clip.length));
            options.InitialDelay = Mathf.Max(0f, request.BindingDelaySeconds);
            options.DoNotAutoRecycleIfNotDonePlaying = false;

            var source = manager.PlaySound(clip, options);
            if (source == null)
            {
                return AudioBackendResult.Failure("MMSoundManager Sfx 播放失败：" + resourcesKey);
            }

            var sourceId = GetSfxSourceId(source);
            RegisterSfxSource(source, resourcesKey, request.CueId, owned: true);
            return AudioBackendResult.Success(resourcesKey, sourceId);
        }

        public MusicBackendResult Play(MusicPlaybackRequest request)
        {
            if (request.Binding == null)
            {
                return MusicBackendResult.Failure("音乐绑定为空。");
            }

            var resourcesKey = AudioAssetManifestLoader.NormalizeKey(request.Binding.ClipKey);
            if (!TryLoadMusicClip(resourcesKey, out var clip, out var failureReason))
            {
                return MusicBackendResult.Failure(failureReason);
            }

            var manager = MMSoundManager.Instance;
            if (manager == null)
            {
                return MusicBackendResult.Failure("MMSoundManager 未就绪：" + resourcesKey);
            }

            var options = MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Music;
            options.Volume = Mathf.Clamp(request.Volume, 0f, 2f);
            options.Loop = request.Binding.Loop;
            options.Fade = request.FadeInSeconds > 0f;
            options.FadeInitialVolume = options.Fade ? 0f : options.Volume;
            options.FadeDuration = Mathf.Max(0f, request.FadeInSeconds);
            options.PlaybackTime = Mathf.Clamp(
                request.StartOffsetSeconds,
                0f,
                Mathf.Max(0f, clip.length - 0.001f));
            options.Persistent = true;
            options.DoNotAutoRecycleIfNotDonePlaying = true;

            var source = manager.PlaySound(clip, options);
            if (source == null)
            {
                return MusicBackendResult.Failure("MMSoundManager Music 播放失败：" + resourcesKey);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RegisterMusicSource(source, resourcesKey);
#endif
            return MusicBackendResult.Success(
                new MusicPlaybackHandle(resourcesKey, source, GetSourceId(source)),
                resourcesKey);
        }

        public void FadeOut(MusicPlaybackHandle handle, float durationSeconds, Action completed)
        {
            var source = handle?.NativeHandle as AudioSource;
            var manager = MMSoundManager.Instance;
            if (source == null || manager == null)
            {
                completed?.Invoke();
                return;
            }

            if (durationSeconds <= 0f)
            {
                manager.FreeSound(source);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnregisterMusicSource(source);
#endif
                completed?.Invoke();
                return;
            }

            var runner = manager.gameObject.GetComponent<MusicFadeRunner>();
            if (runner == null)
            {
                runner = manager.gameObject.AddComponent<MusicFadeRunner>();
            }

            runner.Schedule(source, durationSeconds, () =>
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnregisterMusicSource(source);
#endif
                completed?.Invoke();
            });
        }

        public void Stop(MusicPlaybackHandle handle)
        {
            var source = handle?.NativeHandle as AudioSource;
            if (source == null)
            {
                return;
            }

            var manager = MMSoundManager.Instance;
            if (manager != null)
            {
                manager.FreeSound(source);
            }
            else
            {
                source.Stop();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnregisterMusicSource(source);
#endif
        }

        public void ResetPlaySession()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            musicSources.Clear();
            musicClipKeys.Clear();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public MusicBackendResult PlayPreview(MusicPreviewRequest request)
        {
            var resourcesKey = AudioAssetManifestLoader.NormalizeKey(request.ClipKey);
            if (!TryLoadMusicClip(resourcesKey, out var clip, out var failureReason))
            {
                return MusicBackendResult.Failure(failureReason);
            }

            var manager = MMSoundManager.Instance;
            if (manager == null)
            {
                return MusicBackendResult.Failure("MMSoundManager 未就绪：" + resourcesKey);
            }

            var options = MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Music;
            options.Volume = Mathf.Clamp(DecibelsToLinear(request.VolumeDb), 0f, 2f);
            options.Loop = request.Loop;
            options.Fade = request.FadeInSeconds > 0f;
            options.FadeInitialVolume = options.Fade ? 0f : options.Volume;
            options.FadeDuration = Mathf.Max(0f, request.FadeInSeconds);
            options.PlaybackTime = Mathf.Clamp(
                request.StartOffsetSeconds,
                0f,
                Mathf.Max(0f, clip.length - 0.001f));
            options.Persistent = true;
            options.DoNotAutoRecycleIfNotDonePlaying = true;

            var source = manager.PlaySound(clip, options);
            if (source == null)
            {
                return MusicBackendResult.Failure("MMSoundManager Music Preview 播放失败：" + resourcesKey);
            }

            RegisterMusicSource(source, resourcesKey);
            return MusicBackendResult.Success(
                new MusicPlaybackHandle(resourcesKey, source, GetSourceId(source)),
                resourcesKey);
        }

        public IReadOnlyList<SfxTrackSourceSnapshot> GetPlayingSfxSources()
        {
            var manager = MMSoundManager.Instance;
            if (manager == null)
            {
                return Array.Empty<SfxTrackSourceSnapshot>();
            }

            var sounds = manager.GetSoundsPlaying(MMSoundManager.MMSoundManagerTracks.Sfx);
            var result = new List<SfxTrackSourceSnapshot>(sounds?.Count ?? 0);
            for (var i = 0; i < (sounds?.Count ?? 0); i++)
            {
                var source = sounds[i].Source;
                if (source == null || !source.isPlaying)
                {
                    continue;
                }

                var sourceId = GetSfxSourceId(source);
                var owned = sfxOwnedIds.Contains(sourceId);
                var clipKey = ResolveSfxClipKey(source, sourceId);
                sfxCueIds.TryGetValue(sourceId, out var cueId);
                // Track for StopSfxSource without promoting escape sources to "owned".
                RegisterSfxSource(source, clipKey, cueId, owned: false);
                result.Add(new SfxTrackSourceSnapshot(
                    sourceId,
                    clipKey,
                    cueId,
                    source.time,
                    source.loop,
                    isPlaying: true,
                    claimed: owned));
            }

            return result;
        }

        public bool StopSfxSource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
            {
                return false;
            }

            if (!sfxSources.TryGetValue(sourceId, out var source) || source == null)
            {
                // Last chance: resolve from live Sfx track so unclaimed sources remain stoppable.
                source = FindLiveSfxSource(sourceId);
                if (source == null)
                {
                    sfxSources.Remove(sourceId);
                    sfxClipKeys.Remove(sourceId);
                    sfxCueIds.Remove(sourceId);
                    sfxOwnedIds.Remove(sourceId);
                    return false;
                }
            }

            var manager = MMSoundManager.Instance;
            if (manager != null)
            {
                manager.FreeSound(source);
            }
            else
            {
                source.Stop();
            }

            sfxSources.Remove(sourceId);
            sfxClipKeys.Remove(sourceId);
            sfxCueIds.Remove(sourceId);
            sfxOwnedIds.Remove(sourceId);
            return true;
        }

        public int StopAllSfxSources()
        {
            var playing = GetPlayingSfxSources();
            var stopped = 0;
            for (var i = 0; i < playing.Count; i++)
            {
                if (StopSfxSource(playing[i].SourceId))
                {
                    stopped++;
                }
            }

            return stopped;
        }

        public IReadOnlyList<SceneAudioOrphanSnapshot> GetSceneAudioOrphans()
        {
            var manager = MMSoundManager.Instance;
            var pooled = new HashSet<int>();
            if (manager != null)
            {
                CollectPooledInstanceIds(manager, MMSoundManager.MMSoundManagerTracks.Sfx, pooled);
                CollectPooledInstanceIds(manager, MMSoundManager.MMSoundManagerTracks.Music, pooled);
            }

            var sources = UnityEngine.Object.FindObjectsByType<AudioSource>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            if (sources == null || sources.Length == 0)
            {
                return Array.Empty<SceneAudioOrphanSnapshot>();
            }

            var result = new List<SceneAudioOrphanSnapshot>();
            for (var i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source == null || !source.isPlaying)
                {
                    continue;
                }

                if (pooled.Contains(source.GetInstanceID()))
                {
                    continue;
                }

                result.Add(new SceneAudioOrphanSnapshot(
                    BuildHierarchyPath(source.transform),
                    source.clip != null ? source.clip.name : string.Empty,
                    source.time,
                    source.loop));
            }

            return result;
        }

        public IReadOnlyList<MusicTrackSourceSnapshot> GetPlayingMusicSources()
        {
            var manager = MMSoundManager.Instance;
            if (manager == null)
            {
                return Array.Empty<MusicTrackSourceSnapshot>();
            }

            var sounds = manager.GetSoundsPlaying(MMSoundManager.MMSoundManagerTracks.Music);
            var result = new List<MusicTrackSourceSnapshot>(sounds?.Count ?? 0);
            for (var i = 0; i < (sounds?.Count ?? 0); i++)
            {
                var source = sounds[i].Source;
                if (source == null || !source.isPlaying)
                {
                    continue;
                }

                var sourceId = GetSourceId(source);
                var clipKey = ResolveClipKey(source.clip);
                RegisterMusicSource(source, clipKey);
                result.Add(new MusicTrackSourceSnapshot(sourceId, clipKey, source.time));
            }

            return result;
        }

        public double GetPlaybackPosition(MusicPlaybackHandle handle)
        {
            var source = handle?.NativeHandle as AudioSource;
            return source == null ? 0d : Math.Max(0d, source.time);
        }

        public void Pause(MusicPlaybackHandle handle)
        {
            var source = handle?.NativeHandle as AudioSource;
            if (source == null)
            {
                return;
            }

            var manager = MMSoundManager.Instance;
            var fadeRunner = manager == null ? null : manager.gameObject.GetComponent<MusicFadeRunner>();
            if (fadeRunner != null && fadeRunner.TryPause(source))
            {
                return;
            }

            if (source.isPlaying)
            {
                source.Pause();
            }
        }

        public void Resume(MusicPlaybackHandle handle, double positionSeconds)
        {
            var source = handle?.NativeHandle as AudioSource;
            if (source == null)
            {
                return;
            }

            if (source.clip != null)
            {
                source.time = Mathf.Clamp(
                    (float)Math.Max(0d, positionSeconds),
                    0f,
                    Mathf.Max(0f, source.clip.length - 0.001f));
            }

            source.Play();
            var manager = MMSoundManager.Instance;
            var fadeRunner = manager == null ? null : manager.gameObject.GetComponent<MusicFadeRunner>();
            fadeRunner?.TryResume(source);
        }

        public void StopMusicTrackSource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
            {
                return;
            }

            if (musicSources.TryGetValue(sourceId, out var source) && source != null)
            {
                var manager = MMSoundManager.Instance;
                if (manager != null)
                {
                    manager.FreeSound(source);
                }
                else
                {
                    source.Stop();
                }
            }

            musicSources.Remove(sourceId);
        }
#endif

        public sealed class MusicFadeRunner : MonoBehaviour
        {
            private readonly Dictionary<int, FadeState> fadeStates = new Dictionary<int, FadeState>();

            public void Schedule(AudioSource source, float durationSeconds, Action completed)
            {
                StartCoroutine(Fade(source, durationSeconds, completed));
            }

            public bool TryPause(AudioSource source)
            {
                if (source == null || !fadeStates.TryGetValue(source.GetInstanceID(), out var state))
                {
                    return false;
                }

                state.Paused = true;
                if (source.isPlaying)
                {
                    source.Pause();
                }

                return true;
            }

            public bool TryResume(AudioSource source)
            {
                if (source == null || !fadeStates.TryGetValue(source.GetInstanceID(), out var state))
                {
                    return false;
                }

                state.Paused = false;
                return true;
            }

            private System.Collections.IEnumerator Fade(
                AudioSource source,
                float durationSeconds,
                Action completed)
            {
                var sourceId = source == null ? 0 : source.GetInstanceID();
                var state = new FadeState();
                if (sourceId != 0)
                {
                    fadeStates[sourceId] = state;
                }

                try
                {
                    var initialVolume = source == null ? 0f : Mathf.Max(0f, source.volume);
                    var elapsed = 0f;
                    while (source != null
                        && (source.isPlaying || state.Paused)
                        && elapsed < durationSeconds)
                    {
                        if (!state.Paused)
                        {
                            elapsed += Time.unscaledDeltaTime;
                            source.volume = Mathf.Lerp(initialVolume, 0f, Mathf.Clamp01(elapsed / durationSeconds));
                        }

                        yield return null;
                    }

                    if (source != null && !state.Paused && source.isPlaying)
                    {
                        var manager = MMSoundManager.Instance;
                        if (manager != null)
                        {
                            manager.FreeSound(source);
                        }
                        else
                        {
                            source.Stop();
                        }
                    }

                    if (!state.Paused)
                    {
                        completed?.Invoke();
                    }
                }
                finally
                {
                    if (sourceId != 0)
                    {
                        fadeStates.Remove(sourceId);
                    }
                }
            }

            private sealed class FadeState
            {
                public bool Paused;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool TryLoadMusicClip(string resourcesKey, out AudioClip clip, out string failureReason)
        {
            clip = null;
            failureReason = string.Empty;
            if (!AudioAssetManifestLoader.TryGet(resourcesKey, out var entry)
                || entry == null
                || !string.Equals(entry.kind, "BGM", StringComparison.OrdinalIgnoreCase))
            {
                failureReason = "正式音频 manifest 未登记 BGM 素材键：" + resourcesKey;
                return false;
            }

            clip = Resources.Load<AudioClip>(resourcesKey);
            if (clip == null)
            {
                failureReason = "Resources BGM 素材不存在：" + resourcesKey;
                return false;
            }

            return true;
        }
#else
        private bool TryLoadMusicClip(string resourcesKey, out AudioClip clip, out string failureReason)
        {
            clip = null;
            failureReason = string.Empty;
            if (!AudioAssetManifestLoader.TryGet(resourcesKey, out var entry)
                || entry == null
                || !string.Equals(entry.kind, "BGM", StringComparison.OrdinalIgnoreCase))
            {
                failureReason = "正式音频 manifest 未登记 BGM 素材键：" + resourcesKey;
                return false;
            }

            clip = Resources.Load<AudioClip>(resourcesKey);
            if (clip == null)
            {
                failureReason = "Resources BGM 素材不存在：" + resourcesKey;
                return false;
            }

            return true;
        }
#endif

        public int FadeOutPlayingForCue(string cueId, float fadeOutSeconds)
        {
            if (string.IsNullOrEmpty(cueId))
            {
                return 0;
            }

            var manager = MMSoundManager.Instance;
            if (manager == null)
            {
                return 0;
            }

            var toFade = new List<AudioSource>();
            foreach (var pair in sfxCueIds)
            {
                if (!string.Equals(pair.Value, cueId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!sfxSources.TryGetValue(pair.Key, out var source) || source == null || !source.isPlaying)
                {
                    continue;
                }

                toFade.Add(source);
            }

            if (toFade.Count == 0)
            {
                return 0;
            }

            var clampedFade = Mathf.Max(0f, fadeOutSeconds);
            MusicFadeRunner fadeRunner = null;
            if (clampedFade > 0f)
            {
                fadeRunner = manager.gameObject.GetComponent<MusicFadeRunner>();
                if (fadeRunner == null)
                {
                    fadeRunner = manager.gameObject.AddComponent<MusicFadeRunner>();
                }
            }

            for (var i = 0; i < toFade.Count; i++)
            {
                var source = toFade[i];
                if (clampedFade <= 0f)
                {
                    manager.FreeSound(source);
                    UnregisterSfxSource(source);
                    continue;
                }

                fadeRunner.Schedule(source, clampedFade, () => UnregisterSfxSource(source));
            }

            return toFade.Count;
        }

        private void RegisterSfxSource(AudioSource source, string resourcesKey, string cueId, bool owned)
        {
            if (source == null)
            {
                return;
            }

            var sourceId = GetSfxSourceId(source);
            sfxSources[sourceId] = source;
            if (!string.IsNullOrEmpty(resourcesKey))
            {
                sfxClipKeys[sourceId] = resourcesKey;
            }

            if (!string.IsNullOrEmpty(cueId))
            {
                sfxCueIds[sourceId] = cueId;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (owned)
            {
                sfxOwnedIds.Add(sourceId);
            }
#endif
        }

        private void UnregisterSfxSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            var sourceId = GetSfxSourceId(source);
            sfxSources.Remove(sourceId);
            sfxClipKeys.Remove(sourceId);
            sfxCueIds.Remove(sourceId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            sfxOwnedIds.Remove(sourceId);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private AudioSource FindLiveSfxSource(string sourceId)
        {
            var manager = MMSoundManager.Instance;
            if (manager == null || string.IsNullOrEmpty(sourceId))
            {
                return null;
            }

            var sounds = manager.GetSoundsPlaying(MMSoundManager.MMSoundManagerTracks.Sfx);
            for (var i = 0; i < (sounds?.Count ?? 0); i++)
            {
                var source = sounds[i].Source;
                if (source != null && string.Equals(GetSfxSourceId(source), sourceId, StringComparison.Ordinal))
                {
                    return source;
                }
            }

            return null;
        }

        private static void CollectPooledInstanceIds(
            MMSoundManager manager,
            MMSoundManager.MMSoundManagerTracks track,
            HashSet<int> into)
        {
            if (manager == null || into == null)
            {
                return;
            }

            var sounds = manager.GetSoundsPlaying(track);
            for (var i = 0; i < (sounds?.Count ?? 0); i++)
            {
                var source = sounds[i].Source;
                if (source != null)
                {
                    into.Add(source.GetInstanceID());
                }
            }
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var parts = new List<string>(8);
            var current = transform;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        private string ResolveSfxClipKey(AudioSource source, string sourceId)
        {
            if (!string.IsNullOrEmpty(sourceId) && sfxClipKeys.TryGetValue(sourceId, out var resourcesKey))
            {
                return resourcesKey;
            }

            if (source?.clip == null)
            {
                return string.Empty;
            }

            return "<unknown>" + source.clip.name;
        }

        private void RegisterMusicSource(AudioSource source, string resourcesKey)
        {
            if (source == null)
            {
                return;
            }

            var sourceId = GetSourceId(source);
            musicSources[sourceId] = source;
            if (source.clip != null && !string.IsNullOrEmpty(resourcesKey))
            {
                musicClipKeys[source.clip] = resourcesKey;
            }
        }

        private void UnregisterMusicSource(AudioSource source)
        {
            if (source != null)
            {
                musicSources.Remove(GetSourceId(source));
            }
        }

        private string ResolveClipKey(AudioClip clip)
        {
            if (clip == null)
            {
                return string.Empty;
            }

            if (musicClipKeys.TryGetValue(clip, out var resourcesKey))
            {
                return resourcesKey;
            }

            return "<unknown>" + clip.name;
        }
#endif

        private static string GetSourceId(AudioSource source)
        {
            return source == null ? string.Empty : "music-source:" + source.GetInstanceID();
        }

        private static string GetSfxSourceId(AudioSource source)
        {
            return source == null ? string.Empty : "sfx-source:" + source.GetInstanceID();
        }

        private static float DecibelsToLinear(float decibels)
        {
            return Mathf.Pow(10f, decibels / 20f);
        }
    }
}
