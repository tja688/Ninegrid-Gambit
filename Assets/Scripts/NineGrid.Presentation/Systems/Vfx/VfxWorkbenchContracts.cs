#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace NineGrid.Presentation.Systems
{
    public sealed class VfxCueAggregate
    {
        public string AggregateKey { get; internal set; }
        public string BindingKey { get; internal set; }
        public string CueOrStateId { get; internal set; }
        public bool IsPulse { get; internal set; }
        public int Requested { get; internal set; }
        public int Played { get; internal set; }
        public int Suppressed { get; internal set; }
        public int Unbound { get; internal set; }
        public int InvalidBinding { get; internal set; }
        public int PlayerUnavailable { get; internal set; }
        public int DomainUnavailable { get; internal set; }
        public int BackendFailure { get; internal set; }
        public double LastTime { get; internal set; }
        public string LastFailureReason { get; internal set; }
        public IReadOnlyList<double> RecentTimestamps { get; internal set; }
    }

    public sealed class VfxWorkbenchActivePulse
    {
        public string InstanceId { get; internal set; }
        public string BindingKey { get; internal set; }
        public string CueId { get; internal set; }
        public string PlayerId { get; internal set; }
        public string MaterialKey { get; internal set; }
        public string SpatialOwnership { get; internal set; }
    }

    public sealed class VfxWorkbenchActiveStateSlot
    {
        public string OwnerLabel { get; internal set; }
        public string Slot { get; internal set; }
        public string StateId { get; internal set; }
        public string BindingKey { get; internal set; }
        public string PlayerId { get; internal set; }
        public string InstanceId { get; internal set; }
        public string SpatialOwnership { get; internal set; }
    }

    public sealed class VfxWorkbenchSnapshot
    {
        public long Revision { get; internal set; }
        public IReadOnlyList<VfxHistoryRecord> History { get; internal set; }
        public IReadOnlyList<VfxWorkbenchActivePulse> ActivePulses { get; internal set; }
        public IReadOnlyList<VfxWorkbenchActiveStateSlot> ActiveStateSlots { get; internal set; }
        public IReadOnlyList<VfxCueAggregate> Aggregates { get; internal set; }
        public Systems.Vfx.VfxDiagnosticsSnapshot Diagnostics { get; internal set; }
    }

    public sealed class VfxWorkbenchApplyResult
    {
        public bool Succeeded { get; internal set; }
        public long Revision { get; internal set; }
        public string Error { get; internal set; }
    }

    public sealed class VfxWorkbenchPreviewResult
    {
        public bool Succeeded { get; internal set; }
        public string Channel { get; internal set; }
        public string BindingKey { get; internal set; }
        public string PlayerId { get; internal set; }
        public string MaterialKey { get; internal set; }
        public string VariantId { get; internal set; }
        public string InstanceId { get; internal set; }
        public string FailureReason { get; internal set; }
    }
}
#endif
