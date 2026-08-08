using System;
using MoreMountains.Tools;
using NineGrid.Content.Audio;
using UnityEngine;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 项目唯一音频播放 Adapter：SFX 与 BGM 均由深模块给出正式 Resources 键，分别进入 MMSoundManager Sfx/Music 轨。
    /// </summary>
    public sealed class MMSoundManagerAudioPlaybackAdapter : IAudioPlaybackAdapter, IMusicPlaybackAdapter
    {
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
            return source == null
                ? AudioBackendResult.Failure("MMSoundManager Sfx 播放失败：" + resourcesKey)
                : AudioBackendResult.Success(resourcesKey);
        }

        public MusicBackendResult Play(MusicPlaybackRequest request)
        {
            if (request.Binding == null)
            {
                return MusicBackendResult.Failure("音乐绑定为空。");
            }

            var resourcesKey = AudioAssetManifestLoader.NormalizeKey(request.Binding.ClipKey);
            if (!AudioAssetManifestLoader.TryGet(resourcesKey, out var entry)
                || entry == null
                || !string.Equals(entry.kind, "BGM", StringComparison.OrdinalIgnoreCase))
            {
                return MusicBackendResult.Failure("正式音频 manifest 未登记 BGM 素材键：" + resourcesKey);
            }

            var clip = Resources.Load<AudioClip>(resourcesKey);
            if (clip == null)
            {
                return MusicBackendResult.Failure("Resources BGM 素材不存在：" + resourcesKey);
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

            return MusicBackendResult.Success(
                new MusicPlaybackHandle(resourcesKey, source),
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
                completed?.Invoke();
                return;
            }

            var runner = manager.gameObject.GetComponent<MusicFadeRunner>();
            if (runner == null)
            {
                runner = manager.gameObject.AddComponent<MusicFadeRunner>();
            }

            runner.Schedule(source, durationSeconds, completed);
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
        }

        public sealed class MusicFadeRunner : MonoBehaviour
        {
            public void Schedule(AudioSource source, float durationSeconds, Action completed)
            {
                StartCoroutine(Fade(source, durationSeconds, completed));
            }

            private System.Collections.IEnumerator Fade(
                AudioSource source,
                float durationSeconds,
                Action completed)
            {
                var initialVolume = source == null ? 0f : Mathf.Max(0f, source.volume);
                var elapsed = 0f;
                while (source != null && source.isPlaying && elapsed < durationSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    source.volume = Mathf.Lerp(initialVolume, 0f, Mathf.Clamp01(elapsed / durationSeconds));
                    yield return null;
                }

                if (source != null)
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

                completed?.Invoke();
            }
        }

    }
}
