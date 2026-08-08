using System;
using System.Collections.Generic;
using System.Globalization;
using NineGrid.Content.Audio;
using NineGrid.Core;
using NineGrid.Flow.Diagnostics;
using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PreviewStarted,
        PreviewStopped,
        PreviewReplaced,
        PreviewResumed,
        OverlapAnomaly,
#endif
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
        public MusicPlaybackHandle(string clipKey, object nativeHandle = null, string sourceId = null)
        {
            ClipKey = clipKey ?? string.Empty;
            NativeHandle = nativeHandle;
            SourceId = sourceId ?? string.Empty;
        }

        public string ClipKey { get; }
        public object NativeHandle { get; }
        public string SourceId { get; }
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public readonly struct MusicPreviewRequest
    {
        public MusicPreviewRequest(
            string clipKey,
            float volumeDb,
            float startOffsetSeconds,
            float fadeInSeconds,
            bool loop)
        {
            ClipKey = clipKey ?? string.Empty;
            VolumeDb = volumeDb;
            StartOffsetSeconds = startOffsetSeconds;
            FadeInSeconds = fadeInSeconds;
            Loop = loop;
        }

        public string ClipKey { get; }
        public float VolumeDb { get; }
        public float StartOffsetSeconds { get; }
        public float FadeInSeconds { get; }
        public bool Loop { get; }
    }

    public sealed class MusicPreviewResult
    {
        public bool Succeeded { get; internal set; }
        public string ClipKey { get; internal set; }
        public string Reason { get; internal set; }
    }

    /// <summary>Optional Editor/Development capability of the actual music Adapter.</summary>
    public interface IMusicPlaybackDiagnosticsAdapter : IMusicPlaybackAdapter
    {
        MusicBackendResult PlayPreview(MusicPreviewRequest request);

        IReadOnlyList<MusicTrackSourceSnapshot> GetPlayingMusicSources();

        double GetPlaybackPosition(MusicPlaybackHandle handle);

        void Pause(MusicPlaybackHandle handle);

        void Resume(MusicPlaybackHandle handle, double positionSeconds);

        void StopMusicTrackSource(string sourceId);
    }
#endif

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public string SceneName { get; internal set; }
        public int ChainId { get; internal set; }
        public int BatchId { get; internal set; }
        public MusicOverlapAnomaly Anomaly { get; internal set; }
#endif
    }

    public interface IMusicSystem : ISystem
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        IReadOnlyList<MusicHistoryRecord> History { get; }
        IReadOnlyList<MusicOverlapAnomaly> OverlapAnomalies { get; }
        MusicAuditResult LastAudit { get; }
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        MusicPreviewResult BeginPreview(MusicPreviewRequest request);

        void EndPreview(string stableSource = null);

        MusicAuditResult AuditMusicTrack(string trigger);

        MusicAuditResult StopUnknownMusic(string stableSource = null);
#endif
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
        private readonly List<MusicOverlapAnomaly> mOverlapAnomalies;
        private readonly List<PreviewSuspension> mPreviewSuspensions =
            new List<PreviewSuspension>(2);
        private MusicAuditResult mLastAudit;
        private MusicDiagnosticsTicker mDiagnosticsTicker;
        private ActiveMusic mPreview;
#endif
        private ActiveMusic mCurrent;
        private ActiveMusic mRetiring;
        private DesiredMusicState? mDesiredState;
        private string mLastRequestSource = string.Empty;
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
            mOverlapAnomalies = new List<MusicOverlapAnomaly>(mHistoryCapacity);
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
        public IReadOnlyList<MusicOverlapAnomaly> OverlapAnomalies => mOverlapAnomalies;
        public MusicAuditResult LastAudit => mLastAudit;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (mPreview != null)
            {
                EndPreview("音乐状态切换");
            }
#endif
            var requestedAt = mClock.UnscaledTime;
            mLastRequestSource = request.StableSource ?? string.Empty;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AuditMusicTrack("StateRequest");
