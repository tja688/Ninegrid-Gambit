using DG.Tweening;
using UnityEngine;

namespace NineGrid.Cards
{
    public enum CardVisualTarget
    {
        Base = 0,
        Hover = 1,
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

        public CardVisualTarget CurrentTarget => _currentTarget;

        public ManagedCard BoundCard => _card;

        public bool IsGroundHoverEligible =>
            _card != null && _card.DisplayMode == CardDisplayMode.GroundCardMode;

        private void Awake()
        {
            _transform = transform;
        }

        public void Bind(ManagedCard card)
        {
            _card = card;
        }

        public void SetTarget(CardVisualTarget target)
        {
            if (_transform == null)
            {
                _transform = transform;
            }

            _currentTarget = target;
            KillFeedbackMotion();

            switch (target)
            {
                case CardVisualTarget.Hover:
                    PlayHover();
                    break;

                case CardVisualTarget.Base:
                    PlayBase();
                    break;
            }
        }

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
        }

        private void PlayHover()
        {
            var settings = ResolveLayoutSettings();
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

        private void PlayBase()
        {
            var settings = ResolveLayoutSettings();
            var baseScale = GetBaseScale();

            _transform.localRotation = Quaternion.identity;
            _feedbackSequence = DOTween.Sequence()
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            _feedbackSequence.Append(
                _transform
                    .DOScale(baseScale, settings.hoverExitDuration)
                    .SetEase(Ease.OutQuad));
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

        private static GroundFieldLayoutSettings ResolveLayoutSettings()
        {
            var field = GroundFieldManagerSingleton.Instance;
            return field != null ? field.LayoutSettings : new GroundFieldLayoutSettings();
        }
    }

    internal static class CardDisplayModeVisuals
    {
        private const float DragCardScale = 1.06f;

        public static Vector3 GetBaseLocalScale(CardDisplayMode mode)
        {
            return mode switch
            {
                CardDisplayMode.CardDeckMode => Vector3.one * 0.55f,
                CardDisplayMode.HandCardMode => Vector3.one,
                CardDisplayMode.GroundCardMode => Vector3.one,
                CardDisplayMode.RemovedMode => Vector3.one * 1.08f,
                CardDisplayMode.DragCardMode => Vector3.one * DragCardScale,
                _ => Vector3.one,
            };
        }

        public static int GetSortingOrder(CardDisplayMode mode)
        {
            return mode switch
            {
                CardDisplayMode.CardDeckMode => -30,
                CardDisplayMode.HandCardMode => 0,
                CardDisplayMode.GroundCardMode => -10,
                CardDisplayMode.RemovedMode => 20,
                CardDisplayMode.DragCardMode => 30,
                _ => 0,
            };
        }
    }
}
