using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Audio
{
    /// <summary>
    /// Master audio configuration SO. Holds all 106 AudioCueEntries mapped from the sound design document.
    /// GameAudioService looks up cues by AudioKey at runtime to play via Audio Manager Pro.
    /// Designers and AI tools can manage each cue's SFXObject reference, volume, pitch, loop, etc. here.
    /// </summary>
    [CreateAssetMenu(fileName = "GameAudioCueDatabase", menuName = "NineGrid/Audio Cue Database", order = 0)]
    public sealed class GameAudioCueSO : ScriptableObject
    {
        [Tooltip("All audio cue entries. The lookup dictionary is built at Awake from this array.")]
        public AudioCueEntry[] cues = new AudioCueEntry[0];

        Dictionary<AudioKey, AudioCueEntry> _lookup;

        /// <summary>Build or rebuild the internal lookup dictionary from the cues array.</summary>
        public void RebuildLookup()
        {
            _lookup = new Dictionary<AudioKey, AudioCueEntry>();
            if (cues == null) return;
            foreach (var entry in cues)
            {
                if (entry == null) continue;
                _lookup[entry.key] = entry;
            }
        }

        /// <summary>Get a cue entry by key. Returns null if not found.</summary>
        public AudioCueEntry GetCue(AudioKey key)
        {
            if (_lookup == null) RebuildLookup();
            _lookup.TryGetValue(key, out var entry);
            return entry;
        }

        /// <summary>Try to get a cue entry by key.</summary>
        public bool TryGetCue(AudioKey key, out AudioCueEntry entry)
        {
            if (_lookup == null) RebuildLookup();
            return _lookup.TryGetValue(key, out entry);
        }

        void OnEnable()
        {
            RebuildLookup();
        }

        void OnValidate()
        {
            // Rebuild lookup when the array is modified in the Inspector
            RebuildLookup();
        }
    }
}