#endif

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
                var noOp = ReturnResult(
                    MusicRequestOutcome.NoOp,
                    request,
                    binding.ClipKey,
                    mCurrent.ActualClipKey,
                    mCurrent.Generation,
                    reason: "状态或解析结果仍为当前音乐。",
                    historyOutcome: MusicHistoryOutcome.NoOp,
                    traceKind: PerfTraceKinds.MusicStateNoOp);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                AuditMusicTrack("StateRequest.NoOp");
#endif
                return noOp;
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
                BeginFadeOut(retiring, retiring.FadeOutSeconds, request, requestedAt);
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
                var failed = ReturnResult(
                    MusicRequestOutcome.BackendFailure,
                    request,
                    binding.ClipKey,
                    backend.ActualClipKey,
                    generation,
                    backend.FailureReason,
                    MusicHistoryOutcome.BackendFailure,
                    PerfTraceKinds.MusicStateBackendFailure);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                AuditMusicTrack("PlaybackFailure");
#endif
                return failed;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AuditMusicTrack("Playback");
#endif
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EndPreview(stableSource ?? "MusicSystem.StopAll");
#endif
            var source = string.IsNullOrWhiteSpace(stableSource) ? "MusicSystem.StopAll" : stableSource;
            StopActive(ref mCurrent, source);
            StopActive(ref mRetiring, source);
            mDesiredState = null;
            mLastRequestSource = source;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AuditMusicTrack("StopAll");
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public MusicPreviewResult BeginPreview(MusicPreviewRequest request)
        {
            var diagnostics = mPlayback as IMusicPlaybackDiagnosticsAdapter;
            if (diagnostics == null)
            {
                return PreviewFailure(request.ClipKey, "当前播放 Adapter 不支持 Editor Preview。");
            }

            if (string.IsNullOrWhiteSpace(request.ClipKey))
            {
                return PreviewFailure(request.ClipKey, "试听素材为空。");
            }

            if (mPreview != null)
            {
                StopPreviewSource(MusicHistoryOutcome.PreviewReplaced, "切换 Editor Preview");
            }

            if (mPreviewSuspensions.Count == 0)
            {
                SuspendSource(mCurrent, diagnostics);
                SuspendSource(mRetiring, diagnostics);
            }

            MusicBackendResult backend;
            try
            {
                backend = diagnostics.PlayPreview(request);
            }
            catch (Exception exception)
            {
                backend = MusicBackendResult.Failure(exception.Message);
            }

            if (!backend.Succeeded || backend.Handle == null)
            {
                ResumeSuspendedSources(diagnostics);
                return PreviewFailure(request.ClipKey, backend.FailureReason);
            }

            var state = mCurrent?.State ?? mDesiredState ?? DesiredMusicState.MainMenu;
            var actualClipKey = string.IsNullOrEmpty(backend.ActualClipKey)
                ? request.ClipKey
                : backend.ActualClipKey;
            mPreview = new ActiveMusic(
                state,
                "Editor Preview",
                request.ClipKey,
                actualClipKey,
                mCurrent?.Generation ?? mNextGeneration,
                0f,
                backend.Handle);
            Record(
                MusicHistoryOutcome.PreviewStarted,
                state,
                "Editor Preview",
                request.ClipKey,
                actualClipKey,
                mPreview.Generation,
                null,
                mClock.UnscaledTime,
                PerfTraceKinds.MusicStateStarted);
            AuditMusicTrack("PreviewStart");
            return new MusicPreviewResult
            {
                Succeeded = true,
                ClipKey = actualClipKey,
                Reason = string.Empty,
            };
        }

        public void EndPreview(string stableSource = null)
        {
            var diagnostics = mPlayback as IMusicPlaybackDiagnosticsAdapter;
            if (mPreview != null)
            {
                StopPreviewSource(MusicHistoryOutcome.PreviewStopped,
                    string.IsNullOrWhiteSpace(stableSource) ? "Editor Preview 结束" : stableSource);
            }

            if (diagnostics != null)
            {
                ResumeSuspendedSources(diagnostics);
            }
            else
            {
                mPreviewSuspensions.Clear();
            }

            AuditMusicTrack("PreviewEnd");
        }

        public MusicAuditResult AuditMusicTrack(string trigger)
        {
            var diagnostics = mPlayback as IMusicPlaybackDiagnosticsAdapter;
            if (diagnostics == null)
            {
                mLastAudit = new MusicAuditResult(
                    trigger,
                    Array.Empty<MusicTrackSourceSnapshot>(),
                    Array.Empty<string>(),
                    Array.Empty<MusicTrackSourceSnapshot>(),
                    null);
                return mLastAudit;
            }

            IReadOnlyList<MusicTrackSourceSnapshot> observed;
            try
            {
                observed = diagnostics.GetPlayingMusicSources() ?? Array.Empty<MusicTrackSourceSnapshot>();
            }
            catch (Exception exception)
            {
                observed = Array.Empty<MusicTrackSourceSnapshot>();
                Record(
                    MusicHistoryOutcome.OverlapAnomaly,
                    mCurrent?.State ?? mDesiredState ?? DesiredMusicState.MainMenu,
                    mLastRequestSource,
                    mCurrent?.BindingClipKey,
                    mCurrent?.ActualClipKey,
                    mCurrent?.Generation ?? 0L,
                    "Music 轨巡检失败：" + exception.Message,
                    mClock.UnscaledTime,
                    PerfTraceKinds.MusicOverlapAnomaly);
            }

            var actual = new List<MusicTrackSourceSnapshot>(observed.Count);
            for (var i = 0; i < observed.Count; i++)
            {
                if (observed[i].IsPlaying)
                {
                    actual.Add(observed[i]);
                }
            }

            var claimed = new List<string>(3);
            AddClaimedSource(claimed, mCurrent);
            AddClaimedSource(claimed, mRetiring);
            AddClaimedSource(claimed, mPreview);

            var unknown = new List<MusicTrackSourceSnapshot>();
            for (var i = 0; i < actual.Count; i++)
            {
                if (!IsClaimed(actual[i], mCurrent)
                    && !IsClaimed(actual[i], mRetiring)
                    && !IsClaimed(actual[i], mPreview))
                {
                    unknown.Add(actual[i]);
                }
            }

            MusicOverlapAnomaly anomaly = null;
            if (unknown.Count > 0)
            {
                anomaly = new MusicOverlapAnomaly(
                    trigger,
                    mCurrent?.Generation ?? mRetiring?.Generation ?? 0L,
                    mDesiredState?.ToString(),
                    mCurrent?.BindingClipKey,
                    mCurrent?.StableSource,
                    mRetiring?.StableSource,
                    mLastRequestSource,
                    SceneManager.GetActiveScene().name,
                    DirectorTrace.CurrentChainId,
                    DirectorTrace.ActiveBatchId,
                    mClock.UnscaledTime,
                    actual.ToArray(),
                    claimed.ToArray(),
                    unknown.ToArray());
                AddAnomaly(anomaly);
                Record(
                    MusicHistoryOutcome.OverlapAnomaly,
                    mCurrent?.State ?? mRetiring?.State ?? mDesiredState ?? DesiredMusicState.MainMenu,
                    mLastRequestSource,
                    mCurrent?.BindingClipKey,
                    mCurrent?.ActualClipKey,
                    anomaly.MusicGeneration,
                    "未知 Music 来源：" + unknown.Count.ToString(CultureInfo.InvariantCulture),
                    anomaly.Time,
                    PerfTraceKinds.MusicOverlapAnomaly,
                    anomaly);
            }

            mLastAudit = new MusicAuditResult(
                trigger,
                actual.ToArray(),
                claimed.ToArray(),
                unknown.ToArray(),
                anomaly);
            return mLastAudit;
        }

        public MusicAuditResult StopUnknownMusic(string stableSource = null)
        {
            var diagnostics = mPlayback as IMusicPlaybackDiagnosticsAdapter;
            var audit = AuditMusicTrack("StopUnknownMusic.Before");
            if (diagnostics == null || audit.UnknownSources.Count == 0)
            {
                return audit;
            }

            for (var i = 0; i < audit.UnknownSources.Count; i++)
            {
                var source = audit.UnknownSources[i];
                if (string.IsNullOrEmpty(source.SourceId))
                {
                    continue;
                }

                try
                {
                    diagnostics.StopMusicTrackSource(source.SourceId);
                }
                catch (Exception exception)
                {
                    Record(
                        MusicHistoryOutcome.Stopped,
                        mCurrent?.State ?? mDesiredState ?? DesiredMusicState.MainMenu,
                        stableSource ?? "MusicSystem.StopUnknownMusic",
                        source.ClipKey,
                        source.ClipKey,
                        mCurrent?.Generation ?? 0L,
                        exception.Message,
                        mClock.UnscaledTime,
                        PerfTraceKinds.MusicStateStopped);
                }
            }

            return AuditMusicTrack("StopUnknownMusic.After");
        }
