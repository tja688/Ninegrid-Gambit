using System;
using System.Collections.Generic;
using NineGrid.Content.Audio;
using NineGrid.Core;
using NineGrid.Flow.Diagnostics;
using QFramework;
using UnityEngine;
using System.Collections;

namespace NineGrid.Presentation.Systems
{
    public enum AudioCueOutcome
    {
        Played,
        Unbound,
        Suppressed,
        Cooldown,
        BackendFailure,
    }

    public enum AudioHistoryOutcome
    {
        Requested,
        Scheduled,
        Cancelled,
        Played,
        Unbound,
        Suppressed,
        Cooldown,
        BackendFailure,
    }
    public readonly struct AudioScheduleKey
    {
        public AudioScheduleKey(long value)
        {
            Value = value;
        }

        public long Value { get; }
        public bool IsValid => Value > 0;
    }

    public interface IAudioCueScheduler
    {
        AudioScheduleKey Schedule(float delaySeconds, Action callback);
        bool Cancel(AudioScheduleKey key);
    }


    public readonly struct AudioPlaybackRequest
    {
        public AudioPlaybackRequest(
            string cueId,
            string cueNote,
            string clipKey,
            float volume,
            float startOffsetSeconds,
            float bindingDelaySeconds)
        {
            CueId = cueId ?? string.Empty;
            CueNote = cueNote ?? string.Empty;
            ClipKey = clipKey ?? string.Empty;
            Volume = volume;
            StartOffsetSeconds = startOffsetSeconds;
            BindingDelaySeconds = bindingDelaySeconds;
        }

        public string CueId { get; }
        public string CueNote { get; }
        public string ClipKey { get; }
        public float Volume { get; }
        public float StartOffsetSeconds { get; }
        public float BindingDelaySeconds { get; }
    }

