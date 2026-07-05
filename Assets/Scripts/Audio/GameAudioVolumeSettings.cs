using System;
using UnityEngine;

namespace NineGrid.Audio
{
    /// <summary>
    /// 玩家音量偏好：主音量 × 音乐 / 音效通道，持久化到 PlayerPrefs。
    /// </summary>
    public static class GameAudioVolumeSettings
    {
        const string KeyMaster = "NineGrid.Audio.MasterVolume";
        const string KeyMusic = "NineGrid.Audio.MusicVolume";
        const string KeySfx = "NineGrid.Audio.SfxVolume";
        const string KeyVersion = "NineGrid.Audio.VolumeVersion";
        const int CurrentVersion = 1;

        public const float DefaultMaster = 1f;
        public const float DefaultMusic = 0.6f;
        public const float DefaultSfx = 0.6f;

        static bool _loaded;
        static float _master = DefaultMaster;
        static float _music = DefaultMusic;
        static float _sfx = DefaultSfx;

        public static event Action Changed;

        public static float Master
        {
            get
            {
                EnsureLoaded();
                return _master;
            }
            set
            {
                EnsureLoaded();
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(_master, clamped))
                {
                    return;
                }

                _master = clamped;
                Save();
                Changed?.Invoke();
            }
        }

        public static float Music
        {
            get
            {
                EnsureLoaded();
                return _music;
            }
            set
            {
                EnsureLoaded();
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(_music, clamped))
                {
                    return;
                }

                _music = clamped;
                Save();
                Changed?.Invoke();
            }
        }

        public static float Sfx
        {
            get
            {
                EnsureLoaded();
                return _sfx;
            }
            set
            {
                EnsureLoaded();
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(_sfx, clamped))
                {
                    return;
                }

                _sfx = clamped;
                Save();
                Changed?.Invoke();
            }
        }

        public static float EffectiveMusic => Master * Music;
        public static float EffectiveSfx => Master * Sfx;

        public static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _master = PlayerPrefs.GetFloat(KeyMaster, DefaultMaster);
            _music = PlayerPrefs.GetFloat(KeyMusic, DefaultMusic);
            _sfx = PlayerPrefs.GetFloat(KeySfx, DefaultSfx);

            if (PlayerPrefs.GetInt(KeyVersion, 0) < CurrentVersion)
            {
                _master = DefaultMaster;
                _music = DefaultMusic;
                _sfx = DefaultSfx;
                Save();
                PlayerPrefs.SetInt(KeyVersion, CurrentVersion);
                PlayerPrefs.Save();
            }

            _loaded = true;
        }

        static void Save()
        {
            PlayerPrefs.SetFloat(KeyMaster, _master);
            PlayerPrefs.SetFloat(KeyMusic, _music);
            PlayerPrefs.SetFloat(KeySfx, _sfx);
            PlayerPrefs.Save();
        }
    }
}
