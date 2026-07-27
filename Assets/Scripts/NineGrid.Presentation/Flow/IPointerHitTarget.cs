using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 轮询命中目标：替代 OnMouse*，由 <see cref="PointerHitRouter"/> 驱动。
    /// </summary>
    public interface IPointerHitTarget
    {
        Collider2D HitCollider { get; }

        /// <summary>越大越优先（通常取 SortingGroup / SpriteRenderer.sortingOrder）。</summary>
        int HitSortOrder { get; }

        /// <summary>同类 sort 打平时的类型优先级，越大越优先。</summary>
        int HitTypePriority { get; }

        void HandlePointerEnter();

        void HandlePointerExit();

        void HandlePointerDown();
    }
}