    public readonly struct AudioBackendResult
    {
        private AudioBackendResult(bool succeeded, string actualClipKey, string failureReason, string sourceId)
        {
            Succeeded = succeeded;
            ActualClipKey = actualClipKey ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string ActualClipKey { get; }
        public string FailureReason { get; }
        public string SourceId { get; }

        public static AudioBackendResult Success(string actualClipKey, string sourceId = null)
        {
            return new AudioBackendResult(true, actualClipKey, string.Empty, sourceId);
        }

        public static AudioBackendResult Failure(string reason)
        {
            return new AudioBackendResult(false, string.Empty, reason, string.Empty);
        }
    }

    public interface IAudioPlaybackAdapter
    {
        AudioBackendResult Play(AudioPlaybackRequest request);
    }

    public interface IAudioClock
    {
        double UnscaledTime { get; }
    }

    public sealed class AudioCueResult
    {
        public AudioCueOutcome Outcome { get; internal set; }
        public string CueId { get; internal set; }
        public string CueNote { get; internal set; }
        public string BindingKey { get; internal set; }
        public string ActualClipKey { get; internal set; }
        public string VariantId { get; internal set; }
        public string FailureReason { get; internal set; }
    }

    public sealed class AudioHistoryRecord
    {
        public long Sequence { get; internal set; }
        public AudioHistoryOutcome Outcome { get; internal set; }
        public string CueId { get; internal set; }
        public string CueNote { get; internal set; }
        public string BindingKey { get; internal set; }
        public string DiagnosticSource { get; internal set; }
        public string CardDefId { get; internal set; }
        public string SkillId { get; internal set; }
        public string RoomId { get; internal set; }
        public string ItemDefId { get; internal set; }
        public string ContentId { get; internal set; }
        public int DiagnosticCardUid { get; internal set; }
        public string ActualClipKey { get; internal set; }
        public string VariantId { get; internal set; }
        public string SourceId { get; internal set; }
        public string FailureReason { get; internal set; }
        public long ScheduleKey { get; internal set; }
        public float ScheduleDelaySeconds { get; internal set; }
        public double Time { get; internal set; }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public sealed class AudioCueAggregate
    {
        public string AggregateKey { get; internal set; }
        public string BindingKey { get; internal set; }
        public string CueId { get; internal set; }
        public int Requested { get; internal set; }
        public int Played { get; internal set; }
        public int Suppressed { get; internal set; }
        public int Cooldown { get; internal set; }
        public int Unbound { get; internal set; }
        public int BackendFailure { get; internal set; }
        public double LastTime { get; internal set; }
        public string LastFailureReason { get; internal set; }
        public IReadOnlyList<double> RecentTimestamps { get; internal set; }
    }

    public sealed class AudioWorkbenchSnapshot
    {
        public long Revision { get; internal set; }
        public IReadOnlyList<AudioHistoryRecord> History { get; internal set; }
        public IReadOnlyList<SfxTrackSourceSnapshot> PlayingSources { get; internal set; }
        public IReadOnlyList<SceneAudioOrphanSnapshot> SceneOrphans { get; internal set; }
        public IReadOnlyList<AudioPersistAnomalySnapshot> PersistAnomalies { get; internal set; }
        public IReadOnlyList<AudioCueAggregate> Aggregates { get; internal set; }
    }

    public sealed class AudioWorkbenchApplyResult
    {
        public bool Succeeded { get; internal set; }
        public long Revision { get; internal set; }
        public string Error { get; internal set; }
    }

    public sealed class AudioWorkbenchPreviewResult
    {
        public bool Succeeded { get; internal set; }
        public string ActualClipKey { get; internal set; }
        public string VariantId { get; internal set; }
        public string SourceId { get; internal set; }
        public string FailureReason { get; internal set; }
    }
#endif

    public interface IAudioSystem : ISystem
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        IReadOnlyList<AudioHistoryRecord> History { get; }
        AudioWorkbenchSnapshot GetWorkbenchSnapshot();
        AudioWorkbenchApplyResult ApplyWorkbenchCatalog(string catalogJson);
        AudioWorkbenchPreviewResult PreviewWorkbenchBinding(string bindingKey, bool includeBindingDelay);
        bool StopSfxSource(string sourceId);
        int StopAllSfxSources();
#endif
        AudioCueResult RequestCue(AudioCueRequest request);
        AudioScheduleKey ScheduleCue(AudioCueRequest request, float delaySeconds);
        bool CancelScheduledCue(AudioScheduleKey key);
    }

    public sealed class AudioSystem : AbstractSystem, IAudioSystem
    {
        public const int DefaultHistoryCapacity = 256;
        private const int AggregateTimestampCapacity = 32;
        private const double BurstWindowSeconds = 1d;
        private const int BurstThreshold = 4;
        private const double BurstReportCooldownSeconds = 2d;
        private const string SuppressedReason = "workbench binding disabled";

        private AudioBindingCatalog mCatalog;
        private readonly IAudioPlaybackAdapter mPlayback;
        private readonly IAudioClock mClock;
        private readonly IAudioCueScheduler mScheduler;
        private readonly Func<double> mRandomValue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly int mHistoryCapacity;
        private readonly List<AudioHistoryRecord> mHistory;
        private readonly Dictionary<string, AggregateState> mAggregates =
            new Dictionary<string, AggregateState>(StringComparer.Ordinal);
        private readonly HashSet<string> mWorkbenchPreviewSourceIds =
            new HashSet<string>(StringComparer.Ordinal);
        private long mRevision;
        private long mNextHistorySequence;
#endif
        private readonly Dictionary<AudioBinding, double> mLastPlayedAt =
            new Dictionary<AudioBinding, double>();
        private readonly Dictionary<AudioBinding, string> mLastVariantIds =
            new Dictionary<AudioBinding, string>();
        private readonly Dictionary<long, PendingScheduledCue> mPendingSchedules =
            new Dictionary<long, PendingScheduledCue>();
        private readonly Dictionary<string, double> mLastPlayedAtByCue =
            new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, Queue<double>> mBurstWindowByCue =
            new Dictionary<string, Queue<double>>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> mLastBurstReportAtByCue =
            new Dictionary<string, double>(StringComparer.Ordinal);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private AudioDiagnosticsService mDiagnosticsService;
#endif

        public AudioSystem(
            AudioBindingCatalog catalog,
            IAudioPlaybackAdapter playback,
            IAudioClock clock,
            int historyCapacity = DefaultHistoryCapacity,
            IAudioCueScheduler scheduler = null,
            Func<double> randomValue = null)
        {
            mCatalog = catalog ?? AudioBindingCatalog.FromJson(string.Empty);
            mPlayback = playback ?? new NullAudioPlaybackAdapter();
            mClock = clock ?? new RealtimeAudioClock();
            mScheduler = scheduler;
            mRandomValue = randomValue ?? (() => UnityEngine.Random.value);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            mHistoryCapacity = Math.Max(1, historyCapacity);
            mHistory = new List<AudioHistoryRecord>(mHistoryCapacity);
#endif
        }

        public static IAudioSystem EnsureRegistered(
            IArchitecture architecture = null,
            AudioBindingCatalog catalog = null,
            IAudioPlaybackAdapter playback = null,
            IAudioClock clock = null)
        {
            var arch = architecture ?? NineGridArchitecture.Interface;
            if (arch == null)
            {
                throw new InvalidOperationException("Architecture is not available for AudioSystem.");
            }

            var existing = arch.GetSystem<IAudioSystem>();
            if (existing != null)
            {
                return existing;
            }

            var created = new AudioSystem(
                catalog ?? AudioBindingCatalog.LoadFromResources(),
                playback ?? new MMSoundManagerAudioPlaybackAdapter(),
                clock ?? new RealtimeAudioClock());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (playback is IAudioPlaybackDiagnosticsAdapter diagnosticsAdapter)
            {
                created.mDiagnosticsService = AudioDiagnosticsService.Install(diagnosticsAdapter);
            }
            else if (created.mPlayback is IAudioPlaybackDiagnosticsAdapter fallbackDiagnostics)
            {
                created.mDiagnosticsService = AudioDiagnosticsService.Install(fallbackDiagnostics);
            }
#endif
            arch.RegisterSystem<IAudioSystem>(created);
            return created;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public IReadOnlyList<AudioHistoryRecord> History => mHistory;

        public AudioWorkbenchSnapshot GetWorkbenchSnapshot()
        {
            var historyCopy = new AudioHistoryRecord[mHistory.Count];
            for (var i = 0; i < mHistory.Count; i++)
            {
                historyCopy[i] = CloneHistoryRecord(mHistory[i]);
            }

            IReadOnlyList<SfxTrackSourceSnapshot> playing = Array.Empty<SfxTrackSourceSnapshot>();
            IReadOnlyList<SceneAudioOrphanSnapshot> orphans = Array.Empty<SceneAudioOrphanSnapshot>();
            if (mPlayback is IAudioPlaybackDiagnosticsAdapter diagnostics)
            {
                var live = diagnostics.GetPlayingSfxSources();
                if (live != null && live.Count > 0)
                {
                    PruneWorkbenchPreviewSourceIds(live);
                    var copy = new SfxTrackSourceSnapshot[live.Count];
                    for (var i = 0; i < live.Count; i++)
                    {
                        var src = live[i];
                        var isPreview = !string.IsNullOrEmpty(src.SourceId)
                            && mWorkbenchPreviewSourceIds.Contains(src.SourceId);
                        copy[i] = new SfxTrackSourceSnapshot(
                            src.SourceId,
                            src.ClipKey,
                            src.CueId,
                            src.PlaybackPositionSeconds,
                            src.Loop,
                            src.IsPlaying,
                            src.Claimed,
                            workbenchPreview: isPreview);
                    }

                    playing = copy;
                }
                else
                {
                    mWorkbenchPreviewSourceIds.Clear();
                }

                var liveOrphans = diagnostics.GetSceneAudioOrphans();
                if (liveOrphans != null && liveOrphans.Count > 0)
                {
                    var orphanCopy = new SceneAudioOrphanSnapshot[liveOrphans.Count];
                    for (var i = 0; i < liveOrphans.Count; i++)
                    {
                        orphanCopy[i] = liveOrphans[i];
                    }

                    orphans = orphanCopy;
                }
            }

            IReadOnlyList<AudioPersistAnomalySnapshot> persist =
                mDiagnosticsService?.RecentPersistAnomalies ?? Array.Empty<AudioPersistAnomalySnapshot>();

            var aggregates = new AudioCueAggregate[mAggregates.Count];
            var index = 0;
            foreach (var pair in mAggregates)
            {
                aggregates[index++] = pair.Value.ToImmutable();
            }

            return new AudioWorkbenchSnapshot
            {
                Revision = mRevision,
                History = historyCopy,
                PlayingSources = playing,
                SceneOrphans = orphans,
                PersistAnomalies = persist,
                Aggregates = aggregates,
            };
        }

        public AudioWorkbenchApplyResult ApplyWorkbenchCatalog(string catalogJson)
        {
            if (!AudioBindingCatalog.TryFromJson(catalogJson, out var catalog, out var error))
            {
                return new AudioWorkbenchApplyResult
                {
                    Succeeded = false,
                    Revision = mRevision,
                    Error = error ?? "catalog apply failed.",
                };
            }

            mCatalog = catalog;
            mLastPlayedAt.Clear();
            mLastVariantIds.Clear();
            mRevision++;
            return new AudioWorkbenchApplyResult
            {
                Succeeded = true,
                Revision = mRevision,
                Error = string.Empty,
            };
        }

        public AudioWorkbenchPreviewResult PreviewWorkbenchBinding(string bindingKey, bool includeBindingDelay)
        {
            if (string.IsNullOrWhiteSpace(bindingKey))
            {
                return PreviewFailure("binding key is empty.");
            }

            if (!TryFindBindingByKey(bindingKey, out var binding) || binding == null)
            {
                return PreviewFailure("binding not found.");
            }

            var variant = ResolveVariant(binding);
            if (variant == null || string.IsNullOrWhiteSpace(variant.ClipKey))
            {
                return PreviewFailure("声音绑定缺少有效素材。");
            }

            var playbackRequest = new AudioPlaybackRequest(
                binding.CueId,
                binding.Note,
                variant.ClipKey,
                DecibelsToLinear(binding.VolumeDb + variant.VolumeTrimDb),
                Math.Max(0f, variant.StartOffsetSeconds),
                includeBindingDelay ? Math.Max(0f, binding.BindingDelaySeconds) : 0f);

            AudioBackendResult backend;
            try
            {
                backend = mPlayback.Play(playbackRequest);
            }
            catch (Exception exception)
            {
                backend = AudioBackendResult.Failure(exception.Message);
            }

            if (!backend.Succeeded)
            {
                return PreviewFailure(backend.FailureReason);
            }

            if (!string.IsNullOrEmpty(backend.SourceId))
            {
                mWorkbenchPreviewSourceIds.Add(backend.SourceId);
            }

            return new AudioWorkbenchPreviewResult
            {
                Succeeded = true,
                ActualClipKey = string.IsNullOrEmpty(backend.ActualClipKey)
                    ? variant.ClipKey
                    : backend.ActualClipKey,
                VariantId = variant.VariantId,
                SourceId = backend.SourceId,
                FailureReason = string.Empty,
            };
        }

        public bool StopSfxSource(string sourceId)
        {
            if (!string.IsNullOrEmpty(sourceId))
            {
                mWorkbenchPreviewSourceIds.Remove(sourceId);
            }

            if (!(mPlayback is IAudioPlaybackDiagnosticsAdapter diagnostics))
            {
                return false;
            }

            return diagnostics.StopSfxSource(sourceId);
        }

        public int StopAllSfxSources()
        {
            mWorkbenchPreviewSourceIds.Clear();
            if (!(mPlayback is IAudioPlaybackDiagnosticsAdapter diagnostics))
            {
                return 0;
            }

            return diagnostics.StopAllSfxSources();
        }

        private void PruneWorkbenchPreviewSourceIds(IReadOnlyList<SfxTrackSourceSnapshot> live)
        {
            if (mWorkbenchPreviewSourceIds.Count == 0 || live == null)
            {
                return;
            }

            var liveIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < live.Count; i++)
            {
                if (!string.IsNullOrEmpty(live[i].SourceId))
                {
                    liveIds.Add(live[i].SourceId);
                }
            }

            mWorkbenchPreviewSourceIds.RemoveWhere(id => !liveIds.Contains(id));
        }

        private static AudioWorkbenchPreviewResult PreviewFailure(string reason)
        {
            return new AudioWorkbenchPreviewResult
            {
                Succeeded = false,
                ActualClipKey = string.Empty,
                VariantId = string.Empty,
                SourceId = string.Empty,
                FailureReason = reason ?? string.Empty,
            };
        }

        private bool TryFindBindingByKey(string bindingKey, out AudioBinding binding)
        {
            binding = null;
            var bindings = mCatalog.Bindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                var candidate = bindings[i];
                if (candidate != null
                    && string.Equals(candidate.BindingKey, bindingKey, StringComparison.Ordinal))
                {
                    binding = candidate;
                    return true;
                }
            }

            return false;
        }

        private static AudioHistoryRecord CloneHistoryRecord(AudioHistoryRecord source)
        {
            return new AudioHistoryRecord
            {
                Sequence = source.Sequence,
                Outcome = source.Outcome,
                CueId = source.CueId,
                CueNote = source.CueNote,
                BindingKey = source.BindingKey,
                DiagnosticSource = source.DiagnosticSource,
                CardDefId = source.CardDefId,
                SkillId = source.SkillId,
                RoomId = source.RoomId,
                ItemDefId = source.ItemDefId,
                ContentId = source.ContentId,
                DiagnosticCardUid = source.DiagnosticCardUid,
                ActualClipKey = source.ActualClipKey,
                VariantId = source.VariantId,
                SourceId = source.SourceId,
                FailureReason = source.FailureReason,
                ScheduleKey = source.ScheduleKey,
                ScheduleDelaySeconds = source.ScheduleDelaySeconds,
                Time = source.Time,
            };
        }
#endif
        public AudioScheduleKey ScheduleCue(AudioCueRequest request, float delaySeconds)
        {
            var scheduler = mScheduler ?? UnityAudioCueScheduler.Instance;
            var clampedDelay = Math.Max(0f, delaySeconds);
            AudioScheduleKey key = default;
            key = scheduler.Schedule(clampedDelay, () =>
            {
                mPendingSchedules.Remove(key.Value);
                RequestCue(request);
            });
            if (!key.IsValid)
            {
                return key;
            }

            mPendingSchedules[key.Value] = new PendingScheduledCue(request, clampedDelay);
            var scheduledAt = mClock.UnscaledTime;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddHistory(CreateHistoryRecord(
                AudioHistoryOutcome.Scheduled,
                request,
                scheduledAt,
                scheduleKey: key.Value,
                scheduleDelaySeconds: clampedDelay));
#endif
            RecordTrace(
                PerfTraceKinds.AudioCueScheduled,
                request,
                AudioHistoryOutcome.Scheduled,
                scheduledAt,
                clipKey: null,
                note: null,
                reason: null,
                scheduleKey: key.Value,
                scheduleDelaySeconds: clampedDelay);
            return key;
        }

        public bool CancelScheduledCue(AudioScheduleKey key)
        {
            if (!key.IsValid)
            {
                return false;
            }

            var scheduler = mScheduler ?? UnityAudioCueScheduler.Instance;
            if (!scheduler.Cancel(key))
            {
                return false;
            }

            PendingScheduledCue pending;
            var hadPending = mPendingSchedules.TryGetValue(key.Value, out pending);
            mPendingSchedules.Remove(key.Value);
            var cancelledAt = mClock.UnscaledTime;
            var request = hadPending
                ? pending.Request
                : AudioCueRequest.Simple(string.Empty, "AudioSystem.CancelScheduledCue");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var cancelledRecord = CreateHistoryRecord(
                AudioHistoryOutcome.Cancelled,
                request,
                cancelledAt,
                scheduleKey: key.Value,
                scheduleDelaySeconds: hadPending ? pending.DelaySeconds : 0f);
            cancelledRecord.FailureReason = "explicit cancel";
            AddHistory(cancelledRecord);
#endif
            RecordTrace(
                PerfTraceKinds.AudioCueCancelled,
                request,
                AudioHistoryOutcome.Cancelled,
                cancelledAt,
                clipKey: null,
                note: null,
                reason: "explicit cancel",
                scheduleKey: key.Value,
                scheduleDelaySeconds: hadPending ? pending.DelaySeconds : 0f);
            return true;
        }


        public AudioCueResult RequestCue(AudioCueRequest request)
        {
            var requestedAt = mClock.UnscaledTime;
            AudioBinding resolvedBinding = null;
            var hasResolvedBinding = !string.IsNullOrWhiteSpace(request.CueId)
                && mCatalog.TryResolve(request, out resolvedBinding)
                && resolvedBinding != null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddHistory(CreateHistoryRecord(
                AudioHistoryOutcome.Requested,
                request,
                requestedAt,
                cueNote: hasResolvedBinding ? resolvedBinding.Note : null,
                bindingKey: hasResolvedBinding ? resolvedBinding.BindingKey : null,
                actualClipKey: hasResolvedBinding ? resolvedBinding.ClipKey : null));
#endif
            RecordTrace(
                PerfTraceKinds.AudioCueRequest,
                request,
                AudioHistoryOutcome.Requested,
                requestedAt,
                clipKey: hasResolvedBinding ? resolvedBinding.ClipKey : null,
                note: hasResolvedBinding ? resolvedBinding.Note : null,
                reason: null);

            if (!hasResolvedBinding)
            {
                return RecordUnbound(request, string.IsNullOrWhiteSpace(request.CueId)
                    ? "cue ID 为空。"
                    : "声音绑定不存在。");
            }

            if (!resolvedBinding.Enabled)
            {
                return RecordSuppressed(request, resolvedBinding);
            }

            var variant = ResolveVariant(resolvedBinding);
            if (variant == null || string.IsNullOrWhiteSpace(variant.ClipKey))
            {
                return RecordUnbound(request, "声音绑定缺少有效素材。");
            }

            var now = mClock.UnscaledTime;
            if (resolvedBinding.MinimumIntervalSeconds > 0f
                && mLastPlayedAt.TryGetValue(resolvedBinding, out var lastPlayedAt)
                && now >= lastPlayedAt
                && now - lastPlayedAt < resolvedBinding.MinimumIntervalSeconds)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var cooldownRecord = CreateHistoryRecord(
                    AudioHistoryOutcome.Cooldown,
                    request,
                    now,
                    cueNote: resolvedBinding.Note,
                    bindingKey: resolvedBinding.BindingKey);
                cooldownRecord.FailureReason = "minimum interval";
                AddHistory(cooldownRecord);
#endif
                RecordTrace(
                    PerfTraceKinds.AudioCueCooldown,
                    request,
                    AudioHistoryOutcome.Cooldown,
                    now,
                    resolvedBinding.ClipKey,
                    resolvedBinding.Note,
                    "minimum interval");
                return new AudioCueResult
                {
                    Outcome = AudioCueOutcome.Cooldown,
                    CueId = request.CueId,
                    CueNote = resolvedBinding.Note,
                    BindingKey = resolvedBinding.BindingKey,
                    FailureReason = "minimum interval",
                };
            }

            var playbackRequest = new AudioPlaybackRequest(
                request.CueId,
                resolvedBinding.Note,
                variant.ClipKey,
                DecibelsToLinear(resolvedBinding.VolumeDb + variant.VolumeTrimDb),
                Math.Max(0f, variant.StartOffsetSeconds),
                Math.Max(0f, resolvedBinding.BindingDelaySeconds));

            AudioBackendResult backend;
            try
            {
                backend = mPlayback.Play(playbackRequest);
            }
            catch (Exception exception)
            {
                backend = AudioBackendResult.Failure(exception.Message);
            }

            if (!backend.Succeeded)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var failureRecord = CreateHistoryRecord(
                    AudioHistoryOutcome.BackendFailure,
                    request,
                    now,
                    cueNote: resolvedBinding.Note,
                    bindingKey: resolvedBinding.BindingKey,
                    actualClipKey: variant.ClipKey,
                    variantId: variant.VariantId);
                failureRecord.FailureReason = backend.FailureReason;
                AddHistory(failureRecord);
#endif
                RecordTrace(
                    PerfTraceKinds.AudioCueBackendFailure,
                    request,
                    AudioHistoryOutcome.BackendFailure,
                    now,
                    variant.ClipKey,
                    resolvedBinding.Note,
                    backend.FailureReason);
                return new AudioCueResult
                {
                    Outcome = AudioCueOutcome.BackendFailure,
                    CueId = request.CueId,
                    CueNote = resolvedBinding.Note,
                    BindingKey = resolvedBinding.BindingKey,
                    ActualClipKey = variant.ClipKey,
                    VariantId = variant.VariantId,
                    FailureReason = backend.FailureReason,
                };
            }

