using UnityEngine;

namespace NineGrid.Content.Vfx
{
    /// <summary>一次 VFX 请求的运行时定位依据；UID/坐标不进绑定主键。</summary>
    public readonly struct VfxSpatialContext
    {
        public static readonly VfxSpatialContext Empty = new VfxSpatialContext(
            string.Empty,
            null,
            null,
            0);

        public VfxSpatialContext(
            string semanticRole,
            IVfxDomainHost domainHost,
            Vector3? positionSnapshot,
            int diagnosticOwnerUid = 0,
            int amount = 0,
            Vector3? targetPositionSnapshot = null)
        {
            SemanticRole = semanticRole ?? string.Empty;
            DomainHost = domainHost;
            PositionSnapshot = positionSnapshot;
            DiagnosticOwnerUid = diagnosticOwnerUid > 0 ? diagnosticOwnerUid : 0;
            Amount = amount;
            TargetPositionSnapshot = targetPositionSnapshot;
        }

        public string SemanticRole { get; }
        public IVfxDomainHost DomainHost { get; }
        public Vector3? PositionSnapshot { get; }
        public int DiagnosticOwnerUid { get; }

        /// <summary>可选语义数量（如金币增量）；程序化播放器按需消费，不进绑定主键。</summary>
        public int Amount { get; }

        /// <summary>可选目标位置快照（弹道类播放器的终点）；来源位置仍走 PositionSnapshot，不进绑定主键。</summary>
        public Vector3? TargetPositionSnapshot { get; }

        public bool HasDomainHost => DomainHost != null;

        public bool HasTargetPosition => TargetPositionSnapshot.HasValue;
    }
}