#endif

        protected override void OnInit()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (Application.isPlaying)
            {
                mDiagnosticsTicker = MusicDiagnosticsTicker.Install(this);
            }
#endif
        }

        protected override void OnDeinit()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SceneManager.sceneLoaded -= OnSceneLoaded;
            mDiagnosticsTicker?.Dispose();
            mDiagnosticsTicker = null;
            EndPreview("MusicSystem.OnDeinit");
#endif
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                AuditMusicTrack("FadeOutComplete");
#endif
                return;
            }

            Record(
                MusicHistoryOutcome.StaleCallback,
                retiring.State,
                retiring.StableSource,
                retiring.BindingClipKey,
                retiring.ActualClipKey,
                retiring.Generation,
                "旧代数回调被忽略；currentGeneration="
                + (mCurrent?.Generation ?? 0L).ToString(CultureInfo.InvariantCulture),
                mClock.UnscaledTime,
                PerfTraceKinds.MusicStateStaleCallback);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AuditMusicTrack("StaleFadeOutComplete");
#endif
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
            string traceKind
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            , MusicOverlapAnomaly anomaly = null
#endif
            )
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
                SceneName = SceneManager.GetActiveScene().name,
                ChainId = DirectorTrace.CurrentChainId,
                BatchId = DirectorTrace.ActiveBatchId,
                Anomaly = anomaly,
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
                    ["scene"] = SceneManager.GetActiveScene().name,
                };
                if (!string.IsNullOrEmpty(reason))
                {
                    payload["reason"] = reason;
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (anomaly != null)
                {
                    payload["desiredState"] = anomaly.DesiredState;
                    payload["requestSource"] = anomaly.RequestSource;
                    payload["claimedSourceIds"] = string.Join(",", anomaly.ClaimedSourceIds);
                    payload["unknownSources"] = string.Join(",", FormatSources(anomaly.UnknownSources));
                }
#endif
                DirectorTrace.AppendBusyFields(payload);
                payload["batchId"] = DirectorTrace.ActiveBatchId.ToString(CultureInfo.InvariantCulture);
                payload["chainId"] = DirectorTrace.CurrentChainId.ToString(CultureInfo.InvariantCulture);
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
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AuditMusicTrack("SceneLoaded:" + scene.name);
        }

        private MusicPreviewResult PreviewFailure(string clipKey, string reason)
        {
            return new MusicPreviewResult
            {
                Succeeded = false,
                ClipKey = clipKey ?? string.Empty,
                Reason = reason ?? "音乐试听失败。",
            };
        }

        private void SuspendSource(ActiveMusic active, IMusicPlaybackDiagnosticsAdapter diagnostics)
        {
            if (active == null || active.Handle == null)
            {
                return;
            }

            for (var i = 0; i < mPreviewSuspensions.Count; i++)
            {
                if (ReferenceEquals(mPreviewSuspensions[i].Active, active))
                {
                    return;
                }
            }

            double position;
            try
            {
                position = diagnostics.GetPlaybackPosition(active.Handle);
                diagnostics.Pause(active.Handle);
            }
            catch
            {
                position = 0d;
            }

            mPreviewSuspensions.Add(new PreviewSuspension(active, position));
        }

        private void ResumeSuspendedSources(IMusicPlaybackDiagnosticsAdapter diagnostics)
        {
            for (var i = 0; i < mPreviewSuspensions.Count; i++)
            {
                var suspended = mPreviewSuspensions[i];
                var stillOwned = ReferenceEquals(mCurrent, suspended.Active)
                    || ReferenceEquals(mRetiring, suspended.Active);
                if (!stillOwned || suspended.Active?.Handle == null)
                {
                    continue;
                }

                try
                {
                    diagnostics.Resume(suspended.Active.Handle, suspended.PositionSeconds);
                    Record(
                        MusicHistoryOutcome.PreviewResumed,
                        suspended.Active.State,
                        suspended.Active.StableSource,
                        suspended.Active.BindingClipKey,
                        suspended.Active.ActualClipKey,
                        suspended.Active.Generation,
                        suspended.PositionSeconds.ToString("R", CultureInfo.InvariantCulture),
                        mClock.UnscaledTime,
                        PerfTraceKinds.MusicStateStarted);
                }
                catch (Exception exception)
                {
                    Record(
                        MusicHistoryOutcome.PreviewResumed,
                        suspended.Active.State,
                        suspended.Active.StableSource,
                        suspended.Active.BindingClipKey,
                        suspended.Active.ActualClipKey,
                        suspended.Active.Generation,
                        exception.Message,
                        mClock.UnscaledTime,
                        PerfTraceKinds.MusicStateStarted);
                }
            }

            mPreviewSuspensions.Clear();
        }

        private void StopPreviewSource(MusicHistoryOutcome outcome, string reason)
        {
            if (mPreview == null)
            {
                return;
            }

            var stopped = mPreview;
            mPreview = null;
            try
            {
                mPlayback.Stop(stopped.Handle);
            }
            catch (Exception exception)
            {
                reason = reason + ": " + exception.Message;
            }

            Record(
                outcome,
                stopped.State,
                stopped.StableSource,
                stopped.BindingClipKey,
                stopped.ActualClipKey,
                stopped.Generation,
                reason,
                mClock.UnscaledTime,
                PerfTraceKinds.MusicStateStopped);
        }

        private void AddClaimedSource(List<string> claimed, ActiveMusic active)
        {
            var sourceId = active?.Handle?.SourceId;
            if (!string.IsNullOrEmpty(sourceId) && !claimed.Contains(sourceId))
            {
                claimed.Add(sourceId);
            }
        }

        private static bool IsClaimed(MusicTrackSourceSnapshot source, ActiveMusic active)
        {
            if (active?.Handle == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(active.Handle.SourceId)
                && string.Equals(source.SourceId, active.Handle.SourceId, StringComparison.Ordinal))
            {
                return true;
            }

            return string.IsNullOrEmpty(active.Handle.SourceId)
                && string.Equals(source.ClipKey, active.ActualClipKey, StringComparison.OrdinalIgnoreCase);
        }

        private void AddAnomaly(MusicOverlapAnomaly anomaly)
        {
            if (mOverlapAnomalies.Count >= mHistoryCapacity)
            {
                mOverlapAnomalies.RemoveAt(0);
            }

            mOverlapAnomalies.Add(anomaly);
        }

        private static IEnumerable<string> FormatSources(IReadOnlyList<MusicTrackSourceSnapshot> sources)
        {
            for (var i = 0; i < (sources?.Count ?? 0); i++)
            {
                yield return sources[i].DisplayName;
            }
        }

        private void AddHistory(MusicHistoryRecord record)
        {
            if (mHistory.Count >= mHistoryCapacity)
            {
                mHistory.RemoveAt(0);
            }

            mHistory.Add(record);
        }

        private sealed class PreviewSuspension
        {
            public PreviewSuspension(ActiveMusic active, double positionSeconds)
            {
                Active = active;
                PositionSeconds = positionSeconds;
            }

            public ActiveMusic Active { get; }
            public double PositionSeconds { get; }
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
