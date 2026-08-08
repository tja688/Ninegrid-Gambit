using System;
using System.Collections.Generic;

namespace NineGrid.Content.Audio
{
    public enum DesiredMusicState
    {
        MainMenu = 0,
        RunExploration = 1,
        Battle = 2,
        BossBattle = 3,
        Victory = 4,
        Defeat = 5,
    }

    public readonly struct MusicStateRequest
    {
        public MusicStateRequest(DesiredMusicState state, string stableSource)
        {
            State = state;
            StableSource = stableSource ?? string.Empty;
        }

        public DesiredMusicState State { get; }
        public string StableSource { get; }
        public bool HasStableSource => !string.IsNullOrWhiteSpace(StableSource);
    }

    [Serializable]
    public sealed class MusicBindingCatalogDto
    {
        public int schemaVersion;
        public string ticket;
        public MusicBindingDto[] bindings;
    }

    [Serializable]
    public sealed class MusicBindingDto
    {
        public string state;
        public bool enabled = true;
        public string clipKey;
        public float volumeDb = -6f;
        public float startOffsetSeconds;
        public float fadeInSeconds = 0.15f;
        public float fadeOutSeconds = 0.2f;
        public bool loop = true;
    }

    public sealed class MusicBinding
    {
        public MusicBinding(
            DesiredMusicState state,
            bool enabled,
            string clipKey,
            float volumeDb,
            float startOffsetSeconds,
            float fadeInSeconds,
            float fadeOutSeconds,
            bool loop)
        {
            State = state;
            Enabled = enabled;
            ClipKey = clipKey ?? string.Empty;
            VolumeDb = volumeDb;
            StartOffsetSeconds = startOffsetSeconds;
            FadeInSeconds = fadeInSeconds;
            FadeOutSeconds = fadeOutSeconds;
            Loop = loop;
        }

        public DesiredMusicState State { get; }
        public bool Enabled { get; }
        public string ClipKey { get; }
        public float VolumeDb { get; }
        public float StartOffsetSeconds { get; }
        public float FadeInSeconds { get; }
        public float FadeOutSeconds { get; }
        public bool Loop { get; }
    }

    public sealed class MusicBindingCatalog
    {
        private readonly MusicBinding[] mBindings;

        private MusicBindingCatalog(MusicBinding[] bindings)
        {
            mBindings = bindings ?? Array.Empty<MusicBinding>();
        }

        public IReadOnlyList<MusicBinding> Bindings => mBindings;

        public static MusicBindingCatalog FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new MusicBindingCatalog(Array.Empty<MusicBinding>());
            }

            MusicBindingCatalogDto dto;
            try
            {
                dto = UnityEngine.JsonUtility.FromJson<MusicBindingCatalogDto>(json);
            }
            catch (Exception)
            {
                return new MusicBindingCatalog(Array.Empty<MusicBinding>());
            }

            var rows = dto?.bindings ?? Array.Empty<MusicBindingDto>();
            var bindings = new System.Collections.Generic.List<MusicBinding>(rows.Length);
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null
                    || !Enum.TryParse(row.state, ignoreCase: true, out DesiredMusicState state))
                {
                    continue;
                }

                bindings.Add(new MusicBinding(
                    state,
                    row.enabled,
                    row.clipKey,
                    row.volumeDb,
                    row.startOffsetSeconds,
                    row.fadeInSeconds,
                    row.fadeOutSeconds,
                    row.loop));
            }

            return new MusicBindingCatalog(bindings.ToArray());
        }

        public static MusicBindingCatalog LoadFromResources()
        {
            var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(MusicBindingCatalogPaths.ManifestResourcesKey);
            return FromJson(asset == null ? string.Empty : asset.text);
        }

        public bool TryResolve(DesiredMusicState state, out MusicBinding binding)
        {
            for (var i = mBindings.Length - 1; i >= 0; i--)
            {
                if (mBindings[i].State == state)
                {
                    binding = mBindings[i];
                    return true;
                }
            }

            binding = null;
            return false;
        }
    }

    public static class MusicBindingCatalogPaths
    {
        public const string ManifestAssetPath = "Assets/Resources/audio/audio_music.json";
        public const string ManifestResourcesKey = "audio/audio_music";
    }
}
