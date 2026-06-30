using System;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Contracts;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Reactions
{
    /// <summary>
    /// 未选中选项落屏退场：抛物线飞出画面（BounceCards AnimateFallOff）。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "SelectionOptionFallOffPerformance")]
    public sealed class SelectionFallOffFlow : MonoBehaviour, IPlannedReaction
    {
        [Header("Physics")]
        [SerializeField, Min(0f)] private float launchUpward = 3.4f;
        [SerializeField, Min(0.1f)] private float gravity = 24f;
        [SerializeField, Min(0f)] private float horizontalReach = 4.2f;
        [SerializeField, Min(0f)] private float horizontalReachStep = 0.9f;
        [SerializeField, Min(0f)] private float spinMin = 55f;
        [SerializeField, Min(0f)] private float spinMax = 145f;
        [SerializeField, Min(0f)] private float staggerStep = 0.035f;
        [SerializeField, Min(0f)] private float belowScreenPadding = 1.6f;
        [SerializeField, Min(0.01f)] private float cardHalfHeight = 0.55f;

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;

        private readonly List<Tween> activeTweens = new();

        public bool IsPlaying => activeTweens.Count > 0;

        private void OnDisable()
        {
            StopAndRestore();
        }

        private void OnValidate()
        {
            launchUpward = Mathf.Max(0f, launchUpward);
            gravity = Mathf.Max(0.1f, gravity);
            horizontalReach = Mathf.Max(0f, horizontalReach);
            horizontalReachStep = Mathf.Max(0f, horizontalReachStep);
            spinMin = Mathf.Max(0f, spinMin);
            spinMax = Mathf.Max(spinMin, spinMax);
            staggerStep = Mathf.Max(0f, staggerStep);
            belowScreenPadding = Mathf.Max(0f, belowScreenPadding);
            cardHalfHeight = Mathf.Max(0.01f, cardHalfHeight);
        }

        public void Play(
            Transform option,
            int optionIndex,
            int selectedIndex,
            float baseLocalRotationZ,
            Camera worldCamera,
            Action onComplete = null)
        {
            if (option == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopTweenFor(option);

            Vector3 start = option.position;
            float awayDirection = optionIndex < selectedIndex ? -1f : 1f;
            int distanceFromSelected = Mathf.Abs(optionIndex - selectedIndex);
            float horizontalOffset = awayDirection
                * (horizontalReach + distanceFromSelected * horizontalReachStep);
            Vector3 end = ResolveFallTarget(start, worldCamera);
            end.x = start.x + horizontalOffset;

            float drop = Mathf.Max(0.1f, start.y - end.y);
            float launchUp = launchUpward;
            float duration = (launchUp + Mathf.Sqrt(launchUp * launchUp + 2f * gravity * drop)) / gravity;
            float velocityX = horizontalOffset / duration;
            float startRotZ = option.localEulerAngles.z;
            float spinZ = baseLocalRotationZ + awayDirection * ResolveSpin(distanceFromSelected);
            float stagger = staggerStep * distanceFromSelected;

            Tween tween = DOTween.To(() => 0f, progress =>
                {
                    if (option == null)
                    {
                        return;
                    }

                    float elapsed = progress * duration;
                    option.position = new Vector3(
                        start.x + velocityX * elapsed,
                        start.y + launchUp * elapsed - 0.5f * gravity * elapsed * elapsed,
                        start.z);
                    option.localRotation = Quaternion.Euler(
                        0f,
                        0f,
                        Mathf.LerpAngle(startRotZ, spinZ, progress));
                },
                1f,
                duration)
                .SetDelay(stagger)
                .SetEase(Ease.Linear)
                .SetTarget(option);

            if (ignoreTimeScale)
            {
                tween.SetUpdate(true);
            }

            tween.OnComplete(() =>
            {
                RemoveTween(tween);
                if (option != null)
                {
                    option.gameObject.SetActive(false);
                }

                onComplete?.Invoke();
            });

            activeTweens.Add(tween);
        }

        public void StopAndRestore()
        {
            for (var i = activeTweens.Count - 1; i >= 0; i--)
            {
                Tween tween = activeTweens[i];
                if (tween != null && tween.IsActive())
                {
                    tween.Kill(false);
                }
            }

            activeTweens.Clear();
        }

        private void StopTweenFor(Transform option)
        {
            if (option != null)
            {
                DOTween.Kill(option);
            }

            for (var i = activeTweens.Count - 1; i >= 0; i--)
            {
                Tween tween = activeTweens[i];
                if (tween == null || !tween.IsActive())
                {
                    activeTweens.RemoveAt(i);
                }
            }
        }

        private void RemoveTween(Tween tween)
        {
            activeTweens.Remove(tween);
        }

        private float ResolveSpin(int distanceFromSelected)
        {
            float t = spinMax > spinMin
                ? spinMin + ((distanceFromSelected * 37) % 100) / 100f * (spinMax - spinMin)
                : spinMin;
            return t;
        }

        private Vector3 ResolveFallTarget(Vector3 start, Camera worldCamera)
        {
            if (worldCamera == null)
            {
                return start + new Vector3(0f, -12f, 0f);
            }

            float depth = Mathf.Abs(start.z - worldCamera.transform.position.z);
            Vector3 belowScreen = worldCamera.ViewportToWorldPoint(new Vector3(0.5f, -0.2f, depth));
            float targetY = belowScreen.y - belowScreenPadding - cardHalfHeight;
            return new Vector3(start.x, targetY, start.z);
        }
    }
}
