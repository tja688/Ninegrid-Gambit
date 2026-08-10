using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using NineGrid.Content.Vfx;
using NineGrid.Flow.Diagnostics;

namespace NineGrid.Presentation.Systems.Vfx
{
    internal sealed class VfxDiagnosticsTracker
    {
        public const int DefaultRecordCapacity = 512;
        public const int DefaultFrameCapacity = 300;
        public const int PeakContributorCapacity = 64;

        private readonly VfxReleaseCounters mReleaseCounters = new VfxReleaseCounters();
        private long mNextCorrelationId;
        private int mFrameCreated;
        private int mFrameCompleted;
        private int mFrameIndex;
        private double mNow;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly int mRecordCapacity;
        private readonly int mFrameCapacity;
        private readonly List<VfxLifecycleRecord> mRecords;
        private readonly List<VfxFrameStats> mFrames;
        private readonly Dictionary<string, BindingAggregateState> mBindingAggregates =
            new Dictionary<string, BindingAggregateState>(StringComparer.Ordinal);
        private readonly Dictionary<string, PlayerAggregateState> mPlayerAggregates =
            new Dictionary<string, PlayerAggregateState>(StringComparer.Ordinal);
        private readonly List<ActiveContributor> mActiveContributors = new List<ActiveContributor>(32);
        private readonly VfxSessionCounters mSession = new VfxSessionCounters();
        private readonly VfxPeakSnapshot mPeak = new VfxPeakSnapshot
        {
            Contributors = Array.Empty<VfxPeakContributor>(),
        };
        private readonly List<VfxPeakContributor> mPeakContributorBuffer = new List<VfxPeakContributor>(PeakContributorCapacity);
        private long mNextRecordSequence;
#endif

        public VfxDiagnosticsTracker(
            int recordCapacity = DefaultRecordCapacity,
            int frameCapacity = DefaultFrameCapacity)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            mRecordCapacity = Math.Max(1, recordCapacity);
            mFrameCapacity = Math.Max(1, frameCapacity);
            mRecords = new List<VfxLifecycleRecord>(mRecordCapacity);
            mFrames = new List<VfxFrameStats>(mFrameCapacity);
#endif
        }

        public VfxReleaseCounters ReleaseCounters => mReleaseCounters;

        public void SetClock(double now)
        {
            mNow = now;
        }

        public long BeginCorrelation()
        {
            return ++mNextCorrelationId;
        }

        public void RecordRequest(
            long correlationId,
            string cueOrStateId,
            bool isPulse,
            string diagnosticSource,
            string cardDefId,
            string skillId,
            string roomId,
            string itemDefId,
            string contentId,
            int diagnosticCardUid)
        {
            mReleaseCounters.TotalRequests++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            mSession.TotalRequests++;
#endif
            AppendRecord(
                correlationId,
                VfxLifecyclePhase.Request,
                VfxEndReason.None,
                isIssue: false,
                isPulse,
                cueOrStateId,
                bindingKey: string.Empty,
                playerId: string.Empty,
                variantId: string.Empty,
                materialKey: string.Empty,
                instanceId: string.Empty,
                spatialOwnership: string.Empty,
                domainLabel: string.Empty,
                ownerSlotKey: string.Empty,
                diagnosticSource,
                cardDefId,
                skillId,
                roomId,
                itemDefId,
                contentId,
                diagnosticCardUid,
                overrideSummary: string.Empty,
                failureReason: string.Empty);
            RecordPerfTrace(
                PerfTraceKinds.VfxLifecycleRequest,
                cueOrStateId,
                diagnosticSource,
                cardDefId,
                skillId,
                roomId,
                itemDefId,
                contentId,
                diagnosticCardUid,
                correlationId,
                phase: "request",
                bindingKey: string.Empty,
                playerId: string.Empty,
                instanceId: string.Empty,
                endReason: string.Empty,
                failureReason: string.Empty);
        }

