using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地卡牌 hover 命中代理：挂在 Standard Card 根节点，由预制体或运行时装配。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(CardVisualDriver))]
    public sealed class GroundCardHitProxy : MonoBehaviour
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
            if (!CanRespondToHover())
            {
                return;
            }

            _driver ??= GetComponent<CardVisualDriver>();
            _driver?.SetTarget(CardVisualTarget.Hover);

            var defId = _driver?.BoundCard?.DefId;
            if (!string.IsNullOrEmpty(defId))
            {
                DescriptionHoverSink.RequestShow(defId);
            }
        }

        private void OnMouseExit()
        {
            DescriptionHoverSink.RequestClear();

            if (!CanRespondToHover())
            {
                return;
            }

            _driver ??= GetComponent<CardVisualDriver>();
            _driver?.SetTarget(CardVisualTarget.Base);
        }

        private void OnMouseDown()
        {
            _driver ??= GetComponent<CardVisualDriver>();
            var card = _driver?.BoundCard;
            if (card == null)
            {
                return;
            }

            switch (card.CoreKind)
            {
                case CardPresentationKind.Avatar:
                case CardPresentationKind.Unknown:
                    return;
            }

            if (card.CoreKind == CardPresentationKind.Monster)
            {
                FieldBattleManagerSingleton.Instance?.TryHandleBattleClick(card);
                return;
            }

            if (!CanRespondToPickup())
            {
                return;
            }

            CardHandManagerSingleton.Instance?.TryBeginDragFromGround(card);
        }

        private bool CanRespondToHover()
        {
            var hand = CardHandManagerSingleton.Instance;
            if (hand != null && (hand.IsBusy || hand.IsDragging))
            {
                return false;
            }

            var field = GroundFieldManagerSingleton.Instance;
            if (field != null && field.IsBusy)
            {
                return false;
            }

            _driver ??= GetComponent<CardVisualDriver>();
            return _driver != null && _driver.IsGroundHoverEligible;
        }

        private bool CanRespondToPickup()
        {
            var hand = CardHandManagerSingleton.Instance;
            if (hand == null || !hand.CanAcceptCard || hand.IsDragging)
            {
                return false;
            }

            var field = GroundFieldManagerSingleton.Instance;
            if (field != null && field.IsBusy)
            {
                return false;
            }

            _driver ??= GetComponent<CardVisualDriver>();
            return _driver != null && _driver.IsGroundHoverEligible;
        }
    }
}
