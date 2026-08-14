using DG.Tweening;
using NineGrid.Cards.Vfx;
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
        private Vector3 _authoredBaseScale = Vector3.one;

        public CardVisualTarget CurrentTarget => _currentTarget;

        public ManagedCard BoundCard => _card;

        /// <summary>进入时预制体根缩放基准（ADR-0024）；mode/hover/hop 倍率相对此值。</summary>
        public Vector3 AuthoredBaseScale => _authoredBaseScale;

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

        /// <summary>
        /// 在 Instantiate 后、首次 ApplyDisplayMode 前捕获预制体根缩放。
        /// </summary>
        public void CaptureAuthoredBaseScale(Vector3 scale)
        {
            if (scale.sqrMagnitude <= 0.0001f)
            {
                scale = Vector3.one;
            }

            _authoredBaseScale = scale;
        }

        public void InterruptFeedbackMotion()
        {
            KillFeedbackMotion();
            if (_transform == null)
            {
                _transform = transform;
            }

            // 中断 hover/selected 时精确回到 mode 基准，避免 hop 等后续以残值当基准（ADR-0024）。
            _transform.localScale = GetBaseScale();
            _transform.localRotation = Quaternion.identity;
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
                if (target == CardVisualTarget.Base
                    && _card?.DisplayMode == CardDisplayMode.HandCardMode
                    && TryResolveHandLayoutPosition(out var layoutPosition))
                {
                    var delta = _transform.position - layoutPosition;
                    delta.z = 0f;
                    if (delta.sqrMagnitude > HandCardLifeEngagementPolicy.AnchorEngagedEpsilonSq)
                    {
                        KillFeedbackMotion();
                        PlayHandBase();
                    }
                }

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
            _transform.localScale = CardDisplayModeVisuals.GetBaseLocalScale(mode, AuthoredBaseScale);
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

            var hand = CardEntityLifecycleHook.HandOrNull();
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
            return CardDisplayModeVisuals.GetBaseLocalScale(mode, AuthoredBaseScale);
        }

        private static GroundFieldLayoutSettings ResolveGroundLayoutSettings()
        {
            var field = GroundFieldGeometryHook.FieldOrNull();
            return field != null ? field.LayoutSettings : new GroundFieldLayoutSettings();
        }

        private static CardHandLayoutSettings ResolveHandLayoutSettings()
        {
            var hand = CardEntityLifecycleHook.HandOrNull();
            return hand != null ? hand.LayoutSettings : new CardHandLayoutSettings();
        }
    }

    /// <summary>
    /// ADR-0024：显示模式缩放倍率相对「进入时预制体基准」，不得写死绝对尺寸。
    /// </summary>
    public static class CardDisplayModeVisuals
    {
        public const float DragCardScaleMultiplier = 1.06f;
        public const float RemovedModeScaleMultiplier = 1.08f;

        public static float GetModeScaleMultiplier(CardDisplayMode mode)
        {
            return mode switch
            {
                CardDisplayMode.RemovedMode => RemovedModeScaleMultiplier,
                CardDisplayMode.DragCardMode => DragCardScaleMultiplier,
                _ => 1f,
            };
        }

        public static Vector3 GetBaseLocalScale(CardDisplayMode mode) =>
            GetBaseLocalScale(mode, Vector3.one);

        public static Vector3 GetBaseLocalScale(CardDisplayMode mode, Vector3 authoredBaseScale)
        {
            if (authoredBaseScale.sqrMagnitude <= 0.0001f)
            {
                authoredBaseScale = Vector3.one;
            }

            return authoredBaseScale * GetModeScaleMultiplier(mode);
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