        public void RecordResolve(
            long correlationId,
            string cueOrStateId,
            bool isPulse,
            bool resolved,
            string bindingKey,
            string playerId,
            string diagnosticSource,
            string cardDefId,
            string skillId,
            string roomId,
            string itemDefId,
            string contentId,
            int diagnosticCardUid,
            string failureReason)
        {
            AppendRecord(
                correlationId,
                VfxLifecyclePhase.Resolve,
                VfxEndReason.None,
                isIssue: false,
                isPulse,
                cueOrStateId,
                bindingKey,
                playerId,
                variantId: string.Empty,
                materialKey: string.Empty,
                instanceId: string.Empty,
                spatialOwnership: string.Empty,
                domainLabel: string.Empty,
                ownerSlotKey: string.Empty,
                diagnosticSource,
                cardDefId,
                skillId,
                roomId,
                itemDefId,
                contentId,
                diagnosticCardUid,
                overrideSummary: string.Empty,
                failureReason: resolved ? string.Empty : failureReason);
            RecordPerfTrace(
                PerfTraceKinds.VfxLifecycleResolve,
                cueOrStateId,
                diagnosticSource,
                cardDefId,
                skillId,
                roomId,
                itemDefId,
                contentId,
                diagnosticCardUid,
                correlationId,
                phase: "resolve",
                bindingKey,
                playerId,
                instanceId: string.Empty,
                endReason: string.Empty,
                failureReason: resolved ? string.Empty : failureReason);
        }

        public void RecordCreate(
            long correlationId,
            string cueOrStateId,
            bool isPulse,
            string bindingKey,
            string playerId,
            bool succeeded,
            string diagnosticSource,
            string cardDefId,
            string skillId,
            string roomId,
            string itemDefId,
            string contentId,
            int diagnosticCardUid,
            string failureReason)
        {
            AppendRecord(
                correlationId,
                VfxLifecyclePhase.Create,
                VfxEndReason.None,
                isIssue: !succeeded,
                isPulse,
                cueOrStateId,
                bindingKey,
                playerId,
                variantId: string.Empty,
                materialKey: string.Empty,
                instanceId: string.Empty,
                spatialOwnership: string.Empty,
                domainLabel: string.Empty,
                ownerSlotKey: string.Empty,
                diagnosticSource,
                cardDefId,
                skillId,
                roomId,
                itemDefId,
                contentId,
                diagnosticCardUid,
                overrideSummary: string.Empty,
                failureReason);
            RecordPerfTrace(
                PerfTraceKinds.VfxLifecycleCreate,
                cueOrStateId,
                diagnosticSource,
                cardDefId,
                skillId,
                roomId,
                itemDefId,
                contentId,
                diagnosticCardUid,
                correlationId,
                phase: "create",
                bindingKey,
                playerId,
                instanceId: string.Empty,
                endReason: string.Empty,
                failureReason);
        }

        public void RecordStart(long correlationId, in VfxInstanceDiagnosticContext context)
        {
            mFrameCreated++;
            mReleaseCounters.TotalStarted++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            mSession.TotalStarted++;
            ObserveBindingStart(context.BindingKey, context.CueOrStateId, context.PlayerId);
            TrackActiveContributor(context);
#endif
            AppendRecord(
                correlationId,
                VfxLifecyclePhase.Start,
                VfxEndReason.None,
                isIssue: false,
                context.IsPulse,
                context.CueOrStateId,
                context.BindingKey,
                context.PlayerId,
                context.VariantId,
                context.MaterialKey,
                context.InstanceId,
                VfxDiagnosticFormatting.SpatialOwnershipLabel(context.SpatialOwnership),
                context.DomainLabel,
                context.OwnerSlotKey,
                context.DiagnosticSource,
                context.CardDefId,
                context.SkillId,
                context.RoomId,
                context.ItemDefId,
                context.ContentId,
                context.DiagnosticCardUid,
                context.OverrideSummary,
                failureReason: string.Empty);
            RecordPerfTrace(
                PerfTraceKinds.VfxLifecycleStart,
                context.CueOrStateId,
                context.DiagnosticSource,
                context.CardDefId,
                context.SkillId,
                context.RoomId,
                context.ItemDefId,
                context.ContentId,
                context.DiagnosticCardUid,
                correlationId,
                phase: "start",
                context.BindingKey,
                context.PlayerId,
                context.InstanceId,
                endReason: string.Empty,
                failureReason: string.Empty);
        }

