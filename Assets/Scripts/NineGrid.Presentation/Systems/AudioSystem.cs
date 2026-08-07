using System;
using System.Collections.Generic;
using NineGrid.Content.Audio;
using NineGrid.Core;
using NineGrid.Flow.Diagnostics;
using QFramework;
using UnityEngine;

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
        Played,
        Unbound,
        Cooldown,
        BackendFailure,
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
        public string FailureReason { get; internal set; }
        public double Time { get; internal set; }
    }

    public interface IAudioSystem : ISystem
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        IReadOnlyList<AudioHistoryRecord> History { get; }
#endif
        AudioCueResult RequestCue(AudioCueRequest request);
    }

    public sealed class AudioSystem : AbstractSystem, IAudioSystem
    {
        public const int DefaultHistoryCapacity = 256;

        private readonly AudioBindingCatalog mCatalog;
        private readonly IAudioPlaybackAdapter mPlayback;
        private readonly IAudioClock mClock;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly int mHistoryCapacity;
        private readonly List<AudioHistoryRecord> mHistory;
#endif
        private readonly Dictionary<AudioBinding, double> mLastPlayedAt =
            new Dictionary<AudioBinding, double>();

        public AudioSystem(
            AudioBindingCatalog catalog,
            IAudioPlaybackAdapter playback,
            IAudioClock clock,
            int historyCapacity = DefaultHistoryCapacity)
        {
            mCatalog = catalog ?? AudioBindingCatalog.FromJson(string.Empty);
            mPlayback = playback ?? new NullAudioPlaybackAdapter();
            mClock = clock ?? new RealtimeAudioClock();
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
            arch.RegisterSystem<IAudioSystem>(created);
            return created;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public IReadOnlyList<AudioHistoryRecord> History => mHistory;
#endif

        public AudioCueResult RequestCue(AudioCueRequest request)
        {
            var requestedAt = mClock.UnscaledTime;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddHistory(new AudioHistoryRecord
            {
                Outcome = AudioHistoryOutcome.Requested,
                CueId = request.CueId,
                DiagnosticSource = request.DiagnosticSource,
                Time = requestedAt,
            });
#endif
            RecordTrace(
                PerfTraceKinds.AudioCueRequest,
                request,
                AudioHistoryOutcome.Requested,
                requestedAt,
                clipKey: null,
                note: null,
                reason: null);

            if (string.IsNullOrWhiteSpace(request.CueId)
                || !mCatalog.TryResolve(request, out var binding)
                || binding == null
                || !binding.Enabled
                || string.IsNullOrWhiteSpace(binding.ClipKey))
            {
                return RecordUnbound(request, string.IsNullOrWhiteSpace(request.CueId)
                    ? "cue ID 为空。"
                    : "声音绑定不存在、已禁用或缺少素材。");
            }

            var now = mClock.UnscaledTime;
            if (binding.MinimumIntervalSeconds > 0f
                && mLastPlayedAt.TryGetValue(binding, out var lastPlayedAt)
                && now >= lastPlayedAt
                && now - lastPlayedAt < binding.MinimumIntervalSeconds)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                AddHistory(new AudioHistoryRecord
                {
                    Outcome = AudioHistoryOutcome.Cooldown,
                    CueId = request.CueId,
                    CueNote = binding.Note,
                    BindingKey = binding.BindingKey,
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
                    binding.ClipKey,
                    binding.Note,
                    "minimum interval");
                return new AudioCueResult
                {
                    Outcome = AudioCueOutcome.Cooldown,
                    CueId = request.CueId,
                    CueNote = binding.Note,
                    BindingKey = binding.BindingKey,
                    FailureReason = "minimum interval",
                };
            }

            var playbackRequest = new AudioPlaybackRequest(
                request.CueId,
                binding.Note,
                binding.ClipKey,
                DecibelsToLinear(binding.VolumeDb),
                Math.Max(0f, binding.StartOffsetSeconds),
                Math.Max(0f, binding.BindingDelaySeconds));

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
                    CueNote = binding.Note,
                    BindingKey = binding.BindingKey,
                    DiagnosticSource = request.DiagnosticSource,
                    ActualClipKey = binding.ClipKey,
                    FailureReason = backend.FailureReason,
                    Time = now,
                });
#endif
                RecordTrace(
                    PerfTraceKinds.AudioCueBackendFailure,
                    request,
                    AudioHistoryOutcome.BackendFailure,
                    now,
                    binding.ClipKey,
                    binding.Note,
                    backend.FailureReason);
                return new AudioCueResult
                {
                    Outcome = AudioCueOutcome.BackendFailure,
                    CueId = request.CueId,
                    CueNote = binding.Note,
                    BindingKey = binding.BindingKey,
                    ActualClipKey = binding.ClipKey,
                    FailureReason = backend.FailureReason,
                };
            }

            var actualClipKey = string.IsNullOrEmpty(backend.ActualClipKey)
                ? binding.ClipKey
                : backend.ActualClipKey;
            mLastPlayedAt[binding] = now;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddHistory(new AudioHistoryRecord
            {
                Outcome = AudioHistoryOutcome.Played,
                CueId = request.CueId,
                CueNote = binding.Note,
                BindingKey = binding.BindingKey,
                DiagnosticSource = request.DiagnosticSource,
                ActualClipKey = actualClipKey,
                Time = now,
            });
#endif
            RecordTrace(
                PerfTraceKinds.AudioCuePlayed,
                request,
                AudioHistoryOutcome.Played,
                now,
                actualClipKey,
                binding.Note,
                null);
            return new AudioCueResult
            {
                Outcome = AudioCueOutcome.Played,
                CueId = request.CueId,
                CueNote = binding.Note,
                BindingKey = binding.BindingKey,
                ActualClipKey = actualClipKey,
            };
        }

        protected override void OnInit()
        {
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

        private static void RecordTrace(
            string kind,
            AudioCueRequest request,
            AudioHistoryOutcome outcome,
            double time,
            string clipKey,
            string note,
            string reason)
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

                DirectorTrace.AppendBusyFields(payload);
                payload["batchId"] = DirectorTrace.ActiveBatchId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                payload["sessionId"] = DiagTraceShared.CurrentSessionId;
                payload["runTag"] = DiagTraceShared.RunTag;
                PerfTraceRecorder.Record(kind, uid: -1, PerfTraceSites.AudioSystemCue, payload);
            }
            catch (Exception)
            {
                // 音频打点失败不干扰玩法路径。
            }
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
