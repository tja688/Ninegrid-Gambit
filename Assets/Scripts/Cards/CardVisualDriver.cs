using DG.Tweening;
using UnityEngine;

namespace NineGrid.Cards
{
    public enum CardVisualTarget
    {
        Base = 0,
        Hover = 1,
        Selected = 2,
    }

    /// <summary>
    /// 卡牌视觉目标驱动：SetTarget + Kill 覆盖，不依赖 OnExit 回退动画。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardVisualDriver : MonoBehaviour
    {
        private ManagedCard _card;
        private Transform _transform;
        private Sequence _feedbackSequence;
        private CardVisualTarget _currentTarget = CardVisualTarget.Base;
        private Vector3 _handHoverBaseWorldPosition;

        public CardVisualTarget CurrentTarget => _currentTarget;

        public ManagedCard BoundCard => _card;

        public bool IsGroundHoverEligible =>
            _card != null && _card.DisplayMode == CardDisplayMode.GroundCardMode;

        public bool IsHandHoverEligible =>
            _card != null && _card.DisplayMode == CardDisplayMode.HandCardMode;

        private void Awake()
        {
            _transform = transform;
        }

        public void Bind(ManagedCard card)
        {
            _card = card;
        }

        public void InterruptFeedbackMotion()
        {
            KillFeedbackMotion();
        }

        public void SetTarget(CardVisualTarget target)
        {
            if (_transform == null)
            {
                _transform = transform;
            }

            if (ShouldSuppressHover(target))
            {
                return;
            }

            if (_currentTarget == target)
            {
                return;
            }

            _currentTarget = target;
            KillFeedbackMotion();

            switch (target)
            {
                case CardVisualTarget.Hover:
                    PlayHover();
                    break;

                case CardVisualTarget.Selected:
                    PlaySelected();
                    break;

                case CardVisualTarget.Base:
                    PlayBase();
                    break;
            }
        }

        public bool IsSelectedVisual => _currentTarget == CardVisualTarget.Selected;

        public void SnapToDisplayMode()
        {
            if (_transform == null)
            {
                _transform = transform;
            }

            _currentTarget = CardVisualTarget.Base;
            KillFeedbackMotion();

            var mode = _card?.DisplayMode ?? CardDisplayMode.HandCardMode;
            _transform.localScale = CardDisplayModeVisuals.GetBaseLocalScale(mode);
            _transform.localRotation = Quaternion.identity;

            if (mode == CardDisplayMode.HandCardMode && TryResolveHandLayoutPosition(out var layoutPosition))
            {
                _handHoverBaseWorldPosition = layoutPosition;
                _transform.position = layoutPosition;
            }
        }

        private void PlayHover()
        {
            if (_card?.DisplayMode == CardDisplayMode.HandCardMode)
            {
                PlayHandHover();
                return;
            }

            PlayGroundHover();
        }

        private void PlaySelected()
        {
            if (_card?.DisplayMode == CardDisplayMode.HandCardMode)
            {
                PlayHandHover();
                return;
            }

            PlayGroundSelected();
        }

        private void PlayGroundSelected()
        {
            var settings = ResolveGroundLayoutSettings();
            var baseScale = GetBaseScale();
            var hoverScale = baseScale * (1f + settings.hoverScaleIntensity);

            _transform.localRotation = Quaternion.identity;

            var sequence = DOTween.Sequence()
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

            sequence.Join(
                _transform
                    .DOScale(hoverScale, settings.hoverEnterDuration)
                    .SetEase(Ease.OutBack));

            _feedbackSequence = sequence;
        }

        private void PlayGroundHover()
        {
            var settings = ResolveGroundLayoutSettings();
            var baseScale = GetBaseScale();
            var hoverScale = baseScale * (1f + settings.hoverScaleIntensity);

            _transform.localRotation = Quaternion.identity;

            var sequence = DOTween.Sequence()
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

            sequence.Join(
                _transform
                    .DOScale(hoverScale, settings.hoverEnterDuration)
                    .SetEase(Ease.OutBack));
            sequence.Join(
                _transform
                    .DOPunchRotation(
                        new Vector3(0f, 0f, settings.hoverPunchAngle),
                        settings.hoverEnterDuration,
                        settings.hoverPunchVibrato,
                        0.5f));

            _feedbackSequence = sequence;
        }

        private void PlayHandHover()
        {
            var settings = ResolveHandLayoutSettings();
            var baseScale = GetBaseScale();
            var hoverScale = baseScale * (1f + settings.hoverScaleIntensity);

            if (!TryResolveHandLayoutPosition(out _handHoverBaseWorldPosition))
            {
                _handHoverBaseWorldPosition = _transform.position;
            }
            else
            {
                _transform.position = _handHoverBaseWorldPosition;
            }

            var hoverPosition = _handHoverBaseWorldPosition + Vector3.up * settings.hoverPopYOffset;

            _transform.localRotation = Quaternion.identity;

            var sequence = DOTween.Sequence()
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

            sequence.Join(
                _transform
                    .DOMove(hoverPosition, settings.hoverEnterDuration)
                    .SetEase(Ease.OutBack));
            sequence.Join(
                _transform
                    .DOScale(hoverScale, settings.hoverEnterDuration)
                    .SetEase(Ease.OutBack));
            sequence.Join(
                _transform
                    .DOPunchRotation(
                        new Vector3(0f, 0f, settings.hoverPunchAngle),
                        settings.hoverEnterDuration,
                        settings.hoverPunchVibrato,
                        0.5f));

            _feedbackSequence = sequence;
        }

        private void PlayBase()
        {
            if (_card?.DisplayMode == CardDisplayMode.HandCardMode)
            {
                PlayHandBase();
                return;
            }

            PlayGroundBase();
        }

        private void PlayGroundBase()
        {
            var settings = ResolveGroundLayoutSettings();
            var baseScale = GetBaseScale();

            _transform.localRotation = Quaternion.identity;
            _feedbackSequence = DOTween.Sequence()
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            _feedbackSequence.Append(
                _transform
                    .DOScale(baseScale, settings.hoverExitDuration)
                    .SetEase(Ease.OutQuad));
        }

        private void PlayHandBase()
        {
            var settings = ResolveHandLayoutSettings();
            var baseScale = GetBaseScale();

            if (!TryResolveHandLayoutPosition(out _handHoverBaseWorldPosition))
            {
                _handHoverBaseWorldPosition = _transform.position;
            }

            _transform.localRotation = Quaternion.identity;
            _feedbackSequence = DOTween.Sequence()
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            _feedbackSequence.Join(
                _transform
                    .DOMove(_handHoverBaseWorldPosition, settings.hoverExitDuration)
                    .SetEase(Ease.OutQuad));
            _feedbackSequence.Join(
                _transform
                    .DOScale(baseScale, settings.hoverExitDuration)
                    .SetEase(Ease.OutQuad));
        }

        private bool TryResolveHandLayoutPosition(out Vector3 layoutPosition)
        {
            layoutPosition = default;
            if (_card == null)
            {
                return false;
            }

            var hand = CardHandManagerSingleton.Instance;
            return hand != null && hand.TryGetHandLayoutWorldPosition(_card, out layoutPosition);
        }

        private bool ShouldSuppressHover(CardVisualTarget target)
        {
            if (target != CardVisualTarget.Hover)
            {
                return false;
            }

            var effectManager = GetComponent<CardEffectManager>();
            return effectManager != null && effectManager.TryConsumeHoverSuppression();
        }

        private void KillFeedbackMotion()
        {
            if (_feedbackSequence != null && _feedbackSequence.IsActive())
            {
                _feedbackSequence.Kill();
            }

            _feedbackSequence = null;
        }

        private Vector3 GetBaseScale()
        {
            var mode = _card?.DisplayMode ?? CardDisplayMode.GroundCardMode;
            return CardDisplayModeVisuals.GetBaseLocalScale(mode);
        }

        private static GroundFieldLayoutSettings ResolveGroundLayoutSettings()
        {
            var field = GroundFieldGeometryHook.FieldOrNull();
            return field != null ? field.LayoutSettings : new GroundFieldLayoutSettings();
        }

        private static CardHandLayoutSettings ResolveHandLayoutSettings()
        {
            var hand = CardHandManagerSingleton.Instance;
            return hand != null ? hand.LayoutSettings : new CardHandLayoutSettings();
        }
    }

