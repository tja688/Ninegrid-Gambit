using System;
using System.Collections.Generic;
using System.IO;
using NineGrid.Content.Audio;
using UnityEngine;

namespace NineGrid.Content.Audio
{
    /// <summary>
    /// Fixed Resources-relative keys. Keys include the formal "audio" namespace;
    /// runtime never scans a directory or strips the root from a catalog key.
    /// </summary>
    public static class AudioAssetManifestLoader
    {
        private static AudioAssetManifest sManifest;
        private static Dictionary<string, AudioAssetManifestEntry> sEntries;

        public static AudioAssetManifest Load()
        {
            if (sManifest != null)
            {
                return sManifest;
            }

            var asset = Resources.Load<TextAsset>(AudioAssetPaths.ManifestResourcesKey);
            if (asset == null)
            {
                Debug.LogError("[AudioAssetManifest] Missing formal manifest: " + AudioAssetPaths.ManifestResourcesKey);
                sManifest = new AudioAssetManifest { entries = Array.Empty<AudioAssetManifestEntry>() };
                sEntries = new Dictionary<string, AudioAssetManifestEntry>(StringComparer.OrdinalIgnoreCase);
                return sManifest;
            }

            try
            {
                sManifest = JsonUtility.FromJson<AudioAssetManifest>(asset.text);
            }
            catch (Exception ex)
            {
                Debug.LogError("[AudioAssetManifest] Invalid manifest: " + ex.Message);
                sManifest = new AudioAssetManifest { entries = Array.Empty<AudioAssetManifestEntry>() };
            }

            if (sManifest == null)
            {
                sManifest = new AudioAssetManifest { entries = Array.Empty<AudioAssetManifestEntry>() };
            }

            sEntries = new Dictionary<string, AudioAssetManifestEntry>(StringComparer.OrdinalIgnoreCase);
            var entries = sManifest.entries ?? Array.Empty<AudioAssetManifestEntry>();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.resourcesKey))
                {
                    continue;
                }

                sEntries[NormalizeKey(entry.resourcesKey)] = entry;
            }

            return sManifest;
        }

        public static bool TryGet(string resourcesKey, out AudioAssetManifestEntry entry)
        {
            Load();
            return sEntries.TryGetValue(NormalizeKey(resourcesKey), out entry);
        }

        public static bool IsKnownFormalKey(string resourcesKey)
        {
            return TryGet(resourcesKey, out _);
        }

        public static string NormalizeKey(string resourcesKey)
        {
            if (string.IsNullOrWhiteSpace(resourcesKey))
            {
                return string.Empty;
            }

            var key = resourcesKey.Trim().Replace('\\', '/');
            if (key.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase))
            {
                key = key.Substring("Assets/Resources/".Length);
            }

            var extension = Path.GetExtension(key);
            if (!string.IsNullOrEmpty(extension))
            {
                key = key.Substring(0, key.Length - extension.Length);
            }

            return key;
        }

        public static void ClearCache()
        {
            sManifest = null;
            sEntries = null;
        }
    }
}