            var actualClipKey = string.IsNullOrEmpty(backend.ActualClipKey)
                ? variant.ClipKey
                : backend.ActualClipKey;
            mLastPlayedAt[resolvedBinding] = now;
            if (!string.IsNullOrEmpty(variant.VariantId))
            {
                mLastVariantIds[resolvedBinding] = variant.VariantId;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddHistory(CreateHistoryRecord(
                AudioHistoryOutcome.Played,
                request,
                now,
                cueNote: resolvedBinding.Note,
                bindingKey: resolvedBinding.BindingKey,
                actualClipKey: actualClipKey,
                variantId: variant.VariantId,
                sourceId: backend.SourceId));
#endif
            RecordTrace(
                PerfTraceKinds.AudioCuePlayed,
                request,
                AudioHistoryOutcome.Played,
                now,
                actualClipKey,
                resolvedBinding.Note,
                null);
            TrackBurstIfNeeded(request, actualClipKey, now);
            return new AudioCueResult
            {
                Outcome = AudioCueOutcome.Played,
                CueId = request.CueId,
                CueNote = resolvedBinding.Note,
                BindingKey = resolvedBinding.BindingKey,
                ActualClipKey = actualClipKey,
                VariantId = variant.VariantId,
            };
        }

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            mDiagnosticsService?.Dispose();
            mDiagnosticsService = null;
#endif
        }

        private AudioCueResult RecordUnbound(AudioCueRequest request, string reason)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var record = CreateHistoryRecord(
                AudioHistoryOutcome.Unbound,
                request,
                mClock.UnscaledTime);
            record.FailureReason = reason;
            AddHistory(record);
