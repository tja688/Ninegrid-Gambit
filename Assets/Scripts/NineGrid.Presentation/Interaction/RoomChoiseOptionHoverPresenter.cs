using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 房间选择悬停：仅 1.1x 缩放，无两侧推挤。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomChoiseOptionHoverPresenter : MonoBehaviour, IInteractivePresenter
    {
        public readonly struct OptionActor
        {
            public OptionActor(Transform transform, Vector3 baseLocalScale, int baselineSortingOrder)
            {
                Transform = transform;
                BaseLocalScale = baseLocalScale;
                BaselineSortingOrder = baselineSortingOrder;
            }

            public Transform Transform { get; }
            public Vector3 BaseLocalScale { get; }
            public int BaselineSortingOrder { get; }
        }

        [Header("Hover")]
        [SerializeField, Min(1f)] private float hoverScale = 1.1f;
        [SerializeField, Min(0.05f)] private float duration = 0.25f;
        [SerializeField, Min(0f)] private float overshoot = 1.2f;
        [SerializeField, Min(0)] private int focusSortingBoost = 10;

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;

        private readonly List<OptionActor> options = new();
        private readonly List<Tween> activeTweens = new();
        private int hoveredIndex = -1;

        private void OnDisable()
        {
            ForceReset();
        }

        private void OnValidate()
        {
            duration = Mathf.Max(0.05f, duration);
            hoverScale = Mathf.Max(1f, hoverScale);
            overshoot = Mathf.Max(0f, overshoot);
        }

        public void Enter()
        {
        }

        public void UpdatePresenter()
        {
        }

        public void Exit()
        {
            PlayReset();
        }

        public void ForceReset()
        {
            StopAndRestore();
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
            Vector3 hover = Vector3.one * hoverScale;
            for (var i = 0; i < options.Count; i++)
            {
                OptionActor actor = options[i];
                if (actor.Transform == null)
                {
                    continue;
                }

                bool focused = i == index;
                SelectionOptionVisual.ApplySortingOrder(
                    actor.Transform,
                    actor.BaselineSortingOrder + (focused ? focusSortingBoost : 0));
                Vector3 targetScale = focused ? Vector3.Scale(actor.BaseLocalScale, hover) : actor.BaseLocalScale;
                TrackTween(actor.Transform
                    .DOScale(targetScale, safeDuration)
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
                    .DOScale(actor.BaseLocalScale, safeDuration)
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

                actor.Transform.localScale = actor.BaseLocalScale;
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
