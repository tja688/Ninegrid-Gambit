using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 选择层选项悬停：居中抬起 + 两侧推挤（BounceCards AnimateHover / AnimateReset）。
    /// 本地反馈，供 SelectionOverlayMode 或预览壳直驱。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelectionOptionHoverPerformance : MonoBehaviour
    {
        public readonly struct OptionActor
        {
            public OptionActor(
                Transform transform,
                Vector3 baseLocalPosition,
                float baseLocalRotationZ,
                int baselineSortingOrder)
            {
                Transform = transform;
                BaseLocalPosition = baseLocalPosition;
                BaseLocalRotationZ = baseLocalRotationZ;
                BaselineSortingOrder = baselineSortingOrder;
            }

            public Transform Transform { get; }
            public Vector3 BaseLocalPosition { get; }
            public float BaseLocalRotationZ { get; }
            public int BaselineSortingOrder { get; }
        }

        [Header("Hover")]
        [SerializeField, Min(0f)] private float pushOffset = 1.6f;
        [SerializeField, Min(0f)] private float liftY = 0.28f;
        [SerializeField, Min(0.05f)] private float duration = 0.4f;
        [SerializeField, Min(0f)] private float siblingDelayStep = 0.03f;
        [SerializeField, Min(0f)] private float overshoot = 1.4f;
        [SerializeField, Min(0)] private int focusSortingBoost = 20;

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;

        private readonly List<OptionActor> options = new();
        private readonly List<Tween> activeTweens = new();
        private int hoveredIndex = -1;

        private void OnDisable()
        {
            StopAndRestore();
        }

        private void OnValidate()
        {
            duration = Mathf.Max(0.05f, duration);
            pushOffset = Mathf.Max(0f, pushOffset);
            liftY = Mathf.Max(0f, liftY);
            siblingDelayStep = Mathf.Max(0f, siblingDelayStep);
            overshoot = Mathf.Max(0f, overshoot);
        }

        public void SetOptions(IReadOnlyList<OptionActor> actors)
        {
            StopAndRestore();
            options.Clear();
            if (actors == null)
            {
                return;
            }

            for (var i = 0; i < actors.Count; i++)
            {
                options.Add(actors[i]);
            }
        }

        public void PlayHover(int index)
        {
            if (index < 0 || index >= options.Count)
            {
                PlayReset();
                return;
            }

            if (index == hoveredIndex)
            {
                return;
            }

            hoveredIndex = index;
            KillActiveTweens();

            float safeDuration = duration;
            float safeOvershoot = overshoot;
            for (var i = 0; i < options.Count; i++)
            {
                OptionActor actor = options[i];
                if (actor.Transform == null)
                {
                    continue;
                }

                if (i == index)
                {
                    SelectionOptionVisual.ApplySortingOrder(
                        actor.Transform,
                        actor.BaselineSortingOrder + options.Count + focusSortingBoost);
                    TrackTween(actor.Transform
                        .DOLocalMove(actor.BaseLocalPosition + new Vector3(0f, liftY, 0f), safeDuration)
                        .SetEase(Ease.OutBack, safeOvershoot));
                    TrackTween(actor.Transform
                        .DOLocalRotate(Vector3.zero, safeDuration)
                        .SetEase(Ease.OutBack, safeOvershoot));
                    continue;
                }

                SelectionOptionVisual.ApplySortingOrder(actor.Transform, actor.BaselineSortingOrder);
                float direction = i < index ? -1f : 1f;
                int distance = Mathf.Abs(index - i);
                Vector3 targetPos = actor.BaseLocalPosition + new Vector3(direction * pushOffset, 0f, 0f);
                float delay = siblingDelayStep * distance;

                TrackTween(actor.Transform
                    .DOLocalMove(targetPos, safeDuration)
                    .SetDelay(delay)
                    .SetEase(Ease.OutBack, safeOvershoot));
                TrackTween(actor.Transform
                    .DOLocalRotate(new Vector3(0f, 0f, actor.BaseLocalRotationZ), safeDuration)
                    .SetDelay(delay)
                    .SetEase(Ease.OutBack, safeOvershoot));
            }
        }

        public void PlayReset()
        {
            if (hoveredIndex < 0)
            {
                return;
            }

            hoveredIndex = -1;
            KillActiveTweens();

            float safeDuration = duration;
            float safeOvershoot = overshoot;
            for (var i = 0; i < options.Count; i++)
            {
                OptionActor actor = options[i];
                if (actor.Transform == null)
                {
                    continue;
                }

                SelectionOptionVisual.ApplySortingOrder(actor.Transform, actor.BaselineSortingOrder);
                TrackTween(actor.Transform
                    .DOLocalMove(actor.BaseLocalPosition, safeDuration)
                    .SetEase(Ease.OutBack, safeOvershoot));
                TrackTween(actor.Transform
                    .DOLocalRotate(new Vector3(0f, 0f, actor.BaseLocalRotationZ), safeDuration)
                    .SetEase(Ease.OutBack, safeOvershoot));
            }
        }

        public void StopAndRestore()
        {
            KillActiveTweens();
            hoveredIndex = -1;

            for (var i = 0; i < options.Count; i++)
            {
                OptionActor actor = options[i];
                if (actor.Transform == null)
                {
                    continue;
                }

                actor.Transform.localPosition = actor.BaseLocalPosition;
                actor.Transform.localRotation = Quaternion.Euler(0f, 0f, actor.BaseLocalRotationZ);
                SelectionOptionVisual.ApplySortingOrder(actor.Transform, actor.BaselineSortingOrder);
            }
        }

        private void TrackTween(Tween tween)
        {
            if (tween == null)
            {
                return;
            }

            if (ignoreTimeScale)
            {
                tween.SetUpdate(true);
            }

            activeTweens.Add(tween);
        }

        private void KillActiveTweens()
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
    }
}