#endif
            RecordTrace(
                PerfTraceKinds.AudioCueUnbound,
                request,
                AudioHistoryOutcome.Unbound,
                mClock.UnscaledTime,
                clipKey: null,
                note: null,
                reason);
            return new AudioCueResult
            {
                Outcome = AudioCueOutcome.Unbound,
                CueId = request.CueId,
                FailureReason = reason,
            };
        }

        private AudioCueResult RecordSuppressed(AudioCueRequest request, AudioBinding binding)
        {
            var now = mClock.UnscaledTime;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var record = CreateHistoryRecord(
                AudioHistoryOutcome.Suppressed,
                request,
                now,
                cueNote: binding.Note,
                bindingKey: binding.BindingKey);
            record.FailureReason = SuppressedReason;
            AddHistory(record);
#endif
            RecordTrace(
                PerfTraceKinds.AudioCueSuppressed,
                request,
                AudioHistoryOutcome.Suppressed,
                now,
                clipKey: binding.ClipKey,
                note: binding.Note,
                reason: SuppressedReason,
                bindingKey: binding.BindingKey);
            return new AudioCueResult
            {
                Outcome = AudioCueOutcome.Suppressed,
                CueId = request.CueId,
                CueNote = binding.Note,
                BindingKey = binding.BindingKey,
                FailureReason = SuppressedReason,
            };
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private AudioHistoryRecord CreateHistoryRecord(
            AudioHistoryOutcome outcome,
            AudioCueRequest request,
            double time,
            string cueNote = null,
            string bindingKey = null,
            string actualClipKey = null,
            string variantId = null,
            string sourceId = null,
            long scheduleKey = 0L,
            float scheduleDelaySeconds = 0f)
        {
            return new AudioHistoryRecord
            {
                Outcome = outcome,
                CueId = request.CueId,
                CueNote = cueNote,
                BindingKey = bindingKey,
                DiagnosticSource = request.DiagnosticSource,
                CardDefId = request.CardDefId,
                SkillId = request.SkillId,
                RoomId = request.RoomId,
                ItemDefId = request.ItemDefId,
                ContentId = request.ContentId,
                DiagnosticCardUid = request.DiagnosticCardUid,
                ActualClipKey = actualClipKey,
                VariantId = variantId,
                SourceId = sourceId,
                ScheduleKey = scheduleKey,
                ScheduleDelaySeconds = scheduleDelaySeconds,
                Time = time,
            };
        }
#endif

        private void RecordTrace(
            string kind,
            AudioCueRequest request,
            AudioHistoryOutcome outcome,
            double time,
            string clipKey,
            string note,
            string reason,
            long scheduleKey = 0L,
            float scheduleDelaySeconds = 0f,
            string bindingKey = null)
        {
            try
            {
                var payload = new Dictionary<string, string>
                {
                    ["outcome"] = outcome.ToString(),
                    ["cueId"] = request.CueId ?? string.Empty,
                    ["diagnosticSource"] = request.DiagnosticSource ?? string.Empty,
                    ["time"] = time.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                };
                if (!string.IsNullOrEmpty(clipKey))
                {
                    payload["clipKey"] = clipKey;
                }

                if (!string.IsNullOrEmpty(note))
                {
                    payload["note"] = note;
                }

                if (!string.IsNullOrEmpty(bindingKey))
                {
                    payload["bindingKey"] = bindingKey;
                }

                if (!string.IsNullOrEmpty(reason))
                {
                    payload["reason"] = reason;
                }

                if (scheduleKey > 0L)
                {
                    payload["scheduleKey"] = scheduleKey.ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
                    payload["scheduleDelaySeconds"] = scheduleDelaySeconds.ToString(
                        "R",
                        System.Globalization.CultureInfo.InvariantCulture);
                }

                if (!string.IsNullOrEmpty(request.CardDefId))
                {
                    payload["cardDefId"] = request.CardDefId;
                }

                if (!string.IsNullOrEmpty(request.SkillId))
                {
                    payload["skillId"] = request.SkillId;
                }

                if (!string.IsNullOrEmpty(request.RoomId))
                {
                    payload["roomId"] = request.RoomId;
                }

                if (!string.IsNullOrEmpty(request.ItemDefId))
                {
                    payload["itemDefId"] = request.ItemDefId;
                }

                if (!string.IsNullOrEmpty(request.ContentId))
                {
                    payload["contentId"] = request.ContentId;
                }

                if (outcome == AudioHistoryOutcome.Played
                    || outcome == AudioHistoryOutcome.Requested)
                {
                    var cueId = request.CueId ?? string.Empty;
                    if (!string.IsNullOrEmpty(cueId)
                        && mLastPlayedAtByCue.TryGetValue(cueId, out var previousAt))
                    {
                        var sinceLastMs = Math.Max(0d, (time - previousAt) * 1000d);
                        payload["sinceLastPlayMs"] = sinceLastMs.ToString(
                            "R",
                            System.Globalization.CultureInfo.InvariantCulture);
                    }

                    if (outcome == AudioHistoryOutcome.Played && !string.IsNullOrEmpty(cueId))
                    {
                        mLastPlayedAtByCue[cueId] = time;
                    }
                }

                DirectorTrace.AppendBusyFields(payload);
                payload["batchId"] = DirectorTrace.ActiveBatchId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                payload["sessionId"] = DiagTraceShared.CurrentSessionId;
                payload["runTag"] = DiagTraceShared.RunTag;
                if (request.DiagnosticCardUid > 0)
                {
                    payload["diagnosticCardUid"] = request.DiagnosticCardUid.ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
                }

                // 运行时 UID 可进诊断，但不得参与绑定解析主键。
                PerfTraceRecorder.Record(
                    kind,
                    uid: request.DiagnosticCardUid > 0 ? request.DiagnosticCardUid : -1,
                    PerfTraceSites.AudioSystemCue,
                    payload);
            }
            catch (Exception)
            {
                // 音频打点失败不干扰玩法路径。
            }
        }

        private void TrackBurstIfNeeded(AudioCueRequest request, string clipKey, double now)
        {
            if (string.IsNullOrWhiteSpace(request.CueId))
            {
                return;
            }

            if (!mBurstWindowByCue.TryGetValue(request.CueId, out var window))
            {
                window = new Queue<double>();
                mBurstWindowByCue[request.CueId] = window;
            }

            while (window.Count > 0 && now - window.Peek() > BurstWindowSeconds)
            {
                window.Dequeue();
            }

            window.Enqueue(now);
            if (window.Count < BurstThreshold)
            {
                return;
            }

            if (mLastBurstReportAtByCue.TryGetValue(request.CueId, out var lastReportAt)
                && now - lastReportAt < BurstReportCooldownSeconds)
            {
                return;
            }

            mLastBurstReportAtByCue[request.CueId] = now;
            try
            {
                var payload = new Dictionary<string, string>
                {
                    ["cueId"] = request.CueId,
                    ["clipKey"] = clipKey ?? string.Empty,
                    ["diagnosticSource"] = request.DiagnosticSource ?? string.Empty,
                    ["playsInWindow"] = window.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["windowSeconds"] = BurstWindowSeconds.ToString(
                        "R",
                        System.Globalization.CultureInfo.InvariantCulture),
                    ["directorIdle"] = (!DirectorTrace.DirectorMainlineBusy
                        && !DirectorTrace.DirectorBypassBusy)
                        ? "true"
                        : "false",
                    ["reason"] = "同一 cue 在短窗口内高频播放。",
                };

                if (!string.IsNullOrEmpty(request.CardDefId))
                {
                    payload["cardDefId"] = request.CardDefId;
                }

                if (!string.IsNullOrEmpty(request.SkillId))
                {
                    payload["skillId"] = request.SkillId;
                }

                DirectorTrace.AppendBusyFields(payload);
                payload["batchId"] = DirectorTrace.ActiveBatchId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                payload["sessionId"] = DiagTraceShared.CurrentSessionId;
                payload["runTag"] = DiagTraceShared.RunTag;
                if (request.DiagnosticCardUid > 0)
                {
                    payload["diagnosticCardUid"] = request.DiagnosticCardUid.ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
                }

                PerfTraceRecorder.Record(
                    PerfTraceKinds.AudioCueBurstAnomaly,
                    uid: request.DiagnosticCardUid > 0 ? request.DiagnosticCardUid : -1,
                    PerfTraceSites.AudioSystemCue,
                    payload);
            }
            catch (Exception)
            {
                // 音频打点失败不干扰玩法路径。
            }
        }

        private readonly struct PendingScheduledCue
        {
            public PendingScheduledCue(AudioCueRequest request, float delaySeconds)
            {
                Request = request;
                DelaySeconds = delaySeconds;
            }

            public AudioCueRequest Request { get; }
            public float DelaySeconds { get; }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void AddHistory(AudioHistoryRecord record)
        {
            record.Sequence = ++mNextHistorySequence;
            if (mHistory.Count >= mHistoryCapacity)
            {
                mHistory.RemoveAt(0);
            }

            mHistory.Add(record);
            UpdateAggregate(record);
        }

        private void UpdateAggregate(AudioHistoryRecord record)
        {
            switch (record.Outcome)
            {
                case AudioHistoryOutcome.Requested:
                case AudioHistoryOutcome.Played:
                case AudioHistoryOutcome.Suppressed:
                case AudioHistoryOutcome.Cooldown:
                case AudioHistoryOutcome.Unbound:
                case AudioHistoryOutcome.BackendFailure:
                    break;
                default:
                    return;
            }

            var bindingKey = record.BindingKey ?? string.Empty;
            var cueId = record.CueId ?? string.Empty;
            var aggregateKey = !string.IsNullOrEmpty(bindingKey) ? bindingKey : cueId;
            if (string.IsNullOrEmpty(aggregateKey))
            {
                return;
            }

            if (!mAggregates.TryGetValue(aggregateKey, out var state))
            {
                state = new AggregateState(aggregateKey, bindingKey, cueId);
                mAggregates[aggregateKey] = state;
            }

            state.Observe(record);
        }

        private sealed class AggregateState
        {
            private readonly Queue<double> mTimestamps = new Queue<double>(AggregateTimestampCapacity);
            private readonly List<double> mTimestampBuffer = new List<double>(AggregateTimestampCapacity);

            public AggregateState(string aggregateKey, string bindingKey, string cueId)
            {
                AggregateKey = aggregateKey ?? string.Empty;
                BindingKey = bindingKey ?? string.Empty;
                CueId = cueId ?? string.Empty;
            }

            public string AggregateKey { get; }
            public string BindingKey { get; private set; }
            public string CueId { get; private set; }
            public int Requested { get; private set; }
            public int Played { get; private set; }
            public int Suppressed { get; private set; }
            public int Cooldown { get; private set; }
            public int Unbound { get; private set; }
            public int BackendFailure { get; private set; }
            public double LastTime { get; private set; }
            public string LastFailureReason { get; private set; }

            public void Observe(AudioHistoryRecord record)
            {
                if (!string.IsNullOrEmpty(record.BindingKey))
                {
                    BindingKey = record.BindingKey;
                }

                if (!string.IsNullOrEmpty(record.CueId))
                {
                    CueId = record.CueId;
                }

                LastTime = record.Time;
                switch (record.Outcome)
                {
                    case AudioHistoryOutcome.Requested:
                        Requested++;
                        break;
                    case AudioHistoryOutcome.Played:
                        Played++;
                        break;
                    case AudioHistoryOutcome.Suppressed:
                        Suppressed++;
                        LastFailureReason = record.FailureReason ?? string.Empty;
                        break;
                    case AudioHistoryOutcome.Cooldown:
                        Cooldown++;
                        LastFailureReason = record.FailureReason ?? string.Empty;
                        break;
                    case AudioHistoryOutcome.Unbound:
                        Unbound++;
                        LastFailureReason = record.FailureReason ?? string.Empty;
                        break;
                    case AudioHistoryOutcome.BackendFailure:
                        BackendFailure++;
                        LastFailureReason = record.FailureReason ?? string.Empty;
                        break;
                }

                while (mTimestamps.Count >= AggregateTimestampCapacity)
                {
                    mTimestamps.Dequeue();
                }

                mTimestamps.Enqueue(record.Time);
            }

            public AudioCueAggregate ToImmutable()
            {
                mTimestampBuffer.Clear();
                foreach (var stamp in mTimestamps)
                {
                    mTimestampBuffer.Add(stamp);
                }

                return new AudioCueAggregate
                {
                    AggregateKey = AggregateKey,
                    BindingKey = BindingKey,
                    CueId = CueId,
                    Requested = Requested,
                    Played = Played,
                    Suppressed = Suppressed,
                    Cooldown = Cooldown,
                    Unbound = Unbound,
                    BackendFailure = BackendFailure,
                    LastTime = LastTime,
                    LastFailureReason = LastFailureReason ?? string.Empty,
                    RecentTimestamps = mTimestampBuffer.ToArray(),
                };
            }
        }
#endif

        private ResolvedAudioVariant ResolveVariant(AudioBinding binding)
        {
            var valid = new List<ResolvedAudioVariant>();
            for (var i = 0; i < binding.Variants.Count; i++)
            {
                var row = binding.Variants[i];
                if (row == null || string.IsNullOrWhiteSpace(row.clipKey) || row.weight <= 0f)
                {
                    continue;
                }

                valid.Add(new ResolvedAudioVariant(
                    string.IsNullOrWhiteSpace(row.variantId) ? "variant-" + i : row.variantId,
                    row.clipKey,
                    row.weight,
                    row.volumeTrimDb,
                    Math.Max(0f, row.startOffsetSeconds)));
            }

            if (valid.Count == 0)
            {
                return string.IsNullOrWhiteSpace(binding.ClipKey)
                    ? null
                    : new ResolvedAudioVariant(string.Empty, binding.ClipKey, 1f, 0f, binding.StartOffsetSeconds);
            }

            if (valid.Count > 1 && mLastVariantIds.TryGetValue(binding, out var previous))
            {
                valid.RemoveAll(candidate => string.Equals(candidate.VariantId, previous, StringComparison.Ordinal));
            }

            var totalWeight = 0d;
            for (var i = 0; i < valid.Count; i++)
            {
                totalWeight += valid[i].Weight;
            }

            var roll = Math.Max(0d, Math.Min(0.999999999d, mRandomValue())) * totalWeight;
            for (var i = 0; i < valid.Count; i++)
            {
                roll -= valid[i].Weight;
                if (roll < 0d)
                {
                    return valid[i];
                }
            }

            return valid[valid.Count - 1];
        }

        private sealed class ResolvedAudioVariant
        {
            public ResolvedAudioVariant(string variantId, string clipKey, float weight, float volumeTrimDb, float startOffsetSeconds)
            {
                VariantId = variantId ?? string.Empty;
                ClipKey = clipKey ?? string.Empty;
                Weight = weight;
                VolumeTrimDb = volumeTrimDb;
                StartOffsetSeconds = startOffsetSeconds;
            }

            public string VariantId { get; }
            public string ClipKey { get; }
            public float Weight { get; }
            public float VolumeTrimDb { get; }
            public float StartOffsetSeconds { get; }
        }

        private sealed class UnityAudioCueScheduler : MonoBehaviour, IAudioCueScheduler
        {
            private readonly Dictionary<long, Coroutine> mPending = new Dictionary<long, Coroutine>();
            private long mNextKey;
            private static UnityAudioCueScheduler sInstance;

            public static UnityAudioCueScheduler Instance
            {
                get
                {
                    if (sInstance == null)
                    {
                        var host = new GameObject(nameof(UnityAudioCueScheduler));
                        DontDestroyOnLoad(host);
                        sInstance = host.AddComponent<UnityAudioCueScheduler>();
                    }

                    return sInstance;
                }
            }

            public AudioScheduleKey Schedule(float delaySeconds, Action callback)
            {
                var key = new AudioScheduleKey(++mNextKey);
                mPending[key.Value] = StartCoroutine(WaitAndInvoke(key.Value, delaySeconds, callback));
                return key;
            }

            public bool Cancel(AudioScheduleKey key)
            {
                if (!key.IsValid || !mPending.TryGetValue(key.Value, out var coroutine))
                {
                    return false;
                }

                StopCoroutine(coroutine);
                mPending.Remove(key.Value);
                return true;
            }

            private IEnumerator WaitAndInvoke(long key, float delaySeconds, Action callback)
            {
                if (delaySeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(delaySeconds);
                }

                mPending.Remove(key);
                callback?.Invoke();
            }
        }

        private static float DecibelsToLinear(float decibels)
        {
            return Mathf.Pow(10f, decibels / 20f);
        }

        private sealed class NullAudioPlaybackAdapter : IAudioPlaybackAdapter
        {
            public AudioBackendResult Play(AudioPlaybackRequest request)
            {
                return AudioBackendResult.Failure("音频播放 Adapter 未安装。");
            }
        }

        private sealed class RealtimeAudioClock : IAudioClock
        {
            public double UnscaledTime => Time.realtimeSinceStartup;
        }
    }
}
