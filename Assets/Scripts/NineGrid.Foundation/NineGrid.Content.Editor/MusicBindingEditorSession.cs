#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NineGrid.Content.Audio;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public sealed class MusicBindingEditorEntry
    {
        private MusicBindingDto savedDto;

        internal MusicBindingEditorEntry(MusicBindingDto dto)
        {
            Dto = MusicBindingEditorSession.CloneDto(dto);
            savedDto = MusicBindingEditorSession.CloneDto(dto);
        }

        public MusicBindingDto Dto { get; }
        public string State => Dto?.state ?? string.Empty;
        public bool IsDirty => !MusicBindingEditorSession.DtoEquals(Dto, savedDto);

        internal void MarkSaved()
        {
            savedDto = MusicBindingEditorSession.CloneDto(Dto);
        }

        internal void Revert()
        {
            var restored = MusicBindingEditorSession.CloneDto(savedDto);
            if (restored == null)
            {
                return;
            }

            Dto.state = restored.state;
            Dto.enabled = restored.enabled;
            Dto.clipKey = restored.clipKey;
            Dto.volumeDb = restored.volumeDb;
            Dto.startOffsetSeconds = restored.startOffsetSeconds;
            Dto.fadeInSeconds = restored.fadeInSeconds;
            Dto.fadeOutSeconds = restored.fadeOutSeconds;
            Dto.loop = restored.loop;
        }

        internal MusicBindingDto GetSavedDto()
        {
            return MusicBindingEditorSession.CloneDto(savedDto);
        }
    }

    /// <summary>
    /// BGM authoring snapshot/work-copy seam used by the tuning workbench.
    /// Preview and runtime diagnostics live in MusicSystem; this class only owns JSON edits.
    /// </summary>
    public sealed class MusicBindingEditorSession
    {
        private readonly List<MusicBindingEditorEntry> entries = new List<MusicBindingEditorEntry>();
        private MusicBindingCatalogDto diskCatalog;
        private string diskJson = string.Empty;
        private string diskPath = string.Empty;

        public IReadOnlyList<MusicBindingEditorEntry> Entries => entries;
        public string SavedJson => diskJson;
        public int DirtyCount => entries.Count(entry => entry.IsDirty);

        public static MusicBindingEditorSession LoadFromDisk()
        {
            var session = new MusicBindingEditorSession();
            session.ReloadFromDisk();
            return session;
        }

        public void ReloadFromDisk()
        {
            var path = ResolveAbsolutePath(MusicBindingCatalogPaths.ManifestAssetPath);
            var json = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
            LoadFromJson(json);
            diskPath = path;
        }

        public void LoadFromJson(string json)
        {
            diskJson = json ?? string.Empty;
            diskCatalog = ParseCatalog(diskJson);
            diskPath = string.Empty;
            entries.Clear();

            var rows = diskCatalog.bindings ?? Array.Empty<MusicBindingDto>();
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null)
                {
                    entries.Add(new MusicBindingEditorEntry(rows[i]));
                }
            }

            entries.Sort((left, right) => string.Compare(
                left.State,
                right.State,
                StringComparison.OrdinalIgnoreCase));
        }

        public MusicBindingEditorEntry Find(DesiredMusicState state)
        {
            return entries.FirstOrDefault(entry =>
                Enum.TryParse(entry.State, true, out DesiredMusicState parsed)
                && parsed == state);
        }

        public bool TrySave(MusicBindingEditorEntry entry, out string error)
        {
            error = null;
            if (entry == null || !entry.IsDirty)
            {
                return true;
            }

            return TryWrite(BuildRowsForSave(entry), entry, out error);
        }

        public bool TrySaveAll(out string error)
        {
            error = null;
            if (DirtyCount == 0)
            {
                return true;
            }

            return TryWrite(BuildRowsForSave(null), null, out error);
        }

        public void Revert(MusicBindingEditorEntry entry)
        {
            entry?.Revert();
        }

        public void RevertAllDirty()
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].IsDirty)
                {
                    entries[i].Revert();
                }
            }
        }

        public bool TryReplaceWorkingDto(
            string state,
            MusicBindingDto replacement,
            out MusicBindingEditorEntry entry,
            out string error)
        {
            entry = null;
            error = null;
            if (replacement == null)
            {
                error = "空补丁：replacement 不能为空。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(state))
            {
                error = "空补丁：state 不能为空。";
                return false;
            }

            entry = entries.FirstOrDefault(candidate =>
                string.Equals(candidate.State, state.Trim(), StringComparison.OrdinalIgnoreCase));
            if (entry?.Dto == null)
            {
                error = "找不到 BGM 状态：" + state;
                return false;
            }

            entry.Dto.enabled = replacement.enabled;
            entry.Dto.clipKey = replacement.clipKey ?? string.Empty;
            entry.Dto.volumeDb = replacement.volumeDb;
            entry.Dto.startOffsetSeconds = Math.Max(0f, replacement.startOffsetSeconds);
            entry.Dto.fadeInSeconds = Math.Max(0f, replacement.fadeInSeconds);
            entry.Dto.fadeOutSeconds = Math.Max(0f, replacement.fadeOutSeconds);
            entry.Dto.loop = replacement.loop;
            // state remains session-authoritative.
            return true;
        }

        public MusicBindingCatalogDto BuildWorkingCatalog()
        {
            var rows = new MusicBindingDto[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                rows[i] = CloneDto(entries[i].Dto);
            }

            return new MusicBindingCatalogDto
            {
                schemaVersion = diskCatalog?.schemaVersion ?? 1,
                ticket = diskCatalog?.ticket ?? "#172",
                bindings = rows,
            };
        }

        public string BuildWorkingJson()
        {
            return JsonUtility.ToJson(BuildWorkingCatalog(), true);
        }

        public void LoadFromSnapshots(string savedJson, string workingJson)
        {
            LoadFromJson(savedJson);
            var workingCatalog = ParseCatalog(workingJson);
            var workingRows = workingCatalog.bindings ?? Array.Empty<MusicBindingDto>();
            for (var i = 0; i < workingRows.Length; i++)
            {
                var working = workingRows[i];
                if (working == null || string.IsNullOrWhiteSpace(working.state))
                {
                    continue;
                }

                var entry = entries.FirstOrDefault(candidate =>
                    string.Equals(candidate.State, working.state, StringComparison.OrdinalIgnoreCase));
                if (entry?.Dto == null)
                {
                    entries.Add(new MusicBindingEditorEntry(working));
                    continue;
                }

                entry.Dto.enabled = working.enabled;
                entry.Dto.clipKey = working.clipKey ?? string.Empty;
                entry.Dto.volumeDb = working.volumeDb;
                entry.Dto.startOffsetSeconds = Math.Max(0f, working.startOffsetSeconds);
                entry.Dto.fadeInSeconds = Math.Max(0f, working.fadeInSeconds);
                entry.Dto.fadeOutSeconds = Math.Max(0f, working.fadeOutSeconds);
                entry.Dto.loop = working.loop;
            }

            entries.Sort((left, right) => string.Compare(
                left.State,
                right.State,
                StringComparison.OrdinalIgnoreCase));
        }

        internal static MusicBindingDto CloneDto(MusicBindingDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new MusicBindingDto
            {
                state = source.state,
                enabled = source.enabled,
                clipKey = source.clipKey,
                volumeDb = source.volumeDb,
                startOffsetSeconds = source.startOffsetSeconds,
                fadeInSeconds = source.fadeInSeconds,
                fadeOutSeconds = source.fadeOutSeconds,
                loop = source.loop,
            };
        }

        internal static bool DtoEquals(MusicBindingDto left, MusicBindingDto right)
        {
            return string.Equals(
                JsonUtility.ToJson(left),
                JsonUtility.ToJson(right),
                StringComparison.Ordinal);
        }

        private MusicBindingDto[] BuildRowsForSave(MusicBindingEditorEntry selected)
        {
            var rows = new MusicBindingDto[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                rows[i] = selected == null || ReferenceEquals(selected, entry)
                    ? CloneDto(entry.Dto)
                    : entry.GetSavedDto();
            }

            return rows;
        }

        private bool TryWrite(
            MusicBindingDto[] rows,
            MusicBindingEditorEntry selected,
            out string error)
        {
            error = null;
            var output = new MusicBindingCatalogDto
            {
                schemaVersion = diskCatalog?.schemaVersion ?? 1,
                ticket = diskCatalog?.ticket ?? "#172",
                bindings = rows ?? Array.Empty<MusicBindingDto>(),
            };
            var json = JsonUtility.ToJson(output, true);

            if (!string.IsNullOrEmpty(diskPath))
            {
                try
                {
                    var directory = Path.GetDirectoryName(diskPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(diskPath, json, new UTF8Encoding(false));
                    AssetDatabase.ImportAsset(
                        MusicBindingCatalogPaths.ManifestAssetPath,
                        ImportAssetOptions.ForceUpdate);
                }
                catch (Exception exception)
                {
                    error = "BGM JSON 写入失败：" + exception.Message;
                    return false;
                }
            }

            diskCatalog = output;
            diskJson = json;
            if (selected != null)
            {
                selected.MarkSaved();
            }
            else
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i].IsDirty)
                    {
                        entries[i].MarkSaved();
                    }
                }
            }

            return true;
        }

        private static MusicBindingCatalogDto ParseCatalog(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new MusicBindingCatalogDto
                {
                    schemaVersion = 1,
                    ticket = "#172",
                    bindings = Array.Empty<MusicBindingDto>(),
                };
            }

            try
            {
                var catalog = JsonUtility.FromJson<MusicBindingCatalogDto>(json);
                if (catalog == null)
                {
                    throw new InvalidDataException("JsonUtility returned null.");
                }

                catalog.bindings ??= Array.Empty<MusicBindingDto>();
                return catalog;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("BGM JSON 解析失败：" + exception.Message, exception);
            }
        }

        private static string ResolveAbsolutePath(string assetPath)
        {
            var normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            return Path.IsPathRooted(normalized)
                ? normalized
                : Path.Combine(Directory.GetCurrentDirectory(), normalized);
        }
    }
}
#endif
