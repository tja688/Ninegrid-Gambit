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
            int diagnosticOwnerUid = 0)
        {
            SemanticRole = semanticRole ?? string.Empty;
            DomainHost = domainHost;
            PositionSnapshot = positionSnapshot;
            DiagnosticOwnerUid = diagnosticOwnerUid > 0 ? diagnosticOwnerUid : 0;
        }

        public string SemanticRole { get; }
        public IVfxDomainHost DomainHost { get; }
        public Vector3? PositionSnapshot { get; }
        public int DiagnosticOwnerUid { get; }

        public bool HasDomainHost => DomainHost != null;
    }
}