        public void RecordComplete(
            long correlationId,
            in VfxInstanceDiagnosticContext context,
            VfxEndReason endReason,
            string failureReason = null)
        {
            var isIssue = VfxEndReasons.IsIssue(endReason);
            if (isIssue)
            {
                IncrementIssueCounter(endReason);
            }

            mFrameCompleted++;
            mReleaseCounters.TotalCompleted++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            mSession.TotalCompleted++;
            if (isIssue)
            {
                mSession.TotalIssues++;
            }

            ObserveBindingComplete(context.BindingKey, context.CueOrStateId, context.PlayerId, isIssue);
            RemoveActiveContributor(context.InstanceId);
#endif
            AppendRecord(
                correlationId,
                VfxLifecyclePhase.Complete,
                endReason,
                isIssue,
                context.IsPulse,
                context.CueOrStateId,
                context.BindingKey,
                context.PlayerId,
                context.VariantId,
                context.MaterialKey,
                context.InstanceId,
                VfxDiagnosticFormatting.SpatialOwnershipLabel(context.SpatialOwnership),
                context.DomainLabel,
                context.OwnerSlotKey,
                context.DiagnosticSource,
                context.CardDefId,
                context.SkillId,
                context.RoomId,
                context.ItemDefId,
                context.ContentId,
                context.DiagnosticCardUid,
                context.OverrideSummary,
                failureReason ?? string.Empty);
            RecordPerfTrace(
                PerfTraceKinds.VfxLifecycleComplete,
                context.CueOrStateId,
                context.DiagnosticSource,
                context.CardDefId,
                context.SkillId,
                context.RoomId,
                context.ItemDefId,
                context.ContentId,
                context.DiagnosticCardUid,
                correlationId,
                phase: "complete",
                context.BindingKey,
                context.PlayerId,
                context.InstanceId,
                endReason: endReason.ToString(),
                failureReason ?? string.Empty);
        }

        public void RecordTerminalFailure(
            long correlationId,
            string cueOrStateId,
            bool isPulse,
            VfxEndReason endReason,
            string bindingKey,
            string playerId,
            string diagnosticSource,
            string cardDefId,
            string skillId,
            string roomId,
            string itemDefId,
            string contentId,
            int diagnosticCardUid,
            string failureReason)
        {
            var context = new VfxInstanceDiagnosticContext(
                instanceId: string.Empty,
                cueOrStateId,
                isPulse,
                bindingKey,
                playerId,
                variantId: string.Empty,
                materialKey: string.Empty,
                VfxSpatialOwnership.Independent,
                domainLabel: string.Empty,
                ownerSlotKey: string.Empty,
                diagnosticSource,
                cardDefId,
                skillId,
                roomId,
                itemDefId,
                contentId,
                diagnosticCardUid,
                overrideSummary: string.Empty);
            RecordComplete(correlationId, context, endReason, failureReason);
        }

        public void OnFrameEnd(int activeCount)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (activeCount > mSession.PeakActive)
            {
                mSession.PeakActive = activeCount;
                CapturePeak(activeCount);
            }

            UpdateAggregatePeaks(activeCount);
            AppendFrame(mFrameCreated, mFrameCompleted, activeCount);
#endif
            mFrameCreated = 0;
            mFrameCompleted = 0;
            mFrameIndex++;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public VfxDiagnosticsSnapshot GetSnapshot()
        {
            var records = new VfxLifecycleRecord[mRecords.Count];
            for (var i = 0; i < mRecords.Count; i++)
            {
                records[i] = CloneRecord(mRecords[i]);
            }

            var frames = new VfxFrameStats[mFrames.Count];
            mFrames.CopyTo(frames);

            var bindingAggregates = new VfxBindingAggregate[mBindingAggregates.Count];
            var bindingIndex = 0;
            foreach (var pair in mBindingAggregates)
            {
                bindingAggregates[bindingIndex++] = pair.Value.ToImmutable();
            }

            var playerAggregates = new VfxPlayerAggregate[mPlayerAggregates.Count];
            var playerIndex = 0;
            foreach (var pair in mPlayerAggregates)
            {
                playerAggregates[playerIndex++] = pair.Value.ToImmutable();
            }

            var active = new VfxActiveInstanceSnapshot[mActiveContributors.Count];
            for (var i = 0; i < mActiveContributors.Count; i++)
            {
                active[i] = mActiveContributors[i].ToSnapshot();
            }

            var peakContributors = mPeak.Contributors ?? Array.Empty<VfxPeakContributor>();

            return new VfxDiagnosticsSnapshot
            {
                RecentRecords = records,
                RecentFrames = frames,
                Session = CloneSession(mSession),
                Peak = new VfxPeakSnapshot
                {
                    ActiveCount = mPeak.ActiveCount,
                    FrameIndex = mPeak.FrameIndex,
                    Time = mPeak.Time,
                    Contributors = peakContributors,
                },
                BindingAggregates = bindingAggregates,
                PlayerAggregates = playerAggregates,
                ActiveInstances = active,
            };
        }

