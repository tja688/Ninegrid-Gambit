using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.Audio
{
    /// <summary>
    /// 把 <see cref="GameAudioVolumeSettings"/> 应用到 AMP 的 MusicManager / SFXManager。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAudioVolumeController : MonoBehaviour
    {
        [SerializeField] bool logApply;

        void OnEnable()
        {
            GameAudioVolumeSettings.Changed += Apply;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            GameAudioVolumeSettings.Changed -= Apply;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void Start()
        {
            GameAudioVolumeSettings.EnsureLoaded();
            Apply();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Apply();
        }

        public void Apply()
        {
            GameAudioVolumeSettings.EnsureLoaded();

            float music = GameAudioVolumeSettings.EffectiveMusic;
            float sfx = GameAudioVolumeSettings.EffectiveSfx;

            if (MusicManager.Main != null)
            {
                MusicManager.Main.MasterVolume = music;
            }

            if (SFXManager.Main != null)
            {
                SFXManager.Main.MasterVolume = sfx;
            }

            if (logApply)
            {
                Debug.Log(
                    $"[AudioVolume] master={GameAudioVolumeSettings.Master:F2} " +
                    $"music={GameAudioVolumeSettings.Music:F2}→{music:F2} " +
                    $"sfx={GameAudioVolumeSettings.Sfx:F2}→{sfx:F2}");
            }
        }
    }
}
