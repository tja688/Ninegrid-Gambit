using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using NineGrid.Content.Vfx;
using NineGrid.Core;
using NineGrid.Presentation.Systems.Vfx;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Systems
{
    public enum VfxCueOutcome
    {
        Played,
        Unbound,
        Suppressed,
        InvalidBinding,
        PlayerUnavailable,
        DomainUnavailable,
        BackendFailure,
    }

    public enum VfxHistoryOutcome
    {
        Requested,
        Scheduled,
        Cancelled,
        Played,
        Unbound,
        Suppressed,
        InvalidBinding,
        PlayerUnavailable,
        DomainUnavailable,
        BackendFailure,
    }

    public readonly struct VfxScheduleKey
    {
        public VfxScheduleKey(long value)
        {
            Value = value;
        }

        public long Value { get; }
        public bool IsValid => Value > 0;
    }

    public interface IVfxCueScheduler
    {
        VfxScheduleKey Schedule(float delaySeconds, Action callback);
        bool Cancel(VfxScheduleKey key);
    }

    public sealed class VfxCueResult
    {
        public VfxCueOutcome Outcome { get; internal set; }
        public string CueId { get; internal set; }
        public string CueNote { get; internal set; }
        public string BindingKey { get; internal set; }
        public string PlayerId { get; internal set; }
        public string MaterialKey { get; internal set; }
        public string VariantId { get; internal set; }
        public string InstanceId { get; internal set; }
        public string FailureReason { get; internal set; }

        /// <summary>程序化播放器一次性给出的批次表现计划（如首达/末达窗口）；非金牌播放器为空。</summary>
        public VfxPresentationPlan PresentationPlan { get; internal set; }

        public bool HasPresentationPlan => PresentationPlan.IsValid;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public sealed class VfxHistoryRecord
    {
        public long Sequence { get; internal set; }
        public VfxHistoryOutcome Outcome { get; internal set; }
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
        public string PlayerId { get; internal set; }
        public string MaterialKey { get; internal set; }
        public string VariantId { get; internal set; }
        public string InstanceId { get; internal set; }
        public string FailureReason { get; internal set; }
        public long ScheduleKey { get; internal set; }
        public float ScheduleDelaySeconds { get; internal set; }
        public double Time { get; internal set; }
    }
#endif

    public interface IVfxSystem : ISystem
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        IReadOnlyList<VfxHistoryRecord> History { get; }
        VfxDiagnosticsSnapshot GetDiagnosticsSnapshot();
        VfxWorkbenchSnapshot GetWorkbenchSnapshot();
        VfxWorkbenchApplyResult ApplyWorkbenchCatalog(string catalogJson);
        VfxWorkbenchPreviewResult PreviewWorkbenchBinding(string bindingKey, bool includeBindingDelay, bool isStateBinding);
        VfxStateSlotResult ClearWorkbenchStateSlot(string ownerLabel, string slot);
#endif
        VfxReleaseCounters ReleaseCounters { get; }
        VfxCueResult RequestCue(VfxCueRequest request, VfxSpatialContext spatialContext = default);
        VfxScheduleKey ScheduleCue(VfxCueRequest request, float delaySeconds, VfxSpatialContext spatialContext = default);
        bool CancelScheduledCue(VfxScheduleKey key);
        VfxStateSlotResult SetSlot(
            IVfxSlotOwner owner,
            string slot,
            string desiredState,
            VfxSpatialContext spatialContext = default);
        VfxStateSlotResult ClearSlotIf(IVfxSlotOwner owner, string slot, string expectedState);
        void ReleaseOwner(IVfxSlotOwner owner);
        void Tick(float deltaTime);
        void ClearSceneInstances();
    }

    public sealed class VfxSystem : AbstractSystem, IVfxSystem
    {
        public const int DefaultHistoryCapacity = 256;
        private const string SuppressedReason = "binding disabled";

        private VfxBindingCatalog mCatalog;
        private readonly IVfxPlayerFactory mPlayerFactory;
        private readonly IAudioClock mClock;
        private readonly IVfxCueScheduler mScheduler;
        private readonly IVfxDomainHostResolver mDomainHostResolver;
        private readonly Func<double> mRandomValue;
        private readonly VfxDiagnosticsTracker mDiagnostics;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly int mHistoryCapacity;
        private readonly List<VfxHistoryRecord> mHistory;
        private long mNextHistorySequence;
#endif
        private readonly Dictionary<VfxCueBinding, double> mLastPlayedAt =
            new Dictionary<VfxCueBinding, double>();
        private readonly Dictionary<VfxCueBinding, string> mLastVariantIds =
            new Dictionary<VfxCueBinding, string>();
        private readonly Dictionary<long, PendingScheduledCue> mPendingSchedules =
            new Dictionary<long, PendingScheduledCue>();
        private readonly List<ActivePulseInstance> mActiveInstances = new List<ActivePulseInstance>();
        private readonly Dictionary<VfxSlotKey, ActiveStateSlot> mActiveStateSlots =
            new Dictionary<VfxSlotKey, ActiveStateSlot>();
        private readonly List<ActiveStateSlot> mExitingStateSlots = new List<ActiveStateSlot>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const int AggregateTimestampCapacity = 32;
        private long mWorkbenchRevision;
        private readonly Dictionary<string, WorkbenchAggregateState> mAggregates =
            new Dictionary<string, WorkbenchAggregateState>(StringComparer.Ordinal);
        private readonly Dictionary<string, VfxWorkbenchPreviewOwner> mPreviewOwnersByLabel =
            new Dictionary<string, VfxWorkbenchPreviewOwner>(StringComparer.Ordinal);
#endif

        public VfxSystem(
            VfxBindingCatalog catalog,
            IVfxPlayerFactory playerFactory,
            IAudioClock clock,
            int historyCapacity = DefaultHistoryCapacity,
            IVfxCueScheduler scheduler = null,
            IVfxDomainHostResolver domainHostResolver = null,
            Func<double> randomValue = null)
        {
            mCatalog = catalog ?? VfxBindingCatalog.FromJson(string.Empty);
            mPlayerFactory = playerFactory ?? new NullVfxPlayerFactory();
            mClock = clock ?? new RealtimeAudioClock();
            mScheduler = scheduler;
            mDomainHostResolver = domainHostResolver;
            mRandomValue = randomValue ?? (() => UnityEngine.Random.value);
            mDiagnostics = new VfxDiagnosticsTracker(historyCapacity);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            mHistoryCapacity = Math.Max(1, historyCapacity);
            mHistory = new List<VfxHistoryRecord>(mHistoryCapacity);
#endif
        }

        public static IVfxSystem EnsureRegistered(
            IArchitecture architecture = null,
            VfxBindingCatalog catalog = null,
            IVfxPlayerFactory playerFactory = null,
            IAudioClock clock = null)
        {
            var arch = architecture ?? NineGridArchitecture.Interface;
            if (arch == null)
            {
                throw new InvalidOperationException("Architecture is not available for VfxSystem.");
            }

            var existing = arch.GetSystem<IVfxSystem>();
            if (existing != null)
            {
                return existing;
            }

            var created = new VfxSystem(
                catalog ?? VfxBindingCatalog.LoadFromResources(),
                playerFactory ?? new DefaultVfxPlayerFactory(),
                clock ?? new RealtimeAudioClock());
            arch.RegisterSystem<IVfxSystem>(created);
            return created;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public IReadOnlyList<VfxHistoryRecord> History => mHistory;

        public VfxDiagnosticsSnapshot GetDiagnosticsSnapshot()
        {
            return mDiagnostics.GetSnapshot();
        }

        public VfxWorkbenchSnapshot GetWorkbenchSnapshot()
        {
            var historyCopy = new VfxHistoryRecord[mHistory.Count];
            for (var i = 0; i < mHistory.Count; i++)
            {
                historyCopy[i] = CloneHistoryRecord(mHistory[i]);
            }

            var activePulses = new List<VfxWorkbenchActivePulse>(mActiveInstances.Count);
            for (var i = 0; i < mActiveInstances.Count; i++)
            {
                var pulse = mActiveInstances[i];
                var context = pulse.DiagnosticContext;
                activePulses.Add(new VfxWorkbenchActivePulse
                {
                    InstanceId = pulse.InstanceId,
                    BindingKey = context.BindingKey,
                    CueId = context.CueOrStateId,
                    PlayerId = context.PlayerId,
                    MaterialKey = context.MaterialKey,
                    SpatialOwnership = VfxDiagnosticFormatting.SpatialOwnershipLabel(context.SpatialOwnership),
                });
            }

            var activeStates = new List<VfxWorkbenchActiveStateSlot>(mActiveStateSlots.Count + mExitingStateSlots.Count);
            foreach (var pair in mActiveStateSlots)
            {
                activeStates.Add(ProjectActiveStateSlot(pair.Value));
            }

            for (var i = 0; i < mExitingStateSlots.Count; i++)
            {
                activeStates.Add(ProjectActiveStateSlot(mExitingStateSlots[i]));
            }

            var aggregates = new VfxCueAggregate[mAggregates.Count];
            var aggregateIndex = 0;
            foreach (var pair in mAggregates)
            {
                aggregates[aggregateIndex++] = pair.Value.ToImmutable();
            }

            return new VfxWorkbenchSnapshot
            {
                Revision = mWorkbenchRevision,
                History = historyCopy,
                ActivePulses = activePulses,
                ActiveStateSlots = activeStates,
                Aggregates = aggregates,
                Diagnostics = mDiagnostics.GetSnapshot(),
            };
        }

        public VfxWorkbenchApplyResult ApplyWorkbenchCatalog(string catalogJson)
        {
            if (!VfxBindingCatalog.TryFromJson(catalogJson, out var catalog, out var error))
            {
                return new VfxWorkbenchApplyResult
                {
                    Succeeded = false,
                    Revision = mWorkbenchRevision,
                    Error = error ?? "catalog apply failed.",
                };
            }

            mCatalog = catalog;
            mLastPlayedAt.Clear();
            mLastVariantIds.Clear();
            mWorkbenchRevision++;
            RebuildActiveStateSlotsAfterCatalogApply();
            return new VfxWorkbenchApplyResult
            {
                Succeeded = true,
                Revision = mWorkbenchRevision,
                Error = string.Empty,
            };
        }

        public VfxWorkbenchPreviewResult PreviewWorkbenchBinding(
            string bindingKey,
            bool includeBindingDelay,
            bool isStateBinding)
        {
            if (string.IsNullOrWhiteSpace(bindingKey))
            {
                return PreviewFailure("binding key is empty.", isStateBinding ? "state" : "cue");
            }

            if (isStateBinding)
            {
                return PreviewStateBinding(bindingKey);
            }

            return PreviewCueBinding(bindingKey, includeBindingDelay);
        }

        public VfxStateSlotResult ClearWorkbenchStateSlot(string ownerLabel, string slot)
        {
            if (string.IsNullOrWhiteSpace(ownerLabel) || string.IsNullOrWhiteSpace(slot))
            {
                return new VfxStateSlotResult
                {
                    Outcome = VfxStateSlotOutcome.Cleared,
                    FailureReason = "owner or slot is empty.",
                };
            }

            if (!mPreviewOwnersByLabel.TryGetValue(ownerLabel, out var owner))
            {
                foreach (var pair in mActiveStateSlots)
                {
                    if (string.Equals(pair.Key.Slot, slot, StringComparison.Ordinal)
                        && string.Equals(BuildOwnerLabel(pair.Key.Owner), ownerLabel, StringComparison.Ordinal))
                    {
                        return ClearSlotInternal(pair.Key, expectedState: null, conditional: false, VfxEndReason.SlotCleared);
                    }
                }

                return new VfxStateSlotResult
                {
                    Outcome = VfxStateSlotOutcome.Cleared,
                    FailureReason = "active owner not found.",
                };
            }

            return SetSlot(owner, slot, null);
        }

        private VfxWorkbenchPreviewResult PreviewCueBinding(string bindingKey, bool includeBindingDelay)
        {
            if (!TryFindCueBindingByKey(bindingKey, out var binding) || binding == null)
            {
                return PreviewFailure("cue binding not found.", "cue", bindingKey);
            }

            var request = new VfxCueRequest(
                binding.CueId,
                "VfxWorkbench.Preview",
                binding.SelectorCardDefId,
                binding.SelectorSkillId,
                binding.SelectorRoomId,
                binding.SelectorItemDefId,
                binding.SelectorContentId);
            var delay = includeBindingDelay ? Math.Max(0f, binding.BindingDelaySeconds) : 0f;
            VfxCueResult result;
            if (delay > 0f)
            {
                var key = ScheduleCue(request, delay);
                if (!key.IsValid)
                {
                    return PreviewFailure("failed to schedule preview.", "cue", bindingKey);
                }

                return new VfxWorkbenchPreviewResult
                {
                    Succeeded = true,
                    Channel = "cue",
                    BindingKey = bindingKey,
                    PlayerId = binding.PlayerId,
                    MaterialKey = binding.MaterialKey,
                    VariantId = string.Empty,
                    InstanceId = string.Empty,
                    FailureReason = string.Empty,
                };
            }

            result = RequestCueCore(request, default, preview: true);
            if (result.Outcome != VfxCueOutcome.Played)
            {
                return PreviewFailure(result.FailureReason ?? result.Outcome.ToString(), "cue", bindingKey);
            }

            return new VfxWorkbenchPreviewResult
            {
                Succeeded = true,
                Channel = "cue",
                BindingKey = bindingKey,
                PlayerId = result.PlayerId,
                MaterialKey = result.MaterialKey,
                VariantId = result.VariantId,
                InstanceId = result.InstanceId,
                FailureReason = string.Empty,
            };
        }

        private VfxWorkbenchPreviewResult PreviewStateBinding(string bindingKey)
        {
            if (!TryFindStateBindingByKey(bindingKey, out var binding) || binding == null)
            {
                return PreviewFailure("state binding not found.", "state", bindingKey);
            }

            var owner = GetOrCreatePreviewOwner(bindingKey);
            var result = SetSlot(owner, "preview", binding.StateId);
            if (result.Outcome != VfxStateSlotOutcome.Applied
                && result.Outcome != VfxStateSlotOutcome.NoOp)
            {
                return PreviewFailure(result.FailureReason ?? result.Outcome.ToString(), "state", bindingKey);
            }

            return new VfxWorkbenchPreviewResult
            {
                Succeeded = true,
                Channel = "state",
                BindingKey = bindingKey,
                PlayerId = result.PlayerId,
                MaterialKey = binding.MaterialKey,
                InstanceId = result.InstanceId,
                FailureReason = string.Empty,
            };
        }

        private VfxWorkbenchPreviewOwner GetOrCreatePreviewOwner(string ownerLabel)
        {
            if (!mPreviewOwnersByLabel.TryGetValue(ownerLabel, out var owner))
            {
                owner = new VfxWorkbenchPreviewOwner(ownerLabel);
                mPreviewOwnersByLabel[ownerLabel] = owner;
            }

            return owner;
        }

        private static VfxWorkbenchActiveStateSlot ProjectActiveStateSlot(ActiveStateSlot slot)
        {
            return new VfxWorkbenchActiveStateSlot
            {
                OwnerLabel = BuildOwnerLabel(slot.Key.Owner),
                Slot = slot.Key.Slot,
                StateId = slot.DesiredStateId,
                BindingKey = slot.BindingKey,
                PlayerId = slot.PlayerId,
                InstanceId = slot.InstanceId,
                SpatialOwnership = VfxDiagnosticFormatting.SpatialOwnershipLabel(slot.SpatialOwnership),
            };
        }

        private static string BuildOwnerLabel(IVfxSlotOwner owner)
        {
            if (owner is VfxWorkbenchPreviewOwner preview)
            {
                return preview.Label;
            }

            return owner?.GetType().Name ?? "owner";
        }

        private static VfxWorkbenchPreviewResult PreviewFailure(
            string reason,
            string channel,
            string bindingKey = null)
        {
            return new VfxWorkbenchPreviewResult
            {
                Succeeded = false,
                Channel = channel ?? string.Empty,
                BindingKey = bindingKey ?? string.Empty,
                PlayerId = string.Empty,
                MaterialKey = string.Empty,
                VariantId = string.Empty,
                InstanceId = string.Empty,
                FailureReason = reason ?? string.Empty,
            };
        }

        private bool TryFindCueBindingByKey(string bindingKey, out VfxCueBinding binding)
        {
            binding = null;
            var bindings = mCatalog.CueBindings;
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

        private bool TryFindStateBindingByKey(string bindingKey, out VfxStateBinding binding)
        {
            binding = null;
            var bindings = mCatalog.StateBindings;
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

        private void RebuildActiveStateSlotsAfterCatalogApply()
        {
            if (mActiveStateSlots.Count == 0)
            {
                return;
            }

            var snapshots = new List<(IVfxSlotOwner Owner, string Slot, string DesiredState, VfxSpatialContext Spatial)>();
            foreach (var pair in mActiveStateSlots)
            {
                var slot = pair.Value;
                snapshots.Add((pair.Key.Owner, pair.Key.Slot, slot.DesiredStateId, slot.SpatialContext));
            }

            for (var i = 0; i < snapshots.Count; i++)
            {
                var key = new VfxSlotKey(snapshots[i].Owner, snapshots[i].Slot);
                ClearSlotInternal(key, expectedState: null, conditional: false, VfxEndReason.SlotReplaced);
            }

            for (var i = 0; i < snapshots.Count; i++)
            {
                SetSlot(
                    snapshots[i].Owner,
                    snapshots[i].Slot,
                    snapshots[i].DesiredState,
                    snapshots[i].Spatial);
            }
        }

        private static VfxHistoryRecord CloneHistoryRecord(VfxHistoryRecord source)
        {
            if (source == null)
            {
                return null;
            }

            return new VfxHistoryRecord
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
                PlayerId = source.PlayerId,
                MaterialKey = source.MaterialKey,
                VariantId = source.VariantId,
                InstanceId = source.InstanceId,
                FailureReason = source.FailureReason,
                ScheduleKey = source.ScheduleKey,
                ScheduleDelaySeconds = source.ScheduleDelaySeconds,
                Time = source.Time,
            };
        }

        private void UpdateAggregate(VfxHistoryRecord record)
        {
            switch (record.Outcome)
            {
                case VfxHistoryOutcome.Requested:
                case VfxHistoryOutcome.Played:
                case VfxHistoryOutcome.Suppressed:
                case VfxHistoryOutcome.Unbound:
                case VfxHistoryOutcome.InvalidBinding:
                case VfxHistoryOutcome.PlayerUnavailable:
                case VfxHistoryOutcome.DomainUnavailable:
                case VfxHistoryOutcome.BackendFailure:
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
                state = new WorkbenchAggregateState(aggregateKey, bindingKey, cueId, isPulse: true);
                mAggregates[aggregateKey] = state;
            }

            state.Observe(record);
        }

        private sealed class WorkbenchAggregateState
        {
            private readonly Queue<double> mTimestamps = new Queue<double>(AggregateTimestampCapacity);
            private readonly List<double> mTimestampBuffer = new List<double>(AggregateTimestampCapacity);

            public WorkbenchAggregateState(string aggregateKey, string bindingKey, string cueOrStateId, bool isPulse)
            {
                AggregateKey = aggregateKey ?? string.Empty;
                BindingKey = bindingKey ?? string.Empty;
                CueOrStateId = cueOrStateId ?? string.Empty;
                IsPulse = isPulse;
            }

            public string AggregateKey { get; }
            public string BindingKey { get; private set; }
            public string CueOrStateId { get; private set; }
            public bool IsPulse { get; }
            public int Requested { get; private set; }
            public int Played { get; private set; }
            public int Suppressed { get; private set; }
            public int Unbound { get; private set; }
            public int InvalidBinding { get; private set; }
            public int PlayerUnavailable { get; private set; }
            public int DomainUnavailable { get; private set; }
            public int BackendFailure { get; private set; }
            public double LastTime { get; private set; }
            public string LastFailureReason { get; private set; }

            public void Observe(VfxHistoryRecord record)
            {
                if (!string.IsNullOrEmpty(record.BindingKey))
                {
                    BindingKey = record.BindingKey;
                }

                if (!string.IsNullOrEmpty(record.CueId))
                {
                    CueOrStateId = record.CueId;
                }

                LastTime = record.Time;
                switch (record.Outcome)
                {
                    case VfxHistoryOutcome.Requested:
                        Requested++;
                        break;
                    case VfxHistoryOutcome.Played:
                        Played++;
                        break;
                    case VfxHistoryOutcome.Suppressed:
                        Suppressed++;
                        break;
                    case VfxHistoryOutcome.Unbound:
                        Unbound++;
                        LastFailureReason = record.FailureReason;
                        break;
                    case VfxHistoryOutcome.InvalidBinding:
                        InvalidBinding++;
                        LastFailureReason = record.FailureReason;
                        break;
                    case VfxHistoryOutcome.PlayerUnavailable:
                        PlayerUnavailable++;
                        LastFailureReason = record.FailureReason;
                        break;
                    case VfxHistoryOutcome.DomainUnavailable:
                        DomainUnavailable++;
                        LastFailureReason = record.FailureReason;
                        break;
                    case VfxHistoryOutcome.BackendFailure:
                        BackendFailure++;
                        LastFailureReason = record.FailureReason;
                        break;
                }

                mTimestamps.Enqueue(record.Time);
                while (mTimestamps.Count > AggregateTimestampCapacity)
                {
                    mTimestamps.Dequeue();
                }
            }

            public VfxCueAggregate ToImmutable()
            {
                mTimestampBuffer.Clear();
                foreach (var timestamp in mTimestamps)
                {
                    mTimestampBuffer.Add(timestamp);
                }

                return new VfxCueAggregate
                {
                    AggregateKey = AggregateKey,
                    BindingKey = BindingKey,
                    CueOrStateId = CueOrStateId,
                    IsPulse = IsPulse,
                    Requested = Requested,
                    Played = Played,
                    Suppressed = Suppressed,
                    Unbound = Unbound,
                    InvalidBinding = InvalidBinding,
                    PlayerUnavailable = PlayerUnavailable,
                    DomainUnavailable = DomainUnavailable,
                    BackendFailure = BackendFailure,
                    LastTime = LastTime,
                    LastFailureReason = LastFailureReason ?? string.Empty,
                    RecentTimestamps = mTimestampBuffer.ToArray(),
                };
            }
        }

        private sealed class VfxWorkbenchPreviewOwner : IVfxSlotOwner
        {
            public VfxWorkbenchPreviewOwner(string label)
            {
                Label = label ?? string.Empty;
            }

            public string Label { get; }

            public VfxStateRequest BuildStateRequest(string stateId)
            {
                return new VfxStateRequest(stateId, "VfxWorkbench.Preview", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            }
        }
#endif

        public VfxReleaseCounters ReleaseCounters => mDiagnostics.ReleaseCounters;

        public VfxScheduleKey ScheduleCue(
            VfxCueRequest request,
            float delaySeconds,
            VfxSpatialContext spatialContext = default)
        {
            var scheduler = mScheduler ?? UnityVfxCueScheduler.Instance;
            var clampedDelay = Math.Max(0f, delaySeconds);
            VfxScheduleKey key = default;
            key = scheduler.Schedule(clampedDelay, () =>
            {
                mPendingSchedules.Remove(key.Value);
                RequestCue(request, spatialContext);
            });
            if (!key.IsValid)
            {
                return key;
            }

            mPendingSchedules[key.Value] = new PendingScheduledCue(request, spatialContext, clampedDelay);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddHistory(CreateHistoryRecord(
                VfxHistoryOutcome.Scheduled,
                request,
                mClock.UnscaledTime,
                scheduleKey: key.Value,
                scheduleDelaySeconds: clampedDelay));
#endif
            return key;
        }

        public bool CancelScheduledCue(VfxScheduleKey key)
        {
            if (!key.IsValid)
            {
                return false;
            }

            var scheduler = mScheduler ?? UnityVfxCueScheduler.Instance;
            if (!scheduler.Cancel(key))
            {
                return false;
            }

            PendingScheduledCue pending;
            var hadPending = mPendingSchedules.TryGetValue(key.Value, out pending);
            mPendingSchedules.Remove(key.Value);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var request = hadPending
                ? pending.Request
                : VfxCueRequest.Simple(string.Empty, "VfxSystem.CancelScheduledCue");
            AddHistory(CreateHistoryRecord(
                VfxHistoryOutcome.Cancelled,
                request,
                mClock.UnscaledTime,
                scheduleKey: key.Value,
                failureReason: "explicit cancel"));
#endif
            return true;
        }

        public VfxStateSlotResult SetSlot(
            IVfxSlotOwner owner,
            string slot,
            string desiredState,
            VfxSpatialContext spatialContext = default)
        {
            if (owner == null)
            {
                return new VfxStateSlotResult
                {
                    Outcome = VfxStateSlotOutcome.InvalidBinding,
                    FailureReason = "owner 为空。",
                };
            }

            if (string.IsNullOrWhiteSpace(slot))
            {
                return new VfxStateSlotResult
                {
                    Outcome = VfxStateSlotOutcome.InvalidBinding,
                    FailureReason = "slot 为空。",
                };
            }

            var key = new VfxSlotKey(owner, slot);
            if (string.IsNullOrWhiteSpace(desiredState))
            {
                return ClearSlotInternal(key, expectedState: null, conditional: false, VfxEndReason.SlotCleared);
            }

            mActiveStateSlots.TryGetValue(key, out var existing);
            if (existing != null
                && string.Equals(existing.DesiredStateId, desiredState, StringComparison.Ordinal)
                && !existing.Exiting)
            {
                return new VfxStateSlotResult
                {
                    Outcome = VfxStateSlotOutcome.NoOp,
                    StateId = desiredState,
                    BindingKey = existing.BindingKey,
                    PlayerId = existing.PlayerId,
                    InstanceId = existing.InstanceId,
                };
            }

            if (existing != null)
            {
                BeginStateExit(existing, VfxEndReason.SlotReplaced);
            }

            return StartResolvedState(owner, slot, desiredState, spatialContext);
        }

        public VfxStateSlotResult ClearSlotIf(IVfxSlotOwner owner, string slot, string expectedState)
        {
            if (owner == null || string.IsNullOrWhiteSpace(slot))
            {
                return new VfxStateSlotResult
                {
                    Outcome = VfxStateSlotOutcome.InvalidBinding,
                    FailureReason = "owner 或 slot 无效。",
                };
            }

            if (string.IsNullOrWhiteSpace(expectedState))
            {
                return new VfxStateSlotResult
                {
                    Outcome = VfxStateSlotOutcome.InvalidBinding,
                    FailureReason = "expectedState 为空。",
                };
            }

            var key = new VfxSlotKey(owner, slot);
            return ClearSlotInternal(key, expectedState, conditional: true, VfxEndReason.SlotCleared);
        }

        public void ReleaseOwner(IVfxSlotOwner owner)
        {
            if (owner == null)
            {
                return;
            }

            var keys = new List<VfxSlotKey>(mActiveStateSlots.Count);
            foreach (var pair in mActiveStateSlots)
            {
                if (ReferenceEquals(pair.Key.Owner, owner))
                {
                    keys.Add(pair.Key);
                }
            }

            for (var i = 0; i < keys.Count; i++)
            {
                ClearSlotInternal(keys[i], expectedState: null, conditional: false, VfxEndReason.OwnerReleased);
            }
        }

        public VfxCueResult RequestCue(VfxCueRequest request, VfxSpatialContext spatialContext = default)
        {
            return RequestCueCore(request, spatialContext, preview: false);
        }

        private VfxCueResult RequestCueCore(
            VfxCueRequest request,
            VfxSpatialContext spatialContext,
            bool preview)
        {
            var requestedAt = mClock.UnscaledTime;
            mDiagnostics.SetClock(requestedAt);
            var correlationId = mDiagnostics.BeginCorrelation();
            mDiagnostics.RecordRequest(
                correlationId,
                request.CueId,
                isPulse: true,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticCardUid);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddHistory(CreateHistoryRecord(
                VfxHistoryOutcome.Requested,
                request,
                requestedAt));
#endif

            if (!mCatalog.TryResolveCueStrict(request, out var binding, out var resolveError))
            {
                if (resolveError != null && resolveError.Code == VfxBindingResolveCode.Ambiguous)
                {
                    mDiagnostics.RecordResolve(
                        correlationId,
                        request.CueId,
                        isPulse: true,
                        resolved: false,
                        resolveError.BindingKey ?? string.Empty,
                        playerId: string.Empty,
                        request.DiagnosticSource,
                        request.CardDefId,
                        request.SkillId,
                        request.RoomId,
                        request.ItemDefId,
                        request.ContentId,
                        request.DiagnosticCardUid,
                        resolveError.Message ?? "绑定解析歧义。");
                    return RecordInvalidBinding(request, correlationId, resolveError);
                }

                var reason = resolveError?.Message ?? "视觉特效 cue 绑定不存在。";
                mDiagnostics.RecordResolve(
                    correlationId,
                    request.CueId,
                    isPulse: true,
                    resolved: false,
                    bindingKey: string.Empty,
                    playerId: string.Empty,
                    request.DiagnosticSource,
                    request.CardDefId,
                    request.SkillId,
                    request.RoomId,
                    request.ItemDefId,
                    request.ContentId,
                    request.DiagnosticCardUid,
                    reason);
                return RecordUnbound(request, correlationId, reason);
            }

            mDiagnostics.RecordResolve(
                correlationId,
                request.CueId,
                isPulse: true,
                resolved: true,
                binding.BindingKey,
                binding.PlayerId,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticCardUid,
                failureReason: string.Empty);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PatchLatestRequested(binding);
#endif

            if (!binding.Enabled && !preview)
            {
                return RecordSuppressed(request, correlationId, binding);
            }

            if (!VfxPlayerRegistry.SupportsPulse(binding.PlayerId))
            {
                return RecordPlayerUnavailable(request, correlationId, binding, "播放器未注册或不支持 Pulse。");
            }

            if (binding.SpatialOwnership == VfxSpatialOwnership.Attached
                && !TryAcceptAttachedSpatial(spatialContext, out var attachedFailure))
            {
                return RecordDomainUnavailable(request, correlationId, binding, attachedFailure);
            }

            var acceptedSpatial = AcceptSpatial(binding, spatialContext);
            var now = mClock.UnscaledTime;
            mDiagnostics.SetClock(now);
            if (!preview
                && binding.MinimumIntervalSeconds > 0f
                && mLastPlayedAt.TryGetValue(binding, out var lastPlayedAt)
                && now >= lastPlayedAt
                && now - lastPlayedAt < binding.MinimumIntervalSeconds)
            {
                return RecordSuppressed(request, correlationId, binding, "minimum interval");
            }

            if (binding.BindingDelaySeconds > 0f)
            {
                var capturedBinding = binding;
                var capturedSpatial = acceptedSpatial;
                var scheduler = mScheduler ?? UnityVfxCueScheduler.Instance;
                VfxScheduleKey delayKey = default;
                delayKey =                 scheduler.Schedule(binding.BindingDelaySeconds, () =>
                {
                    StartResolvedCue(request, capturedBinding, capturedSpatial, mClock.UnscaledTime, correlationId, preview);
                });
                if (delayKey.IsValid)
                {
                    return new VfxCueResult
                    {
                        Outcome = VfxCueOutcome.Played,
                        CueId = request.CueId,
                        CueNote = binding.Note,
                        BindingKey = binding.BindingKey,
                        PlayerId = binding.PlayerId,
                    };
                }
            }

            return StartResolvedCue(request, binding, acceptedSpatial, now, correlationId, preview);
        }

        public void Tick(float deltaTime)
        {
            if (mActiveInstances.Count > 0)
            {
                for (var i = mActiveInstances.Count - 1; i >= 0; i--)
                {
                    var instance = mActiveInstances[i];
                    if (instance.Player == null)
                    {
                        CompletePulseInstance(instance, VfxEndReason.NaturalComplete);
                        mActiveInstances.RemoveAt(i);
                        continue;
                    }

                    if (instance.Ownership == VfxSpatialOwnership.Attached
                        && instance.SpatialContext.HasDomainHost
                        && (instance.SpatialContext.DomainHost == null
                            || !instance.SpatialContext.DomainHost.IsAvailable))
                    {
                        instance.Player.Cancel();
                        CompletePulseInstance(instance, VfxEndReason.AttachedHostLost);
                        mActiveInstances.RemoveAt(i);
                        continue;
                    }

                    if (instance.Player.Tick(deltaTime))
                    {
                        CompletePulseInstance(instance, VfxEndReason.NaturalComplete);
                        mActiveInstances.RemoveAt(i);
                    }
                }
            }

            if (mActiveStateSlots.Count > 0)
            {
                List<VfxSlotKey> completedKeys = null;
                foreach (var pair in mActiveStateSlots)
                {
                    var slot = pair.Value;
                    if (slot.Player == null)
                    {
                        if (completedKeys == null)
                        {
                            completedKeys = new List<VfxSlotKey>();
                        }

                        CompleteStateSlot(slot, VfxEndReason.NaturalComplete);
                        completedKeys.Add(pair.Key);
                        continue;
                    }

                    if (slot.SpatialOwnership == VfxSpatialOwnership.Attached
                        && slot.SpatialContext.HasDomainHost
                        && (slot.SpatialContext.DomainHost == null
                            || !slot.SpatialContext.DomainHost.IsAvailable))
                    {
                        slot.Player.Cancel();
                        if (completedKeys == null)
                        {
                            completedKeys = new List<VfxSlotKey>();
                        }

                        CompleteStateSlot(slot, VfxEndReason.AttachedHostLost);
                        completedKeys.Add(pair.Key);
                        continue;
                    }

                    if (slot.Player.Tick(deltaTime))
                    {
                        if (completedKeys == null)
                        {
                            completedKeys = new List<VfxSlotKey>();
                        }

                        CompleteStateSlot(slot, VfxEndReason.NaturalComplete);
                        completedKeys.Add(pair.Key);
                    }
                }

                if (completedKeys != null)
                {
                    for (var i = 0; i < completedKeys.Count; i++)
                    {
                        mActiveStateSlots.Remove(completedKeys[i]);
                    }
                }
            }

            if (mExitingStateSlots.Count > 0)
            {
                for (var i = mExitingStateSlots.Count - 1; i >= 0; i--)
                {
                    var slot = mExitingStateSlots[i];
                    if (slot.Player == null || slot.Player.Tick(deltaTime))
                    {
                        CompleteStateSlot(slot, VfxEndReason.NaturalComplete);
                        mExitingStateSlots.RemoveAt(i);
                    }
                }
            }

            mDiagnostics.SetClock(mClock.UnscaledTime);
            mDiagnostics.OnFrameEnd(CountActiveInstances());
        }

        public void ClearSceneInstances()
        {
            for (var i = mActiveInstances.Count - 1; i >= 0; i--)
            {
                var instance = mActiveInstances[i];
                if (instance.Ownership != VfxSpatialOwnership.Attached)
                {
                    continue;
                }

                instance.Player?.Cancel();
                CompletePulseInstance(instance, VfxEndReason.SceneExit);
                mActiveInstances.RemoveAt(i);
            }

            var attachedKeys = new List<VfxSlotKey>();
            foreach (var pair in mActiveStateSlots)
            {
                if (pair.Value.Binding != null
                    && pair.Value.Binding.SpatialOwnership == VfxSpatialOwnership.Attached)
                {
                    attachedKeys.Add(pair.Key);
                }
            }

            for (var i = 0; i < attachedKeys.Count; i++)
            {
                ClearSlotInternal(attachedKeys[i], expectedState: null, conditional: false, VfxEndReason.SceneExit);
            }
        }

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            mActiveInstances.Clear();
            mPendingSchedules.Clear();
            mActiveStateSlots.Clear();
            mExitingStateSlots.Clear();
        }

        private VfxCueResult StartResolvedCue(
            VfxCueRequest request,
            VfxCueBinding binding,
            VfxSpatialContext acceptedSpatial,
            double now,
            long correlationId,
            bool preview = false)
        {
            mDiagnostics.SetClock(now);
            if (!mPlayerFactory.TryCreatePulsePlayer(binding.PlayerId, out var player, out var factoryReason))
            {
                mDiagnostics.RecordCreate(
                    correlationId,
                    request.CueId,
                    isPulse: true,
                    binding.BindingKey,
                    binding.PlayerId,
                    succeeded: false,
                    request.DiagnosticSource,
                    request.CardDefId,
                    request.SkillId,
                    request.RoomId,
                    request.ItemDefId,
                    request.ContentId,
                    request.DiagnosticCardUid,
                    factoryReason);
                return RecordPlayerUnavailable(request, correlationId, binding, factoryReason);
            }

            mDiagnostics.RecordCreate(
                correlationId,
                request.CueId,
                isPulse: true,
                binding.BindingKey,
                binding.PlayerId,
                succeeded: true,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticCardUid,
                failureReason: string.Empty);

            string variantId = string.Empty;
            string materialKey = string.Empty;
            float fps = binding.Fps;
            float speed = VfxBindingPlayback.ResolveSpeed(binding.Speed);
            float scale = binding.Scale;
            float startOffset = binding.StartOffsetSeconds;
            var tint = binding.Tint;
            var useUnscaledTime = binding.UseUnscaledTime;

            if (VfxPlayerRegistry.IsMaterialPlayer(binding.PlayerId))
            {
                var variant = ResolveVariant(binding);
                if (variant == null || string.IsNullOrWhiteSpace(variant.materialKey))
                {
                    return RecordUnbound(request, correlationId, "视觉特效绑定缺少有效素材。");
                }

                variantId = variant.variantId ?? string.Empty;
                materialKey = variant.materialKey;
                fps = binding.Fps + variant.fpsTrim;
                scale = binding.Scale + variant.scaleTrim;
                startOffset = Math.Max(0f, variant.startOffsetSeconds);
            }

            var startRequest = new VfxPulseStartRequest(
                request,
                binding,
                variantId,
                materialKey,
                fps,
                speed,
                scale,
                startOffset,
                tint,
                useUnscaledTime,
                acceptedSpatial);

            VfxPlayerStartResult backend;
            try
            {
                backend = player.StartPulse(startRequest);
            }
            catch (Exception exception)
            {
                backend = VfxPlayerStartResult.Failure(exception.Message);
            }

            if (!backend.Succeeded)
            {
                return RecordBackendFailure(
                    request,
                    correlationId,
                    binding,
                    variantId,
                    materialKey,
                    backend.FailureReason);
            }

            if (!preview)
            {
                mLastPlayedAt[binding] = now;
            }

            if (!string.IsNullOrEmpty(variantId))
            {
                mLastVariantIds[binding] = variantId;
            }

            var diagnosticContext = BuildPulseDiagnosticContext(
                request,
                binding,
                acceptedSpatial,
                variantId,
                materialKey,
                fps,
                speed,
                scale,
                backend.InstanceId,
                ownerSlotKey: string.Empty);
            mDiagnostics.RecordStart(correlationId, diagnosticContext);

            mActiveInstances.Add(new ActivePulseInstance
            {
                CorrelationId = correlationId,
                InstanceId = backend.InstanceId,
                Ownership = binding.SpatialOwnership,
                SpatialContext = acceptedSpatial,
                DiagnosticContext = diagnosticContext,
                Player = player,
            });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddHistory(CreateHistoryRecord(
                VfxHistoryOutcome.Played,
                request,
                now,
                cueNote: binding.Note,
                bindingKey: binding.BindingKey,
                playerId: binding.PlayerId,
                materialKey: materialKey,
                variantId: variantId,
                instanceId: backend.InstanceId));
#endif

            return new VfxCueResult
            {
                Outcome = VfxCueOutcome.Played,
                CueId = request.CueId,
                CueNote = binding.Note,
                BindingKey = binding.BindingKey,
                PlayerId = binding.PlayerId,
                MaterialKey = materialKey,
                VariantId = variantId,
                InstanceId = backend.InstanceId,
                PresentationPlan = backend.PresentationPlan,
            };
        }

        private static VfxSpatialContext AcceptSpatial(VfxCueBinding binding, VfxSpatialContext spatialContext)
        {
            if (binding.SpatialOwnership == VfxSpatialOwnership.Independent)
            {
                return new VfxSpatialContext(
                    spatialContext.SemanticRole,
                    null,
                    spatialContext.PositionSnapshot,
                    spatialContext.DiagnosticOwnerUid,
                    spatialContext.Amount);
            }

            return spatialContext;
        }

        private bool TryAcceptAttachedSpatial(VfxSpatialContext spatialContext, out string failureReason)
        {
            failureReason = string.Empty;
            if (!spatialContext.HasDomainHost)
            {
                failureReason = "附着型特效缺少视觉域宿主。";
                return false;
            }

            IVfxDomainHost host = spatialContext.DomainHost;
            if (mDomainHostResolver != null
                && !mDomainHostResolver.TryResolveHost(spatialContext.DomainHost, out host))
            {
                failureReason = "视觉域宿主无法解析。";
                return false;
            }

            if (host == null || !host.IsAvailable)
            {
                failureReason = "视觉域宿主不可用。";
                return false;
            }

            return true;
        }

        private VfxMaterialVariantDto ResolveVariant(VfxCueBinding binding)
        {
            var variants = binding.Variants;
            if (variants == null || variants.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(binding.MaterialKey))
                {
                    return null;
                }

                return new VfxMaterialVariantDto
                {
                    variantId = "default",
                    materialKey = binding.MaterialKey,
                    weight = 1f,
                };
            }

            var candidates = new List<VfxMaterialVariantDto>();
            var totalWeight = 0f;
            for (var i = 0; i < variants.Count; i++)
            {
                var row = variants[i];
                if (row == null || string.IsNullOrWhiteSpace(row.materialKey) || row.weight <= 0f)
                {
                    continue;
                }

                candidates.Add(row);
                totalWeight += row.weight;
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            mLastVariantIds.TryGetValue(binding, out var lastVariantId);
            var eligible = new List<VfxMaterialVariantDto>(candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidates.Count > 1
                    && !string.IsNullOrEmpty(lastVariantId)
                    && string.Equals(candidate.variantId, lastVariantId, StringComparison.Ordinal))
                {
                    continue;
                }

                eligible.Add(candidate);
            }

            if (eligible.Count == 0)
            {
                eligible.AddRange(candidates);
            }

            var pickWeight = (float)(mRandomValue() * totalWeight);
            var cursor = 0f;
            for (var i = 0; i < eligible.Count; i++)
            {
                cursor += eligible[i].weight;
                if (pickWeight <= cursor)
                {
                    return eligible[i];
                }
            }

            return eligible[eligible.Count - 1];
        }

        private VfxStateSlotResult ClearSlotInternal(
            VfxSlotKey key,
            string expectedState,
            bool conditional,
            VfxEndReason exitReason)
        {
            if (!mActiveStateSlots.TryGetValue(key, out var slot))
            {
                return new VfxStateSlotResult
                {
                    Outcome = VfxStateSlotOutcome.NoOp,
                    StateId = expectedState ?? string.Empty,
                };
            }

            if (conditional
                && !string.Equals(slot.DesiredStateId, expectedState, StringComparison.Ordinal))
            {
                return new VfxStateSlotResult
                {
                    Outcome = VfxStateSlotOutcome.NoOp,
                    StateId = slot.DesiredStateId,
                    BindingKey = slot.BindingKey,
                    PlayerId = slot.PlayerId,
                    InstanceId = slot.InstanceId,
                };
            }

            var clearedState = slot.DesiredStateId;
            BeginStateExit(slot, exitReason);
            return new VfxStateSlotResult
            {
                Outcome = VfxStateSlotOutcome.Cleared,
                StateId = clearedState,
                BindingKey = slot.BindingKey,
                PlayerId = slot.PlayerId,
                InstanceId = slot.InstanceId,
            };
        }

        private void BeginStateExit(ActiveStateSlot slot, VfxEndReason exitReason)
        {
            if (slot == null)
            {
                return;
            }

            mActiveStateSlots.Remove(slot.Key);
            slot.Exiting = true;
            var immediate = slot.Binding == null
                || !VfxStateExitMode.IsSegment(slot.Binding.ExitMode);
            var exitLoops = slot.Binding?.ExitLoopLimit ?? 1;
            slot.Player?.BeginExit(immediate, exitLoops);
            if (immediate)
            {
                slot.Player?.Cancel();
                CompleteStateSlot(slot, exitReason);
                return;
            }

            mExitingStateSlots.Add(slot);
        }

        private VfxStateSlotResult StartResolvedState(
            IVfxSlotOwner owner,
            string slot,
            string desiredState,
            VfxSpatialContext spatialContext)
        {
            var request = owner.BuildStateRequest(desiredState);
            var now = mClock.UnscaledTime;
            mDiagnostics.SetClock(now);
            var correlationId = mDiagnostics.BeginCorrelation();
            mDiagnostics.RecordRequest(
                correlationId,
                request.StateId,
                isPulse: false,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticOwnerUid);

            if (!mCatalog.TryResolveStateStrict(request, out var binding, out var resolveError))
            {
                if (resolveError != null && resolveError.Code == VfxBindingResolveCode.Ambiguous)
                {
                    mDiagnostics.RecordResolve(
                        correlationId,
                        request.StateId,
                        isPulse: false,
                        resolved: false,
                        resolveError.BindingKey ?? string.Empty,
                        playerId: string.Empty,
                        request.DiagnosticSource,
                        request.CardDefId,
                        request.SkillId,
                        request.RoomId,
                        request.ItemDefId,
                        request.ContentId,
                        request.DiagnosticOwnerUid,
                        resolveError.Message ?? "绑定解析歧义。");
                    return RecordStateInvalidBinding(request, correlationId, resolveError);
                }

                var reason = resolveError?.Message ?? "持续视觉状态绑定不存在。";
                mDiagnostics.RecordResolve(
                    correlationId,
                    request.StateId,
                    isPulse: false,
                    resolved: false,
                    bindingKey: string.Empty,
                    playerId: string.Empty,
                    request.DiagnosticSource,
                    request.CardDefId,
                    request.SkillId,
                    request.RoomId,
                    request.ItemDefId,
                    request.ContentId,
                    request.DiagnosticOwnerUid,
                    reason);
                return RecordStateUnbound(request, correlationId, reason);
            }

            mDiagnostics.RecordResolve(
                correlationId,
                request.StateId,
                isPulse: false,
                resolved: true,
                binding.BindingKey,
                binding.PlayerId,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticOwnerUid,
                failureReason: string.Empty);

            if (!binding.Enabled)
            {
                return RecordStateSuppressed(request, correlationId, binding);
            }

            if (!VfxPlayerRegistry.SupportsState(binding.PlayerId))
            {
                return RecordStatePlayerUnavailable(
                    request,
                    correlationId,
                    binding,
                    "播放器未注册或不支持 State。");
            }

            if (binding.SpatialOwnership == VfxSpatialOwnership.Attached
                && !TryAcceptAttachedSpatial(spatialContext, out var attachedFailure))
            {
                return RecordStateDomainUnavailable(request, correlationId, binding, attachedFailure);
            }

            var acceptedSpatial = AcceptStateSpatial(binding, spatialContext);
            if (!mPlayerFactory.TryCreateStatePlayer(binding.PlayerId, out var player, out var factoryReason))
            {
                mDiagnostics.RecordCreate(
                    correlationId,
                    request.StateId,
                    isPulse: false,
                    binding.BindingKey,
                    binding.PlayerId,
                    succeeded: false,
                    request.DiagnosticSource,
                    request.CardDefId,
                    request.SkillId,
                    request.RoomId,
                    request.ItemDefId,
                    request.ContentId,
                    request.DiagnosticOwnerUid,
                    factoryReason);
                return RecordStatePlayerUnavailable(request, correlationId, binding, factoryReason);
            }

            mDiagnostics.RecordCreate(
                correlationId,
                request.StateId,
                isPulse: false,
                binding.BindingKey,
                binding.PlayerId,
                succeeded: true,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticOwnerUid,
                failureReason: string.Empty);

            string variantId = string.Empty;
            string materialKey = string.Empty;
            float fps = binding.Fps;
            float speed = VfxBindingPlayback.ResolveSpeed(binding.Speed);
            float scale = binding.Scale;
            float startOffset = binding.StartOffsetSeconds;
            var tint = binding.Tint;
            var useUnscaledTime = binding.UseUnscaledTime;

            if (VfxPlayerRegistry.IsMaterialPlayer(binding.PlayerId))
            {
                var variant = ResolveStateVariant(binding);
                if (variant == null || string.IsNullOrWhiteSpace(variant.materialKey))
                {
                    return RecordStateUnbound(request, correlationId, "视觉特效绑定缺少有效素材。");
                }

                variantId = variant.variantId ?? string.Empty;
                materialKey = variant.materialKey;
                fps = binding.Fps + variant.fpsTrim;
                scale = binding.Scale + variant.scaleTrim;
                startOffset = Math.Max(0f, variant.startOffsetSeconds);
            }

            var startRequest = new VfxStateStartRequest(
                request,
                binding,
                variantId,
                materialKey,
                fps,
                speed,
                scale,
                startOffset,
                tint,
                useUnscaledTime,
                acceptedSpatial);

            VfxPlayerStartResult backend;
            try
            {
                backend = player.StartState(startRequest);
            }
            catch (Exception exception)
            {
                backend = VfxPlayerStartResult.Failure(exception.Message);
            }

            if (!backend.Succeeded)
            {
                return RecordStateBackendFailure(
                    request,
                    correlationId,
                    binding,
                    variantId,
                    materialKey,
                    backend.FailureReason);
            }

            var ownerSlotKey = BuildOwnerSlotKey(owner, slot);
            var diagnosticContext = BuildStateDiagnosticContext(
                request,
                binding,
                acceptedSpatial,
                variantId,
                materialKey,
                fps,
                speed,
                scale,
                backend.InstanceId,
                ownerSlotKey);
            mDiagnostics.RecordStart(correlationId, diagnosticContext);

            var key = new VfxSlotKey(owner, slot);
            var activeSlot = new ActiveStateSlot
            {
                Key = key,
                CorrelationId = correlationId,
                DesiredStateId = desiredState,
                Binding = binding,
                BindingKey = binding.BindingKey,
                PlayerId = binding.PlayerId,
                InstanceId = backend.InstanceId,
                Player = player,
                SpatialOwnership = binding.SpatialOwnership,
                SpatialContext = acceptedSpatial,
                DiagnosticContext = diagnosticContext,
            };
            mActiveStateSlots[key] = activeSlot;

            return new VfxStateSlotResult
            {
                Outcome = VfxStateSlotOutcome.Applied,
                StateId = desiredState,
                BindingKey = binding.BindingKey,
                PlayerId = binding.PlayerId,
                InstanceId = backend.InstanceId,
            };
        }

        private static VfxSpatialContext AcceptStateSpatial(
            VfxStateBinding binding,
            VfxSpatialContext spatialContext)
        {
            if (binding.SpatialOwnership == VfxSpatialOwnership.Independent)
            {
                return new VfxSpatialContext(
                    spatialContext.SemanticRole,
                    null,
                    spatialContext.PositionSnapshot,
                    spatialContext.DiagnosticOwnerUid,
                    spatialContext.Amount);
            }

            return spatialContext;
        }

        private VfxMaterialVariantDto ResolveStateVariant(VfxStateBinding binding)
        {
            var variants = binding.Variants;
            if (variants == null || variants.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(binding.MaterialKey))
                {
                    return null;
                }

                return new VfxMaterialVariantDto
                {
                    variantId = "default",
                    materialKey = binding.MaterialKey,
                    weight = 1f,
                };
            }

            var candidates = new List<VfxMaterialVariantDto>();
            var totalWeight = 0f;
            for (var i = 0; i < variants.Count; i++)
            {
                var row = variants[i];
                if (row == null || string.IsNullOrWhiteSpace(row.materialKey) || row.weight <= 0f)
                {
                    continue;
                }

                candidates.Add(row);
                totalWeight += row.weight;
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            var pickWeight = (float)(mRandomValue() * totalWeight);
            var cursor = 0f;
            for (var i = 0; i < candidates.Count; i++)
            {
                cursor += candidates[i].weight;
                if (pickWeight <= cursor)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        private VfxStateSlotResult RecordStateUnbound(VfxStateRequest request, long correlationId, string reason)
        {
            mDiagnostics.RecordTerminalFailure(
                correlationId,
                request.StateId,
                isPulse: false,
                VfxEndReason.Unbound,
                bindingKey: string.Empty,
                playerId: string.Empty,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticOwnerUid,
                reason);
            return new VfxStateSlotResult
            {
                Outcome = VfxStateSlotOutcome.Unbound,
                StateId = request.StateId,
                FailureReason = reason,
            };
        }

        private VfxStateSlotResult RecordStateInvalidBinding(
            VfxStateRequest request,
            long correlationId,
            VfxBindingResolveError resolveError)
        {
            var reason = resolveError?.Message ?? "绑定解析歧义。";
            mDiagnostics.RecordTerminalFailure(
                correlationId,
                request.StateId,
                isPulse: false,
                VfxEndReason.InvalidBinding,
                resolveError?.BindingKey ?? string.Empty,
                playerId: string.Empty,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticOwnerUid,
                reason);
            return new VfxStateSlotResult
            {
                Outcome = VfxStateSlotOutcome.InvalidBinding,
                StateId = request.StateId,
                BindingKey = resolveError?.BindingKey ?? string.Empty,
                FailureReason = reason,
            };
        }

        private VfxStateSlotResult RecordStateSuppressed(
            VfxStateRequest request,
            long correlationId,
            VfxStateBinding binding)
        {
            mDiagnostics.RecordTerminalFailure(
                correlationId,
                request.StateId,
                isPulse: false,
                VfxEndReason.Suppressed,
                binding.BindingKey,
                binding.PlayerId,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticOwnerUid,
                SuppressedReason);
            return new VfxStateSlotResult
            {
                Outcome = VfxStateSlotOutcome.Suppressed,
                StateId = request.StateId,
                BindingKey = binding.BindingKey,
                PlayerId = binding.PlayerId,
                FailureReason = SuppressedReason,
            };
        }

        private VfxStateSlotResult RecordStatePlayerUnavailable(
            VfxStateRequest request,
            long correlationId,
            VfxStateBinding binding,
            string reason)
        {
            mDiagnostics.RecordTerminalFailure(
                correlationId,
                request.StateId,
                isPulse: false,
                VfxEndReason.PlayerUnavailable,
                binding.BindingKey,
                binding.PlayerId,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticOwnerUid,
                reason);
            return new VfxStateSlotResult
            {
                Outcome = VfxStateSlotOutcome.PlayerUnavailable,
                StateId = request.StateId,
                BindingKey = binding.BindingKey,
                PlayerId = binding.PlayerId,
                FailureReason = reason,
            };
        }

        private VfxStateSlotResult RecordStateDomainUnavailable(
            VfxStateRequest request,
            long correlationId,
            VfxStateBinding binding,
            string reason)
        {
            mDiagnostics.RecordTerminalFailure(
                correlationId,
                request.StateId,
                isPulse: false,
                VfxEndReason.DomainUnavailable,
                binding.BindingKey,
                binding.PlayerId,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticOwnerUid,
                reason);
            return new VfxStateSlotResult
            {
                Outcome = VfxStateSlotOutcome.DomainUnavailable,
                StateId = request.StateId,
                BindingKey = binding.BindingKey,
                PlayerId = binding.PlayerId,
                FailureReason = reason,
            };
        }

        private VfxStateSlotResult RecordStateBackendFailure(
            VfxStateRequest request,
            long correlationId,
            VfxStateBinding binding,
            string variantId,
            string materialKey,
            string reason)
        {
            mDiagnostics.RecordTerminalFailure(
                correlationId,
                request.StateId,
                isPulse: false,
                VfxEndReason.BackendFailure,
                binding.BindingKey,
                binding.PlayerId,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticOwnerUid,
                reason);
            return new VfxStateSlotResult
            {
                Outcome = VfxStateSlotOutcome.BackendFailure,
                StateId = request.StateId,
                BindingKey = binding.BindingKey,
                PlayerId = binding.PlayerId,
                FailureReason = reason,
            };
        }

        private VfxCueResult RecordUnbound(VfxCueRequest request, long correlationId, string reason)
        {
            mDiagnostics.RecordTerminalFailure(
                correlationId,
                request.CueId,
                isPulse: true,
                VfxEndReason.Unbound,
                bindingKey: string.Empty,
                playerId: string.Empty,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticCardUid,
                reason);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var record = CreateHistoryRecord(VfxHistoryOutcome.Unbound, request, mClock.UnscaledTime);
            record.FailureReason = reason;
            AddHistory(record);
#endif
            return new VfxCueResult
            {
                Outcome = VfxCueOutcome.Unbound,
                CueId = request.CueId,
                FailureReason = reason,
            };
        }

        private VfxCueResult RecordInvalidBinding(
            VfxCueRequest request,
            long correlationId,
            VfxBindingResolveError resolveError)
        {
            var reason = resolveError?.Message ?? "绑定解析歧义。";
            mDiagnostics.RecordTerminalFailure(
                correlationId,
                request.CueId,
                isPulse: true,
                VfxEndReason.InvalidBinding,
                resolveError?.BindingKey ?? string.Empty,
                playerId: string.Empty,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticCardUid,
                reason);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var record = CreateHistoryRecord(
                VfxHistoryOutcome.InvalidBinding,
                request,
                mClock.UnscaledTime,
                bindingKey: resolveError?.BindingKey,
                failureReason: reason);
            AddHistory(record);
#endif
            return new VfxCueResult
            {
                Outcome = VfxCueOutcome.InvalidBinding,
                CueId = request.CueId,
                BindingKey = resolveError?.BindingKey ?? string.Empty,
                FailureReason = reason,
            };
        }

        private VfxCueResult RecordSuppressed(
            VfxCueRequest request,
            long correlationId,
            VfxCueBinding binding,
            string reason = SuppressedReason)
        {
            mDiagnostics.RecordTerminalFailure(
                correlationId,
                request.CueId,
                isPulse: true,
                VfxEndReason.Suppressed,
                binding.BindingKey,
                binding.PlayerId,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticCardUid,
                reason);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var record = CreateHistoryRecord(
                VfxHistoryOutcome.Suppressed,
                request,
                mClock.UnscaledTime,
                cueNote: binding.Note,
                bindingKey: binding.BindingKey,
                playerId: binding.PlayerId,
                failureReason: reason);
            AddHistory(record);
#endif
            return new VfxCueResult
            {
                Outcome = VfxCueOutcome.Suppressed,
                CueId = request.CueId,
                CueNote = binding.Note,
                BindingKey = binding.BindingKey,
                PlayerId = binding.PlayerId,
                FailureReason = reason,
            };
        }

        private VfxCueResult RecordPlayerUnavailable(
            VfxCueRequest request,
            long correlationId,
            VfxCueBinding binding,
            string reason)
        {
            mDiagnostics.RecordTerminalFailure(
                correlationId,
                request.CueId,
                isPulse: true,
                VfxEndReason.PlayerUnavailable,
                binding.BindingKey,
                binding.PlayerId,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticCardUid,
                reason);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var record = CreateHistoryRecord(
                VfxHistoryOutcome.PlayerUnavailable,
                request,
                mClock.UnscaledTime,
                cueNote: binding.Note,
                bindingKey: binding.BindingKey,
                playerId: binding.PlayerId,
                failureReason: reason);
            AddHistory(record);
#endif
            return new VfxCueResult
            {
                Outcome = VfxCueOutcome.PlayerUnavailable,
                CueId = request.CueId,
                CueNote = binding.Note,
                BindingKey = binding.BindingKey,
                PlayerId = binding.PlayerId,
                FailureReason = reason,
            };
        }

        private VfxCueResult RecordDomainUnavailable(
            VfxCueRequest request,
            long correlationId,
            VfxCueBinding binding,
            string reason)
        {
            mDiagnostics.RecordTerminalFailure(
                correlationId,
                request.CueId,
                isPulse: true,
                VfxEndReason.DomainUnavailable,
                binding.BindingKey,
                binding.PlayerId,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticCardUid,
                reason);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var record = CreateHistoryRecord(
                VfxHistoryOutcome.DomainUnavailable,
                request,
                mClock.UnscaledTime,
                cueNote: binding.Note,
                bindingKey: binding.BindingKey,
                playerId: binding.PlayerId,
                failureReason: reason);
            AddHistory(record);
#endif
            return new VfxCueResult
            {
                Outcome = VfxCueOutcome.DomainUnavailable,
                CueId = request.CueId,
                CueNote = binding.Note,
                BindingKey = binding.BindingKey,
                PlayerId = binding.PlayerId,
                FailureReason = reason,
            };
        }

        private VfxCueResult RecordBackendFailure(
            VfxCueRequest request,
            long correlationId,
            VfxCueBinding binding,
            string variantId,
            string materialKey,
            string reason)
        {
            mDiagnostics.RecordTerminalFailure(
                correlationId,
                request.CueId,
                isPulse: true,
                VfxEndReason.BackendFailure,
                binding.BindingKey,
                binding.PlayerId,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticCardUid,
                reason);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var record = CreateHistoryRecord(
                VfxHistoryOutcome.BackendFailure,
                request,
                mClock.UnscaledTime,
                cueNote: binding.Note,
                bindingKey: binding.BindingKey,
                playerId: binding.PlayerId,
                materialKey: materialKey,
                variantId: variantId,
                failureReason: reason);
            AddHistory(record);
#endif
            return new VfxCueResult
            {
                Outcome = VfxCueOutcome.BackendFailure,
                CueId = request.CueId,
                CueNote = binding.Note,
                BindingKey = binding.BindingKey,
                PlayerId = binding.PlayerId,
                MaterialKey = materialKey,
                VariantId = variantId,
                FailureReason = reason,
            };
        }

        private static VfxInstanceDiagnosticContext BuildPulseDiagnosticContext(
            VfxCueRequest request,
            VfxCueBinding binding,
            VfxSpatialContext spatial,
            string variantId,
            string materialKey,
            float fps,
            float speed,
            float scale,
            string instanceId,
            string ownerSlotKey)
        {
            return new VfxInstanceDiagnosticContext(
                instanceId,
                request.CueId,
                isPulse: true,
                binding.BindingKey,
                binding.PlayerId,
                variantId,
                materialKey,
                binding.SpatialOwnership,
                VfxDiagnosticFormatting.BuildDomainLabel(spatial),
                ownerSlotKey,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticCardUid,
                VfxDiagnosticFormatting.BuildOverrideSummary(fps, speed, scale));
        }

        private static VfxInstanceDiagnosticContext BuildStateDiagnosticContext(
            VfxStateRequest request,
            VfxStateBinding binding,
            VfxSpatialContext spatial,
            string variantId,
            string materialKey,
            float fps,
            float speed,
            float scale,
            string instanceId,
            string ownerSlotKey)
        {
            return new VfxInstanceDiagnosticContext(
                instanceId,
                request.StateId,
                isPulse: false,
                binding.BindingKey,
                binding.PlayerId,
                variantId,
                materialKey,
                binding.SpatialOwnership,
                VfxDiagnosticFormatting.BuildDomainLabel(spatial),
                ownerSlotKey,
                request.DiagnosticSource,
                request.CardDefId,
                request.SkillId,
                request.RoomId,
                request.ItemDefId,
                request.ContentId,
                request.DiagnosticOwnerUid,
                VfxDiagnosticFormatting.BuildOverrideSummary(fps, speed, scale));
        }

        private void CompletePulseInstance(ActivePulseInstance instance, VfxEndReason endReason)
        {
            if (instance == null || instance.Completed)
            {
                return;
            }

            instance.Completed = true;
            mDiagnostics.RecordComplete(instance.CorrelationId, instance.DiagnosticContext, endReason);
        }

        private void CompleteStateSlot(ActiveStateSlot slot, VfxEndReason endReason)
        {
            if (slot == null || slot.Completed)
            {
                return;
            }

            slot.Completed = true;
            mDiagnostics.RecordComplete(slot.CorrelationId, slot.DiagnosticContext, endReason);
        }

        private int CountActiveInstances()
        {
            return mActiveInstances.Count + mActiveStateSlots.Count + mExitingStateSlots.Count;
        }

        private static string BuildOwnerSlotKey(IVfxSlotOwner owner, string slot)
        {
            if (owner == null)
            {
                return string.Empty;
            }

            return RuntimeHelpers.GetHashCode(owner).ToString(CultureInfo.InvariantCulture)
                + ":" + (slot ?? string.Empty);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void PatchLatestRequested(VfxCueBinding binding)
        {
            if (mHistory.Count == 0)
            {
                return;
            }

            var latest = mHistory[mHistory.Count - 1];
            if (latest.Outcome != VfxHistoryOutcome.Requested)
            {
                return;
            }

            latest.CueNote = binding.Note;
            latest.BindingKey = binding.BindingKey;
            latest.PlayerId = binding.PlayerId;
            latest.MaterialKey = binding.MaterialKey;
        }

        private VfxHistoryRecord CreateHistoryRecord(
            VfxHistoryOutcome outcome,
            VfxCueRequest request,
            double time,
            string cueNote = null,
            string bindingKey = null,
            string playerId = null,
            string materialKey = null,
            string variantId = null,
            string instanceId = null,
            string failureReason = null,
            long scheduleKey = 0,
            float scheduleDelaySeconds = 0f)
        {
            return new VfxHistoryRecord
            {
                Sequence = ++mNextHistorySequence,
                Outcome = outcome,
                CueId = request.CueId,
                CueNote = cueNote ?? string.Empty,
                BindingKey = bindingKey ?? string.Empty,
                DiagnosticSource = request.DiagnosticSource,
                CardDefId = request.CardDefId,
                SkillId = request.SkillId,
                RoomId = request.RoomId,
                ItemDefId = request.ItemDefId,
                ContentId = request.ContentId,
                DiagnosticCardUid = request.DiagnosticCardUid,
                PlayerId = playerId ?? string.Empty,
                MaterialKey = materialKey ?? string.Empty,
                VariantId = variantId ?? string.Empty,
                InstanceId = instanceId ?? string.Empty,
                FailureReason = failureReason ?? string.Empty,
                ScheduleKey = scheduleKey,
                ScheduleDelaySeconds = scheduleDelaySeconds,
                Time = time,
            };
        }

        private void AddHistory(VfxHistoryRecord record)
        {
            if (mHistory.Count >= mHistoryCapacity)
            {
                mHistory.RemoveAt(0);
            }

            mHistory.Add(record);
            UpdateAggregate(record);
        }
#endif

        private sealed class ActiveStateSlot
        {
            public VfxSlotKey Key;
            public long CorrelationId;
            public string DesiredStateId;
            public VfxStateBinding Binding;
            public string BindingKey;
            public string PlayerId;
            public string InstanceId;
            public IVfxStatePlayer Player;
            public VfxSpatialOwnership SpatialOwnership;
            public VfxSpatialContext SpatialContext;
            public VfxInstanceDiagnosticContext DiagnosticContext;
            public bool Exiting;
            public bool Completed;
        }

        private readonly struct VfxSlotKey : IEquatable<VfxSlotKey>
        {
            public VfxSlotKey(IVfxSlotOwner owner, string slot)
            {
                Owner = owner;
                Slot = slot ?? string.Empty;
            }

            public IVfxSlotOwner Owner { get; }
            public string Slot { get; }

            public bool Equals(VfxSlotKey other)
            {
                return ReferenceEquals(Owner, other.Owner)
                    && string.Equals(Slot, other.Slot, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is VfxSlotKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RuntimeHelpers.GetHashCode(Owner) * 397)
                        ^ StringComparer.Ordinal.GetHashCode(Slot);
                }
            }
        }

        private sealed class PendingScheduledCue
        {
            public PendingScheduledCue(VfxCueRequest request, VfxSpatialContext spatial, float delaySeconds)
            {
                Request = request;
                Spatial = spatial;
                DelaySeconds = delaySeconds;
            }

            public VfxCueRequest Request { get; }
            public VfxSpatialContext Spatial { get; }
            public float DelaySeconds { get; }
        }

        private sealed class ActivePulseInstance
        {
            public long CorrelationId;
            public string InstanceId;
            public VfxSpatialOwnership Ownership;
            public VfxSpatialContext SpatialContext;
            public VfxInstanceDiagnosticContext DiagnosticContext;
            public IVfxPulsePlayer Player;
            public bool Completed;
        }

        private sealed class RealtimeAudioClock : IAudioClock
        {
            public double UnscaledTime => Time.realtimeSinceStartup;
        }

        private sealed class NullVfxPlayerFactory : IVfxPlayerFactory
        {
            public bool TryCreatePulsePlayer(string playerId, out IVfxPulsePlayer player, out string failureReason)
            {
                player = null;
                failureReason = "VFX player factory is not configured.";
                return false;
            }

            public bool TryCreateStatePlayer(string playerId, out IVfxStatePlayer player, out string failureReason)
            {
                player = null;
                failureReason = "VFX player factory is not configured.";
                return false;
            }
        }

        public sealed class DefaultVfxPlayerFactory : IVfxPlayerFactory
        {
            private readonly VfxSpriteSheetPlayerFactory mSpriteSheetFactory = new VfxSpriteSheetPlayerFactory();
            private readonly VfxGoldFlightPlayerFactory mGoldFlightFactory = new VfxGoldFlightPlayerFactory();

            public bool TryCreatePulsePlayer(string playerId, out IVfxPulsePlayer player, out string failureReason)
            {
                player = null;
                failureReason = string.Empty;
                if (!VfxPlayerRegistry.IsKnownPlayerId(playerId))
                {
                    failureReason = "播放器未注册。";
                    return false;
                }

                if (string.Equals(playerId, VfxPlayerRegistry.SpriteSheet, StringComparison.Ordinal))
                {
                    return mSpriteSheetFactory.TryCreatePulsePlayer(playerId, out player, out failureReason);
                }

                if (string.Equals(playerId, VfxPlayerRegistry.GoldFlight, StringComparison.Ordinal))
                {
                    return mGoldFlightFactory.TryCreatePulsePlayer(playerId, out player, out failureReason);
                }

                failureReason = "播放器尚未实现。";
                return false;
            }

            public bool TryCreateStatePlayer(string playerId, out IVfxStatePlayer player, out string failureReason)
            {
                player = null;
                failureReason = string.Empty;
                if (!VfxPlayerRegistry.SupportsState(playerId))
                {
                    failureReason = "播放器未注册或不支持 State。";
                    return false;
                }

                if (string.Equals(playerId, VfxPlayerRegistry.SpriteSheet, StringComparison.Ordinal))
                {
                    return mSpriteSheetFactory.TryCreateStatePlayer(playerId, out player, out failureReason);
                }

                failureReason = "播放器尚未实现。";
                return false;
            }
        }

        private sealed class UnityVfxCueScheduler : MonoBehaviour, IVfxCueScheduler
        {
            private static UnityVfxCueScheduler sInstance;
            private long mNextKey = 1;
            private readonly Dictionary<long, Coroutine> mActive = new Dictionary<long, Coroutine>();

            public static UnityVfxCueScheduler Instance
            {
                get
                {
                    if (sInstance != null)
                    {
                        return sInstance;
                    }

                    var host = new GameObject(nameof(UnityVfxCueScheduler));
                    DontDestroyOnLoad(host);
                    sInstance = host.AddComponent<UnityVfxCueScheduler>();
                    return sInstance;
                }
            }

            public VfxScheduleKey Schedule(float delaySeconds, Action callback)
            {
                if (callback == null)
                {
                    return default;
                }

                var key = new VfxScheduleKey(mNextKey++);
                mActive[key.Value] = StartCoroutine(Run(delaySeconds, key, callback));
                return key;
            }

            public bool Cancel(VfxScheduleKey key)
            {
                if (!key.IsValid || !mActive.TryGetValue(key.Value, out var coroutine))
                {
                    return false;
                }

                if (coroutine != null)
                {
                    StopCoroutine(coroutine);
                }

                mActive.Remove(key.Value);
                return true;
            }

            private System.Collections.IEnumerator Run(float delaySeconds, VfxScheduleKey key, Action callback)
            {
                if (delaySeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(delaySeconds);
                }

                mActive.Remove(key.Value);
                callback?.Invoke();
            }
        }
    }
}
