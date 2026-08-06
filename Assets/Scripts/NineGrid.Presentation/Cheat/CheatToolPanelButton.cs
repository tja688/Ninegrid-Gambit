#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.Presentation.Cheat
{
    /// <summary>
    /// 作弊面板一级菜单命中代理：挂在场景预置的 Sprite + <see cref="BoxCollider2D"/> 按钮上，
    /// 由 <see cref="PointerHitRouter"/> 驱动（与局内半黑屏 / 详述同一命中路径）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class CheatToolPanelButton : MonoBehaviour, IPointerHitTarget
    {
        /// <summary>
        /// 高于场地面 / 手牌常见 sortingOrder，保证面板打开时点得中（Overlay 类型优先级本就低于 Field）。
        /// </summary>
        public const int HitSort = 10000;

        private BoxCollider2D _collider;
        private Action _onClick;
        private int _hitSortOrder = HitSort;

        public Collider2D HitCollider =>
            _collider != null ? _collider : (_collider = GetComponent<BoxCollider2D>());

        public int HitSortOrder => _hitSortOrder;

        public int HitTypePriority => PointerHitSurfacePriorities.Overlay;

        public void Bind(Action onClick, int hitSortOrder = HitSort)
        {
            _onClick = onClick;
            _hitSortOrder = hitSortOrder;
        }

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            if (_collider != null)
            {
                _collider.isTrigger = false;
            }
        }

        private void OnEnable()
        {
            PointerHitRegistry.Register(this);
        }

        private void OnDisable()
        {
            PointerHitRegistry.Unregister(this);
        }

        public void HandlePointerEnter()
        {
        }

        public void HandlePointerExit()
        {
        }

        public void HandlePointerDown()
        {
            _onClick?.Invoke();
        }
    }
}

#endif
