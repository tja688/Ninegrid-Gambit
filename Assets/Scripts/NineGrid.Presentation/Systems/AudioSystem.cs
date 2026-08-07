using System;
using System.Collections.Generic;
using NineGrid.Content.Audio;
using NineGrid.Core;
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
        public string ActualClipKey { get; internal set; }
        public string FailureReason { get; internal set; }
    }

    public sealed class AudioHistoryRecord
    {
        public AudioHistoryOutcome Outcome { get; internal set; }
        public string CueId { get; internal set; }
        public string CueNote { get; internal set; }
        public string DiagnosticSource { get; internal set; }
        public string ActualClipKey { get; internal set; }
        public string FailureReason { get; internal set; }
        public double Time { get; internal set; }
    }

    public interface IAudioSystem : ISystem
    {
        IReadOnlyList<AudioHistoryRecord> History { get; }
        AudioCueResult RequestCue(AudioCueRequest request);
    }

    public sealed class AudioSystem : AbstractSystem, IAudioSystem
    {
        public const int DefaultHistoryCapacity = 256;

        private readonly AudioBindingCatalog mCatalog;
        private readonly IAudioPlaybackAdapter mPlayback;
        private readonly IAudioClock mClock;
        private readonly int mHistoryCapacity;
        private readonly List<AudioHistoryRecord> mHistory;
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
            mHistoryCapacity = Math.Max(1, historyCapacity);
            mHistory = new List<AudioHistoryRecord>(mHistoryCapacity);
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

        public IReadOnlyList<AudioHistoryRecord> History => mHistory;

        public AudioCueResult RequestCue(AudioCueRequest request)
        {
            AddHistory(new AudioHistoryRecord
            {
                Outcome = AudioHistoryOutcome.Requested,
                CueId = request.CueId,
                DiagnosticSource = request.DiagnosticSource,
                Time = mClock.UnscaledTime,
            });

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
                AddHistory(new AudioHistoryRecord
                {
                    Outcome = AudioHistoryOutcome.Cooldown,
                    CueId = request.CueId,
                    CueNote = binding.Note,
                    DiagnosticSource = request.DiagnosticSource,
                    FailureReason = "minimum interval",
                    Time = now,
                });
                return new AudioCueResult
                {
                    Outcome = AudioCueOutcome.Cooldown,
                    CueId = request.CueId,
                    CueNote = binding.Note,
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
                AddHistory(new AudioHistoryRecord
                {
                    Outcome = AudioHistoryOutcome.BackendFailure,
                    CueId = request.CueId,
                    CueNote = binding.Note,
                    DiagnosticSource = request.DiagnosticSource,
                    FailureReason = backend.FailureReason,
                    Time = now,
                });
                return new AudioCueResult
                {
                    Outcome = AudioCueOutcome.BackendFailure,
                    CueId = request.CueId,
                    CueNote = binding.Note,
                    FailureReason = backend.FailureReason,
                };
            }

            var actualClipKey = string.IsNullOrEmpty(backend.ActualClipKey)
                ? binding.ClipKey
                : backend.ActualClipKey;
            mLastPlayedAt[binding] = now;
            AddHistory(new AudioHistoryRecord
            {
                Outcome = AudioHistoryOutcome.Played,
                CueId = request.CueId,
                CueNote = binding.Note,
                DiagnosticSource = request.DiagnosticSource,
                ActualClipKey = actualClipKey,
                Time = now,
            });
            return new AudioCueResult
            {
                Outcome = AudioCueOutcome.Played,
                CueId = request.CueId,
                CueNote = binding.Note,
                ActualClipKey = actualClipKey,
            };
        }

        protected override void OnInit()
        {
        }

        private AudioCueResult RecordUnbound(AudioCueRequest request, string reason)
        {
            AddHistory(new AudioHistoryRecord
            {
                Outcome = AudioHistoryOutcome.Unbound,
                CueId = request.CueId,
                DiagnosticSource = request.DiagnosticSource,
                FailureReason = reason,
                Time = mClock.UnscaledTime,
            });
            return new AudioCueResult
            {
                Outcome = AudioCueOutcome.Unbound,
                CueId = request.CueId,
                FailureReason = reason,
            };
        }

        private void AddHistory(AudioHistoryRecord record)
        {
            if (mHistory.Count >= mHistoryCapacity)
            {
                mHistory.RemoveAt(0);
            }

            mHistory.Add(record);
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
