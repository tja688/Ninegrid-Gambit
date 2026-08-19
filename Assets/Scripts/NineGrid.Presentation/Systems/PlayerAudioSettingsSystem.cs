using System;
using MoreMountains.Tools;
using NineGrid.Core;
using QFramework;
using UnityEngine;
using UnityEngine.Audio;

namespace NineGrid.Presentation.Systems
{
    public enum PlayerAudioBus
    {
        Master,
        Bgm,
        Sfx,
    }

    /// <summary>Immutable player-owned values. Author defaults are held separately by the settings System.</summary>
    public readonly struct PlayerAudioSettingsSnapshot : IEquatable<PlayerAudioSettingsSnapshot>
    {
        public PlayerAudioSettingsSnapshot(
            float masterVolume,
            bool masterMuted,
            float bgmVolume,
            bool bgmMuted,
            float sfxVolume,
            bool sfxMuted)
        {
            MasterVolume = Clamp(masterVolume);
            MasterMuted = masterMuted;
            BgmVolume = Clamp(bgmVolume);
            BgmMuted = bgmMuted;
            SfxVolume = Clamp(sfxVolume);
            SfxMuted = sfxMuted;
        }

        public float MasterVolume { get; }
        public bool MasterMuted { get; }
        public float BgmVolume { get; }
        public bool BgmMuted { get; }
        public float SfxVolume { get; }
        public bool SfxMuted { get; }

        public float Volume(PlayerAudioBus bus)
        {
            return bus == PlayerAudioBus.Master ? MasterVolume
                : bus == PlayerAudioBus.Bgm ? BgmVolume
                : SfxVolume;
        }

        public bool IsMuted(PlayerAudioBus bus)
        {
            return bus == PlayerAudioBus.Master ? MasterMuted
                : bus == PlayerAudioBus.Bgm ? BgmMuted
                : SfxMuted;
        }

        public float EffectiveVolume(PlayerAudioBus bus)
        {
            return IsMuted(bus) ? 0f : Volume(bus);
        }

        public PlayerAudioSettingsSnapshot WithVolume(PlayerAudioBus bus, float volume)
        {
            volume = Clamp(volume);
            return bus == PlayerAudioBus.Master
                ? new PlayerAudioSettingsSnapshot(volume, MasterMuted, BgmVolume, BgmMuted, SfxVolume, SfxMuted)
                : bus == PlayerAudioBus.Bgm
                    ? new PlayerAudioSettingsSnapshot(MasterVolume, MasterMuted, volume, BgmMuted, SfxVolume, SfxMuted)
                    : new PlayerAudioSettingsSnapshot(MasterVolume, MasterMuted, BgmVolume, BgmMuted, volume, SfxMuted);
        }

        public PlayerAudioSettingsSnapshot WithMuted(PlayerAudioBus bus, bool muted)
        {
            return bus == PlayerAudioBus.Master
                ? new PlayerAudioSettingsSnapshot(MasterVolume, muted, BgmVolume, BgmMuted, SfxVolume, SfxMuted)
                : bus == PlayerAudioBus.Bgm
                    ? new PlayerAudioSettingsSnapshot(MasterVolume, MasterMuted, BgmVolume, muted, SfxVolume, SfxMuted)
                    : new PlayerAudioSettingsSnapshot(MasterVolume, MasterMuted, BgmVolume, BgmMuted, SfxVolume, muted);
        }

        public bool Equals(PlayerAudioSettingsSnapshot other)
        {
            return Mathf.Approximately(MasterVolume, other.MasterVolume)
                && MasterMuted == other.MasterMuted
                && Mathf.Approximately(BgmVolume, other.BgmVolume)
                && BgmMuted == other.BgmMuted
                && Mathf.Approximately(SfxVolume, other.SfxVolume)
                && SfxMuted == other.SfxMuted;
        }

        public override bool Equals(object obj) => obj is PlayerAudioSettingsSnapshot other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(MasterVolume, MasterMuted, BgmVolume, BgmMuted, SfxVolume, SfxMuted);

        private static float Clamp(float value) => Mathf.Clamp01(value);
    }

