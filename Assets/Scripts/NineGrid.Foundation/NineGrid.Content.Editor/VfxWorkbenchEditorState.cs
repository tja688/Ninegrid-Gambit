#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NineGrid.Content.Vfx;
using NineGrid.Presentation.Systems;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public enum VfxWorkbenchConflictKind
    {
        None = 0,
        DiskChanged = 1,
    }

    /// <summary>#202 VFX 工作台单一作者态：三模式观测、工作副本、热应用与冲突门禁。</summary>
    public sealed class VfxWorkbenchEditorState
    {
        public const string TransientSessionKey = "NineGrid.VfxWorkbench.Transient.v1";

        private static VfxWorkbenchEditorState instance;

        private readonly VfxBindingEditorWorkbenchSession bindingSession = new VfxBindingEditorWorkbenchSession();
        private string statusMessage = string.Empty;
        private string errorMessage = string.Empty;
        private string focusedBindingKey = string.Empty;
        private string focusedChannel = "cue";
        private string aggregateLevel = "instance";
        private VfxWorkbenchConflictKind conflictKind;
        private TransientPayload blockedTransient;
        private bool pendingRecoverOverwriteConfirm;
        private long localRevision = 1;
        private long lastObservedRuntimeRevision = -1;
        private long lastObservedHistorySequence = -1;
        private int lastObservedActivePulseCount = -1;
        private int lastObservedActiveStateCount = -1;

        public static VfxWorkbenchEditorState Instance => instance ??= new VfxWorkbenchEditorState();

        public VfxBindingEditorWorkbenchSession BindingSession => bindingSession;
        public string StatusMessage => statusMessage;
        public string ErrorMessage => errorMessage;
        public string FocusedBindingKey => focusedBindingKey;
        public string FocusedChannel => focusedChannel;
        public string AggregateLevel => aggregateLevel;
        public VfxWorkbenchConflictKind ConflictKind => conflictKind;
        public bool HasTransientConflict => conflictKind != VfxWorkbenchConflictKind.None;
        public bool BlocksMutations => HasTransientConflict;
        public long LocalRevision => localRevision;
        public bool IsPlayMode => EditorApplication.isPlaying;

        public static void ResetForTests()
        {
            instance = null;
            SessionState.EraseString(TransientSessionKey);
        }

        public void InitializeFromDisk()
        {
            bindingSession.ReloadFromDisk();
            conflictKind = VfxWorkbenchConflictKind.None;
            blockedTransient = null;
            pendingRecoverOverwriteConfirm = false;
            ClearError();
            statusMessage = "已从磁盘加载 VFX 绑定。";
            TryRestoreTransientOrConflict();
            BumpRevision();
            HotApplyIfPlaying();
        }

        public void ReevaluateTransientForTests()
        {
            TryRestoreTransientOrConflict();
            BumpRevision();
        }

        public void PersistTransient()
        {
            var payload = new TransientPayload
            {
                baselineJson = bindingSession.SavedJson ?? string.Empty,
                workingJson = bindingSession.BuildWorkingJson() ?? string.Empty,
                focusedBindingKey = focusedBindingKey ?? string.Empty,
                focusedChannel = focusedChannel ?? "cue",
                aggregateLevel = aggregateLevel ?? "instance",
                statusMessage = statusMessage ?? string.Empty,
            };
            SessionState.SetString(TransientSessionKey, JsonUtility.ToJson(payload));
        }

        public void OnBeforeAssemblyReload()
        {
            PersistTransient();
        }

        public bool TryDispatchCommand(string command, string payloadJson, out object payload, out string error)
        {
            payload = null;
            error = null;
            if (string.IsNullOrWhiteSpace(command))
            {
                error = "未知命令。";
                return false;
            }

            switch (command)
            {
                case "ping":
                    payload = new { ok = true, revision = localRevision };
                    return true;
                case "focusBinding":
                {
                    focusedBindingKey = ReadString(payloadJson, "bindingKey");
                    focusedChannel = ReadString(payloadJson, "channel");
                    if (string.IsNullOrWhiteSpace(focusedChannel))
                    {
                        focusedChannel = "cue";
                    }

                    aggregateLevel = ReadString(payloadJson, "aggregateLevel");
                    if (string.IsNullOrWhiteSpace(aggregateLevel))
                    {
                        aggregateLevel = "instance";
                    }

                    PersistTransient();
                    BumpRevision();
                    payload = new { focusedBindingKey, focusedChannel, aggregateLevel };
                    return true;
                }
                case "replaceCueBinding":
                    if (!EnsureMutationsAllowed(out error)) return false;
                    return DispatchReplaceCue(payloadJson, out payload, out error);
                case "replaceStateBinding":
                    if (!EnsureMutationsAllowed(out error)) return false;
                    return DispatchReplaceState(payloadJson, out payload, out error);
                case "saveBinding":
                    if (!EnsureMutationsAllowed(out error)) return false;
                    return DispatchSaveBinding(payloadJson, out payload, out error);
                case "saveAll":
                    if (!EnsureMutationsAllowed(out error)) return false;
                    return DispatchSaveAll(out payload, out error);
                case "revertBinding":
                    if (!EnsureMutationsAllowed(out error)) return false;
                    return DispatchRevertBinding(payloadJson, out payload, out error);
                case "revertAll":
                    if (!EnsureMutationsAllowed(out error)) return false;
                    return DispatchRevertAll(out payload, out error);
                case "reloadFromDisk":
                    if (!EnsureMutationsAllowed(out error)) return false;
                    return DispatchReload(out payload, out error);
                case "previewBinding":
                    return DispatchPreviewBinding(payloadJson, out payload, out error);
                case "clearStateSlot":
                    return DispatchClearStateSlot(payloadJson, out payload, out error);
                case "recoverTransient":
                    return DispatchRecoverTransient(out payload, out error);
                case "discardTransient":
                    return DispatchDiscardTransient(out payload, out error);
                default:
                    error = "未知命令：" + command;
                    return false;
            }
        }

        public bool TickRuntimeObservation()
        {
            if (!IsPlayMode)
            {
                lastObservedRuntimeRevision = -1;
                lastObservedHistorySequence = -1;
                lastObservedActivePulseCount = -1;
                lastObservedActiveStateCount = -1;
                return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RefreshRuntimeHistory();
            var vfx = NineGridArchitecture.Interface?.GetSystem<IVfxSystem>();
            var snap = vfx?.GetWorkbenchSnapshot();
            var runtimeRevision = snap?.Revision ?? 0;
            var historySequence = 0L;
            if (snap?.History != null && snap.History.Count > 0)
            {
                historySequence = snap.History[snap.History.Count - 1].Sequence;
            }

            var pulseCount = snap?.ActivePulses?.Count ?? 0;
            var stateCount = snap?.ActiveStateSlots?.Count ?? 0;
            var changed = runtimeRevision != lastObservedRuntimeRevision
                || historySequence != lastObservedHistorySequence
                || pulseCount != lastObservedActivePulseCount
                || stateCount != lastObservedActiveStateCount;
            lastObservedRuntimeRevision = runtimeRevision;
            lastObservedHistorySequence = historySequence;
            lastObservedActivePulseCount = pulseCount;
            lastObservedActiveStateCount = stateCount;
            if (!changed)
            {
                return false;
            }

            BumpRevision();
            return true;
#else
            return false;
#endif
        }

        public object BuildSnapshotPayload()
        {
            RefreshRuntimeHistory();
            var cueDeclarations = bindingSession.CueEntries.Select(entry => new
            {
                cueId = entry.CueId,
                note = entry.Note,
                module = entry.Module,
                authoritativeEmitter = entry.AuthoritativeEmitter,
                bindingKey = entry.BindingKey,
                hasBinding = entry.HasBinding,
                isDirty = entry.IsDirty,
                isUnbound = entry.IsUnbound,
                isDisabled = entry.IsDisabled,
                isBroken = bindingSession.IsBrokenCue(entry),
                authoringStatus = entry.Dto?.authoringStatus,
                savedEnabled = entry.GetSavedDto()?.enabled ?? entry.Dto?.enabled ?? true,
                dto = entry.Dto,
                isProgrammatic = entry.Dto != null && !VfxPlayerRegistry.IsMaterialPlayer(entry.Dto.playerId),
            }).ToArray();

            var stateDeclarations = bindingSession.StateEntries.Select(entry => new
            {
                stateId = entry.StateId,
                note = entry.Note,
                module = entry.Module,
                authoritativeEmitter = entry.AuthoritativeEmitter,
                bindingKey = entry.BindingKey,
                hasBinding = entry.HasBinding,
                isDirty = entry.IsDirty,
                isUnbound = entry.IsUnbound,
                isDisabled = entry.IsDisabled,
                isBroken = bindingSession.IsBrokenState(entry),
                authoringStatus = entry.Dto?.authoringStatus,
                savedEnabled = entry.GetSavedDto()?.enabled ?? entry.Dto?.enabled ?? true,
                dto = entry.Dto,
                isProgrammatic = entry.Dto != null && !VfxPlayerRegistry.IsMaterialPlayer(entry.Dto.playerId),
            }).ToArray();

            var materialOptions = bindingSession.MaterialOptions.Select(option => new
            {
                materialId = option.MaterialId,
                assetPath = option.AssetPath,
            }).ToArray();

            object runtime = null;
            if (IsPlayMode)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var vfx = NineGridArchitecture.Interface?.GetSystem<IVfxSystem>();
                runtime = ProjectRuntime(vfx?.GetWorkbenchSnapshot());
#endif
            }

            return new
            {
                playMode = IsPlayMode,
                conflict = conflictKind.ToString(),
                blocksMutations = BlocksMutations,
                pendingRecoverOverwriteConfirm,
                focusedBindingKey,
                focusedChannel,
                aggregateLevel,
                statusMessage,
                errorMessage,
                cueDirtyCount = bindingSession.CueDirtyCount,
                stateDirtyCount = bindingSession.StateDirtyCount,
                savedJson = bindingSession.SavedJson,
                workingJson = bindingSession.BuildWorkingJson(),
                cueDeclarations,
                stateDeclarations,
                materialOptions,
                runtime,
                modes = new[] { "实时实例", "静态绑定库", "持续状态" },
                defaultMode = IsPlayMode ? "实时实例" : "静态绑定库",
            };
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static object ProjectRuntime(VfxWorkbenchSnapshot snap)
        {
            if (snap == null)
            {
                return null;
            }

            return new
            {
                revision = snap.Revision,
                history = (snap.History ?? Array.Empty<VfxHistoryRecord>()).Select(ProjectHistory).ToArray(),
                activeInstances = (snap.ActivePulses ?? Array.Empty<VfxWorkbenchActivePulse>()).Select(p => new
                {
                    instanceId = p.InstanceId,
                    bindingKey = p.BindingKey,
                    cueId = p.CueId,
                    playerId = p.PlayerId,
                    materialKey = p.MaterialKey,
                    spatialOwnership = p.SpatialOwnership,
                }).ToArray(),
                activeStateSlots = (snap.ActiveStateSlots ?? Array.Empty<VfxWorkbenchActiveStateSlot>()).Select(s => new
                {
                    ownerLabel = s.OwnerLabel,
                    slot = s.Slot,
                    stateId = s.StateId,
                    bindingKey = s.BindingKey,
                    playerId = s.PlayerId,
                    instanceId = s.InstanceId,
                    spatialOwnership = s.SpatialOwnership,
                }).ToArray(),
                aggregates = (snap.Aggregates ?? Array.Empty<VfxCueAggregate>()).Select(a => new
                {
                    aggregateKey = a.AggregateKey,
                    bindingKey = a.BindingKey,
                    cueOrStateId = a.CueOrStateId,
                    isPulse = a.IsPulse,
                    requested = a.Requested,
                    played = a.Played,
                    suppressed = a.Suppressed,
                    unbound = a.Unbound,
                    invalidBinding = a.InvalidBinding,
                    playerUnavailable = a.PlayerUnavailable,
                    domainUnavailable = a.DomainUnavailable,
                    backendFailure = a.BackendFailure,
                    lastTime = a.LastTime,
                    lastFailureReason = a.LastFailureReason,
                    recentTimestamps = a.RecentTimestamps,
                }).ToArray(),
                diagnostics = snap.Diagnostics == null ? null : new
                {
                    session = snap.Diagnostics.Session,
                    peak = snap.Diagnostics.Peak,
                    bindingAggregates = snap.Diagnostics.BindingAggregates,
                    playerAggregates = snap.Diagnostics.PlayerAggregates,
                },
            };
        }

        private static object ProjectHistory(VfxHistoryRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return new
            {
                sequence = record.Sequence,
                outcome = record.Outcome.ToString(),
                cueId = record.CueId,
                cueNote = record.CueNote,
                bindingKey = record.BindingKey,
                diagnosticSource = record.DiagnosticSource,
                playerId = record.PlayerId,
                materialKey = record.MaterialKey,
                variantId = record.VariantId,
                instanceId = record.InstanceId,
                failureReason = record.FailureReason,
                time = record.Time,
            };
        }
#endif

        private bool EnsureMutationsAllowed(out string error)
        {
            error = null;
            if (!BlocksMutations)
            {
                return true;
            }

            error = "磁盘已变更：请先选择「恢复临时版本并覆盖磁盘」或「丢弃临时版本并使用磁盘」。";
            SetError(error);
            return false;
        }

        private bool DispatchReplaceCue(string payloadJson, out object payload, out string error)
        {
            payload = null;
            var key = ReadString(payloadJson, "originalBindingKey");
            var dto = ReadNestedDto<VfxCueBindingDto>(payloadJson, "replacement");
            if (!bindingSession.TryReplaceCueDto(key, dto, out var entry, out error))
            {
                SetError(error);
                return false;
            }

            FocusBinding(entry.BindingKey, "cue");
            AfterMutation("已更新 cue 工作副本：" + entry.CueId);
            payload = new { bindingKey = entry.BindingKey, dirty = entry.IsDirty };
            return true;
        }

        private bool DispatchReplaceState(string payloadJson, out object payload, out string error)
        {
            payload = null;
            var key = ReadString(payloadJson, "originalBindingKey");
            var dto = ReadNestedDto<VfxStateBindingDto>(payloadJson, "replacement");
            if (!bindingSession.TryReplaceStateDto(key, dto, out var entry, out error))
            {
                SetError(error);
                return false;
            }

            FocusBinding(entry.BindingKey, "state");
            AfterMutation("已更新 state 工作副本：" + entry.StateId);
            payload = new { bindingKey = entry.BindingKey, dirty = entry.IsDirty };
            return true;
        }

        private bool DispatchSaveBinding(string payloadJson, out object payload, out string error)
        {
            payload = null;
            var key = ReadString(payloadJson, "bindingKey");
            var channel = ReadString(payloadJson, "channel");
            if (string.Equals(channel, "state", StringComparison.OrdinalIgnoreCase))
            {
                var entry = bindingSession.FindStateEntryByBindingKey(key);
                if (!bindingSession.TrySaveState(entry, out error))
                {
                    SetError(error);
                    return false;
                }

                AfterMutation("已保存 state：" + key, requireHotApply: true);
                payload = new { saved = true, channel = "state", bindingKey = key };
                return true;
            }

            var cueEntry = bindingSession.FindCueEntryByBindingKey(key);
            if (!bindingSession.TrySaveCue(cueEntry, out error))
            {
                SetError(error);
                return false;
            }

            AfterMutation("已保存 cue：" + key, requireHotApply: true);
            payload = new { saved = true, channel = "cue", bindingKey = key };
            return true;
        }

        private bool DispatchSaveAll(out object payload, out string error)
        {
            payload = null;
            pendingRecoverOverwriteConfirm = false;
            if (!bindingSession.TrySaveAll(out error))
            {
                SetError(error);
                return false;
            }

            AfterMutation("已保存全部脏绑定。", requireHotApply: true);
            payload = new { saved = true };
            return true;
        }

        private bool DispatchRevertBinding(string payloadJson, out object payload, out string error)
        {
            payload = null;
            error = null;
            var key = ReadString(payloadJson, "bindingKey");
            var channel = ReadString(payloadJson, "channel");
            if (string.Equals(channel, "state", StringComparison.OrdinalIgnoreCase))
            {
                bindingSession.RevertState(bindingSession.FindStateEntryByBindingKey(key));
            }
            else
            {
                bindingSession.RevertCue(bindingSession.FindCueEntryByBindingKey(key));
            }

            AfterMutation("已回撤绑定：" + key, requireHotApply: true);
            payload = new { reverted = true };
            return true;
        }

        private bool DispatchRevertAll(out object payload, out string error)
        {
            error = null;
            bindingSession.RevertAllDirty();
            AfterMutation("已回撤全部脏工作副本。", requireHotApply: true);
            payload = new { reverted = true };
            return true;
        }

        private bool DispatchReload(out object payload, out string error)
        {
            error = null;
            bindingSession.ReloadFromDisk();
            conflictKind = VfxWorkbenchConflictKind.None;
            blockedTransient = null;
            SessionState.EraseString(TransientSessionKey);
            AfterMutation("已从磁盘重载。", requireHotApply: true);
            payload = new { reloaded = true };
            return true;
        }

        private bool DispatchPreviewBinding(string payloadJson, out object payload, out string error)
        {
            payload = null;
            error = null;
            var key = ReadString(payloadJson, "bindingKey");
            var channel = ReadString(payloadJson, "channel");
            var includeDelay = ReadBool(payloadJson, "includeBindingDelay", defaultValue: true);
            var isState = string.Equals(channel, "state", StringComparison.OrdinalIgnoreCase);

            if (!IsPlayMode)
            {
                error = "预览需要在 Play Mode 下进行。";
                SetError(error);
                return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var vfx = NineGridArchitecture.Interface?.GetSystem<IVfxSystem>();
            if (vfx == null)
            {
                error = "VfxSystem 不可用。";
                SetError(error);
                return false;
            }

            var result = vfx.PreviewWorkbenchBinding(key, includeDelay, isState);
            payload = result;
            statusMessage = result.Succeeded ? "预览已触发：" + key : "预览失败：" + result.FailureReason;
            if (!result.Succeeded)
            {
                error = result.FailureReason;
                SetError(error);
            }

            BumpRevision();
            return result.Succeeded;
#else
            error = "不可用。";
            return false;
#endif
        }

        private bool DispatchClearStateSlot(string payloadJson, out object payload, out string error)
        {
            payload = null;
            error = null;
            var ownerLabel = ReadString(payloadJson, "ownerLabel");
            var slot = ReadString(payloadJson, "slot");
            if (!IsPlayMode)
            {
                error = "清除持续状态需要在 Play Mode 下进行。";
                SetError(error);
                return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var vfx = NineGridArchitecture.Interface?.GetSystem<IVfxSystem>();
            if (vfx == null)
            {
                error = "VfxSystem 不可用。";
                SetError(error);
                return false;
            }

            var result = vfx.ClearWorkbenchStateSlot(ownerLabel, slot);
            payload = new
            {
                outcome = result.Outcome.ToString(),
                failureReason = result.FailureReason,
            };
            statusMessage = "已请求清除槽：" + ownerLabel + "/" + slot;
            BumpRevision();
            return true;
#else
            error = "不可用。";
            return false;
#endif
        }

        private bool DispatchRecoverTransient(out object payload, out string error)
        {
            payload = null;
            error = null;
            if (blockedTransient == null)
            {
                error = "没有可恢复的临时版本。";
                SetError(error);
                return false;
            }

            bindingSession.LoadFromSnapshots(blockedTransient.baselineJson, blockedTransient.workingJson);
            focusedBindingKey = blockedTransient.focusedBindingKey ?? string.Empty;
            focusedChannel = blockedTransient.focusedChannel ?? "cue";
            aggregateLevel = blockedTransient.aggregateLevel ?? "instance";
            conflictKind = VfxWorkbenchConflictKind.None;
            blockedTransient = null;
            pendingRecoverOverwriteConfirm = true;
            AfterMutation("已恢复临时版本；下次保存将覆盖当前磁盘。", requireHotApply: true);
            payload = new { recovered = true, requiresSaveConfirm = true };
            return true;
        }

        private bool DispatchDiscardTransient(out object payload, out string error)
        {
            payload = null;
            error = null;
            SessionState.EraseString(TransientSessionKey);
            blockedTransient = null;
            conflictKind = VfxWorkbenchConflictKind.None;
            pendingRecoverOverwriteConfirm = false;
            bindingSession.ReloadFromDisk();
            AfterMutation("已丢弃临时版本并使用磁盘。", requireHotApply: true);
            payload = new { discarded = true };
            return true;
        }

        private void TryRestoreTransientOrConflict()
        {
            var raw = SessionState.GetString(TransientSessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            TransientPayload payload;
            try
            {
                payload = JsonUtility.FromJson<TransientPayload>(raw);
            }
            catch
            {
                SessionState.EraseString(TransientSessionKey);
                return;
            }

            if (payload == null)
            {
                SessionState.EraseString(TransientSessionKey);
                return;
            }

            var diskJson = bindingSession.SavedJson ?? string.Empty;
            if (string.Equals(diskJson, payload.baselineJson ?? string.Empty, StringComparison.Ordinal))
            {
                bindingSession.LoadFromSnapshots(payload.baselineJson, payload.workingJson);
                focusedBindingKey = payload.focusedBindingKey ?? string.Empty;
                focusedChannel = payload.focusedChannel ?? "cue";
                aggregateLevel = payload.aggregateLevel ?? "instance";
                statusMessage = string.IsNullOrEmpty(payload.statusMessage)
                    ? "已恢复未保存工作副本。"
                    : payload.statusMessage;
                conflictKind = VfxWorkbenchConflictKind.None;
                blockedTransient = null;
                HotApplyIfPlaying();
                return;
            }

            conflictKind = VfxWorkbenchConflictKind.DiskChanged;
            blockedTransient = payload;
            statusMessage = "磁盘相对临时基线已变化：写操作已阻塞，请显式恢复或丢弃临时版。";
            SetError(statusMessage);
        }

        private void FocusBinding(string bindingKey, string channel)
        {
            focusedBindingKey = bindingKey ?? string.Empty;
            focusedChannel = string.IsNullOrWhiteSpace(channel) ? "cue" : channel;
            PersistTransient();
            BumpRevision();
        }

        private void AfterMutation(string status, bool requireHotApply = false)
        {
            statusMessage = status ?? string.Empty;
            ClearError();
            PersistTransient();
            BumpRevision();
            if (requireHotApply || IsPlayMode)
            {
                HotApplyIfPlaying();
            }
        }

        private void HotApplyIfPlaying()
        {
            if (!IsPlayMode || BlocksMutations)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var vfx = NineGridArchitecture.Interface?.GetSystem<IVfxSystem>();
            if (vfx == null)
            {
                return;
            }

            var result = vfx.ApplyWorkbenchCatalog(bindingSession.BuildWorkingJson());
            if (!result.Succeeded)
            {
                SetError("热应用失败：" + result.Error);
            }
#endif
        }

        private void RefreshRuntimeHistory()
        {
            if (!IsPlayMode)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var vfx = NineGridArchitecture.Interface?.GetSystem<IVfxSystem>();
            var snap = vfx?.GetWorkbenchSnapshot();
            if (snap?.History == null)
            {
                return;
            }

            var proxies = new List<VfxHistoryRecordProxy>(snap.History.Count);
            for (var i = 0; i < snap.History.Count; i++)
            {
                var record = snap.History[i];
                if (record == null)
                {
                    continue;
                }

                proxies.Add(new VfxHistoryRecordProxy
                {
                    Sequence = record.Sequence,
                    Outcome = record.Outcome.ToString(),
                    CueId = record.CueId,
                    CueNote = record.CueNote,
                    BindingKey = record.BindingKey,
                    DiagnosticSource = record.DiagnosticSource,
                    PlayerId = record.PlayerId,
                    MaterialKey = record.MaterialKey,
                    VariantId = record.VariantId,
                    InstanceId = record.InstanceId,
                    FailureReason = record.FailureReason,
                    Time = record.Time,
                });
            }

            bindingSession.SetPlaybackHistory(proxies);
#endif
        }

        private void BumpRevision()
        {
            localRevision++;
        }

        private void SetError(string message)
        {
            errorMessage = message ?? string.Empty;
        }

        private void ClearError()
        {
            errorMessage = string.Empty;
        }

        private static string ReadString(string json, string field)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            try
            {
                var dict = Newtonsoft.Json.Linq.JObject.Parse(json);
                return dict[field]?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool ReadBool(string json, string field, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return defaultValue;
            }

            try
            {
                var dict = Newtonsoft.Json.Linq.JObject.Parse(json);
                return dict[field]?.ToObject<bool>() ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private static T ReadNestedDto<T>(string json, string field) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var dict = Newtonsoft.Json.Linq.JObject.Parse(json);
                var token = dict[field];
                return token == null || token.Type == Newtonsoft.Json.Linq.JTokenType.Null
                    ? null
                    : token.ToObject<T>();
            }
            catch
            {
                return null;
            }
        }

        [Serializable]
        private sealed class TransientPayload
        {
            public string baselineJson;
            public string workingJson;
            public string focusedBindingKey;
            public string focusedChannel;
            public string aggregateLevel;
            public string statusMessage;
        }
    }
}
#endif
