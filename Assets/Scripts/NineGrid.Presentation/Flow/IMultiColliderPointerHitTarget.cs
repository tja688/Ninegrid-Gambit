using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 多框 / 自定义命中表面（场地面九格）：Router 优先走本接口，而非单一 <see cref="IPointerHitTarget.HitCollider"/>。
    /// </summary>
    public interface IMultiColliderPointerHitTarget
    {
        bool TryOverlapScreenPoint(Camera camera, Vector2 screen, out Vector3 world);
    }
}
