#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NineGrid.Content.Audio;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public sealed class AudioBindingEditorDeclaration
    {
        public AudioBindingEditorDeclaration(string cueId, string note, string module, string authoritativeEmitter)
        {
            CueId = cueId ?? string.Empty;
            Note = note ?? string.Empty;
            Module = module ?? string.Empty;
            AuthoritativeEmitter = authoritativeEmitter ?? string.Empty;
        }

        public string CueId { get; }
        public string Note { get; }
        public string Module { get; }
        public string AuthoritativeEmitter { get; }
    }

    public sealed class AudioBindingEditorClipOption
    {
        public AudioBindingEditorClipOption(string resourcesKey, string assetPath)
        {
            ResourcesKey = AudioAssetManifestLoader.NormalizeKey(resourcesKey);
            AssetPath = assetPath ?? string.Empty;
        }

        public string ResourcesKey { get; }
        public string AssetPath { get; }
    }

    public sealed class AudioBindingEditorEntry
    {
        private readonly string declaredCueId;
        private readonly string declaredNote;
        private readonly string declaredModule;
        private readonly string authoritativeEmitter;
        private readonly HashSet<string> historicalBindingKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private AudioBindingDto savedDto;

        internal AudioBindingEditorEntry(
            AudioBindingDto dto,
            AudioBindingDto savedDto,
            AudioBindingEditorDeclaration declaration)
        {
            Dto = dto;
            this.savedDto = AudioBindingEditorSession.CloneDto(savedDto);
            AddHistoricalBindingKey(AudioBindingEditorSession.ComputeBindingKey(savedDto));
            declaredCueId = dto?.cueId ?? declaration?.CueId ?? string.Empty;
            declaredNote = declaration?.Note ?? string.Empty;
            declaredModule = declaration?.Module ?? string.Empty;
            authoritativeEmitter = declaration?.AuthoritativeEmitter ?? string.Empty;
        }

        public AudioBindingDto Dto { get; internal set; }
        public string CueId => Dto?.cueId ?? declaredCueId;
        public string Note => string.IsNullOrWhiteSpace(Dto?.note) ? declaredNote : Dto.note;
        public string Module => string.IsNullOrWhiteSpace(Dto?.module) ? declaredModule : Dto.module;
        public string AuthoritativeEmitter => authoritativeEmitter;
        public string BindingKey => AudioBindingEditorSession.ComputeBindingKey(Dto);
        public bool HasBinding => Dto != null;
        public bool IsDirty => Dto != null && !AudioBindingEditorSession.DtoEquals(Dto, savedDto);
        public bool IsUnbound => Dto == null;
        public bool IsDisabled => Dto != null && !Dto.enabled;

        public void CreateDraftBinding()
        {
            if (Dto != null)
            {
                return;
            }

            Dto = new AudioBindingDto
            {
                cueId = declaredCueId,
                note = declaredNote,
                module = declaredModule,
                enabled = true,
                clipKey = string.Empty,
                volumeDb = 0f,
                startOffsetSeconds = 0f,
                bindingDelaySeconds = 0f,
                minimumIntervalSeconds = 0f,
                selectorCardDefId = string.Empty,
                selectorSkillId = string.Empty,
                selectorRoomId = string.Empty,
                selectorItemDefId = string.Empty,
                selectorContentId = string.Empty,
            };
        }

        public bool IsBroken(IReadOnlyCollection<string> knownClipKeys)
        {
            if (Dto == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(Dto.clipKey))
            {
                return true;
            }

            return knownClipKeys != null
                && knownClipKeys.Count > 0
                && !knownClipKeys.Contains(AudioAssetManifestLoader.NormalizeKey(Dto.clipKey));
        }

        public void Revert()
        {
            Dto = AudioBindingEditorSession.CloneDto(savedDto);
        }

        internal void MarkSaved()
        {
            AddHistoricalBindingKey(AudioBindingEditorSession.ComputeBindingKey(savedDto));
            savedDto = AudioBindingEditorSession.CloneDto(Dto);
            AddHistoricalBindingKey(AudioBindingEditorSession.ComputeBindingKey(savedDto));
        }

        internal AudioBindingDto GetSavedDto()
        {
            return AudioBindingEditorSession.CloneDto(savedDto);
        }

        internal bool MatchesHistoricalBindingKey(string key)
        {
            return !string.IsNullOrEmpty(key) && historicalBindingKeys.Contains(key);
        }

        private void AddHistoricalBindingKey(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                historicalBindingKeys.Add(key);
            }
        }
    }

    public sealed class AudioBindingEditorSession
    {
        private readonly List<AudioBindingEditorEntry> entries = new List<AudioBindingEditorEntry>();
        private readonly List<AudioHistoryRecord> playbackHistory = new List<AudioHistoryRecord>();
        private readonly List<AudioBindingEditorClipOption> clipOptions = new List<AudioBindingEditorClipOption>();
        private readonly HashSet<string> knownClipKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AudioBindingEditorDeclaration> declarationsByCueId =
            new Dictionary<string, AudioBindingEditorDeclaration>(StringComparer.Ordinal);
        private AudioBindingCatalogDto diskCatalog;
        private string diskJson = string.Empty;
        private string diskPath = string.Empty;
        private AudioBindingEditorEntry focusedEntry;

        public IReadOnlyList<AudioBindingEditorEntry> Entries => entries;
        public IReadOnlyList<AudioHistoryRecord> PlaybackHistory => playbackHistory;
        public IReadOnlyCollection<string> KnownClipKeys => knownClipKeys;
        public IReadOnlyList<AudioBindingEditorClipOption> ClipOptions => clipOptions;
        public string SavedJson => diskJson;
        public AudioBindingEditorEntry FocusedEntry => focusedEntry;
        public string SearchText { get; set; } = string.Empty;
        public bool FilterDirtyOnly { get; set; }
        public bool FilterUnboundOnly { get; set; }
        public bool FilterBrokenOnly { get; set; }
        public bool FilterFailedOnly { get; set; }
        public int DirtyCount => entries.Count(entry => entry.IsDirty);
        public int UnboundCount => entries.Count(entry => entry.IsUnbound);
        public int BrokenCount => entries.Count(IsBroken);
        public int FailedCount => entries.Count(IsFailed);

        public static AudioBindingEditorSession LoadFromDisk()
        {
            var session = new AudioBindingEditorSession();
            session.ReloadFromDisk();
            return session;
        }

        public void ReloadFromDisk()
        {
            var path = ResolveAbsolutePath(AudioBindingCatalogPaths.ManifestAssetPath);
            var json = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
            LoadFromJson(json, ScanDeclarations(), LoadClipOptions());
            diskPath = path;
        }

        public void LoadFromJson(
            string json,
            IEnumerable<AudioBindingEditorDeclaration> declarations,
            IEnumerable<AudioBindingEditorClipOption> clipOptions)
        {
            diskJson = json ?? string.Empty;
            diskCatalog = ParseCatalog(diskJson);
            diskPath = string.Empty;
            entries.Clear();
            playbackHistory.Clear();
            focusedEntry = null;
            declarationsByCueId.Clear();
            this.clipOptions.Clear();
            knownClipKeys.Clear();

            if (declarations != null)
            {
                foreach (var declaration in declarations)
                {
                    if (declaration == null || string.IsNullOrWhiteSpace(declaration.CueId))
                    {
                        continue;
                    }

                    declarationsByCueId[declaration.CueId] = declaration;
                }
            }

            if (clipOptions != null)
            {
                foreach (var option in clipOptions)
                {
                    if (option == null || string.IsNullOrWhiteSpace(option.ResourcesKey))
                    {
                        continue;
                    }

                    this.clipOptions.Add(option);
                    knownClipKeys.Add(option.ResourcesKey);
                }
            }

            var bindingRows = diskCatalog?.bindings ?? Array.Empty<AudioBindingDto>();
            for (var i = 0; i < bindingRows.Length; i++)
            {
                var dto = bindingRows[i];
                if (dto == null)
                {
                    continue;
                }

                declarationsByCueId.TryGetValue(dto.cueId ?? string.Empty, out var declaration);
                entries.Add(new AudioBindingEditorEntry(
                    CloneDto(dto),
                    CloneDto(dto),
                    declaration));
            }

            foreach (var declaration in declarationsByCueId.Values)
            {
                if (entries.Any(entry => string.Equals(entry.CueId, declaration.CueId, StringComparison.Ordinal)))
                {
                    continue;
                }

                entries.Add(new AudioBindingEditorEntry(null, null, declaration));
            }

            entries.Sort(CompareEntries);
        }

        public IEnumerable<AudioBindingEditorEntry> GetFilteredEntries()
        {
            var search = (SearchText ?? string.Empty).Trim();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (FilterDirtyOnly && !entry.IsDirty)
                {
                    continue;
                }

                if (FilterUnboundOnly && !entry.IsUnbound)
                {
                    continue;
                }

                if (FilterBrokenOnly && !IsBroken(entry))
                {
                    continue;
                }

                if (FilterFailedOnly && !IsFailed(entry))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(search) && !MatchesSearch(entry, search))
                {
                    continue;
                }

                yield return entry;
            }
        }

        public void Focus(AudioBindingEditorEntry entry)
        {
            focusedEntry = entry;
        }

        public void SetPlaybackHistory(IEnumerable<AudioHistoryRecord> history)
        {
            playbackHistory.Clear();
            if (history == null)
            {
                return;
            }

            foreach (var record in history)
            {
                if (record != null)
                {
                    playbackHistory.Add(record);
                }
            }
        }

        public AudioBindingEditorEntry FindEntryForHistory(AudioHistoryRecord record)
        {
            if (record == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(record.BindingKey))
            {
                var current = entries.FirstOrDefault(entry =>
                    string.Equals(entry.BindingKey, record.BindingKey, StringComparison.Ordinal));
                if (current != null)
                {
                    return current;
                }

                var historical = entries.FirstOrDefault(entry => entry.MatchesHistoricalBindingKey(record.BindingKey));
                if (historical != null)
                {
                    return historical;
                }
            }

            return entries.FirstOrDefault(entry =>
                string.Equals(entry.CueId, record.CueId, StringComparison.Ordinal)
                && (string.IsNullOrEmpty(record.CueNote)
                    || string.Equals(entry.Note, record.CueNote, StringComparison.Ordinal))
                && (string.IsNullOrEmpty(record.ActualClipKey)
                    || string.Equals(
                        AudioAssetManifestLoader.NormalizeKey(entry.Dto?.clipKey),
                        AudioAssetManifestLoader.NormalizeKey(record.ActualClipKey),
                        StringComparison.OrdinalIgnoreCase)));
        }

        public bool TrySave(AudioBindingEditorEntry entry, out string error)
        {
            error = null;
            if (entry == null || entry.IsUnbound || !entry.IsDirty)
            {
                return true;
            }

            var rows = BuildRowsForSave(entry);
            if (!ValidateBindingKeys(rows, out error)
                || !TryWriteCatalog(rows, out var json, out error))
            {
                return false;
            }

            diskCatalog.bindings = rows;
            diskJson = json;
            entry.MarkSaved();
            return true;
        }

        public bool TrySaveAll(out string error)
        {
            error = null;
            if (DirtyCount == 0)
            {
                return true;
            }

            var rows = BuildRowsForSave(null);
            if (!ValidateBindingKeys(rows, out error)
                || !TryWriteCatalog(rows, out var json, out error))
            {
                return false;
            }

            diskCatalog.bindings = rows;
            diskJson = json;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].IsDirty)
                {
                    entries[i].MarkSaved();
                }
            }

            return true;
        }

        public void Revert(AudioBindingEditorEntry entry)
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

        public bool IsBroken(AudioBindingEditorEntry entry)
        {
            return entry != null && entry.IsBroken(knownClipKeys);
        }

        public bool IsFailed(AudioBindingEditorEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            for (var i = playbackHistory.Count - 1; i >= 0; i--)
            {
                var record = playbackHistory[i];
                if (record == null || record.Outcome != AudioHistoryOutcome.BackendFailure)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(record.BindingKey)
                    && (string.Equals(record.BindingKey, entry.BindingKey, StringComparison.Ordinal)
                        || entry.MatchesHistoricalBindingKey(record.BindingKey)))
                {
                    return true;
                }

                if (string.IsNullOrEmpty(record.BindingKey)
                    && string.Equals(record.CueId, entry.CueId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public AudioBindingEditorClipOption FindClipOption(string resourcesKey)
        {
            var normalized = AudioAssetManifestLoader.NormalizeKey(resourcesKey);
            return clipOptions.FirstOrDefault(option =>
                string.Equals(option.ResourcesKey, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public static string ComputeBindingKey(AudioBindingDto dto)
        {
            if (dto == null)
            {
                return string.Empty;
            }

            return AudioBindingKey.Compose(
                dto.cueId,
                dto.selectorCardDefId,
                dto.selectorSkillId,
                dto.selectorRoomId,
                dto.selectorItemDefId,
                dto.selectorContentId);
        }

        internal static AudioBindingDto CloneDto(AudioBindingDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new AudioBindingDto
            {
                cueId = source.cueId,
                note = source.note,
                module = source.module,
                enabled = source.enabled,
                clipKey = source.clipKey,
                volumeDb = source.volumeDb,
                startOffsetSeconds = source.startOffsetSeconds,
                bindingDelaySeconds = source.bindingDelaySeconds,
                minimumIntervalSeconds = source.minimumIntervalSeconds,
                selectorCardDefId = source.selectorCardDefId,
                selectorSkillId = source.selectorSkillId,
                selectorRoomId = source.selectorRoomId,
                selectorItemDefId = source.selectorItemDefId,
                selectorContentId = source.selectorContentId,
            };
        }

        internal static bool DtoEquals(AudioBindingDto left, AudioBindingDto right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(JsonUtility.ToJson(left), JsonUtility.ToJson(right), StringComparison.Ordinal);
        }

        private AudioBindingDto[] BuildRowsForSave(AudioBindingEditorEntry selected)
        {
            var savedByKey = new Dictionary<string, AudioBindingDto>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Count; i++)
            {
                var saved = entries[i].GetSavedDto();
                if (saved != null)
                {
                    savedByKey[ComputeBindingKey(saved)] = saved;
                }
            }

            var sourceRows = diskCatalog?.bindings ?? Array.Empty<AudioBindingDto>();
            var rows = new List<AudioBindingDto>(sourceRows.Length + entries.Count);
            var emittedKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < sourceRows.Length; i++)
            {
                var source = sourceRows[i];
                if (source == null)
                {
                    continue;
                }

                var sourceKey = ComputeBindingKey(source);
                var matching = entries.FirstOrDefault(entry => entry.MatchesHistoricalBindingKey(sourceKey));
                var useCurrent = selected == null || ReferenceEquals(matching, selected);
                AudioBindingDto row;
                if (useCurrent && matching != null && matching.Dto != null)
                {
                    row = matching.Dto;
                }
                else if (savedByKey.TryGetValue(sourceKey, out var saved))
                {
                    row = saved;
                }
                else
                {
                    row = source;
                }

                rows.Add(CloneDto(row));
                emittedKeys.Add(ComputeBindingKey(row));
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Dto == null || entry.GetSavedDto() != null)
                {
                    continue;
                }

                if (selected == null || ReferenceEquals(entry, selected))
                {
                    var key = entry.BindingKey;
                    if (emittedKeys.Add(key))
                    {
                        rows.Add(CloneDto(entry.Dto));
                    }
                }
            }

            return rows.ToArray();
        }

        private static bool ValidateBindingKeys(AudioBindingDto[] rows, out string error)
        {
            error = null;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < (rows?.Length ?? 0); i++)
            {
                var key = ComputeBindingKey(rows[i]);
                if (string.IsNullOrEmpty(key) || keys.Add(key))
                {
                    continue;
                }

                error = "保存失败：多个声音绑定使用相同的 cue/内容选择器。请先修正重复选择器。";
                return false;
            }

            return true;
        }

        private bool TryWriteCatalog(AudioBindingDto[] rows, out string json, out string error)
        {
            error = null;
            var output = new AudioBindingCatalogDto
            {
                schemaVersion = diskCatalog?.schemaVersion ?? 1,
                ticket = diskCatalog?.ticket ?? "#169",
                bindings = rows ?? Array.Empty<AudioBindingDto>(),
            };
            json = JsonUtility.ToJson(output, true);

            if (string.IsNullOrEmpty(diskPath))
            {
                return true;
            }

            try
            {
                var directory = Path.GetDirectoryName(diskPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(diskPath, json, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(AudioBindingCatalogPaths.ManifestAssetPath, ImportAssetOptions.ForceUpdate);
                return true;
            }
            catch (Exception exception)
            {
                error = "音频绑定 JSON 写入失败：" + exception.Message;
                return false;
            }
        }

        private static AudioBindingCatalogDto ParseCatalog(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AudioBindingCatalogDto
                {
                    schemaVersion = 1,
                    ticket = "#169",
                    bindings = Array.Empty<AudioBindingDto>(),
                };
            }

            try
            {
                var catalog = JsonUtility.FromJson<AudioBindingCatalogDto>(json);
                if (catalog == null)
                {
                    throw new InvalidDataException("JsonUtility returned null.");
                }

                catalog.bindings ??= Array.Empty<AudioBindingDto>();
                return catalog;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("音频绑定 JSON 解析失败：" + exception.Message, exception);
            }
        }

        private static string ResolveAbsolutePath(string assetPath)
        {
            var normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            if (Path.IsPathRooted(normalized))
            {
                return normalized;
            }

            return Path.Combine(Directory.GetCurrentDirectory(), normalized);
        }

        private static List<AudioBindingEditorDeclaration> ScanDeclarations()
        {
            var result = new List<AudioBindingEditorDeclaration>();
            var presentation = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(
                    assembly.GetName().Name,
                    "NineGrid.Presentation",
                    StringComparison.Ordinal));
            if (presentation == null)
            {
                try
                {
                    presentation = Assembly.Load("NineGrid.Presentation");
                }
                catch
                {
                    return result;
                }
            }

            var scan = AudioCueDeclarationScanner.Scan(presentation);
            for (var i = 0; i < scan.Declarations.Count; i++)
            {
                var declaration = scan.Declarations[i];
                result.Add(new AudioBindingEditorDeclaration(
                    declaration.Attribute.CueId,
                    declaration.Attribute.Note,
                    declaration.Attribute.Module,
                    declaration.Attribute.AuthoritativeEmitter));
            }

            return result;
        }

        private static List<AudioBindingEditorClipOption> LoadClipOptions()
        {
            var result = new List<AudioBindingEditorClipOption>();
            var manifest = AudioAssetManifestLoader.Load();
            var assetEntries = manifest?.entries ?? Array.Empty<AudioAssetManifestEntry>();
            for (var i = 0; i < assetEntries.Length; i++)
            {
                var entry = assetEntries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.resourcesKey))
                {
                    continue;
                }

                result.Add(new AudioBindingEditorClipOption(entry.resourcesKey, entry.assetPath));
            }

            return result;
        }

        private bool MatchesSearch(AudioBindingEditorEntry entry, string search)
        {
            var technical = entry.Dto == null
                ? string.Empty
                : string.Join(
                    " ",
                    entry.Dto.clipKey,
                    FindClipOption(entry.Dto.clipKey)?.AssetPath,
                    entry.Dto.selectorCardDefId,
                    entry.Dto.selectorSkillId,
                    entry.Dto.selectorRoomId,
                    entry.Dto.selectorItemDefId,
                    entry.Dto.selectorContentId,
                    entry.Dto.module);
            var status = entry.IsUnbound
                ? "未绑定 unbound"
                : IsBroken(entry)
                    ? "断链 broken"
                    : IsFailed(entry)
                        ? "失败 failed"
                        : entry.IsDisabled ? "已禁用 disabled" : "已绑定 bound";
            var haystack = string.Join(
                " ",
                entry.Note,
                entry.CueId,
                entry.Module,
                entry.AuthoritativeEmitter,
                status,
                technical);
            return haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CompareEntries(AudioBindingEditorEntry left, AudioBindingEditorEntry right)
        {
            var note = string.Compare(left?.Note, right?.Note, StringComparison.OrdinalIgnoreCase);
            if (note != 0)
            {
                return note;
            }

            return string.Compare(left?.CueId, right?.CueId, StringComparison.Ordinal);
        }
    }
}
#endif
