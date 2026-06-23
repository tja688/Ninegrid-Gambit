using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 场地卡 Hover 本地反馈：Y 偏移 + sorting boost（烘焙自 ItemCardInteract focus 参数）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardHoverPerformance : MonoBehaviour
    {
        [Header("Focus (Hover)")]
        [SerializeField] private Vector3 focusLocalOffset = new(0f, 0.3f, 0f);
        [SerializeField, Min(0f)] private float focusDuration = 0.2f;
        [SerializeField] private Ease focusEase = Ease.OutQuad;
        [SerializeField, Min(0)] private int focusSortingBoost = 10;
        [SerializeField, Min(0)] private int focusSortingOrderFloor = 1000;

        [Header("Reject")]
        [SerializeField, Min(0.01f)] private float rejectShakeDuration = 0.25f;
        [SerializeField] private Vector3 rejectShakeStrength = new(0.08f, 0.08f, 0f);

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;

        private readonly Dictionary<Transform, ActorVisualState> actorStates = new();
        private Transform focusedActor;

        private struct ActorVisualState
        {
            public Vector3 BaselineLocalPosition;
            public int BaselineSortingOrder;
            public bool HasBaseline;
        }

        private void OnDisable()
        {
            StopAndRestore();
        }

        private void OnValidate()
        {
            focusDuration = Mathf.Max(0f, focusDuration);
            focusSortingOrderFloor = Mathf.Max(0, focusSortingOrderFloor);
            rejectShakeDuration = Mathf.Max(0.01f, rejectShakeDuration);
        }

        public void Play(Transform actor)
        {
            if (actor == null)
            {
                return;
            }

            StopAndRestore(actor);
            focusedActor = actor;
            EnsureBaseline(actor);
            KillActorTweens(actor);

            Vector3 focusPosition = actorStates[actor].BaselineLocalPosition + focusLocalOffset;
            Tween move = actor
                .DOLocalMove(focusPosition, focusDuration)
                .SetEase(focusEase)
                .SetTarget(actor);
            ApplyTweenSettings(move);

            SpriteRenderer renderer = GetPrimaryRenderer(actor);
            if (renderer != null)
            {
                int boostedOrder = Mathf.Max(
                    actorStates[actor].BaselineSortingOrder + focusSortingBoost,
                    focusSortingOrderFloor);
                Tween sort = TweenSortingOrder(renderer, boostedOrder, focusDuration, focusEase);
                ApplyTweenSettings(sort);
            }
        }

        public void StopAndRestore(Transform actor = null)
        {
            if (actor != null)
            {
                RestoreActorVisual(actor, focusDuration);
                if (ReferenceEquals(focusedActor, actor))
                {
                    focusedActor = null;
                }

                return;
            }

            var actors = new List<Transform>(actorStates.Keys);
            for (var i = 0; i < actors.Count; i++)
            {
                RestoreActorVisual(actors[i], 0f);
            }

            focusedActor = null;
        }

        public void PlayReject(Transform actor)
        {
            if (actor == null)
            {
                return;
            }

            KillActorTweens(actor);
            EnsureBaseline(actor);

            Tween shake = actor
                .DOShakePosition(rejectShakeDuration, rejectShakeStrength, vibrato: 12, randomness: 45f, fadeOut: true)
                .SetTarget(actor);
            ApplyTweenSettings(shake);
            shake.OnComplete(() =>
            {
                if (actor != null)
                {
                    RestoreActorVisual(actor, 0f);
                }
            });
        }

        public void UpdateBaseline(Transform actor, Vector3 baselineLocalPosition, int baselineSortingOrder)
        {
            if (actor == null)
            {
                return;
            }

            if (!actorStates.TryGetValue(actor, out ActorVisualState state))
            {
                RegisterBaseline(actor, baselineLocalPosition, baselineSortingOrder);
                return;
            }

            state.BaselineLocalPosition = baselineLocalPosition;
            state.BaselineSortingOrder = baselineSortingOrder;
            actorStates[actor] = state;
        }

        private void RestoreActorVisual(Transform actor, float duration)
        {
            if (actor == null || !actorStates.TryGetValue(actor, out ActorVisualState state))
            {
                return;
            }

            KillActorTweens(actor);

            if (duration <= 0f)
            {
                actor.localPosition = state.BaselineLocalPosition;
                SpriteRenderer renderer = GetPrimaryRenderer(actor);
                if (renderer != null)
                {
                    renderer.sortingOrder = state.BaselineSortingOrder;
                }

                return;
            }

            Tween move = actor
                .DOLocalMove(state.BaselineLocalPosition, duration)
                .SetEase(focusEase)
                .SetTarget(actor);
            ApplyTweenSettings(move);

            SpriteRenderer spriteRenderer = GetPrimaryRenderer(actor);
            if (spriteRenderer != null)
            {
                Tween sort = TweenSortingOrder(spriteRenderer, state.BaselineSortingOrder, duration, focusEase);
                ApplyTweenSettings(sort);
            }
        }

        private void RegisterBaseline(Transform actor, Vector3 baselineLocalPosition, int baselineSortingOrder)
        {
            actorStates[actor] = new ActorVisualState
            {
                BaselineLocalPosition = baselineLocalPosition,
                BaselineSortingOrder = baselineSortingOrder,
                HasBaseline = true,
            };
        }

        private void EnsureBaseline(Transform actor)
        {
            if (actor == null || actorStates.ContainsKey(actor))
            {
                return;
            }

            SpriteRenderer renderer = GetPrimaryRenderer(actor);
            RegisterBaseline(actor, actor.localPosition, renderer != null ? renderer.sortingOrder : 0);
        }

        private void KillActorTweens(Transform actor)
        {
            if (actor == null)
            {
                return;
            }

            DOTween.Kill(actor);
            SpriteRenderer renderer = GetPrimaryRenderer(actor);
            if (renderer != null)
            {
                DOTween.Kill(renderer);
            }
        }

        private void ApplyTweenSettings(Tween tween)
        {
            if (tween == null)
            {
                return;
            }

            if (ignoreTimeScale)
            {
                tween.SetUpdate(true);
            }
        }

        private static Tween TweenSortingOrder(SpriteRenderer renderer, int endValue, float duration, Ease ease)
        {
            if (renderer == null)
            {
                return null;
            }

            return DOTween
                .To(() => renderer != null ? renderer.sortingOrder : endValue, value =>
                {
                    if (renderer != null)
                    {
                        renderer.sortingOrder = value;
                    }
                }, endValue, duration)
                .SetEase(ease)
                .SetTarget(renderer);
        }

        private static SpriteRenderer GetPrimaryRenderer(Transform actor)
        {
            return actor != null ? actor.GetComponent<SpriteRenderer>() : null;
        }
    }
}
