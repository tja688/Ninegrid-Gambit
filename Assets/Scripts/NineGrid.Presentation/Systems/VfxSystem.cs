using System;
using System.Collections.Generic;
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
#endif
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
#endif

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
                return ClearSlotInternal(key, expectedState: null, conditional: false);
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
                BeginStateExit(existing);
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
            return ClearSlotInternal(key, expectedState, conditional: true);
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
                ClearSlotInternal(keys[i], expectedState: null, conditional: false);
            }
        }

        public VfxCueResult RequestCue(VfxCueRequest request, VfxSpatialContext spatialContext = default)
        {
            var requestedAt = mClock.UnscaledTime;
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
                    return RecordInvalidBinding(request, resolveError);
                }

                var reason = resolveError?.Message ?? "视觉特效 cue 绑定不存在。";
                return RecordUnbound(request, reason);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PatchLatestRequested(binding);
#endif

            if (!binding.Enabled)
            {
                return RecordSuppressed(request, binding);
            }

            if (!VfxPlayerRegistry.SupportsPulse(binding.PlayerId))
            {
                return RecordPlayerUnavailable(request, binding, "播放器未注册或不支持 Pulse。");
            }

            if (binding.SpatialOwnership == VfxSpatialOwnership.Attached
                && !TryAcceptAttachedSpatial(spatialContext, out var attachedFailure))
            {
                return RecordDomainUnavailable(request, binding, attachedFailure);
            }

            var acceptedSpatial = AcceptSpatial(binding, spatialContext);
            var now = mClock.UnscaledTime;
            if (binding.MinimumIntervalSeconds > 0f
                && mLastPlayedAt.TryGetValue(binding, out var lastPlayedAt)
                && now >= lastPlayedAt
                && now - lastPlayedAt < binding.MinimumIntervalSeconds)
            {
                return RecordSuppressed(request, binding, "minimum interval");
            }

            if (binding.BindingDelaySeconds > 0f)
            {
                var capturedBinding = binding;
                var capturedSpatial = acceptedSpatial;
                var scheduler = mScheduler ?? UnityVfxCueScheduler.Instance;
                VfxScheduleKey delayKey = default;
                delayKey = scheduler.Schedule(binding.BindingDelaySeconds, () =>
                {
                    StartResolvedCue(request, capturedBinding, capturedSpatial, mClock.UnscaledTime);
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

            return StartResolvedCue(request, binding, acceptedSpatial, now);
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
                        mActiveInstances.RemoveAt(i);
                        continue;
                    }

                    if (instance.Player.Tick(deltaTime))
                    {
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
                    if (slot.Player == null || slot.Player.Tick(deltaTime))
                    {
                        if (completedKeys == null)
                        {
                            completedKeys = new List<VfxSlotKey>();
                        }

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

            if (mExitingStateSlots.Count == 0)
            {
                return;
            }

            for (var i = mExitingStateSlots.Count - 1; i >= 0; i--)
            {
                var slot = mExitingStateSlots[i];
                if (slot.Player == null || slot.Player.Tick(deltaTime))
                {
                    mExitingStateSlots.RemoveAt(i);
                }
            }
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
                ClearSlotInternal(attachedKeys[i], expectedState: null, conditional: false);
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
            double now)
        {
            if (!mPlayerFactory.TryCreatePulsePlayer(binding.PlayerId, out var player, out var factoryReason))
            {
                return RecordPlayerUnavailable(request, binding, factoryReason);
            }

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
                    return RecordUnbound(request, "视觉特效绑定缺少有效素材。");
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
                    binding,
                    variantId,
                    materialKey,
                    backend.FailureReason);
            }

            mLastPlayedAt[binding] = now;
            if (!string.IsNullOrEmpty(variantId))
            {
                mLastVariantIds[binding] = variantId;
            }

            mActiveInstances.Add(new ActivePulseInstance
            {
                InstanceId = backend.InstanceId,
                Ownership = binding.SpatialOwnership,
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
                    spatialContext.DiagnosticOwnerUid);
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
            bool conditional)
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
            BeginStateExit(slot);
            return new VfxStateSlotResult
            {
                Outcome = VfxStateSlotOutcome.Cleared,
                StateId = clearedState,
                BindingKey = slot.BindingKey,
                PlayerId = slot.PlayerId,
                InstanceId = slot.InstanceId,
            };
        }

        private void BeginStateExit(ActiveStateSlot slot)
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
            if (!mCatalog.TryResolveStateStrict(request, out var binding, out var resolveError))
            {
                if (resolveError != null && resolveError.Code == VfxBindingResolveCode.Ambiguous)
                {
                    return RecordStateInvalidBinding(request, resolveError);
                }

                var reason = resolveError?.Message ?? "持续视觉状态绑定不存在。";
                return RecordStateUnbound(request, reason);
            }

            if (!binding.Enabled)
            {
                return RecordStateSuppressed(request, binding);
            }

            if (!VfxPlayerRegistry.SupportsState(binding.PlayerId))
            {
                return RecordStatePlayerUnavailable(request, binding, "播放器未注册或不支持 State。");
            }

            if (binding.SpatialOwnership == VfxSpatialOwnership.Attached
                && !TryAcceptAttachedSpatial(spatialContext, out var attachedFailure))
            {
                return RecordStateDomainUnavailable(request, binding, attachedFailure);
            }

            var acceptedSpatial = AcceptStateSpatial(binding, spatialContext);
            if (!mPlayerFactory.TryCreateStatePlayer(binding.PlayerId, out var player, out var factoryReason))
            {
                return RecordStatePlayerUnavailable(request, binding, factoryReason);
            }

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
                    return RecordStateUnbound(request, "视觉特效绑定缺少有效素材。");
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
                    binding,
                    variantId,
                    materialKey,
                    backend.FailureReason);
            }

            var key = new VfxSlotKey(owner, slot);
            var activeSlot = new ActiveStateSlot
            {
                Key = key,
                DesiredStateId = desiredState,
                Binding = binding,
                BindingKey = binding.BindingKey,
                PlayerId = binding.PlayerId,
                InstanceId = backend.InstanceId,
                Player = player,
                SpatialOwnership = binding.SpatialOwnership,
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
                    spatialContext.DiagnosticOwnerUid);
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

        private VfxStateSlotResult RecordStateUnbound(VfxStateRequest request, string reason)
        {
            return new VfxStateSlotResult
            {
                Outcome = VfxStateSlotOutcome.Unbound,
                StateId = request.StateId,
                FailureReason = reason,
            };
        }

        private VfxStateSlotResult RecordStateInvalidBinding(
            VfxStateRequest request,
            VfxBindingResolveError resolveError)
        {
            var reason = resolveError?.Message ?? "绑定解析歧义。";
            return new VfxStateSlotResult
            {
                Outcome = VfxStateSlotOutcome.InvalidBinding,
                StateId = request.StateId,
                BindingKey = resolveError?.BindingKey ?? string.Empty,
                FailureReason = reason,
            };
        }

        private VfxStateSlotResult RecordStateSuppressed(VfxStateRequest request, VfxStateBinding binding)
        {
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
            VfxStateBinding binding,
            string reason)
        {
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
            VfxStateBinding binding,
            string reason)
        {
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
            VfxStateBinding binding,
            string variantId,
            string materialKey,
            string reason)
        {
            return new VfxStateSlotResult
            {
                Outcome = VfxStateSlotOutcome.BackendFailure,
                StateId = request.StateId,
                BindingKey = binding.BindingKey,
                PlayerId = binding.PlayerId,
                FailureReason = reason,
            };
        }

        private VfxCueResult RecordUnbound(VfxCueRequest request, string reason)
        {
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

        private VfxCueResult RecordInvalidBinding(VfxCueRequest request, VfxBindingResolveError resolveError)
        {
            var reason = resolveError?.Message ?? "绑定解析歧义。";
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
            VfxCueBinding binding,
            string reason = SuppressedReason)
        {
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
            VfxCueBinding binding,
            string reason)
        {
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
            VfxCueBinding binding,
            string reason)
        {
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
            VfxCueBinding binding,
            string variantId,
            string materialKey,
            string reason)
        {
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
        }
#endif

        private sealed class ActiveStateSlot
        {
            public VfxSlotKey Key;
            public string DesiredStateId;
            public VfxStateBinding Binding;
            public string BindingKey;
            public string PlayerId;
            public string InstanceId;
            public IVfxStatePlayer Player;
            public VfxSpatialOwnership SpatialOwnership;
            public bool Exiting;
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
            public string InstanceId;
            public VfxSpatialOwnership Ownership;
            public IVfxPulsePlayer Player;
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
