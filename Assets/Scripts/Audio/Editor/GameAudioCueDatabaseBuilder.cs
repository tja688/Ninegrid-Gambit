using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Audio.Editor
{
    /// <summary>
    /// Editor utility: creates/updates GameAudioCueSO with all 106 hit-points,
    /// auto-links available clips from Assets/Arts/Audios/, and generates AMP SFXObject/Track assets.
    /// Menu: NineGrid/Audio/Rebuild Cue Database
    /// </summary>
    public static class GameAudioCueDatabaseBuilder
    {
        const string DatabasePath = "Assets/Arts/Audios/Data/GameAudioCueDatabase.asset";
        const string SfxFolder = "Assets/Arts/Audios/AMP/SFX";
        const string TrackFolder = "Assets/Arts/Audios/AMP/Tracks";
        const string ClipFolder = "Assets/Arts/Audios";

        static readonly HashSet<AudioKey> BgmKeys = new()
        {
            AudioKey.BgmBattle, AudioKey.BgmBoss, AudioKey.BgmRoute, AudioKey.BgmMainMenu,
        };

        [MenuItem("NineGrid/Audio/Rebuild Cue Database")]
        public static void RebuildFromMenu()
        {
            Rebuild();
            EditorUtility.DisplayDialog("Audio Cue Database",
                $"已重建 {GameAudioCueDefaults.All.Length} 条音效打点配置。\n路径：{DatabasePath}", "OK");
        }

        public static GameAudioCueSO Rebuild()
        {
            EnsureFolder(SfxFolder);
            EnsureFolder(TrackFolder);
            EnsureFolder(Path.GetDirectoryName(DatabasePath));

            var clipCache = BuildClipCache();
            var sfxCache = new Dictionary<AudioKey, SFXObject>();
            var trackCache = new Dictionary<AudioKey, Track>();

            var entries = new AudioCueEntry[GameAudioCueDefaults.All.Length];
            for (int i = 0; i < GameAudioCueDefaults.All.Length; i++)
            {
                var def = GameAudioCueDefaults.All[i];
                entries[i] = BuildEntry(def, clipCache, sfxCache, trackCache);
            }

            var database = AssetDatabase.LoadAssetAtPath<GameAudioCueSO>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<GameAudioCueSO>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            database.cues = entries;
            database.RebuildLookup();
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return database;
        }

        static AudioCueEntry BuildEntry(
            GameAudioCueDefaults.Def def,
            Dictionary<string, AudioClip> clipCache,
            Dictionary<AudioKey, SFXObject> sfxCache,
            Dictionary<AudioKey, Track> trackCache)
        {
            var entry = new AudioCueEntry
            {
                key = def.Key,
                displayName = def.DisplayName,
                triggerContext = def.TriggerContext,
                priority = def.Priority,
                category = def.Category,
                loopMode = def.LoopMode,
                enabled = true,
            };

            if (!GameAudioCueDefaults.ClipFileMap.TryGetValue(def.Key, out var clipStem))
                return entry;

            if (!clipCache.TryGetValue(clipStem, out var clip) || clip == null)
            {
                entry.notes = $"待关联素材：{clipStem}（文件未找到）";
                return entry;
            }

            if (BgmKeys.Contains(def.Key))
            {
                entry.musicTrack = GetOrCreateTrack(def.Key, clip, trackCache);
                entry.notes = $"已关联 Track ← {clip.name}";
            }
            else if (def.LoopMode != LoopMode.None)
            {
                entry.loopClip = clip;
                entry.notes = $"已关联 loopClip ← {clip.name}";
            }
            else
            {
                entry.sfxObject = GetOrCreateSfx(def.Key, clip, sfxCache);
                entry.notes = $"已关联 SFXObject ← {clip.name}";
            }

            return entry;
        }

        static SFXObject GetOrCreateSfx(AudioKey key, AudioClip clip, Dictionary<AudioKey, SFXObject> cache)
        {
            if (cache.TryGetValue(key, out var existing) && existing != null)
                return existing;

            var path = $"{SfxFolder}/{key}.asset";
            var sfx = AssetDatabase.LoadAssetAtPath<SFXObject>(path);
            if (sfx == null)
            {
                sfx = ScriptableObject.CreateInstance<SFXObject>();
                sfx.SFXName = key.ToString();
                sfx.SFXLayers = new[]
                {
                    new SFXLayer
                    {
                        LayerName = "Main",
                        SFX = clip,
                        FixedVolume = 1f,
                        FixedPitch = 1f,
                    }
                };
                sfx.FixedVolume = 1f;
                sfx.FixedPitch = 1f;
                AssetDatabase.CreateAsset(sfx, path);
            }
            else
            {
                sfx.SFXLayers[0].SFX = clip;
                EditorUtility.SetDirty(sfx);
            }

            cache[key] = sfx;
            return sfx;
        }

        static Track GetOrCreateTrack(AudioKey key, AudioClip clip, Dictionary<AudioKey, Track> cache)
        {
            if (cache.TryGetValue(key, out var existing) && existing != null)
                return existing;

            var path = $"{TrackFolder}/{key}.asset";
            var track = AssetDatabase.LoadAssetAtPath<Track>(path);
            if (track == null)
            {
                track = ScriptableObject.CreateInstance<Track>();
                track.Name = key.ToString();
                track.Editions = new[]
                {
                    new Edition
                    {
                        Name = "Main",
                        Soundtrack = clip,
                        SoundtrackVolume = 1f,
                        FadeLength = 0.5f,
                    }
                };
                AssetDatabase.CreateAsset(track, path);
            }
            else
            {
                track.Editions[0].Soundtrack = clip;
                EditorUtility.SetDirty(track);
            }

            cache[key] = track;
            return track;
        }

        static Dictionary<string, AudioClip> BuildClipCache()
        {
            var cache = new Dictionary<string, AudioClip>();
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { ClipFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                var stem = Path.GetFileNameWithoutExtension(path);
                cache[stem] = clip;
            }
            return cache;
        }

        static void EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            var parts = assetPath.Replace('\\', '/').Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
