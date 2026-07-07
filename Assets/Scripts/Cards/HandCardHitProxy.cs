using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 手牌 hover / 拖拽命中代理：挂在 Standard Card 根节点。
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

            var field = GroundFieldManagerSingleton.Instance;
            var size = field != null
                ? field.LayoutSettings.slotHitBoxSize
                : new Vector2(1.6f, 2.2f);

            _collider.size = size;
            _collider.isTrigger = false;
        }

        private void OnMouseEnter()
        {
            if (!CanRespond())
            {
                return;
            }

            var manager = CardHandManagerSingleton.Instance;
            manager?.OnHandCardHoverEnter(_driver.BoundCard);
        }

        private void OnMouseExit()
        {
            if (!CanRespondToExit())
            {
                return;
            }

            var manager = CardHandManagerSingleton.Instance;
            manager?.OnHandCardHoverExit(_driver.BoundCard);
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

        private bool CanRespondToExit()
        {
            if (CardHandManagerSingleton.Instance?.IsDragging == true)
            {
                return false;
            }

            _driver ??= GetComponent<CardVisualDriver>();
            return _driver != null && _driver.IsHandHoverEligible;
        }
    }
}
