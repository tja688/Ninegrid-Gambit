using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 多框 / 自定义命中表面（场地面九格）：Router 优先走本接口，而非单一 <see cref="IPointerHitTarget.HitCollider"/>。
    /// </summary>
    public interface IMultiColliderPointerHitTarget
    {
        bool TryOverlapScreenPoint(Camera camera, Vector2 screen, out Vector3 world);

        /// <summary>
        /// 指针仍落在同一表面上时，按本帧已解析的子目标（格号 / 认领者）刷新悬停。
        /// Router 只按表面身份做 Enter/Exit，跨格不会重入，必须由此补齐（ADR-0023 悬停与点击同源）。
        /// </summary>
        void RefreshPointerHover();
    }
}