    public interface IPlayerAudioSettingsStore
    {
        bool TryGetString(string key, out string value);
        void SetString(string key, string value);
        void DeleteKey(string key);
        void Save();
    }

    public interface IPlayerAudioBusApplier
    {
        void Apply(PlayerAudioSettingsSnapshot settings);
    }

    /// <summary>Stable player-facing seam. Never exposes authoring JSON or its mutable editor work copies.</summary>
    public interface IPlayerAudioSettingsSystem : ISystem
    {
        PlayerAudioSettingsSnapshot AuthorDefaults { get; }
        PlayerAudioSettingsSnapshot Current { get; }
        event Action<PlayerAudioSettingsSnapshot> Changed;

        void SetVolume(PlayerAudioBus bus, float volume);
        void SetMuted(PlayerAudioBus bus, bool muted);
        void ResetToAuthorDefaults();
    }

    public sealed class PlayerAudioSettingsSystem : AbstractSystem, IPlayerAudioSettingsSystem
    {
        private const string PreferencesKey = "NineGrid.PlayerAudioSettings.v1";
        private readonly PlayerAudioSettingsSnapshot mAuthorDefaults;
        private readonly IPlayerAudioSettingsStore mStore;
        private readonly IPlayerAudioBusApplier mApplier;
        private PlayerAudioSettingsSnapshot mCurrent;

        public PlayerAudioSettingsSystem(
            PlayerAudioSettingsSnapshot authorDefaults,
            IPlayerAudioSettingsStore store = null,
            IPlayerAudioBusApplier applier = null)
        {
            mAuthorDefaults = authorDefaults;
            mStore = store ?? new PlayerPrefsAudioSettingsStore();
            mApplier = applier ?? new MMSoundManagerAudioBusApplier();
            mCurrent = LoadOrDefault();
            mApplier.Apply(mCurrent);
        }

        public PlayerAudioSettingsSnapshot AuthorDefaults => mAuthorDefaults;
        public PlayerAudioSettingsSnapshot Current => mCurrent;
        public event Action<PlayerAudioSettingsSnapshot> Changed;

        public static IPlayerAudioSettingsSystem EnsureRegistered(IArchitecture architecture = null)
        {
            var arch = architecture ?? NineGridArchitecture.Interface;
            if (arch == null)
            {
                throw new InvalidOperationException("Architecture is not available for PlayerAudioSettingsSystem.");
            }
            var existing = arch.GetSystem<IPlayerAudioSettingsSystem>();
            if (existing != null)
            {
                if (existing is PlayerAudioSettingsSystem system)
                {
                    system.ApplyToAudioBus();
                }

                return existing;
            }

            var created = new PlayerAudioSettingsSystem(ReadAuthorDefaults());
            arch.RegisterSystem<IPlayerAudioSettingsSystem>(created);
            return created;
        }

        public void SetVolume(PlayerAudioBus bus, float volume)
        {
            SetCurrent(mCurrent.WithVolume(bus, volume));
        }

        public void SetMuted(PlayerAudioBus bus, bool muted)
        {
            SetCurrent(mCurrent.WithMuted(bus, muted));
        }

        public void ResetToAuthorDefaults()
        {
            mStore.DeleteKey(PreferencesKey);
            mStore.Save();
            ApplyAndNotify(mAuthorDefaults);
        }
        public void ApplyToAudioBus()
        {
            mApplier.Apply(mCurrent);
        }

        protected override void OnInit()
        {
        }

        private void SetCurrent(PlayerAudioSettingsSnapshot next)
        {
            if (mCurrent.Equals(next))
            {
                return;
            }

            mStore.SetString(PreferencesKey, JsonUtility.ToJson(PlayerAudioSettingsDto.From(next)));
            mStore.Save();
            ApplyAndNotify(next);
        }

        private void ApplyAndNotify(PlayerAudioSettingsSnapshot next)
        {
            mCurrent = next;
            mApplier.Apply(mCurrent);
            Changed?.Invoke(mCurrent);
        }