        public void ResetForTests()
        {
            mRecords.Clear();
            mFrames.Clear();
            mBindingAggregates.Clear();
            mPlayerAggregates.Clear();
            mActiveContributors.Clear();
            mPeakContributorBuffer.Clear();
            mPeak.ActiveCount = 0;
            mPeak.FrameIndex = 0;
            mPeak.Time = 0d;
            mPeak.Contributors = Array.Empty<VfxPeakContributor>();
            mSession.TotalRequests = 0;
            mSession.TotalStarted = 0;
            mSession.TotalCompleted = 0;
            mSession.TotalIssues = 0;
            mSession.PeakActive = 0;
            mNextRecordSequence = 0;
            mFrameIndex = 0;
            mFrameCreated = 0;
            mFrameCompleted = 0;
            mNextCorrelationId = 0;
            mReleaseCounters.TotalRequests = 0;
            mReleaseCounters.TotalStarted = 0;
            mReleaseCounters.TotalCompleted = 0;
            mReleaseCounters.TotalIssues = 0;
            mReleaseCounters.Unbound = 0;
            mReleaseCounters.InvalidBinding = 0;
            mReleaseCounters.PlayerUnavailable = 0;
            mReleaseCounters.DomainUnavailable = 0;
            mReleaseCounters.BackendFailure = 0;
        }
#endif