    internal static class CardDisplayModeVisuals
    {
        private const float DragCardScale = 1.06f;

        public static Vector3 GetBaseLocalScale(CardDisplayMode mode)
        {
            return mode switch
            {
                CardDisplayMode.CardDeckMode => Vector3.one,
                CardDisplayMode.HandCardMode => Vector3.one,
                CardDisplayMode.GroundCardMode => Vector3.one,
                CardDisplayMode.RemovedMode => Vector3.one * 1.08f,
                CardDisplayMode.DragCardMode => Vector3.one * DragCardScale,
                _ => Vector3.one,
            };
        }

        /// <summary>场地普通卡默认 order。</summary>
        public const int GroundCardSortingOrder = -10;

        /// <summary>Avatar 相对场地其他卡默认高一层，交战重叠时压在邻格之上。</summary>
        public const int GroundAvatarSortingOrder = GroundCardSortingOrder + 1;

        public static int GetSortingOrder(CardDisplayMode mode)
        {
            return GetSortingOrder(mode, CardPresentationKind.Unknown);
        }

        public static int GetSortingOrder(CardDisplayMode mode, CardPresentationKind kind)
        {
            return mode switch
            {
                CardDisplayMode.CardDeckMode => -30,
                CardDisplayMode.HandCardMode => 0,
                CardDisplayMode.GroundCardMode => kind == CardPresentationKind.Avatar
                    ? GroundAvatarSortingOrder
                    : GroundCardSortingOrder,
                CardDisplayMode.RemovedMode => 20,
                CardDisplayMode.DragCardMode => 30,
                _ => 0,
            };
        }
    }
}