        private PlayerAudioSettingsSnapshot LoadOrDefault()
        {
            if (!mStore.TryGetString(PreferencesKey, out var json) || string.IsNullOrWhiteSpace(json))
            {
                return mAuthorDefaults;
            }

            try
            {
                return JsonUtility.FromJson<PlayerAudioSettingsDto>(json).ToSnapshot(mAuthorDefaults);
            }
            catch (Exception)
            {
                return mAuthorDefaults;
            }
        }

        public static readonly PlayerAudioSettingsSnapshot FactoryDefaults =
            new PlayerAudioSettingsSnapshot(1f, false, 0.5f, false, 0.75f, false);

        private static PlayerAudioSettingsSnapshot ReadAuthorDefaults()
        {
            var settings = Resources.Load<MMSoundManagerSettingsSO>("MMSoundManagerSettings");
            var source = settings?.Settings;
            return source == null
                ? FactoryDefaults
                : new PlayerAudioSettingsSnapshot(
                    source.MasterVolume,
                    !source.MasterOn,
                    source.MusicVolume,
                    !source.MusicOn,
                    source.SfxVolume,
                    !source.SfxOn);
        }

        [Serializable]
        private sealed class PlayerAudioSettingsDto
        {
            public float masterVolume;
            public bool masterMuted;
            public float bgmVolume;
            public bool bgmMuted;
            public float sfxVolume;
            public bool sfxMuted;

            public static PlayerAudioSettingsDto From(PlayerAudioSettingsSnapshot snapshot)
            {
                return new PlayerAudioSettingsDto
                {
                    masterVolume = snapshot.MasterVolume,
                    masterMuted = snapshot.MasterMuted,
                    bgmVolume = snapshot.BgmVolume,
                    bgmMuted = snapshot.BgmMuted,
                    sfxVolume = snapshot.SfxVolume,
                    sfxMuted = snapshot.SfxMuted,
                };
            }

            public PlayerAudioSettingsSnapshot ToSnapshot(PlayerAudioSettingsSnapshot fallback)
            {
                return new PlayerAudioSettingsSnapshot(
                    masterVolume,
                    masterMuted,
                    bgmVolume,
                    bgmMuted,
                    sfxVolume,
                    sfxMuted);
            }
        }
    }

    /// <summary>Applies player values to the live mixer without mutating its authoring ScriptableObject.</summary>
    public sealed class MMSoundManagerAudioBusApplier : IPlayerAudioBusApplier
    {
        private const float SilenceDecibels = -80f;

        public void Apply(PlayerAudioSettingsSnapshot settings)
        {
            var manager = MMSoundManager.Instance;
            var soundSettings = manager?.settingsSo;
            var mixer = soundSettings?.TargetAudioMixer;
            var names = soundSettings?.Settings;
            if (mixer == null || names == null)
            {
                return;
            }

            SetMixerVolume(mixer, names.MasterVolumeParameter, settings.EffectiveVolume(PlayerAudioBus.Master));
            SetMixerVolume(mixer, names.MusicVolumeParameter, settings.EffectiveVolume(PlayerAudioBus.Bgm));
            SetMixerVolume(mixer, names.SfxVolumeParameter, settings.EffectiveVolume(PlayerAudioBus.Sfx));
        }

        private static void SetMixerVolume(AudioMixer mixer, string parameter, float linearVolume)
        {
            if (mixer == null || string.IsNullOrWhiteSpace(parameter))
            {
                return;
            }

            var decibels = linearVolume <= 0f
                ? SilenceDecibels
                : Mathf.Clamp(20f * Mathf.Log10(linearVolume), SilenceDecibels, 0f);
            mixer.SetFloat(parameter, decibels);
        }
    }

    internal sealed class PlayerPrefsAudioSettingsStore : IPlayerAudioSettingsStore
    {
        public bool TryGetString(string key, out string value)
        {
            if (!PlayerPrefs.HasKey(key))
            {
                value = string.Empty;
                return false;
            }

            value = PlayerPrefs.GetString(key);
            return true;
        }

        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value ?? string.Empty);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Save() => PlayerPrefs.Save();
    }
}