        private void IncrementIssueCounter(VfxEndReason endReason)
        {
            mReleaseCounters.TotalIssues++;
            switch (endReason)
            {
                case VfxEndReason.Unbound:
                    mReleaseCounters.Unbound++;
                    break;
                case VfxEndReason.InvalidBinding:
                    mReleaseCounters.InvalidBinding++;
                    break;
                case VfxEndReason.PlayerUnavailable:
                    mReleaseCounters.PlayerUnavailable++;
                    break;
                case VfxEndReason.DomainUnavailable:
                    mReleaseCounters.DomainUnavailable++;
                    break;
                case VfxEndReason.BackendFailure:
                    mReleaseCounters.BackendFailure++;
                    break;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void AppendRecord(
            long correlationId,
            VfxLifecyclePhase phase,
            VfxEndReason endReason,
            bool isIssue,
            bool isPulse,
            string cueOrStateId,
            string bindingKey,
            string playerId,
            string variantId,
            string materialKey,
            string instanceId,
            string spatialOwnership,
            string domainLabel,
            string ownerSlotKey,
            string diagnosticSource,
            string cardDefId,
            string skillId,
            string roomId,
            string itemDefId,
            string contentId,
            int diagnosticCardUid,
            string overrideSummary,
            string failureReason)
        {
            if (mRecords.Count >= mRecordCapacity)
            {
                mRecords.RemoveAt(0);
            }

            mRecords.Add(new VfxLifecycleRecord
            {
                Sequence = ++mNextRecordSequence,
                CorrelationId = correlationId,
                Phase = phase,
                EndReason = endReason,
                IsIssue = isIssue,
                IsPulse = isPulse,
                CueOrStateId = cueOrStateId ?? string.Empty,
                BindingKey = bindingKey ?? string.Empty,
                PlayerId = playerId ?? string.Empty,
                VariantId = variantId ?? string.Empty,
                MaterialKey = materialKey ?? string.Empty,
                InstanceId = instanceId ?? string.Empty,
                SpatialOwnership = spatialOwnership ?? string.Empty,
                DomainLabel = domainLabel ?? string.Empty,
                OwnerSlotKey = ownerSlotKey ?? string.Empty,
                DiagnosticSource = diagnosticSource ?? string.Empty,
                CardDefId = cardDefId ?? string.Empty,
                SkillId = skillId ?? string.Empty,
                RoomId = roomId ?? string.Empty,
                ItemDefId = itemDefId ?? string.Empty,
                ContentId = contentId ?? string.Empty,
                DiagnosticCardUid = diagnosticCardUid,
                OverrideSummary = overrideSummary ?? string.Empty,
                FailureReason = failureReason ?? string.Empty,
                Time = mNow,
            });
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendRecord(
            long correlationId,
            VfxLifecyclePhase phase,
            VfxEndReason endReason,
            bool isIssue,
            bool isPulse,
            string cueOrStateId,
            string bindingKey,
            string playerId,
            string variantId,
            string materialKey,
            string instanceId,
            string spatialOwnership,
            string domainLabel,
            string ownerSlotKey,
            string diagnosticSource,
            string cardDefId,
            string skillId,
            string roomId,
            string itemDefId,
            string contentId,
            int diagnosticCardUid,
            string overrideSummary,
            string failureReason)
        {
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void AppendFrame(int created, int completed, int active)
        {
            if (mFrames.Count >= mFrameCapacity)
            {
                mFrames.RemoveAt(0);
            }

            mFrames.Add(new VfxFrameStats
            {
                FrameIndex = mFrameIndex,
                Created = created,
                Completed = completed,
                Active = active,
            });
        }

        private void ObserveBindingStart(string bindingKey, string cueOrStateId, string playerId)
        {
            if (!string.IsNullOrEmpty(bindingKey))
            {
                if (!mBindingAggregates.TryGetValue(bindingKey, out var bindingState))
                {
                    bindingState = new BindingAggregateState(bindingKey, cueOrStateId);
                    mBindingAggregates[bindingKey] = bindingState;
                }

                bindingState.Started++;
                if (!string.IsNullOrEmpty(cueOrStateId))
                {
                    bindingState.CueOrStateId = cueOrStateId;
                }
            }

            if (!string.IsNullOrEmpty(playerId))
            {
                if (!mPlayerAggregates.TryGetValue(playerId, out var playerState))
                {
                    playerState = new PlayerAggregateState(playerId);
                    mPlayerAggregates[playerId] = playerState;
                }

                playerState.Started++;
            }
        }

        private void ObserveBindingComplete(
            string bindingKey,
            string cueOrStateId,
            string playerId,
            bool isIssue)
        {
            if (!string.IsNullOrEmpty(bindingKey)
                && mBindingAggregates.TryGetValue(bindingKey, out var bindingState))
            {
                bindingState.Completed++;
                if (isIssue)
                {
                    bindingState.Issues++;
                }
            }

            if (!string.IsNullOrEmpty(playerId)
                && mPlayerAggregates.TryGetValue(playerId, out var playerState))
            {
                playerState.Completed++;
                if (isIssue)
                {
                    playerState.Issues++;
                }
            }
        }

        private void UpdateAggregatePeaks(int activeCount)
        {
            foreach (var pair in mBindingAggregates)
            {
                var peakForBinding = 0;
                for (var i = 0; i < mActiveContributors.Count; i++)
                {
                    if (string.Equals(
                            mActiveContributors[i].BindingKey,
                            pair.Key,
                            StringComparison.Ordinal))
                    {
                        peakForBinding++;
                    }
                }

                if (peakForBinding > pair.Value.PeakActive)
                {
                    pair.Value.PeakActive = peakForBinding;
                }
            }

            foreach (var pair in mPlayerAggregates)
            {
                var peakForPlayer = 0;
                for (var i = 0; i < mActiveContributors.Count; i++)
                {
                    if (string.Equals(
                            mActiveContributors[i].PlayerId,
                            pair.Key,
                            StringComparison.Ordinal))
                    {
                        peakForPlayer++;
                    }
                }

                if (peakForPlayer > pair.Value.PeakActive)
                {
                    pair.Value.PeakActive = peakForPlayer;
                }
            }
        }

        private void TrackActiveContributor(in VfxInstanceDiagnosticContext context)
        {
            mActiveContributors.Add(new ActiveContributor(context));
            if (mActiveContributors.Count > PeakContributorCapacity)
            {
                mActiveContributors.RemoveAt(0);
            }
        }

        private void RemoveActiveContributor(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return;
            }

            for (var i = mActiveContributors.Count - 1; i >= 0; i--)
            {
                if (string.Equals(mActiveContributors[i].InstanceId, instanceId, StringComparison.Ordinal))
                {
                    mActiveContributors.RemoveAt(i);
                    return;
                }
            }
        }

        private void CapturePeak(int activeCount)
        {
            mPeak.ActiveCount = activeCount;
            mPeak.FrameIndex = mFrameIndex;
            mPeak.Time = mNow;
            mPeakContributorBuffer.Clear();
            var limit = Math.Min(activeCount, PeakContributorCapacity);
            for (var i = 0; i < limit && i < mActiveContributors.Count; i++)
            {
                mPeakContributorBuffer.Add(mActiveContributors[i].ToPeakContributor());
            }

            mPeak.Contributors = mPeakContributorBuffer.ToArray();
        }

        private static VfxLifecycleRecord CloneRecord(VfxLifecycleRecord source)
        {
            return new VfxLifecycleRecord
            {
                Sequence = source.Sequence,
                CorrelationId = source.CorrelationId,
                Phase = source.Phase,
                EndReason = source.EndReason,
                IsIssue = source.IsIssue,
                IsPulse = source.IsPulse,
                CueOrStateId = source.CueOrStateId,
                BindingKey = source.BindingKey,
                PlayerId = source.PlayerId,
                VariantId = source.VariantId,
                MaterialKey = source.MaterialKey,
                InstanceId = source.InstanceId,
                SpatialOwnership = source.SpatialOwnership,
                DomainLabel = source.DomainLabel,
                OwnerSlotKey = source.OwnerSlotKey,
                DiagnosticSource = source.DiagnosticSource,
                CardDefId = source.CardDefId,
                SkillId = source.SkillId,
                RoomId = source.RoomId,
                ItemDefId = source.ItemDefId,
                ContentId = source.ContentId,
                DiagnosticCardUid = source.DiagnosticCardUid,
                OverrideSummary = source.OverrideSummary,
                FailureReason = source.FailureReason,
                Time = source.Time,
            };
        }

        private static VfxSessionCounters CloneSession(VfxSessionCounters source)
        {
            return new VfxSessionCounters
            {
                TotalRequests = source.TotalRequests,
                TotalStarted = source.TotalStarted,
                TotalCompleted = source.TotalCompleted,
                TotalIssues = source.TotalIssues,
                PeakActive = source.PeakActive,
            };
        }

        private sealed class BindingAggregateState
        {
            public BindingAggregateState(string bindingKey, string cueOrStateId)
            {
                BindingKey = bindingKey ?? string.Empty;
                CueOrStateId = cueOrStateId ?? string.Empty;
            }

            public string BindingKey { get; }
            public string CueOrStateId { get; set; }
            public int Started { get; set; }
            public int Completed { get; set; }
            public int Issues { get; set; }
            public int PeakActive { get; set; }

            public VfxBindingAggregate ToImmutable()
            {
                return new VfxBindingAggregate
                {
                    BindingKey = BindingKey,
                    CueOrStateId = CueOrStateId,
                    Started = Started,
                    Completed = Completed,
                    Issues = Issues,
                    PeakActive = PeakActive,
                };
            }
        }

        private sealed class PlayerAggregateState
        {
            public PlayerAggregateState(string playerId)
            {
                PlayerId = playerId ?? string.Empty;
            }

            public string PlayerId { get; }
            public int Started { get; set; }
            public int Completed { get; set; }
            public int Issues { get; set; }
            public int PeakActive { get; set; }

            public VfxPlayerAggregate ToImmutable()
            {
                return new VfxPlayerAggregate
                {
                    PlayerId = PlayerId,
                    Started = Started,
                    Completed = Completed,
                    Issues = Issues,
                    PeakActive = PeakActive,
                };
            }
        }

        private readonly struct ActiveContributor
        {
            public ActiveContributor(in VfxInstanceDiagnosticContext context)
            {
                InstanceId = context.InstanceId;
                BindingKey = context.BindingKey;
                PlayerId = context.PlayerId;
                CueOrStateId = context.CueOrStateId;
                IsPulse = context.IsPulse;
                SpatialOwnership = VfxDiagnosticFormatting.SpatialOwnershipLabel(context.SpatialOwnership);
                OwnerSlotKey = context.OwnerSlotKey;
            }

            public string InstanceId { get; }
            public string BindingKey { get; }
            public string PlayerId { get; }
            public string CueOrStateId { get; }
            public bool IsPulse { get; }
            public string SpatialOwnership { get; }
            public string OwnerSlotKey { get; }

            public VfxPeakContributor ToPeakContributor()
            {
                return new VfxPeakContributor
                {
                    BindingKey = BindingKey,
                    PlayerId = PlayerId,
                    InstanceId = InstanceId,
                    CueOrStateId = CueOrStateId,
                    IsPulse = IsPulse,
                };
            }

            public VfxActiveInstanceSnapshot ToSnapshot()
            {
                return new VfxActiveInstanceSnapshot
                {
                    InstanceId = InstanceId,
                    BindingKey = BindingKey,
                    PlayerId = PlayerId,
                    CueOrStateId = CueOrStateId,
                    IsPulse = IsPulse,
                    SpatialOwnership = SpatialOwnership,
                    OwnerSlotKey = OwnerSlotKey,
                };
            }
        }
#endif

        private static void RecordPerfTrace(
            string kind,
            string cueOrStateId,
            string diagnosticSource,
            string cardDefId,
            string skillId,
            string roomId,
            string itemDefId,
            string contentId,
            int diagnosticCardUid,
            long correlationId,
            string phase,
            string bindingKey,
            string playerId,
            string instanceId,
            string endReason,
            string failureReason)
        {
            try
            {
                var payload = new Dictionary<string, string>
                {
                    ["phase"] = phase ?? string.Empty,
                    ["cueOrStateId"] = cueOrStateId ?? string.Empty,
                    ["diagnosticSource"] = diagnosticSource ?? string.Empty,
                    ["correlationId"] = correlationId.ToString(CultureInfo.InvariantCulture),
                };

                if (!string.IsNullOrEmpty(bindingKey))
                {
                    payload["bindingKey"] = bindingKey;
                }

                if (!string.IsNullOrEmpty(playerId))
                {
                    payload["playerId"] = playerId;
                }

                if (!string.IsNullOrEmpty(instanceId))
                {
                    payload["instanceId"] = instanceId;
                }

                if (!string.IsNullOrEmpty(endReason))
                {
                    payload["endReason"] = endReason;
                }

                if (!string.IsNullOrEmpty(failureReason))
                {
                    payload["reason"] = failureReason;
                }

                if (!string.IsNullOrEmpty(cardDefId))
                {
                    payload["cardDefId"] = cardDefId;
                }

                if (!string.IsNullOrEmpty(skillId))
                {
                    payload["skillId"] = skillId;
                }

                if (!string.IsNullOrEmpty(roomId))
                {
                    payload["roomId"] = roomId;
                }

                if (!string.IsNullOrEmpty(itemDefId))
                {
                    payload["itemDefId"] = itemDefId;
                }

                if (!string.IsNullOrEmpty(contentId))
                {
                    payload["contentId"] = contentId;
                }

                DirectorTrace.AppendBusyFields(payload);
                payload["batchId"] = DirectorTrace.ActiveBatchId.ToString(CultureInfo.InvariantCulture);
                payload["chainId"] = DirectorTrace.CurrentChainId.ToString(CultureInfo.InvariantCulture);
                payload["sessionId"] = DiagTraceShared.CurrentSessionId;
                payload["runTag"] = DiagTraceShared.RunTag;
                if (diagnosticCardUid > 0)
                {
                    payload["diagnosticCardUid"] = diagnosticCardUid.ToString(CultureInfo.InvariantCulture);
                }

                PerfTraceRecorder.Record(
                    kind,
                    uid: diagnosticCardUid > 0 ? diagnosticCardUid : -1,
                    PerfTraceSites.VfxSystemLifecycle,
                    payload);
            }
            catch (Exception)
            {
                // 诊断打点失败不干扰玩法路径。
            }
        }
    }
}
