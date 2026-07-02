using System;
using System.Collections;
using DG.Tweening;
using NineGrid.Presentation.Contracts;
using UnityEngine;

namespace NineGrid.Presentation.Flow.Room
{
    /// <summary>
    /// 房间选择离场：选中卡缓动到 End 锚点。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomChoiseOutFlow : MonoBehaviour, IDirectedFlow
    {
        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float moveDuration = 0.45f;
        [SerializeField] private Ease moveEase = Ease.InBack;
        [SerializeField, Min(0f)] private float overshoot = 1f;
        [SerializeField, Min(0f)] private float unselectedFadeDelay = 0.05f;

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;
        [SerializeField] private bool hideUnselectedImmediately = true;

        private Sequence activeSequence;
        private Coroutine deferCoroutine;

        public bool IsPlaying { get; private set; }
        public float ExpectedDuration => moveDuration;

        private void OnDisable()
        {
            StopAndRestore();
        }

        public void Play(
            Transform selectedCard,
            Transform selectedEnd,
            Transform unselectedCard,
            Action onFinished = null)
        {
            if (!isActiveAndEnabled || selectedCard == null || selectedEnd == null)
            {
                onFinished?.Invoke();
                return;
            }

            StopPlaybackOnly();
            var sequence = DOTween.Sequence().SetTarget(this);
            if (ignoreTimeScale)
            {
                sequence.SetUpdate(true);
            }

            if (unselectedCard != null)
            {
                if (hideUnselectedImmediately)
                {
                    unselectedCard.gameObject.SetActive(false);
                }
                else
                {
                    sequence.Insert(
                        unselectedFadeDelay,
                        unselectedCard.DOScale(Vector3.zero, moveDuration * 0.5f));
                }
            }

            sequence.Insert(
                0f,
                selectedCard.DOMove(selectedEnd.position, moveDuration)
                    .SetEase(moveEase, overshoot));

            IsPlaying = true;
            activeSequence = sequence;
            sequence.OnComplete(() =>
            {
                IsPlaying = false;
                onFinished?.Invoke();
            });

            if (deferCoroutine != null)
            {
                StopCoroutine(deferCoroutine);
            }

            deferCoroutine = StartCoroutine(PlayNextFrame(sequence));
        }

        public void StopAndRestore()
        {
            StopPlaybackOnly();
            IsPlaying = false;
        }

        private IEnumerator PlayNextFrame(Sequence sequence)
        {
            yield return null;
            deferCoroutine = null;
            if (sequence != null && sequence.IsActive())
            {
                sequence.Play();
            }
        }

        private void StopPlaybackOnly()
        {
            if (deferCoroutine != null)
            {
                StopCoroutine(deferCoroutine);
                deferCoroutine = null;
            }

            if (activeSequence != null && activeSequence.IsActive())
            {
                activeSequence.Kill(false);
            }

            activeSequence = null;
            DOTween.Kill(this);
        }
    }
}
