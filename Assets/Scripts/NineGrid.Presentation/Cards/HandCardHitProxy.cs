using NineGrid.Flow;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards
{
    /// <summary>
    /// 手牌拖拽命中代理：挂在 Standard Card 根节点；hover 由 CardHandManagerSingleton 集中解析。
    /// 由 <see cref="PointerHitRouter"/> 轮询驱动（不再使用 OnMouse*）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(CardVisualDriver))]
    public sealed class HandCardHitProxy : MonoBehaviour, IPointerHitTarget
    {
        private const int TypePriority = 20;

        private BoxCollider2D _collider;
        private CardVisualDriver _driver;

        public Collider2D HitCollider => _collider != null ? _collider : (_collider = GetComponent<BoxCollider2D>());

        public int HitSortOrder => ResolveHitSortOrder();

        public int HitTypePriority => TypePriority;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            _driver = GetComponent<CardVisualDriver>();
            ApplyColliderSize();
        }

        private void OnEnable()
        {
            PointerHitRegistry.Register(this);
        }

        private void OnDisable()
        {
            PointerHitRegistry.Unregister(this);
        }

        public void ApplyColliderSize()
        {
            _collider ??= GetComponent<BoxCollider2D>();
            if (_collider == null)
            {
                return;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            var size = field != null
                ? field.LayoutSettings.slotHitBoxSize
                : new Vector2(1.6f, 2.2f);

            _collider.size = size;
            _collider.isTrigger = false;
        }

        public void HandlePointerEnter()
        {
        }

        public void HandlePointerExit()
        {
        }

        public void HandlePointerDown()
        {
            // 主路径：PointerHitRouter 在按下时优先 TryBeginDragFromHoveredCard。
            // 此处作 collider 命中回退（无 hover / 测试夹具）。
            if (!CanRespond())
            {
                return;
            }

            var manager = CardEntityLifecycleHook.HandOrNull();
            manager?.TryBeginDragFromHand(_driver.BoundCard);
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

        private bool CanRespond()
        {
            var manager = CardEntityLifecycleHook.HandOrNull();
            if (manager == null || manager.IsBusy || manager.IsDragging)
            {
                return false;
            }

            _driver ??= GetComponent<CardVisualDriver>();
            return _driver != null && _driver.IsHandHoverEligible;
        }
    }
}
