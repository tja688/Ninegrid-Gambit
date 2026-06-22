using System;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 选中选项确认：抬起扶正，可选移至目标锚点（BounceCards 选中分支）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelectionOptionConfirmPerformance : MonoBehaviour
    {
        [Header("Lift")]
        [SerializeField, Min(0f)] private float liftY = 0.28f;
        [SerializeField, Min(0.05f)] private float liftDuration = 0.5f;
        [SerializeField, Min(0f)] private float overshoot = 1.4f;
        [SerializeField, Min(0)] private int focusSortingBoost = 40;

        [Header("Move To Slot")]
        [SerializeField, Min(0f)] private float moveDelay = 0.12f;
        [SerializeField, Min(0.05f)] private float moveDuration = 0.5f;

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;

        private Sequence activeSequence;

        private void OnDisable()
        {
            StopAndRestore();
        }

        private void OnValidate()
        {
            liftDuration = Mathf.Max(0.05f, liftDuration);
            moveDuration = Mathf.Max(0.05f, moveDuration);
            moveDelay = Mathf.Max(0f, moveDelay);
            liftY = Mathf.Max(0f, liftY);
            overshoot = Mathf.Max(0f, overshoot);
        }

        public void PlayLift(Transform selected, int baselineSortingOrder, int optionCount, Action onComplete = null)
        {
            Play(selected, null, baselineSortingOrder, optionCount, onComplete);
        }

        public void Play(
            Transform selected,
            Transform targetSlot,
            int baselineSortingOrder,
            int optionCount,
            Action onComplete = null)
        {
            if (selected == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopPlaybackOnly();
            SelectionOptionVisual.ApplySortingOrder(
                selected,
                baselineSortingOrder + optionCount + focusSortingBoost);

            activeSequence = DOTween.Sequence().SetTarget(this);
            if (ignoreTimeScale)
            {
                activeSequence.SetUpdate(true);
            }

            activeSequence.Append(
                selected.DOLocalMove(selected.localPosition + new Vector3(0f, liftY, 0f), liftDuration)
                    .SetEase(Ease.OutBack, overshoot));
            activeSequence.Join(
                selected.DOLocalRotate(Vector3.zero, liftDuration)
                    .SetEase(Ease.OutBack, overshoot));

            if (targetSlot != null)
            {
                activeSequence.AppendInterval(moveDelay);
                activeSequence.Append(
                    selected.DOLocalRotate(Vector3.zero, moveDuration)
                        .SetEase(Ease.OutBack, overshoot));
                activeSequence.Join(
                    selected.DOMove(targetSlot.position, moveDuration)
                        .SetEase(Ease.OutBack, overshoot));
            }

            activeSequence.OnComplete(() => onComplete?.Invoke());
            activeSequence.Play();
        }

        public void StopAndRestore()
        {
            StopPlaybackOnly();
        }

        private void StopPlaybackOnly()
        {
            if (activeSequence != null && activeSequence.IsActive())
            {
                activeSequence.Kill(false);
            }

            activeSequence = null;
            DOTween.Kill(this);
        }
    }
}
