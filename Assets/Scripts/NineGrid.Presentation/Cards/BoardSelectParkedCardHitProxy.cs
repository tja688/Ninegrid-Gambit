using NineGrid.Flow;
using NineGrid.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards
{
    /// <summary>
    /// 多选模式下驻留于玩家格的效果卡点击代理：点击即反悔回手。
    /// 由 <see cref="PointerHitRouter"/> 轮询驱动（不再使用 OnMouse*）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class BoardSelectParkedCardHitProxy : MonoBehaviour, IPointerHitTarget
    {
        private const int TypePriority = 40;

        private BoxCollider2D _collider;
        private CardVisualDriver _driver;
        private bool _armed;

        public Collider2D HitCollider => _collider != null ? _collider : (_collider = GetComponent<BoxCollider2D>());

        public int HitSortOrder => ResolveHitSortOrder();

        public int HitTypePriority => TypePriority;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            _driver = GetComponent<CardVisualDriver>();
            SetArmed(false);
        }

        private void OnEnable()
        {
            PointerHitRegistry.Register(this);
        }

        private void OnDisable()
        {
            PointerHitRegistry.Unregister(this);
        }

        public void SetArmed(bool armed)
        {
            _armed = armed;
            if (_collider != null)
            {
                _collider.enabled = armed;
            }
        }

        public void HandlePointerEnter()
        {
        }

        public void HandlePointerExit()
        {
        }

        public void HandlePointerDown()
        {
            if (!_armed || !PresentationInputGates.BoardSelectModeActive)
            {
                return;
            }

            _driver ??= GetComponent<CardVisualDriver>();
            var uid = _driver?.BoundCard?.Uid ?? 0;
            if (uid <= 0)
            {
                return;
            }

            BoardCardSelectModeController.TryAbortByParkedItemClick(uid);
        }

        private int ResolveHitSortOrder()
        {
            var group = GetComponent<SortingGroup>();
            if (group != null)
            {
                return group.sortingOrder;
            }

            var renderer = GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null ? renderer.sortingOrder : 0;
        }
    }
}
