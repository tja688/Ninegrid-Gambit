using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 手牌拖拽命中代理：挂在 Standard Card 根节点；hover 由 CardHandManagerSingleton 集中解析。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(CardVisualDriver))]
    public sealed class HandCardHitProxy : MonoBehaviour
    {
        private BoxCollider2D _collider;
        private CardVisualDriver _driver;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            _driver = GetComponent<CardVisualDriver>();
            ApplyColliderSize();
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

        private void OnMouseDown()
        {
            if (!CanRespond())
            {
                return;
            }

            var manager = CardHandManagerSingleton.Instance;
            manager?.TryBeginDragFromHand(_driver.BoundCard);
        }

        private bool CanRespond()
        {
            var manager = CardHandManagerSingleton.Instance;
            if (manager == null || manager.IsBusy || manager.IsDragging)
            {
                return false;
            }

            _driver ??= GetComponent<CardVisualDriver>();
            return _driver != null && _driver.IsHandHoverEligible;
        }
    }
}
