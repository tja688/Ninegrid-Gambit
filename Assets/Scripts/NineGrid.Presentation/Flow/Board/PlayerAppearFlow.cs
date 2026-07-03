using System.Collections;
using DG.Tweening;
using NineGrid.Presentation.Contracts;
using UnityEngine;
using UnityEngine.Events;

namespace NineGrid.Presentation.Flow.Board
{
    /// <summary>
    /// 玩家登场：在场地中心锚点从极小缩放缓动至正常尺寸（可独立调用）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAppearFlow : MonoBehaviour, IDirectedFlow
    {
        [SerializeField, Min(0.01f)] private float duration = 0.35f;
        [SerializeField] private Ease scaleEase = Ease.OutBack;
        [SerializeField, Min(0f)] private float startScale = 0f;
        [SerializeField] private bool deferPlayOneFrame = true;
        [SerializeField] private bool ignoreTimeScale;

        [Header("Events")]
        [SerializeField] private UnityEvent onComplete;

        private Transform activeActor;
        private Vector3 baselineLocalScale = Vector3.one;
        private Tween activeTween;
        private Coroutine playCoroutine;

        public bool IsPlaying { get; private set; }
        public float ExpectedDuration => duration;

        private void OnDisable()
        {
            StopAndRestore();
        }

        public void Play(Transform actor)
        {
            if (!isActiveAndEnabled || actor == null)
            {
                return;
            }

            StopPlaybackOnly();
            activeActor = actor;
            baselineLocalScale = actor.localScale.sqrMagnitude > 0.0001f
                ? actor.localScale
                : Vector3.one;

            actor.localScale = Vector3.one * startScale;
            activeTween = actor
                .DOScale(baselineLocalScale, duration)
                .SetEase(scaleEase)
                .SetTarget(this)
                .Pause();

            if (ignoreTimeScale)
            {
                activeTween.SetUpdate(true);
            }

            IsPlaying = true;
            activeTween.OnComplete(HandleTweenComplete);
            activeTween.OnKill(HandleTweenKilled);

            if (deferPlayOneFrame)
            {
                playCoroutine = StartCoroutine(PlayNextFrame());
            }
            else
            {
                activeTween.Play();
            }
        }

        [ContextMenu("Stop And Restore")]
        public void StopAndRestore()
        {
            StopPlaybackOnly();

            if (activeActor != null)
            {
                activeActor.localScale = baselineLocalScale;
            }

            activeActor = null;
            IsPlaying = false;
        }

        private IEnumerator PlayNextFrame()
        {
            yield return null;
            playCoroutine = null;

            if (activeTween != null && activeTween.IsActive())
            {
                activeTween.Play();
            }
        }

        private void StopPlaybackOnly()
        {
            if (playCoroutine != null)
            {
                StopCoroutine(playCoroutine);
                playCoroutine = null;
            }

            if (activeTween != null && activeTween.IsActive())
            {
                activeTween.OnComplete(null);
                activeTween.OnKill(null);
                activeTween.Kill();
            }

            activeTween = null;
            DOTween.Kill(this);

            if (activeActor != null)
            {
                DOTween.Kill(activeActor);
            }
        }

        private void HandleTweenComplete()
        {
            IsPlaying = false;
            activeTween = null;
            onComplete?.Invoke();
        }

        private void HandleTweenKilled()
        {
            if (activeTween != null && !activeTween.IsComplete())
            {
                IsPlaying = false;
                activeTween = null;
            }
        }
    }
}
