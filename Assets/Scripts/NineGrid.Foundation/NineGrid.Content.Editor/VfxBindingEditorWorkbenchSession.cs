#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NineGrid.Content;
using NineGrid.Content.Vfx;
using NineGrid.Flow.Presentation;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public sealed class VfxBindingEditorCueEntry
    {
        private readonly string declaredCueId;
        private readonly string declaredNote;
        private readonly string declaredModule;
        private readonly string authoritativeEmitter;
        private VfxCueBindingDto savedDto;

        internal VfxBindingEditorCueEntry(
            VfxCueBindingDto dto,
            VfxCueBindingDto saved,
            VfxCueDeclaration declaration)
        {
            Dto = dto != null ? VfxBindingEditorSession.CloneCueDto(dto) : null;
            savedDto = saved != null ? VfxBindingEditorSession.CloneCueDto(saved) : null;
            declaredCueId = declaration?.Attribute?.CueId ?? dto?.cueId ?? string.Empty;
            declaredNote = declaration?.Attribute?.Note ?? dto?.note ?? string.Empty;
            declaredModule = declaration?.Attribute?.Module ?? dto?.module ?? string.Empty;
            authoritativeEmitter = declaration?.Attribute?.AuthoritativeEmitter ?? string.Empty;
        }

        public VfxCueBindingDto Dto { get; internal set; }
        public string CueId => Dto?.cueId ?? declaredCueId;
        public string Note => string.IsNullOrWhiteSpace(Dto?.note) ? declaredNote : Dto.note;
        public string Module => string.IsNullOrWhiteSpace(Dto?.module) ? declaredModule : Dto.module;
        public string AuthoritativeEmitter => authoritativeEmitter;
        public string BindingKey => VfxBindingEditorSession.ComputeCueBindingKey(Dto);
        public bool HasBinding => Dto != null;
        public bool IsDirty => Dto != null && !DtoEquals(Dto, savedDto);
        public bool IsUnbound => Dto == null;
        public bool IsDisabled => Dto != null && !Dto.enabled;

        public void CreateDraftBinding()
        {
            if (Dto != null)
            {
                return;
            }

            Dto = new VfxCueBindingDto
            {
                cueId = declaredCueId,
                note = declaredNote,
                module = declaredModule,
                enabled = true,
                playerId = VfxPlayerRegistry.SpriteSheet,
                spatialOwnership = "independent",
                materialKey = string.Empty,
                authoringStatus = VfxBindingAuthoringStatuses.AiDraft,
            };
        }

        public void Revert()
        {
            Dto = VfxBindingEditorSession.CloneCueDto(savedDto);
        }

        internal void MarkSaved()
        {
            savedDto = VfxBindingEditorSession.CloneCueDto(Dto);
        }

        internal VfxCueBindingDto GetSavedDto()
        {
            return VfxBindingEditorSession.CloneCueDto(savedDto);
        }

        public bool IsBroken(IReadOnlyCollection<string> knownMaterialIds)
        {
            if (Dto == null || !VfxPlayerRegistry.IsMaterialPlayer(Dto.playerId))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(Dto.materialKey))
            {
                return true;
            }

            return knownMaterialIds != null
                && knownMaterialIds.Count > 0
                && !knownMaterialIds.Contains(Dto.materialKey.Trim());
        }

        private static bool DtoEquals(VfxCueBindingDto a, VfxCueBindingDto b)
        {
            if (a == null && b == null)
            {
                return true;
            }

            if (a == null || b == null)
            {
                return false;
            }

            return string.Equals(
                JsonUtility.ToJson(a),
                JsonUtility.ToJson(b),
                StringComparison.Ordinal);
        }
    }

    public sealed class VfxBindingEditorStateEntry
    {
        private readonly string declaredStateId;
        private readonly string declaredNote;
        private readonly string declaredModule;
        private readonly string authoritativeEmitter;
        private VfxStateBindingDto savedDto;

        internal VfxBindingEditorStateEntry(
            VfxStateBindingDto dto,
            VfxStateBindingDto saved,
            PersistentVfxStateDeclaration declaration)
        {
            Dto = dto != null ? VfxBindingEditorSession.CloneStateDto(dto) : null;
            savedDto = saved != null ? VfxBindingEditorSession.CloneStateDto(saved) : null;
            declaredStateId = declaration?.Attribute?.StateId ?? dto?.stateId ?? string.Empty;
            declaredNote = declaration?.Attribute?.Note ?? dto?.note ?? string.Empty;
            declaredModule = declaration?.Attribute?.Module ?? dto?.module ?? string.Empty;
            authoritativeEmitter = declaration?.Attribute?.AuthoritativeEmitter ?? string.Empty;
        }

        public VfxStateBindingDto Dto { get; internal set; }
        public string StateId => Dto?.stateId ?? declaredStateId;
        public string Note => string.IsNullOrWhiteSpace(Dto?.note) ? declaredNote : Dto.note;
        public string Module => string.IsNullOrWhiteSpace(Dto?.module) ? declaredModule : Dto.module;
        public string AuthoritativeEmitter => authoritativeEmitter;
        public string BindingKey => VfxBindingEditorSession.ComputeStateBindingKey(Dto);
        public bool HasBinding => Dto != null;
        public bool IsDirty => Dto != null && !DtoEquals(Dto, savedDto);
        public bool IsUnbound => Dto == null;
        public bool IsDisabled => Dto != null && !Dto.enabled;

        public void CreateDraftBinding()
        {
            if (Dto != null)
            {
                return;
            }

            Dto = new VfxStateBindingDto
            {
                stateId = declaredStateId,
                note = declaredNote,
                module = declaredModule,
                enabled = true,
                playerId = VfxPlayerRegistry.SpriteSheet,
                spatialOwnership = "attached",
                materialKey = string.Empty,
                authoringStatus = VfxBindingAuthoringStatuses.AiDraft,
            };
        }

        public void Revert()
        {
            Dto = VfxBindingEditorSession.CloneStateDto(savedDto);
        }

        internal void MarkSaved()
        {
            savedDto = VfxBindingEditorSession.CloneStateDto(Dto);
        }

        internal VfxStateBindingDto GetSavedDto()
        {
            return VfxBindingEditorSession.CloneStateDto(savedDto);
        }

        public bool IsBroken(IReadOnlyCollection<string> knownMaterialIds)
        {
            if (Dto == null || !VfxPlayerRegistry.IsMaterialPlayer(Dto.playerId))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(Dto.materialKey))
            {
                return true;
            }

            return knownMaterialIds != null
                && knownMaterialIds.Count > 0
                && !knownMaterialIds.Contains(Dto.materialKey.Trim());
        }

        private static bool DtoEquals(VfxStateBindingDto a, VfxStateBindingDto b)
        {
            if (a == null && b == null)
            {
                return true;
            }

            if (a == null || b == null)
            {
                return false;
            }

            return string.Equals(
                JsonUtility.ToJson(a),
                JsonUtility.ToJson(b),
                StringComparison.Ordinal);
        }
    }

    public sealed class VfxBindingEditorMaterialOption
    {
        public VfxBindingEditorMaterialOption(string materialId, string assetPath)
        {
            MaterialId = materialId ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
        }

        public string MaterialId { get; }
        public string AssetPath { get; }
    }

    /// <summary>#202 VFX 工作台绑定会话：声明 + cue/state 行、脏改与磁盘读写。</summary>
    public sealed class VfxBindingEditorWorkbenchSession
    {
        private readonly VfxBindingEditorSession coreSession = new VfxBindingEditorSession();
        private readonly List<VfxBindingEditorCueEntry> cueEntries = new List<VfxBindingEditorCueEntry>();
        private readonly List<VfxBindingEditorStateEntry> stateEntries = new List<VfxBindingEditorStateEntry>();
        private readonly List<VfxBindingEditorMaterialOption> materialOptions = new List<VfxBindingEditorMaterialOption>();
        private readonly HashSet<string> knownMaterialIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<VfxHistoryRecordProxy> playbackHistory = new List<VfxHistoryRecordProxy>();
        private string simulatedDiskJson;

        public IReadOnlyList<VfxBindingEditorCueEntry> CueEntries => cueEntries;
        public IReadOnlyList<VfxBindingEditorStateEntry> StateEntries => stateEntries;
        public IReadOnlyList<VfxBindingEditorMaterialOption> MaterialOptions => materialOptions;
        public string SavedJson => simulatedDiskJson ?? coreSession.SavedJson;

        public int CueDirtyCount => cueEntries.Count(entry => entry.IsDirty);
        public int StateDirtyCount => stateEntries.Count(entry => entry.IsDirty);
        public int DirtyCount => CueDirtyCount + StateDirtyCount;

        public void ReloadFromDisk()
        {
            simulatedDiskJson = null;
            coreSession.ReloadFromDisk();
            RebuildEntries();
        }

        public bool LoadFromSnapshots(string baselineJson, string workingJson)
        {
            simulatedDiskJson = baselineJson ?? string.Empty;
            var diskPath = coreSession.DiskPath;
            if (!coreSession.LoadFromJson(workingJson ?? string.Empty))
            {
                return false;
            }

            coreSession.DiskPath = diskPath;
            RebuildEntries();
            return true;
        }

        public string BuildWorkingJson()
        {
            SyncCoreFromEntries();
            return coreSession.BuildWorkingJson();
        }

        public VfxBindingEditorCueEntry FindCueEntryByBindingKey(string bindingKey)
        {
            return cueEntries.FirstOrDefault(entry =>
                string.Equals(entry.BindingKey, bindingKey ?? string.Empty, StringComparison.Ordinal));
        }

        public VfxBindingEditorStateEntry FindStateEntryByBindingKey(string bindingKey)
        {
            return stateEntries.FirstOrDefault(entry =>
                string.Equals(entry.BindingKey, bindingKey ?? string.Empty, StringComparison.Ordinal));
        }

        public bool TryCreateCueDraft(string cueId, out VfxBindingEditorCueEntry entry, out string error)
        {
            entry = cueEntries.FirstOrDefault(e => string.Equals(e.CueId, cueId, StringComparison.Ordinal) && e.IsUnbound);
            if (entry == null)
            {
                error = "未找到可创建草稿的 cue 声明：" + cueId;
                return false;
            }

            entry.CreateDraftBinding();
            SyncCoreFromEntries();
            error = null;
            return true;
        }

        public bool TryReplaceCueDto(string originalBindingKey, VfxCueBindingDto replacement, out VfxBindingEditorCueEntry entry, out string error)
        {
            entry = FindCueEntryByBindingKey(originalBindingKey);
            if (entry == null && replacement != null)
            {
                entry = cueEntries.FirstOrDefault(e => string.Equals(e.CueId, replacement.cueId, StringComparison.Ordinal));
            }

            if (entry == null)
            {
                error = "找不到 cue 绑定行。";
                return false;
            }

            entry.Dto = replacement != null
                ? VfxBindingEditorSession.CloneCueDto(replacement)
                : entry.Dto;
            SyncCoreFromEntries();
            error = null;
            return true;
        }

        public bool TryReplaceStateDto(string originalBindingKey, VfxStateBindingDto replacement, out VfxBindingEditorStateEntry entry, out string error)
        {
            entry = FindStateEntryByBindingKey(originalBindingKey);
            if (entry == null && replacement != null)
            {
                entry = stateEntries.FirstOrDefault(e => string.Equals(e.StateId, replacement.stateId, StringComparison.Ordinal));
            }

            if (entry == null)
            {
                error = "找不到 state 绑定行。";
                return false;
            }

            entry.Dto = replacement != null
                ? VfxBindingEditorSession.CloneStateDto(replacement)
                : entry.Dto;
            SyncCoreFromEntries();
            error = null;
            return true;
        }

        public bool TrySaveCue(VfxBindingEditorCueEntry entry, out string error)
        {
            if (entry == null || entry.IsUnbound || !entry.IsDirty)
            {
                error = null;
                return true;
            }

            SyncCoreFromEntries();
            if (!coreSession.TrySaveCue(entry.Dto, out error))
            {
                return false;
            }

            simulatedDiskJson = coreSession.SavedJson;
            entry.MarkSaved();
            RebuildEntries();
            return true;
        }

        public bool TrySaveState(VfxBindingEditorStateEntry entry, out string error)
        {
            if (entry == null || entry.IsUnbound || !entry.IsDirty)
            {
                error = null;
                return true;
            }

            SyncCoreFromEntries();
            if (!coreSession.TrySaveAll(out error))
            {
                return false;
            }

            simulatedDiskJson = coreSession.SavedJson;

            entry.MarkSaved();
            RebuildEntries();
            return true;
        }

        public bool TrySaveAll(out string error)
        {
            SyncCoreFromEntries();
            if (!coreSession.TrySaveAll(out error))
            {
                return false;
            }

            simulatedDiskJson = coreSession.SavedJson;

            for (var i = 0; i < cueEntries.Count; i++)
            {
                if (cueEntries[i].HasBinding)
                {
                    cueEntries[i].MarkSaved();
                }
            }

            for (var i = 0; i < stateEntries.Count; i++)
            {
                if (stateEntries[i].HasBinding)
                {
                    stateEntries[i].MarkSaved();
                }
            }

            RebuildEntries();
            return true;
        }

        public void RevertCue(VfxBindingEditorCueEntry entry)
        {
            entry?.Revert();
            SyncCoreFromEntries();
        }

        public void RevertState(VfxBindingEditorStateEntry entry)
        {
            entry?.Revert();
            SyncCoreFromEntries();
        }

        public void RevertAllDirty()
        {
            for (var i = 0; i < cueEntries.Count; i++)
            {
                if (cueEntries[i].IsDirty)
                {
                    cueEntries[i].Revert();
                }
            }

            for (var i = 0; i < stateEntries.Count; i++)
            {
                if (stateEntries[i].IsDirty)
                {
                    stateEntries[i].Revert();
                }
            }

            SyncCoreFromEntries();
        }

        public void SetPlaybackHistory(IEnumerable<VfxHistoryRecordProxy> history)
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

        public IReadOnlyList<VfxHistoryRecordProxy> PlaybackHistory => playbackHistory;

        public bool IsBrokenCue(VfxBindingEditorCueEntry entry)
        {
            return entry != null && entry.IsBroken(knownMaterialIds);
        }

        public bool IsBrokenState(VfxBindingEditorStateEntry entry)
        {
            return entry != null && entry.IsBroken(knownMaterialIds);
        }

        private void RebuildEntries()
        {
            cueEntries.Clear();
            stateEntries.Clear();
            materialOptions.Clear();
            knownMaterialIds.Clear();

            foreach (var entry in VisualEffectCatalog.All)
            {
                if (string.IsNullOrWhiteSpace(entry?.id))
                {
                    continue;
                }

                var id = entry.id.Trim();
                materialOptions.Add(new VfxBindingEditorMaterialOption(id, entry.sheetPath ?? string.Empty));
                knownMaterialIds.Add(id);
            }

            var scan = VfxDeclarationScanner.Scan(
                VfxBindingCatalog.FromJson(coreSession.SavedJson ?? string.Empty),
                ResolvePresentationAssembly());
            var catalog = coreSession.BuildWorkingCatalog();
            var cueById = new Dictionary<string, VfxCueDeclaration>(StringComparer.Ordinal);
            for (var i = 0; i < scan.CueDeclarations.Count; i++)
            {
                var declaration = scan.CueDeclarations[i];
                var cueId = declaration?.Attribute?.CueId;
                if (!string.IsNullOrWhiteSpace(cueId))
                {
                    cueById[cueId.Trim()] = declaration;
                }
            }

            var stateById = new Dictionary<string, PersistentVfxStateDeclaration>(StringComparer.Ordinal);
            for (var i = 0; i < scan.StateDeclarations.Count; i++)
            {
                var declaration = scan.StateDeclarations[i];
                var stateId = declaration?.Attribute?.StateId;
                if (!string.IsNullOrWhiteSpace(stateId))
                {
                    stateById[stateId.Trim()] = declaration;
                }
            }

            var cueRows = catalog.cueBindings ?? Array.Empty<VfxCueBindingDto>();
            for (var i = 0; i < cueRows.Length; i++)
            {
                var dto = cueRows[i];
                if (dto == null)
                {
                    continue;
                }

                cueById.TryGetValue(dto.cueId ?? string.Empty, out var declaration);
                cueEntries.Add(new VfxBindingEditorCueEntry(dto, dto, declaration));
            }

            foreach (var declaration in cueById.Values)
            {
                var cueId = declaration.Attribute.CueId;
                if (cueEntries.Any(entry => string.Equals(entry.CueId, cueId, StringComparison.Ordinal) && entry.HasBinding))
                {
                    continue;
                }

                cueEntries.Add(new VfxBindingEditorCueEntry(null, null, declaration));
            }

            cueEntries.Sort((a, b) => string.Compare(a.CueId, b.CueId, StringComparison.Ordinal));

            var stateRows = catalog.stateBindings ?? Array.Empty<VfxStateBindingDto>();
            for (var i = 0; i < stateRows.Length; i++)
            {
                var dto = stateRows[i];
                if (dto == null)
                {
                    continue;
                }

                stateById.TryGetValue(dto.stateId ?? string.Empty, out var declaration);
                stateEntries.Add(new VfxBindingEditorStateEntry(dto, dto, declaration));
            }

            foreach (var declaration in stateById.Values)
            {
                var stateId = declaration.Attribute.StateId;
                if (stateEntries.Any(entry => string.Equals(entry.StateId, stateId, StringComparison.Ordinal) && entry.HasBinding))
                {
                    continue;
                }

                stateEntries.Add(new VfxBindingEditorStateEntry(null, null, declaration));
            }

            stateEntries.Sort((a, b) => string.Compare(a.StateId, b.StateId, StringComparison.Ordinal));
        }

        private void SyncCoreFromEntries()
        {
            var catalog = new VfxBindingCatalogDto
            {
                schemaVersion = 1,
                ticket = "#202",
                cueBindings = cueEntries
                    .Where(entry => entry.HasBinding)
                    .Select(entry => VfxBindingEditorSession.CloneCueDto(entry.Dto))
                    .ToArray(),
                stateBindings = stateEntries
                    .Where(entry => entry.HasBinding)
                    .Select(entry => VfxBindingEditorSession.CloneStateDto(entry.Dto))
                    .ToArray(),
            };
            var diskPath = coreSession.DiskPath;
            coreSession.LoadFromJson(JsonUtility.ToJson(catalog, true));
            coreSession.DiskPath = diskPath;
        }

        private static Assembly ResolvePresentationAssembly()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(
                    assembly.GetName().Name,
                    "NineGrid.Presentation",
                    StringComparison.Ordinal))
                ?? typeof(VfxCueAttribute).Assembly;
        }
    }

    public sealed class VfxHistoryRecordProxy
    {
        public long Sequence { get; set; }
        public string Outcome { get; set; }
        public string CueId { get; set; }
        public string CueNote { get; set; }
        public string BindingKey { get; set; }
        public string DiagnosticSource { get; set; }
        public string PlayerId { get; set; }
        public string MaterialKey { get; set; }
        public string VariantId { get; set; }
        public string InstanceId { get; set; }
        public string FailureReason { get; set; }
        public double Time { get; set; }
    }
}
#endif
