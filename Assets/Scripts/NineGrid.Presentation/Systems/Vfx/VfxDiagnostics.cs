using System;
using System.Collections.Generic;
using NineGrid.Content.Vfx;

namespace NineGrid.Presentation.Systems.Vfx
{
    public enum VfxLifecyclePhase
    {
        Request,
        Resolve,
        Create,
        Start,
        Complete,
    }

    public enum VfxEndReason
    {
        None,
        NaturalComplete,
        Suppressed,
        AttachedHostLost,
        SceneExit,
        OwnerReleased,
        SlotCleared,
        SlotReplaced,
        ScheduledCancelled,
        Unbound,
        InvalidBinding,
        PlayerUnavailable,
        DomainUnavailable,
        BackendFailure,
    }

    public static class VfxEndReasons
    {
        public static bool IsIssue(VfxEndReason reason)
        {
            switch (reason)
            {
                case VfxEndReason.Unbound:
                case VfxEndReason.InvalidBinding:
                case VfxEndReason.PlayerUnavailable:
                case VfxEndReason.DomainUnavailable:
                case VfxEndReason.BackendFailure:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>Release 构建保留的轻量累计；不含明细环或峰值钻取。</summary>
    public sealed class VfxReleaseCounters
    {
        public long TotalRequests { get; internal set; }
        public long TotalStarted { get; internal set; }
        public long TotalCompleted { get; internal set; }
        public long TotalIssues { get; internal set; }
        public long Unbound { get; internal set; }
        public long InvalidBinding { get; internal set; }
        public long PlayerUnavailable { get; internal set; }
        public long DomainUnavailable { get; internal set; }
        public long BackendFailure { get; internal set; }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public sealed class VfxLifecycleRecord
    {
        public long Sequence { get; internal set; }
        public long CorrelationId { get; internal set; }
        public VfxLifecyclePhase Phase { get; internal set; }
        public VfxEndReason EndReason { get; internal set; }
        public bool IsIssue { get; internal set; }
        public bool IsPulse { get; internal set; }
        public string CueOrStateId { get; internal set; }
        public string BindingKey { get; internal set; }
        public string PlayerId { get; internal set; }
        public string VariantId { get; internal set; }
        public string MaterialKey { get; internal set; }
        public string InstanceId { get; internal set; }
        public string SpatialOwnership { get; internal set; }
        public string DomainLabel { get; internal set; }
        public string OwnerSlotKey { get; internal set; }
        public string DiagnosticSource { get; internal set; }
        public string CardDefId { get; internal set; }
        public string SkillId { get; internal set; }
        public string RoomId { get; internal set; }
        public string ItemDefId { get; internal set; }
        public string ContentId { get; internal set; }
        public int DiagnosticCardUid { get; internal set; }
        public string OverrideSummary { get; internal set; }
        public string FailureReason { get; internal set; }
        public double Time { get; internal set; }
    }

    public sealed class VfxFrameStats
    {
        public int FrameIndex { get; internal set; }
        public int Created { get; internal set; }
        public int Completed { get; internal set; }
        public int Active { get; internal set; }
    }

    public sealed class VfxPeakContributor
    {
        public string BindingKey { get; internal set; }
        public string PlayerId { get; internal set; }
        public string InstanceId { get; internal set; }
        public string CueOrStateId { get; internal set; }
        public bool IsPulse { get; internal set; }
    }

    public sealed class VfxPeakSnapshot
    {
        public int ActiveCount { get; internal set; }
        public int FrameIndex { get; internal set; }
        public double Time { get; internal set; }
        public IReadOnlyList<VfxPeakContributor> Contributors { get; internal set; }
    }

    public sealed class VfxBindingAggregate
    {
        public string BindingKey { get; internal set; }
        public string CueOrStateId { get; internal set; }
        public int Started { get; internal set; }
        public int Completed { get; internal set; }
        public int Issues { get; internal set; }
        public int PeakActive { get; internal set; }
    }

    public sealed class VfxPlayerAggregate
    {
        public string PlayerId { get; internal set; }
        public int Started { get; internal set; }
        public int Completed { get; internal set; }
        public int Issues { get; internal set; }
        public int PeakActive { get; internal set; }
    }

    public sealed class VfxSessionCounters
    {
        public long TotalRequests { get; internal set; }
        public long TotalStarted { get; internal set; }
        public long TotalCompleted { get; internal set; }
        public long TotalIssues { get; internal set; }
        public int PeakActive { get; internal set; }
    }

    public sealed class VfxActiveInstanceSnapshot
    {
        public string InstanceId { get; internal set; }
        public string BindingKey { get; internal set; }
        public string PlayerId { get; internal set; }
        public string CueOrStateId { get; internal set; }
        public bool IsPulse { get; internal set; }
        public string SpatialOwnership { get; internal set; }
        public string OwnerSlotKey { get; internal set; }
    }

    public sealed class VfxDiagnosticsSnapshot
    {
        public IReadOnlyList<VfxLifecycleRecord> RecentRecords { get; internal set; }
        public IReadOnlyList<VfxFrameStats> RecentFrames { get; internal set; }
        public VfxSessionCounters Session { get; internal set; }
        public VfxPeakSnapshot Peak { get; internal set; }
        public IReadOnlyList<VfxBindingAggregate> BindingAggregates { get; internal set; }
        public IReadOnlyList<VfxPlayerAggregate> PlayerAggregates { get; internal set; }
        public IReadOnlyList<VfxActiveInstanceSnapshot> ActiveInstances { get; internal set; }
    }
#endif

    public readonly struct VfxInstanceDiagnosticContext
    {
        public VfxInstanceDiagnosticContext(
            string instanceId,
            string cueOrStateId,
            bool isPulse,
            string bindingKey,
            string playerId,
            string variantId,
            string materialKey,
            VfxSpatialOwnership spatialOwnership,
            string domainLabel,
            string ownerSlotKey,
            string diagnosticSource,
            string cardDefId,
            string skillId,
            string roomId,
            string itemDefId,
            string contentId,
            int diagnosticCardUid,
            string overrideSummary)
        {
            InstanceId = instanceId ?? string.Empty;
            CueOrStateId = cueOrStateId ?? string.Empty;
            IsPulse = isPulse;
            BindingKey = bindingKey ?? string.Empty;
            PlayerId = playerId ?? string.Empty;
            VariantId = variantId ?? string.Empty;
            MaterialKey = materialKey ?? string.Empty;
            SpatialOwnership = spatialOwnership;
            DomainLabel = domainLabel ?? string.Empty;
            OwnerSlotKey = ownerSlotKey ?? string.Empty;
            DiagnosticSource = diagnosticSource ?? string.Empty;
            CardDefId = cardDefId ?? string.Empty;
            SkillId = skillId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            ItemDefId = itemDefId ?? string.Empty;
            ContentId = contentId ?? string.Empty;
            DiagnosticCardUid = diagnosticCardUid;
            OverrideSummary = overrideSummary ?? string.Empty;
        }

        public string InstanceId { get; }
        public string CueOrStateId { get; }
        public bool IsPulse { get; }
        public string BindingKey { get; }
        public string PlayerId { get; }
        public string VariantId { get; }
        public string MaterialKey { get; }
        public VfxSpatialOwnership SpatialOwnership { get; }
        public string DomainLabel { get; }
        public string OwnerSlotKey { get; }
        public string DiagnosticSource { get; }
        public string CardDefId { get; }
        public string SkillId { get; }
        public string RoomId { get; }
        public string ItemDefId { get; }
        public string ContentId { get; }
        public int DiagnosticCardUid { get; }
        public string OverrideSummary { get; }
    }

    internal static class VfxDiagnosticFormatting
    {
        public static string SpatialOwnershipLabel(VfxSpatialOwnership ownership)
        {
            return ownership == VfxSpatialOwnership.Attached ? "attached" : "independent";
        }

        public static string BuildOverrideSummary(float fps, float speed, float scale)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "fps={0:R};speed={1:R};scale={2:R}",
                fps,
                speed,
                scale);
        }

        public static string BuildDomainLabel(VfxSpatialContext spatial)
        {
            if (!spatial.HasDomainHost)
            {
                return spatial.SemanticRole ?? string.Empty;
            }

            var role = spatial.SemanticRole ?? string.Empty;
            return string.IsNullOrEmpty(role) ? "domain-host" : role;
        }
    }
}
