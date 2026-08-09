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
        private AudioBackendResult(bool succeeded, string actualClipKey, string failureReason)
        {
            Succeeded = succeeded;
            ActualClipKey = actualClipKey ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string ActualClipKey { get; }
        public string FailureReason { get; }

        public static AudioBackendResult Success(string actualClipKey)
        {
            return new AudioBackendResult(true, actualClipKey, string.Empty);
        }

        public static AudioBackendResult Failure(string reason)
        {
            return new AudioBackendResult(false, string.Empty, reason);
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
        public AudioHistoryOutcome Outcome { get; internal set; }
        public string CueId { get; internal set; }
        public string CueNote { get; internal set; }
        public string BindingKey { get; internal set; }
        public string DiagnosticSource { get; internal set; }
        public string ActualClipKey { get; internal set; }
        public string VariantId { get; internal set; }
        public string FailureReason { get; internal set; }
        public long ScheduleKey { get; internal set; }
        public float ScheduleDelaySeconds { get; internal set; }
        public double Time { get; internal set; }
    }

    public interface IAudioSystem : ISystem
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        IReadOnlyList<AudioHistoryRecord> History { get; }
#endif
        AudioCueResult RequestCue(AudioCueRequest request);
        AudioScheduleKey ScheduleCue(AudioCueRequest request, float delaySeconds);
        bool CancelScheduledCue(AudioScheduleKey key);
    }

    public sealed class AudioSystem : AbstractSystem, IAudioSystem
    {
        public const int DefaultHistoryCapacity = 256;
        private const double BurstWindowSeconds = 1d;
        private const int BurstThreshold = 4;
        private const double BurstReportCooldownSeconds = 2d;

        private readonly AudioBindingCatalog mCatalog;
        private readonly IAudioPlaybackAdapter mPlayback;
        private readonly IAudioClock mClock;
        private readonly IAudioCueScheduler mScheduler;
        private readonly Func<double> mRandomValue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly int mHistoryCapacity;
        private readonly List<AudioHistoryRecord> mHistory;
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
            AddHistory(new AudioHistoryRecord
            {
                Outcome = AudioHistoryOutcome.Scheduled,
                CueId = request.CueId,
                DiagnosticSource = request.DiagnosticSource,
                ScheduleKey = key.Value,
                ScheduleDelaySeconds = clampedDelay,
                Time = scheduledAt,
            });
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
            AddHistory(new AudioHistoryRecord
            {
                Outcome = AudioHistoryOutcome.Cancelled,
                CueId = request.CueId,
                DiagnosticSource = request.DiagnosticSource,
                ScheduleKey = key.Value,
                ScheduleDelaySeconds = hadPending ? pending.DelaySeconds : 0f,
                FailureReason = "explicit cancel",
                Time = cancelledAt,
            });
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
            AddHistory(new AudioHistoryRecord
            {
                Outcome = AudioHistoryOutcome.Requested,
                CueId = request.CueId,
                CueNote = hasResolvedBinding ? resolvedBinding.Note : null,
                BindingKey = hasResolvedBinding ? resolvedBinding.BindingKey : null,
                DiagnosticSource = request.DiagnosticSource,
                ActualClipKey = hasResolvedBinding ? resolvedBinding.ClipKey : null,
                Time = requestedAt,
            });
#endif
            RecordTrace(
                PerfTraceKinds.AudioCueRequest,
                request,
                AudioHistoryOutcome.Requested,
                requestedAt,
                clipKey: hasResolvedBinding ? resolvedBinding.ClipKey : null,
                note: hasResolvedBinding ? resolvedBinding.Note : null,
                reason: null);

            if (!hasResolvedBinding || !resolvedBinding.Enabled)
            {
                return RecordUnbound(request, string.IsNullOrWhiteSpace(request.CueId)
                    ? "cue ID 为空。"
                    : "声音绑定不存在或已禁用。");
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
                AddHistory(new AudioHistoryRecord
                {
                    Outcome = AudioHistoryOutcome.Cooldown,
                    CueId = request.CueId,
                    CueNote = resolvedBinding.Note,
                    BindingKey = resolvedBinding.BindingKey,
                    DiagnosticSource = request.DiagnosticSource,
                    FailureReason = "minimum interval",
                    Time = now,
                });
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
                AddHistory(new AudioHistoryRecord
                {
                    Outcome = AudioHistoryOutcome.BackendFailure,
                    CueId = request.CueId,
                    CueNote = resolvedBinding.Note,
                    BindingKey = resolvedBinding.BindingKey,
                    DiagnosticSource = request.DiagnosticSource,
                    ActualClipKey = variant.ClipKey,
                    VariantId = variant.VariantId,
                    FailureReason = backend.FailureReason,
                    Time = now,
                });
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
            AddHistory(new AudioHistoryRecord
            {
                Outcome = AudioHistoryOutcome.Played,
                CueId = request.CueId,
                CueNote = resolvedBinding.Note,
                BindingKey = resolvedBinding.BindingKey,
                DiagnosticSource = request.DiagnosticSource,
                ActualClipKey = actualClipKey,
                VariantId = variant.VariantId,
                Time = now,
            });
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
            AddHistory(new AudioHistoryRecord
            {
                Outcome = AudioHistoryOutcome.Unbound,
                CueId = request.CueId,
                DiagnosticSource = request.DiagnosticSource,
                FailureReason = reason,
                Time = mClock.UnscaledTime,
            });
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

        private void RecordTrace(
            string kind,
            AudioCueRequest request,
            AudioHistoryOutcome outcome,
            double time,
            string clipKey,
            string note,
            string reason,
            long scheduleKey = 0L,
            float scheduleDelaySeconds = 0f)
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
            if (mHistory.Count >= mHistoryCapacity)
            {
                mHistory.RemoveAt(0);
            }

            mHistory.Add(record);
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
