using System;
using System.Collections.Generic;
using System.Globalization;
using NineGrid.Content.Audio;
using NineGrid.Core;
using NineGrid.Flow.Diagnostics;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Systems
{
    public enum MusicRequestOutcome
    {
        Played,
        NoOp,
        InvalidSource,
        Unbound,
        BackendFailure,
    }

    public enum MusicHistoryOutcome
    {
        Requested,
        NoOp,
        InvalidSource,
        Unbound,
        Started,
        Retiring,
        Retired,
        RetiredSourceReleased,
        BackendFailure,
        Stopped,
        StaleCallback,
    }

    public readonly struct MusicPlaybackRequest
    {
        public MusicPlaybackRequest(
            MusicStateRequest stateRequest,
            MusicBinding binding,
            float volume,
            float startOffsetSeconds,
            float fadeInSeconds)
        {
            StateRequest = stateRequest;
            Binding = binding;
            Volume = volume;
            StartOffsetSeconds = startOffsetSeconds;
            FadeInSeconds = fadeInSeconds;
        }

        public MusicStateRequest StateRequest { get; }
        public MusicBinding Binding { get; }
        public float Volume { get; }
        public float StartOffsetSeconds { get; }
        public float FadeInSeconds { get; }
    }

    public sealed class MusicPlaybackHandle
    {
        public MusicPlaybackHandle(string clipKey, object nativeHandle = null)
        {
            ClipKey = clipKey ?? string.Empty;
            NativeHandle = nativeHandle;
        }

        public string ClipKey { get; }
        public object NativeHandle { get; }
    }

    public readonly struct MusicBackendResult
    {
        private MusicBackendResult(
            bool succeeded,
            MusicPlaybackHandle handle,
            string actualClipKey,
            string failureReason)
        {
            Succeeded = succeeded;
            Handle = handle;
            ActualClipKey = actualClipKey ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Succeeded { get; }
        public MusicPlaybackHandle Handle { get; }
        public string ActualClipKey { get; }
        public string FailureReason { get; }

        public static MusicBackendResult Success(MusicPlaybackHandle handle, string actualClipKey)
        {
            return new MusicBackendResult(true, handle, actualClipKey, string.Empty);
        }

        public static MusicBackendResult Failure(string reason)
        {
            return new MusicBackendResult(false, null, string.Empty, reason);
        }
    }

    public interface IMusicPlaybackAdapter
    {
        MusicBackendResult Play(MusicPlaybackRequest request);

        void FadeOut(MusicPlaybackHandle handle, float durationSeconds, Action completed);

        void Stop(MusicPlaybackHandle handle);
    }

    public sealed class MusicRequestResult
    {
        public MusicRequestOutcome Outcome { get; internal set; }
        public DesiredMusicState State { get; internal set; }
        public string StableSource { get; internal set; }
        public string BindingClipKey { get; internal set; }
        public string ActualClipKey { get; internal set; }
        public long MusicGeneration { get; internal set; }
        public string Reason { get; internal set; }
    }

    public sealed class MusicHistoryRecord
    {
        public MusicHistoryOutcome Outcome { get; internal set; }
        public DesiredMusicState State { get; internal set; }
        public string StableSource { get; internal set; }
        public string BindingClipKey { get; internal set; }
        public string ActualClipKey { get; internal set; }
        public long MusicGeneration { get; internal set; }
        public string Reason { get; internal set; }
        public double Time { get; internal set; }
    }

    public interface IMusicSystem : ISystem
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        IReadOnlyList<MusicHistoryRecord> History { get; }
#endif
        DesiredMusicState? DesiredState { get; }
        DesiredMusicState? CurrentState { get; }
        string CurrentStableSource { get; }
        string CurrentClipKey { get; }
        long CurrentMusicGeneration { get; }
        int CurrentSourceCount { get; }
        int RetiringSourceCount { get; }

        MusicRequestResult RequestState(MusicStateRequest request);

        void StopAll(string stableSource);
    }

    public sealed class MusicSystem : AbstractSystem, IMusicSystem
    {
        public const int DefaultHistoryCapacity = 256;

        private readonly MusicBindingCatalog mCatalog;
        private readonly IMusicPlaybackAdapter mPlayback;
        private readonly IAudioClock mClock;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly int mHistoryCapacity;
        private readonly List<MusicHistoryRecord> mHistory;
#endif
        private ActiveMusic mCurrent;
        private ActiveMusic mRetiring;
        private DesiredMusicState? mDesiredState;

        private long mNextGeneration;

        public MusicSystem(
            MusicBindingCatalog catalog,
            IMusicPlaybackAdapter playback,
            IAudioClock clock,
            int historyCapacity = DefaultHistoryCapacity)
        {
            mCatalog = catalog ?? MusicBindingCatalog.FromJson(string.Empty);
            mPlayback = playback ?? new NullMusicPlaybackAdapter();
            mClock = clock ?? new RealtimeAudioClock();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            mHistoryCapacity = Math.Max(1, historyCapacity);
            mHistory = new List<MusicHistoryRecord>(mHistoryCapacity);
#endif
        }

        public static IMusicSystem EnsureRegistered(
            IArchitecture architecture = null,
            MusicBindingCatalog catalog = null,
            IMusicPlaybackAdapter playback = null,
            IAudioClock clock = null)
        {
            var arch = architecture ?? NineGridArchitecture.Interface;
            if (arch == null)
            {
                throw new InvalidOperationException("Architecture is not available for MusicSystem.");
            }

            var existing = arch.GetSystem<IMusicSystem>();
            if (existing != null)
            {
                return existing;
            }

            var created = new MusicSystem(
                catalog ?? MusicBindingCatalog.LoadFromResources(),
                playback ?? new MMSoundManagerAudioPlaybackAdapter(),
                clock ?? new RealtimeAudioClock());
            arch.RegisterSystem<IMusicSystem>(created);
            return created;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public IReadOnlyList<MusicHistoryRecord> History => mHistory;
#endif
        public DesiredMusicState? DesiredState => mDesiredState;
        public DesiredMusicState? CurrentState => mCurrent == null ? (DesiredMusicState?)null : mCurrent.State;
        public string CurrentStableSource => mCurrent?.StableSource ?? string.Empty;
        public string CurrentClipKey => mCurrent?.ActualClipKey ?? string.Empty;
        public long CurrentMusicGeneration => mCurrent?.Generation ?? 0L;
        public int CurrentSourceCount => mCurrent == null ? 0 : 1;
        public int RetiringSourceCount => mRetiring == null ? 0 : 1;

        public MusicRequestResult RequestState(MusicStateRequest request)
        {
            var requestedAt = mClock.UnscaledTime;
            Record(
                MusicHistoryOutcome.Requested,
                request.State,
                request.StableSource,
                bindingClipKey: null,
                actualClipKey: null,
                generation: mCurrent?.Generation ?? 0L,
                reason: null,
                time: requestedAt,
                traceKind: PerfTraceKinds.MusicStateRequested);

            if (!request.HasStableSource)
            {
                return ReturnResult(
                    MusicRequestOutcome.InvalidSource,
                    request,
                    bindingClipKey: null,
                    actualClipKey: null,
                    generation: mCurrent?.Generation ?? 0L,
                    reason: "音乐状态请求必须带稳定 source。",
                    historyOutcome: MusicHistoryOutcome.InvalidSource,
                    traceKind: PerfTraceKinds.MusicStateInvalidSource);
            }

            if (!mCatalog.TryResolve(request.State, out var binding)
                || binding == null
                || !binding.Enabled
                || string.IsNullOrWhiteSpace(binding.ClipKey))
            {
                return ReturnResult(
                    MusicRequestOutcome.Unbound,
                    request,
                    binding?.ClipKey,
                    actualClipKey: null,
                    generation: mCurrent?.Generation ?? 0L,
                    reason: "音乐状态未绑定、已禁用或缺少素材。",
                    historyOutcome: MusicHistoryOutcome.Unbound,
                    traceKind: PerfTraceKinds.MusicStateUnbound);
            }

            mDesiredState = request.State;

            if (mCurrent != null
                && string.Equals(mCurrent.BindingClipKey, binding.ClipKey, StringComparison.Ordinal))
            {
                mCurrent.State = request.State;
                mCurrent.StableSource = request.StableSource;
                return ReturnResult(
                    MusicRequestOutcome.NoOp,
                    request,
                    binding.ClipKey,
                    mCurrent.ActualClipKey,
                    mCurrent.Generation,
                    reason: "状态或解析结果仍为当前音乐。",
                    historyOutcome: MusicHistoryOutcome.NoOp,
                    traceKind: PerfTraceKinds.MusicStateNoOp);
            }

            ReleaseRetiringSource(request, "新音乐状态到来");

            var retiring = mCurrent;
            mCurrent = null;
            if (retiring != null)
            {
                mRetiring = retiring;
                Record(
                    MusicHistoryOutcome.Retiring,
                    retiring.State,
                    retiring.StableSource,
                    retiring.BindingClipKey,
                    retiring.ActualClipKey,
                    retiring.Generation,
                    reason: request.StableSource,
                    time: requestedAt,
                    traceKind: PerfTraceKinds.MusicStateRetiring);
                BeginFadeOut(retiring, binding.FadeOutSeconds, request, requestedAt);
            }

            var generation = ++mNextGeneration;
            MusicBackendResult backend;
            try
            {
                backend = mPlayback.Play(new MusicPlaybackRequest(
                    request,
                    binding,
                    DecibelsToLinear(binding.VolumeDb),
                    Mathf.Max(0f, binding.StartOffsetSeconds),
                    Mathf.Max(0f, binding.FadeInSeconds)));
            }
            catch (Exception exception)
            {
                backend = MusicBackendResult.Failure(exception.Message);
            }

            if (!backend.Succeeded || backend.Handle == null)
            {
                return ReturnResult(
                    MusicRequestOutcome.BackendFailure,
                    request,
                    binding.ClipKey,
                    backend.ActualClipKey,
                    generation,
                    backend.FailureReason,
                    MusicHistoryOutcome.BackendFailure,
                    PerfTraceKinds.MusicStateBackendFailure);
            }

            mCurrent = new ActiveMusic(
                request.State,
                request.StableSource,
                binding.ClipKey,
                string.IsNullOrEmpty(backend.ActualClipKey) ? binding.ClipKey : backend.ActualClipKey,
                generation,
                binding.FadeOutSeconds,
                backend.Handle);
            Record(
                MusicHistoryOutcome.Started,
                request.State,
                request.StableSource,
                binding.ClipKey,
                mCurrent.ActualClipKey,
                generation,
                reason: null,
                time: mClock.UnscaledTime,
                traceKind: PerfTraceKinds.MusicStateStarted);
            return new MusicRequestResult
            {
                Outcome = MusicRequestOutcome.Played,
                State = request.State,
                StableSource = request.StableSource,
                BindingClipKey = binding.ClipKey,
                ActualClipKey = mCurrent.ActualClipKey,
                MusicGeneration = generation,
                Reason = string.Empty,
            };
        }

        public void StopAll(string stableSource)
        {
            var source = string.IsNullOrWhiteSpace(stableSource) ? "MusicSystem.StopAll" : stableSource;
            StopActive(ref mCurrent, source);
            StopActive(ref mRetiring, source);
            mDesiredState = null;
        }

        protected override void OnInit()
        {
        }

        private MusicRequestResult ReturnResult(
            MusicRequestOutcome outcome,
            MusicStateRequest request,
            string bindingClipKey,
            string actualClipKey,
            long generation,
            string reason,
            MusicHistoryOutcome historyOutcome,
            string traceKind)
        {
            Record(
                historyOutcome,
                request.State,
                request.StableSource,
                bindingClipKey,
                actualClipKey,
                generation,
                reason,
                mClock.UnscaledTime,
                traceKind);
            return new MusicRequestResult
            {
                Outcome = outcome,
                State = request.State,
                StableSource = request.StableSource,
                BindingClipKey = bindingClipKey ?? string.Empty,
                ActualClipKey = actualClipKey ?? string.Empty,
                MusicGeneration = generation,
                Reason = reason ?? string.Empty,
            };
        }

        private void BeginFadeOut(
            ActiveMusic retiring,
            float durationSeconds,
            MusicStateRequest nextRequest,
            double requestedAt)
        {
            try
            {
                mPlayback.FadeOut(
                    retiring.Handle,
                    Mathf.Max(0f, durationSeconds),
                    () => CompleteFadeOut(retiring, nextRequest, requestedAt));
            }
            catch (Exception exception)
            {
                try
                {
                    mPlayback.Stop(retiring.Handle);
                }
                catch (Exception stopException)
                {
                    exception = new AggregateException(exception, stopException);
                }

                CompleteFadeOut(retiring, nextRequest, requestedAt, exception.Message);
            }
        }

        private void CompleteFadeOut(
            ActiveMusic retiring,
            MusicStateRequest nextRequest,
            double requestedAt,
            string reason = null)
        {
            if (ReferenceEquals(mRetiring, retiring))
            {
                mRetiring = null;
                Record(
                    MusicHistoryOutcome.Retired,
                    retiring.State,
                    retiring.StableSource,
                    retiring.BindingClipKey,
                    retiring.ActualClipKey,
                    retiring.Generation,
                    reason ?? nextRequest.StableSource,
                    mClock.UnscaledTime,
                    PerfTraceKinds.MusicStateRetired);
                return;
            }

            Record(
                MusicHistoryOutcome.StaleCallback,
                retiring.State,
                retiring.StableSource,
                retiring.BindingClipKey,
                retiring.ActualClipKey,
                retiring.Generation,
                "旧代数回调被忽略；currentGeneration=" + (mCurrent?.Generation ?? 0L).ToString(CultureInfo.InvariantCulture),
                mClock.UnscaledTime,
                PerfTraceKinds.MusicStateStaleCallback);
        }

        private void ReleaseRetiringSource(MusicStateRequest request, string reason)
        {
            if (mRetiring == null)
            {
                return;
            }

            var source = mRetiring;
            mRetiring = null;
            try
            {
                mPlayback.Stop(source.Handle);
            }
            catch (Exception exception)
            {
                reason = reason + ": " + exception.Message;
            }

            Record(
                MusicHistoryOutcome.RetiredSourceReleased,
                source.State,
                source.StableSource,
                source.BindingClipKey,
                source.ActualClipKey,
                source.Generation,
                reason + " -> " + request.StableSource,
                mClock.UnscaledTime,
                PerfTraceKinds.MusicStateRetiredSourceReleased);
        }

        private void StopActive(ref ActiveMusic active, string stableSource)
        {
            if (active == null)
            {
                return;
            }

            var stopped = active;
            active = null;
            try
            {
                mPlayback.Stop(stopped.Handle);
            }
            catch (Exception exception)
            {
                Record(
                    MusicHistoryOutcome.Stopped,
                    stopped.State,
                    stableSource,
                    stopped.BindingClipKey,
                    stopped.ActualClipKey,
                    stopped.Generation,
                    exception.Message,
                    mClock.UnscaledTime,
                    PerfTraceKinds.MusicStateStopped);
                return;
            }

            Record(
                MusicHistoryOutcome.Stopped,
                stopped.State,
                stableSource,
                stopped.BindingClipKey,
                stopped.ActualClipKey,
                stopped.Generation,
                "StopAll",
                mClock.UnscaledTime,
                PerfTraceKinds.MusicStateStopped);
        }

        private void Record(
            MusicHistoryOutcome outcome,
            DesiredMusicState state,
            string stableSource,
            string bindingClipKey,
            string actualClipKey,
            long generation,
            string reason,
            double time,
            string traceKind)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddHistory(new MusicHistoryRecord
            {
                Outcome = outcome,
                State = state,
                StableSource = stableSource ?? string.Empty,
                BindingClipKey = bindingClipKey ?? string.Empty,
                ActualClipKey = actualClipKey ?? string.Empty,
                MusicGeneration = generation,
                Reason = reason ?? string.Empty,
                Time = time,
            });
#endif
            try
            {
                var payload = new Dictionary<string, string>
                {
                    ["outcome"] = outcome.ToString(),
                    ["state"] = state.ToString(),
                    ["stableSource"] = stableSource ?? string.Empty,
                    ["bindingClipKey"] = bindingClipKey ?? string.Empty,
                    ["actualClipKey"] = actualClipKey ?? string.Empty,
                    ["musicGeneration"] = generation.ToString(CultureInfo.InvariantCulture),
                    ["time"] = time.ToString("R", CultureInfo.InvariantCulture),
                };
                if (!string.IsNullOrEmpty(reason))
                {
                    payload["reason"] = reason;
                }

                DirectorTrace.AppendBusyFields(payload);
                payload["batchId"] = DirectorTrace.ActiveBatchId.ToString(CultureInfo.InvariantCulture);
                payload["sessionId"] = DiagTraceShared.CurrentSessionId;
                payload["runTag"] = DiagTraceShared.RunTag;
                PerfTraceRecorder.Record(traceKind, uid: -1, PerfTraceSites.AudioSystemCue, payload);
            }
            catch (Exception)
            {
                // 音乐诊断失败不干扰流程。
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void AddHistory(MusicHistoryRecord record)
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

        private sealed class ActiveMusic
        {
            public ActiveMusic(
                DesiredMusicState state,
                string stableSource,
                string bindingClipKey,
                string actualClipKey,
                long generation,
                float fadeOutSeconds,
                MusicPlaybackHandle handle)
            {
                State = state;
                StableSource = stableSource ?? string.Empty;
                BindingClipKey = bindingClipKey ?? string.Empty;
                ActualClipKey = actualClipKey ?? string.Empty;
                Generation = generation;
                FadeOutSeconds = fadeOutSeconds;
                Handle = handle;
            }

            public DesiredMusicState State { get; set; }
            public string StableSource { get; set; }
            public string BindingClipKey { get; }
            public string ActualClipKey { get; }
            public long Generation { get; }
            public float FadeOutSeconds { get; }
            public MusicPlaybackHandle Handle { get; }
        }

        private sealed class NullMusicPlaybackAdapter : IMusicPlaybackAdapter
        {
            public MusicBackendResult Play(MusicPlaybackRequest request)
            {
                return MusicBackendResult.Failure("音乐播放 Adapter 未安装。");
            }

            public void FadeOut(MusicPlaybackHandle handle, float durationSeconds, Action completed)
            {
                completed?.Invoke();
            }

            public void Stop(MusicPlaybackHandle handle)
            {
            }
        }

        private sealed class RealtimeAudioClock : IAudioClock
        {
            public double UnscaledTime => Time.realtimeSinceStartup;
        }
    }
}
