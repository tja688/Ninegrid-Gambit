using NineGrid.Content.Audio;
using MoreMountains.Tools;
using UnityEngine;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 项目唯一 SFX 播放 Adapter：绑定系统给出正式 Resources 键，本类解析并交给 MMSoundManager Sfx 轨。
    /// </summary>
    public sealed class MMSoundManagerAudioPlaybackAdapter : IAudioPlaybackAdapter
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

            var options = MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
            options.Volume = Mathf.Clamp(request.Volume, 0f, 2f);
            options.PlaybackTime = Mathf.Clamp(request.StartOffsetSeconds, 0f, Mathf.Max(0f, clip.length));
            options.InitialDelay = Mathf.Max(0f, request.BindingDelaySeconds);
            options.DoNotAutoRecycleIfNotDonePlaying = false;

            var source = MMSoundManager.Instance.PlaySound(clip, options);
            return source == null
                ? AudioBackendResult.Failure("MMSoundManager Sfx 播放失败：" + resourcesKey)
                : AudioBackendResult.Success(resourcesKey);
        }
    }
}
