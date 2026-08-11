#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NineGrid.Content.Audio;
using NineGrid.Core;
using NineGrid.Presentation.Systems;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public enum AudioWorkbenchConflictKind
    {
        None = 0,
        DiskChanged = 1,
    }

    /// <summary>
    /// #187 单一作者态权威：双 Session、焦点、状态、瞬态恢复与冲突门禁；Play Mode 下 SFX 脏改热应用。
    /// </summary>
    public sealed class AudioWorkbenchEditorState
    {
        public const string TransientSessionKey = "NineGrid.AudioWorkbench.Transient.v1";

        private static AudioWorkbenchEditorState instance;

        private readonly AudioBindingEditorSession sfxSession = new AudioBindingEditorSession();
        private readonly MusicBindingEditorSession musicSession = new MusicBindingEditorSession();
        private string statusMessage = string.Empty;
        private string errorMessage = string.Empty;
        private string focusedBindingKey = string.Empty;
        private AudioWorkbenchConflictKind conflictKind;
        private TransientPayload blockedTransient;
        private bool pendingRecoverOverwriteConfirm;
        private long localRevision = 1;
        private long lastObservedRuntimeRevision = -1;
        private long lastObservedHistorySequence = -1;
        private int lastObservedMusicHistoryCount = -1;
        private int lastObservedPlayingSourceCount = -1;
        private long lastObservedPlayingFingerprint = -1;
        private int lastObservedUnclaimedSfxCount = -1;
        private int lastObservedSceneOrphanCount = -1;
        private int lastObservedPersistAnomalyCount = -1;
        private int lastObservedMusicPlayingCount = -1;
        private int lastObservedMusicUnknownCount = -1;

        public static AudioWorkbenchEditorState Instance => instance ??= new AudioWorkbenchEditorState();

        public AudioBindingEditorSession SfxSession => sfxSession;
        public MusicBindingEditorSession MusicSession => musicSession;
        public string StatusMessage => statusMessage;
        public string ErrorMessage => errorMessage;
        public string FocusedBindingKey => focusedBindingKey;
        public AudioWorkbenchConflictKind ConflictKind => conflictKind;
        public bool HasTransientConflict => conflictKind != AudioWorkbenchConflictKind.None;
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
            sfxSession.ReloadFromDisk();
            musicSession.ReloadFromDisk();
            conflictKind = AudioWorkbenchConflictKind.None;
            blockedTransient = null;
            pendingRecoverOverwriteConfirm = false;
            ClearError();
            statusMessage = "已从磁盘加载 SFX/BGM。";
            TryRestoreTransientOrConflict();
            BumpRevision();
            HotApplySfxIfPlaying();
        }

        /// <summary>测试缝：在已装入 Session 的前提下重评 SessionState 瞬态恢复/冲突。</summary>
        public void ReevaluateTransientForTests()
        {
            TryRestoreTransientOrConflict();
            BumpRevision();
        }

        public void PersistTransient()
        {
            var payload = new TransientPayload
            {
                baselineSfxJson = sfxSession.SavedJson ?? string.Empty,
                workingSfxJson = sfxSession.BuildWorkingJson() ?? string.Empty,
                baselineMusicJson = musicSession.SavedJson ?? string.Empty,
                workingMusicJson = musicSession.BuildWorkingJson() ?? string.Empty,
                focusedBindingKey = focusedBindingKey ?? string.Empty,
                statusMessage = statusMessage ?? string.Empty,
            };
            SessionState.SetString(TransientSessionKey, JsonUtility.ToJson(payload));
        }

        public void OnBeforeAssemblyReload()
        {
            PersistTransient();
        }

        public void FocusBinding(string bindingKey)
        {
            focusedBindingKey = bindingKey ?? string.Empty;
            var entry = sfxSession.FindEntryByBindingKey(focusedBindingKey);
            if (entry != null)
            {
                sfxSession.Focus(entry);
            }

            PersistTransient();
            BumpRevision();
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
                    var key = ReadString(payloadJson, "bindingKey");
                    FocusBinding(key);
                    payload = new { focusedBindingKey };
                    return true;
                }
                case "createDraft":
                    if (!EnsureMutationsAllowed(out error)) return false;
                    return DispatchCreateDraft(payloadJson, out payload, out error);
                case "replaceSfxBinding":
                    if (!EnsureMutationsAllowed(out error)) return false;
                    return DispatchReplaceSfx(payloadJson, out payload, out error);
                case "replaceMusicBinding":
                    if (!EnsureMutationsAllowed(out error)) return false;
                    return DispatchReplaceMusic(payloadJson, out payload, out error);
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
                case "previewClip":
                    return DispatchPreviewClip(payloadJson, out payload, out error);
                case "stopPreview":
                    AudioBindingEditorPreview.Stop();
                    EndMusicPreviewInternal();
                    statusMessage = "已停止试听。";
                    BumpRevision();
                    payload = new { stopped = true };
                    return true;
                case "stopSfxSource":
                    return DispatchStopSfxSource(payloadJson, out payload, out error);
                case "stopAllSfxSources":
                    return DispatchStopAllSfxSources(out payload, out error);
                case "stopMusicSource":
                    return DispatchStopMusicSource(payloadJson, out payload, out error);
                case "runAiBindPreserve":
                    if (!EnsureMutationsAllowed(out error)) return false;
                    return DispatchAiBind(out payload, out error);
                case "pingClipAsset":
                    return DispatchPingClip(payloadJson, out payload, out error);
                case "stopUnknownMusic":
                    return DispatchStopUnknownMusic(out payload, out error);
                case "previewMusic":
                    return DispatchPreviewMusic(payloadJson, out payload, out error);
                case "endMusicPreview":
                    EndMusicPreviewInternal();
                    statusMessage = "已结束 BGM 试听。";
                    BumpRevision();
                    payload = new { ended = true };
                    return true;
                case "recoverTransient":
                    return DispatchRecoverTransient(out payload, out error);
                case "discardTransient":
                    return DispatchDiscardTransient(out payload, out error);
                default:
                    error = "未知命令：" + command;
                    return false;
            }
        }

        /// <summary>
        /// Play Mode 下比较运行时指纹；有变化则抬升 LocalRevision，供 loopback 推送抓音流。
        /// </summary>
        public bool TickRuntimeObservation()
        {
            if (!IsPlayMode)
            {
                lastObservedRuntimeRevision = -1;
                lastObservedHistorySequence = -1;
                lastObservedMusicHistoryCount = -1;
                lastObservedPlayingSourceCount = -1;
                lastObservedPlayingFingerprint = -1;
                lastObservedUnclaimedSfxCount = -1;
                lastObservedSceneOrphanCount = -1;
                lastObservedPersistAnomalyCount = -1;
                lastObservedMusicPlayingCount = -1;
                lastObservedMusicUnknownCount = -1;
                return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RefreshRuntimeHistory();
            long runtimeRevision = 0;
            long historySequence = 0;
            var playingCount = 0;
            var unclaimedSfx = 0;
            var sceneOrphans = 0;
            var persistAnomalies = 0;
            var musicHistoryCount = 0;
            var musicPlaying = 0;
            var musicUnknown = 0;
            long playingFingerprint = 0;
            var architecture = NineGridArchitecture.Interface;
            var audio = architecture?.GetSystem<IAudioSystem>();
            if (audio != null)
            {
                var snap = audio.GetWorkbenchSnapshot();
                runtimeRevision = snap?.Revision ?? 0;
                playingCount = snap?.PlayingSources?.Count ?? 0;
                sceneOrphans = snap?.SceneOrphans?.Count ?? 0;
                persistAnomalies = snap?.PersistAnomalies?.Count ?? 0;
                if (snap?.PlayingSources != null)
                {
                    for (var i = 0; i < snap.PlayingSources.Count; i++)
                    {
                        var src = snap.PlayingSources[i];
                        if (!src.Claimed)
                        {
                            unclaimedSfx++;
                        }

                        var sid = src.SourceId ?? string.Empty;
                        unchecked
                        {
                            playingFingerprint = (playingFingerprint * 397) ^ sid.GetHashCode();
                        }
                    }
                }

                if (snap?.History != null && snap.History.Count > 0)
                {
                    historySequence = snap.History[snap.History.Count - 1].Sequence;
                }
            }

            var music = architecture?.GetSystem<IMusicSystem>();
            if (music != null)
            {
                if (music.History != null)
                {
                    musicHistoryCount = music.History.Count;
                }

                var audit = music.LastAudit ?? music.AuditMusicTrack("AudioWorkbench.Tick");
                musicPlaying = audit?.ActualSources?.Count ?? 0;
                musicUnknown = audit?.UnknownSources?.Count ?? 0;
                if (audit?.ActualSources != null)
                {
                    for (var i = 0; i < audit.ActualSources.Count; i++)
                    {
                        var sid = audit.ActualSources[i].SourceId ?? string.Empty;
                        unchecked
                        {
                            playingFingerprint = (playingFingerprint * 397) ^ (("m:" + sid).GetHashCode());
                        }
                    }
                }
            }

            var changed = runtimeRevision != lastObservedRuntimeRevision
                || historySequence != lastObservedHistorySequence
                || playingCount != lastObservedPlayingSourceCount
                || playingFingerprint != lastObservedPlayingFingerprint
                || unclaimedSfx != lastObservedUnclaimedSfxCount
                || sceneOrphans != lastObservedSceneOrphanCount
                || persistAnomalies != lastObservedPersistAnomalyCount
                || musicHistoryCount != lastObservedMusicHistoryCount
                || musicPlaying != lastObservedMusicPlayingCount
                || musicUnknown != lastObservedMusicUnknownCount;
            lastObservedRuntimeRevision = runtimeRevision;
            lastObservedHistorySequence = historySequence;
            lastObservedPlayingSourceCount = playingCount;
            lastObservedPlayingFingerprint = playingFingerprint;
            lastObservedUnclaimedSfxCount = unclaimedSfx;
            lastObservedSceneOrphanCount = sceneOrphans;
            lastObservedPersistAnomalyCount = persistAnomalies;
            lastObservedMusicHistoryCount = musicHistoryCount;
            lastObservedMusicPlayingCount = musicPlaying;
            lastObservedMusicUnknownCount = musicUnknown;
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
            var clipOptions = sfxSession.ClipOptions.Select(option => new
            {
                resourcesKey = option.ResourcesKey,
                assetPath = option.AssetPath,
            }).ToArray();

            var declarations = sfxSession.Entries.Select(entry =>
            {
                var saved = entry.GetSavedDto();
                return new
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
                    isBroken = sfxSession.IsBroken(entry),
                    authoringStatus = entry.Dto?.authoringStatus,
                    savedEnabled = saved?.enabled ?? entry.Dto?.enabled ?? true,
                    dto = entry.Dto,
                };
            }).ToArray();

            var musicEntries = musicSession.Entries.Select(entry => new
            {
                state = entry.State,
                isDirty = entry.IsDirty,
                dto = entry.Dto,
            }).ToArray();

            object runtime = null;
            object musicRuntime = null;
            var architecture = NineGridArchitecture.Interface;
            if (architecture != null && IsPlayMode)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var audio = architecture.GetSystem<IAudioSystem>();
                if (audio != null)
                {
                    var snap = audio.GetWorkbenchSnapshot();
                    runtime = ProjectRuntime(snap);
                }

                var music = architecture.GetSystem<IMusicSystem>();
                if (music != null)
                {
                    musicRuntime = ProjectMusicRuntime(music);
                }
#endif
            }

            return new
            {
                playMode = IsPlayMode,
                conflict = conflictKind.ToString(),
                blocksMutations = BlocksMutations,
                pendingRecoverOverwriteConfirm,
                focusedBindingKey,
                statusMessage,
                errorMessage,
                sfxDirtyCount = sfxSession.DirtyCount,
                musicDirtyCount = musicSession.DirtyCount,
                sfxSavedJson = sfxSession.SavedJson,
                sfxWorkingJson = sfxSession.BuildWorkingJson(),
                musicSavedJson = musicSession.SavedJson,
                musicWorkingJson = musicSession.BuildWorkingJson(),
                declarations,
                clipOptions,
                musicEntries,
                runtime,
                musicRuntime,
                modes = new[] { "实时抓音", "静态绑定库", "BGM" },
                defaultMode = IsPlayMode ? "实时抓音" : "静态绑定库",
            };
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static object ProjectRuntime(AudioWorkbenchSnapshot snap)
        {
            if (snap == null)
            {
                return null;
            }

            return new
            {
                revision = snap.Revision,
                history = (snap.History ?? Array.Empty<AudioHistoryRecord>()).Select(ProjectHistory).ToArray(),
                playingSources = (snap.PlayingSources ?? Array.Empty<SfxTrackSourceSnapshot>())
                    .Select(s => new
                    {
                        sourceId = s.SourceId,
                        clipKey = s.ClipKey,
                        cueId = s.CueId,
                        playbackPositionSeconds = s.PlaybackPositionSeconds,
                        loop = s.Loop,
                        isPlaying = s.IsPlaying,
                        claimed = s.Claimed,
                        workbenchPreview = s.WorkbenchPreview,
                    }).ToArray(),
                sceneOrphans = (snap.SceneOrphans ?? Array.Empty<SceneAudioOrphanSnapshot>())
                    .Select(o => new
                    {
                        gameObjectPath = o.GameObjectPath,
                        clipName = o.ClipName,
                        playbackPositionSeconds = o.PlaybackPositionSeconds,
                        loop = o.Loop,
                    }).ToArray(),
                persistAnomalies = (snap.PersistAnomalies ?? Array.Empty<AudioPersistAnomalySnapshot>())
                    .Select(a => new
                    {
                        sourceId = a.SourceId,
                        clipKey = a.ClipKey,
                        cueId = a.CueId,
                        loop = a.Loop,
                        reason = a.Reason,
                        time = a.Time,
                    }).ToArray(),
                aggregates = (snap.Aggregates ?? Array.Empty<AudioCueAggregate>()).Select(a => new
                {
                    aggregateKey = a.AggregateKey,
                    bindingKey = a.BindingKey,
                    cueId = a.CueId,
                    requested = a.Requested,
                    played = a.Played,
                    suppressed = a.Suppressed,
                    cooldown = a.Cooldown,
                    unbound = a.Unbound,
                    backendFailure = a.BackendFailure,
                    lastTime = a.LastTime,
                    lastFailureReason = a.LastFailureReason,
                    recentTimestamps = a.RecentTimestamps,
                }).ToArray(),
            };
        }

        private static object ProjectHistory(AudioHistoryRecord record)
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
                cardDefId = record.CardDefId,
                skillId = record.SkillId,
                roomId = record.RoomId,
                itemDefId = record.ItemDefId,
                contentId = record.ContentId,
                diagnosticCardUid = record.DiagnosticCardUid,
                actualClipKey = record.ActualClipKey,
                variantId = record.VariantId,
                sourceId = record.SourceId,
                failureReason = record.FailureReason,
                scheduleKey = record.ScheduleKey,
                scheduleDelaySeconds = record.ScheduleDelaySeconds,
                time = record.Time,
            };
        }

        private static object ProjectMusicRuntime(IMusicSystem music)
        {
            var audit = music.LastAudit ?? music.AuditMusicTrack("AudioWorkbench.Snapshot");
            var claimed = new HashSet<string>(
                audit?.ClaimedSourceIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            return new
            {
                desired = music.DesiredState?.ToString(),
                current = music.CurrentState?.ToString(),
                currentClipKey = music.CurrentClipKey,
                currentMusicGeneration = music.CurrentMusicGeneration,
                currentSourceCount = music.CurrentSourceCount,
                retiringSourceCount = music.RetiringSourceCount,
                currentStableSource = music.CurrentStableSource,
                playingSources = (audit?.ActualSources ?? Array.Empty<MusicTrackSourceSnapshot>())
                    .Select(s => new
                    {
                        sourceId = s.SourceId,
                        clipKey = s.ClipKey,
                        playbackPositionSeconds = s.PlaybackPositionSeconds,
                        isPlaying = s.IsPlaying,
                        claimed = claimed.Contains(s.SourceId),
                        loop = true,
                    }).ToArray(),
                history = (music.History ?? Array.Empty<MusicHistoryRecord>()).Select(h => new
                {
                    outcome = h.Outcome.ToString(),
                    state = h.State.ToString(),
                    bindingClipKey = h.BindingClipKey,
                    actualClipKey = h.ActualClipKey,
                    musicGeneration = h.MusicGeneration,
                    stableSource = h.StableSource,
                    reason = h.Reason,
                    time = h.Time,
                }).ToArray(),
                lastAudit = audit == null ? null : new
                {
                    trigger = audit.Trigger,
                    hasUnknownSources = audit.HasUnknownSources,
                    claimedSourceIds = audit.ClaimedSourceIds,
                    unknownSources = audit.UnknownSources?.Select(s => new
                    {
                        sourceId = s.SourceId,
                        clipKey = s.ClipKey,
                    }).ToArray(),
                },
                anomalies = (music.OverlapAnomalies ?? Array.Empty<MusicOverlapAnomaly>()).Select(a => new
                {
                    trigger = a.Trigger,
                    musicGeneration = a.MusicGeneration,
                    desiredState = a.DesiredState,
                    reason = a.Trigger,
                    claimedSourceIds = a.ClaimedSourceIds,
                    unknownSources = a.UnknownSources?.Select(s => new
                    {
                        sourceId = s.SourceId,
                        clipKey = s.ClipKey,
                    }).ToArray(),
                    time = a.Time,
                }).ToArray(),
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

        private bool IsRecoveryCommandContext { get; set; }

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

            var diskSfx = sfxSession.SavedJson ?? string.Empty;
            var diskMusic = musicSession.SavedJson ?? string.Empty;
            var baselineMatches =
                string.Equals(diskSfx, payload.baselineSfxJson ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(diskMusic, payload.baselineMusicJson ?? string.Empty, StringComparison.Ordinal);

            if (baselineMatches)
            {
                sfxSession.LoadFromSnapshots(
                    payload.baselineSfxJson,
                    payload.workingSfxJson,
                    CaptureDeclarations(),
                    sfxSession.ClipOptions.ToArray());
                musicSession.LoadFromSnapshots(payload.baselineMusicJson, payload.workingMusicJson);
                focusedBindingKey = payload.focusedBindingKey ?? string.Empty;
                statusMessage = string.IsNullOrEmpty(payload.statusMessage)
                    ? "已恢复未保存工作副本。"
                    : payload.statusMessage;
                conflictKind = AudioWorkbenchConflictKind.None;
                blockedTransient = null;
                HotApplySfxIfPlaying();
                return;
            }

            conflictKind = AudioWorkbenchConflictKind.DiskChanged;
            blockedTransient = payload;
            statusMessage = "磁盘相对临时基线已变化：写操作已阻塞，请显式恢复或丢弃临时版。";
            SetError(statusMessage);
        }

        private IEnumerable<AudioBindingEditorDeclaration> CaptureDeclarations()
        {
            return sfxSession.Entries
                .Select(entry => new AudioBindingEditorDeclaration(
                    entry.CueId,
                    entry.Note,
                    entry.Module,
                    entry.AuthoritativeEmitter))
                .GroupBy(d => d.CueId, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToArray();
        }

        private bool DispatchCreateDraft(string payloadJson, out object payload, out string error)
        {
            payload = null;
            var cueId = ReadString(payloadJson, "cueId");
            if (!sfxSession.TryCreateDraft(cueId, out var entry, out error))
            {
                SetError(error);
                return false;
            }

            FocusBinding(entry.BindingKey);
            AfterMutation("已创建草稿：" + entry.CueId);
            payload = new { bindingKey = entry.BindingKey, cueId = entry.CueId };
            return true;
        }

        private bool DispatchReplaceSfx(string payloadJson, out object payload, out string error)
        {
            payload = null;
            var key = ReadString(payloadJson, "originalBindingKey");
            var dto = ReadNestedDto<AudioBindingDto>(payloadJson, "replacement");
            if (dto == null && string.IsNullOrWhiteSpace(payloadJson))
            {
                error = "空补丁。";
                SetError(error);
                return false;
            }

            if (!sfxSession.TryReplaceWorkingDto(key, dto, out var entry, out error))
            {
                SetError(error);
                return false;
            }

            FocusBinding(entry.BindingKey);
            AfterMutation("已更新 SFX 工作副本：" + entry.CueId);
            payload = new { bindingKey = entry.BindingKey, dirty = entry.IsDirty };
            return true;
        }

        private bool DispatchReplaceMusic(string payloadJson, out object payload, out string error)
        {
            payload = null;
            var state = ReadString(payloadJson, "state");
            var dto = ReadNestedDto<MusicBindingDto>(payloadJson, "replacement");
            if (!musicSession.TryReplaceWorkingDto(state, dto, out var entry, out error))
            {
                SetError(error);
                return false;
            }

            AfterMutation("已更新 BGM 工作副本：" + entry.State);
            payload = new { state = entry.State, dirty = entry.IsDirty };
            return true;
        }

        private bool DispatchSaveBinding(string payloadJson, out object payload, out string error)
        {
            payload = null;
            var key = ReadString(payloadJson, "bindingKey");
            var channel = ReadString(payloadJson, "channel");
            if (string.Equals(channel, "music", StringComparison.OrdinalIgnoreCase)
                || string.Equals(channel, "bgm", StringComparison.OrdinalIgnoreCase))
            {
                var musicEntry = musicSession.Entries.FirstOrDefault(e =>
                    string.Equals(e.State, key, StringComparison.OrdinalIgnoreCase));
                if (!musicSession.TrySave(musicEntry, out error))
                {
                    SetError(error);
                    return false;
                }

                AfterMutation("已保存 BGM：" + key, requireHotApply: true);
                payload = new { saved = true, channel = "music", key };
                return true;
            }

            var entry = sfxSession.FindEntryByBindingKey(key);
            if (pendingRecoverOverwriteConfirm)
            {
                // Explicit recover path: next save is allowed and clears the confirm gate.
                pendingRecoverOverwriteConfirm = false;
            }

            if (!TryValidateHygiene(out error))
            {
                SetError(error);
                return false;
            }

            if (!sfxSession.TrySave(entry, out error))
            {
                SetError(error);
                return false;
            }

            AfterMutation("已保存 SFX：" + (entry?.CueId ?? key), requireHotApply: true);
            payload = new { saved = true, channel = "sfx", bindingKey = entry?.BindingKey };
            return true;
        }

        private bool DispatchSaveAll(out object payload, out string error)
        {
            payload = null;
            if (!TryValidateHygiene(out error))
            {
                SetError(error);
                return false;
            }

            pendingRecoverOverwriteConfirm = false;
            if (!sfxSession.TrySaveAll(out error) || !musicSession.TrySaveAll(out error))
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
            if (string.Equals(channel, "music", StringComparison.OrdinalIgnoreCase)
                || string.Equals(channel, "bgm", StringComparison.OrdinalIgnoreCase))
            {
                var musicEntry = musicSession.Entries.FirstOrDefault(e =>
                    string.Equals(e.State, key, StringComparison.OrdinalIgnoreCase));
                musicSession.Revert(musicEntry);
                AfterMutation("已回撤 BGM：" + key, requireHotApply: true);
                payload = new { reverted = true, channel = "music" };
                return true;
            }

            var entry = sfxSession.FindEntryByBindingKey(key);
            sfxSession.Revert(entry);
            AfterMutation("已回撤 SFX：" + (entry?.CueId ?? key), requireHotApply: true);
            payload = new { reverted = true, channel = "sfx" };
            return true;
        }

        private bool DispatchRevertAll(out object payload, out string error)
        {
            error = null;
            sfxSession.RevertAllDirty();
            musicSession.RevertAllDirty();
            AfterMutation("已回撤全部脏工作副本。", requireHotApply: true);
            payload = new { reverted = true };
            return true;
        }

        private bool DispatchReload(out object payload, out string error)
        {
            error = null;
            sfxSession.ReloadFromDisk();
            musicSession.ReloadFromDisk();
            conflictKind = AudioWorkbenchConflictKind.None;
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
            var includeDelay = ReadBool(payloadJson, "includeBindingDelay");
            var entry = sfxSession.FindEntryByBindingKey(key);
            if (entry?.Dto == null)
            {
                error = "找不到绑定：" + key;
                SetError(error);
                return false;
            }

            if (IsPlayMode)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var audio = NineGridArchitecture.Interface?.GetSystem<IAudioSystem>();
                if (audio == null)
                {
                    error = "IAudioSystem 未注册。";
                    SetError(error);
                    return false;
                }

                var result = audio.PreviewWorkbenchBinding(entry.BindingKey, includeDelay);
                payload = result;
                statusMessage = result.Succeeded
                    ? "试听工作副本：" + entry.Note
                    : result.FailureReason;
                if (!result.Succeeded)
                {
                    error = result.FailureReason;
                    SetError(error);
                }

                BumpRevision();
                return result.Succeeded;
#endif
            }

            var option = sfxSession.FindClipOption(entry.Dto.clipKey);
            var clip = option == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(option.AssetPath);
            var ok = AudioBindingEditorPreview.Play(clip, entry.Dto.startOffsetSeconds);
            if (!ok)
            {
                error = "非运行时试听失败（仅 clip/startOffset；AudioUtil 不可用或素材缺失）。";
                SetError(error);
                return false;
            }

            statusMessage = "非运行时试听（仅 clip/startOffset）：" + entry.Note;
            ClearError();
            BumpRevision();
            payload = new { succeeded = true, limited = true };
            return true;
        }

        private bool DispatchPreviewClip(string payloadJson, out object payload, out string error)
        {
            payload = null;
            var clipKey = ReadString(payloadJson, "clipKey");
            var offset = ReadFloat(payloadJson, "startOffsetSeconds");
            var option = sfxSession.FindClipOption(clipKey);
            var clip = option == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(option.AssetPath);
            if (!AudioBindingEditorPreview.Play(clip, offset))
            {
                error = "无法试听素材：" + clipKey;
                SetError(error);
                return false;
            }

            error = null;
            statusMessage = "正在试听素材：" + clipKey;
            ClearError();
            BumpRevision();
            payload = new { succeeded = true };
            return true;
        }

        private bool DispatchStopSfxSource(string payloadJson, out object payload, out string error)
        {
            payload = null;
            error = null;
            var sourceId = ReadString(payloadJson, "sourceId");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var audio = NineGridArchitecture.Interface?.GetSystem<IAudioSystem>();
            var stopped = audio != null && audio.StopSfxSource(sourceId);
            payload = new { stopped };
            statusMessage = stopped ? "已停止 SourceId：" + sourceId : "停止失败：" + sourceId;
            BumpRevision();
            return stopped;
#else
            error = "StopSfxSource 仅 Editor/Development 可用。";
            return false;
#endif
        }

        private bool DispatchStopAllSfxSources(out object payload, out string error)
        {
            payload = null;
            error = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var audio = NineGridArchitecture.Interface?.GetSystem<IAudioSystem>();
            if (audio == null)
            {
                error = "IAudioSystem 未注册。";
                SetError(error);
                return false;
            }

            var stopped = audio.StopAllSfxSources();
            payload = new { stopped };
            statusMessage = "已停止 " + stopped + " 条 SFX 源。";
            ClearError();
            BumpRevision();
            return true;
#else
            error = "StopAllSfxSources 仅 Editor/Development 可用。";
            return false;
#endif
        }

        private bool DispatchStopMusicSource(string payloadJson, out object payload, out string error)
        {
            payload = null;
            error = null;
            var sourceId = ReadString(payloadJson, "sourceId");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var music = NineGridArchitecture.Interface?.GetSystem<IMusicSystem>();
            if (music == null)
            {
                error = "MusicSystem 未注册。";
                SetError(error);
                return false;
            }

            var stopped = music.StopMusicSource(sourceId);
            payload = new { stopped };
            statusMessage = stopped ? "已停止 Music SourceId：" + sourceId : "停止 Music 失败：" + sourceId;
            if (stopped)
            {
                ClearError();
            }
            else
            {
                SetError(statusMessage);
            }

            BumpRevision();
            return stopped;
#else
            error = "StopMusicSource 仅 Editor/Development 可用。";
            return false;
#endif
        }

        private bool DispatchAiBind(out object payload, out string error)
        {
            error = null;
            var result = AudioAiInitialBinder.RunAndWrite(forceRebindAll: false);
            sfxSession.ReloadFromDisk();
            musicSession.ReloadFromDisk();
            AfterMutation(result?.Report?.summary ?? "AI 绑定完成。", requireHotApply: true);
            payload = new { summary = statusMessage };
            return true;
        }

        private bool DispatchPingClip(string payloadJson, out object payload, out string error)
        {
            payload = null;
            var clipKey = ReadString(payloadJson, "clipKey");
            var option = sfxSession.FindClipOption(clipKey);
            if (option == null || string.IsNullOrEmpty(option.AssetPath))
            {
                error = "找不到素材：" + clipKey;
                SetError(error);
                return false;
            }

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(option.AssetPath);
            if (asset == null)
            {
                error = "素材未导入：" + option.AssetPath;
                SetError(error);
                return false;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            error = null;
            statusMessage = "已定位素材：" + option.AssetPath;
            ClearError();
            BumpRevision();
            payload = new { assetPath = option.AssetPath };
            return true;
        }

        private bool DispatchStopUnknownMusic(out object payload, out string error)
        {
            payload = null;
            error = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var music = NineGridArchitecture.Interface?.GetSystem<IMusicSystem>();
            if (music == null)
            {
                error = "MusicSystem 未注册。";
                SetError(error);
                return false;
            }

            var audit = music.StopUnknownMusic("AudioWorkbenchEditorState.StopUnknownMusic");
            statusMessage = audit.HasUnknownSources
                ? "仍有 " + audit.UnknownSources.Count + " 条未知 Music 来源。"
                : "未知 Music 来源已收口。";
            ClearError();
            BumpRevision();
            payload = audit;
            return true;
#else
            error = "不可用。";
            return false;
#endif
        }

        private bool DispatchPreviewMusic(string payloadJson, out object payload, out string error)
        {
            payload = null;
            error = null;
            var state = ReadString(payloadJson, "state");
            var entry = musicSession.Entries.FirstOrDefault(e =>
                string.Equals(e.State, state, StringComparison.OrdinalIgnoreCase));
            if (entry?.Dto == null)
            {
                error = "找不到 BGM：" + state;
                SetError(error);
                return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var music = NineGridArchitecture.Interface?.GetSystem<IMusicSystem>();
            if (music == null)
            {
                error = "MusicSystem 未注册。";
                SetError(error);
                return false;
            }

            var result = music.BeginPreview(new MusicPreviewRequest(
                entry.Dto.clipKey,
                entry.Dto.volumeDb,
                entry.Dto.startOffsetSeconds,
                entry.Dto.fadeInSeconds,
                entry.Dto.loop));
            payload = result;
            statusMessage = result.Succeeded ? "正在试听 BGM：" + entry.State : result.Reason;
            if (!result.Succeeded)
            {
                error = result.Reason;
                SetError(error);
            }

            BumpRevision();
            return result.Succeeded;
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

            IsRecoveryCommandContext = true;
            try
            {
                sfxSession.LoadFromSnapshots(
                    blockedTransient.baselineSfxJson,
                    blockedTransient.workingSfxJson,
                    CaptureDeclarations(),
                    sfxSession.ClipOptions.ToArray());
                musicSession.LoadFromSnapshots(
                    blockedTransient.baselineMusicJson,
                    blockedTransient.workingMusicJson);
                focusedBindingKey = blockedTransient.focusedBindingKey ?? string.Empty;
                conflictKind = AudioWorkbenchConflictKind.None;
                blockedTransient = null;
                pendingRecoverOverwriteConfirm = true;
                AfterMutation("已恢复临时版本；下次保存将覆盖当前磁盘（需确认）。", requireHotApply: true);
                payload = new { recovered = true, requiresSaveConfirm = true };
                return true;
            }
            finally
            {
                IsRecoveryCommandContext = false;
            }
        }

        private bool DispatchDiscardTransient(out object payload, out string error)
        {
            payload = null;
            error = null;
            SessionState.EraseString(TransientSessionKey);
            blockedTransient = null;
            conflictKind = AudioWorkbenchConflictKind.None;
            pendingRecoverOverwriteConfirm = false;
            sfxSession.ReloadFromDisk();
            musicSession.ReloadFromDisk();
            AfterMutation("已丢弃临时版本并使用磁盘。", requireHotApply: true);
            payload = new { discarded = true };
            return true;
        }

        private bool TryValidateHygiene(out string error)
        {
            error = null;
            var catalog = sfxSession.BuildWorkingCatalog();
            var declared = sfxSession.Entries
                .Select(e => e.CueId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var findings = AudioBindingCatalogHygieneValidator.Validate(
                catalog,
                sfxSession.KnownClipKeys,
                declared);
            var critical = findings.Where(IsCriticalFinding).ToArray();
            if (critical.Length == 0)
            {
                return true;
            }

            error = string.Join("\n", critical.Select(f => f.ToString()));
            return false;
        }

        private static bool IsCriticalFinding(AudioBindingCatalogHygieneValidator.Finding finding)
        {
            if (finding == null)
            {
                return false;
            }

            switch (finding.Category)
            {
                case "duplicate-binding-key":
                case "coverage-conflict":
                case "missing-clip":
                case "forbidden-path":
                case "orphan-binding":
                case "param-range":
                case "pool-null":
                case "pool-weight":
                case "empty-pool":
                case "catalog-null":
                case "null-row":
                case "empty-cue":
                    return true;
                default:
                    return false;
            }
        }

        private void AfterMutation(string status, bool requireHotApply = false)
        {
            statusMessage = status ?? string.Empty;
            ClearError();
            PersistTransient();
            BumpRevision();
            if (requireHotApply || IsPlayMode)
            {
                HotApplySfxIfPlaying();
            }
        }

        private void HotApplySfxIfPlaying()
        {
            if (!IsPlayMode || BlocksMutations)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var audio = NineGridArchitecture.Interface?.GetSystem<IAudioSystem>();
            if (audio == null)
            {
                return;
            }

            var result = audio.ApplyWorkbenchCatalog(sfxSession.BuildWorkingJson());
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
            var audio = NineGridArchitecture.Interface?.GetSystem<IAudioSystem>();
            var snap = audio?.GetWorkbenchSnapshot();
            if (snap?.History != null)
            {
                sfxSession.SetPlaybackHistory(snap.History);
            }
#endif
        }

        private void EndMusicPreviewInternal()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            NineGridArchitecture.Interface?.GetSystem<IMusicSystem>()
                ?.EndPreview("AudioWorkbenchEditorState.EndMusicPreview");
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

        private static bool ReadBool(string json, string field)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                var dict = Newtonsoft.Json.Linq.JObject.Parse(json);
                return dict[field]?.ToObject<bool>() ?? false;
            }
            catch
            {
                return false;
            }
        }

        private static float ReadFloat(string json, string field)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return 0f;
            }

            try
            {
                var dict = Newtonsoft.Json.Linq.JObject.Parse(json);
                return dict[field]?.ToObject<float>() ?? 0f;
            }
            catch
            {
                return 0f;
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
            public string baselineSfxJson;
            public string workingSfxJson;
            public string baselineMusicJson;
            public string workingMusicJson;
            public string focusedBindingKey;
            public string statusMessage;
        }
    }
}
#endif
