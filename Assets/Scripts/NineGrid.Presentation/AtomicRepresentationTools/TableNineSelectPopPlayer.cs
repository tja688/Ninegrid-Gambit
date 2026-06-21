using DG.Tweening;
using UnityEngine;

namespace NineGrid.Presentation.AtomicRepresentationTools
{
    /// <summary>
    /// 单对象选中反馈：轻微放大 + Punch 抖动（业界常用 DOPunchScale / DOPunchRotation 组合）。
    /// 挂到卡牌根节点，通过 UnityEvent 调用 PlaySelect / PlayDeselect。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineSelectPopPlayer : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Scale")]
        [SerializeField, Min(1f)] private float selectScaleMultiplier = 1.09f;
        [SerializeField, Min(0.01f)] private float scaleUpDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float scaleDownDuration = 0.15f;
        [SerializeField] private Ease scaleUpEase = Ease.OutBack;
        [SerializeField] private Ease scaleDownEase = Ease.InOutQuad;

        [Header("Punch Scale")]
        [SerializeField, Min(0f)] private float punchScaleStrength = 0.08f;
        [SerializeField, Min(0.01f)] private float punchScaleDuration = 0.3f;
        [SerializeField, Min(1)] private int punchScaleVibrato = 7;
        [SerializeField, Range(0f, 1f)] private float punchScaleElasticity = 0.62f;

        [Header("Punch Rotation (Z)")]
        [SerializeField, Min(0f)] private float punchRotationZ = 4.5f;
        [SerializeField, Min(0.01f)] private float punchRotationDuration = 0.32f;
        [SerializeField, Min(1)] private int punchRotationVibrato = 6;
        [SerializeField, Range(0f, 1f)] private float punchRotationElasticity = 0.55f;

        [Header("Hold Wobble (Optional)")]
        [SerializeField] private bool enableHoldWobble;
        [SerializeField, Min(0f)] private float holdWobbleRotationZ = 0.9f;
        [SerializeField, Min(1)] private int holdWobbleVibrato = 8;

        [Header("Timing")]
        [SerializeField] private bool ignoreTimeScale;

        private Vector3 baseLocalScale;
        private Vector3 baseLocalEuler;
        private bool isSelected;
        private bool baselineCaptured;

        private Transform ResolvedTarget => target != null ? target : transform;

        private void Awake()
        {
            CaptureBaseline();
        }

        private void OnEnable()
        {
            CaptureBaseline();
        }

        private void OnDisable()
        {
            StopAndRestore();
        }

        private void OnValidate()
        {
            selectScaleMultiplier = Mathf.Max(1f, selectScaleMultiplier);
            scaleUpDuration = Mathf.Max(0.01f, scaleUpDuration);
            scaleDownDuration = Mathf.Max(0.01f, scaleDownDuration);
            punchScaleDuration = Mathf.Max(0.01f, punchScaleDuration);
            punchRotationDuration = Mathf.Max(0.01f, punchRotationDuration);
            punchScaleVibrato = Mathf.Max(1, punchScaleVibrato);
            punchRotationVibrato = Mathf.Max(1, punchRotationVibrato);
            holdWobbleVibrato = Mathf.Max(1, holdWobbleVibrato);
        }

        [ContextMenu("Play Select")]
        public void PlaySelect()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            CaptureBaseline();
            isSelected = true;

            Transform tweenTarget = ResolvedTarget;
            KillTweens(tweenTarget);

            Vector3 selectedScale = baseLocalScale * selectScaleMultiplier;
            Tween scaleTween = tweenTarget
                .DOScale(selectedScale, scaleUpDuration)
                .SetEase(scaleUpEase)
                .SetTarget(this);

            if (ignoreTimeScale)
            {
                scaleTween.SetUpdate(true);
            }

            if (punchScaleStrength > Mathf.Epsilon)
            {
                Tween punchScale = tweenTarget
                    .DOPunchScale(
                        Vector3.one * punchScaleStrength,
                        punchScaleDuration,
                        punchScaleVibrato,
                        punchScaleElasticity)
                    .SetTarget(this);

                if (ignoreTimeScale)
                {
                    punchScale.SetUpdate(true);
                }
            }

            if (punchRotationZ > Mathf.Epsilon)
            {
                Tween punchRotation = tweenTarget
                    .DOPunchRotation(
                        new Vector3(0f, 0f, punchRotationZ),
                        punchRotationDuration,
                        punchRotationVibrato,
                        punchRotationElasticity)
                    .SetTarget(this);

                if (ignoreTimeScale)
                {
                    punchRotation.SetUpdate(true);
                }
            }

            if (enableHoldWobble && holdWobbleRotationZ > Mathf.Epsilon)
            {
                Tween holdWobble = tweenTarget
                    .DOShakeRotation(
                        999f,
                        new Vector3(0f, 0f, holdWobbleRotationZ),
                        holdWobbleVibrato,
                        35f,
                        false)
                    .SetTarget(this);

                if (ignoreTimeScale)
                {
                    holdWobble.SetUpdate(true);
                }
            }
        }

        [ContextMenu("Play Deselect")]
        public void PlayDeselect()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            isSelected = false;

            Transform tweenTarget = ResolvedTarget;
            KillTweens(tweenTarget);

            Tween scaleTween = tweenTarget
                .DOScale(baseLocalScale, scaleDownDuration)
                .SetEase(scaleDownEase)
                .SetTarget(this);

            Tween rotationTween = tweenTarget
                .DOLocalRotate(baseLocalEuler, scaleDownDuration)
                .SetEase(scaleDownEase)
                .SetTarget(this);

            if (ignoreTimeScale)
            {
                scaleTween.SetUpdate(true);
                rotationTween.SetUpdate(true);
            }
        }

        public void SetSelected(bool selected)
        {
            if (selected)
            {
                PlaySelect();
            }
            else
            {
                PlayDeselect();
            }
        }

        [ContextMenu("Stop And Restore")]
        public void StopAndRestore()
        {
            isSelected = false;

            Transform tweenTarget = ResolvedTarget;
            KillTweens(tweenTarget);

            if (!baselineCaptured)
            {
                return;
            }

            tweenTarget.localScale = baseLocalScale;
            tweenTarget.localEulerAngles = baseLocalEuler;
        }

        public void RebindTarget()
        {
            StopAndRestore();
            CaptureBaseline();
        }

        private void CaptureBaseline()
        {
            Transform tweenTarget = ResolvedTarget;
            baseLocalScale = tweenTarget.localScale;
            baseLocalEuler = tweenTarget.localEulerAngles;
            baselineCaptured = true;
        }

        private void KillTweens(Transform tweenTarget)
        {
            DOTween.Kill(this);
            DOTween.Kill(tweenTarget);
        }
    }
}
