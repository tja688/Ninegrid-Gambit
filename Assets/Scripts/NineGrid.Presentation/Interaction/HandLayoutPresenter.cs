using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Shared;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 手牌扇形布局收敛：声明式补位，维护演员基线状态。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "ItemCardInteractPerformance")]
    public sealed class HandLayoutPresenter : MonoBehaviour, IInteractivePresenter
    {
        [Header("Layout")]
        [SerializeField, Min(0f)] private float layoutDuration = 0.25f;
        [SerializeField] private Ease layoutEase = Ease.OutQuad;

        [Header("Restore")]
        [SerializeField, Min(0f)] private float restoreDuration = 0.2f;
        [SerializeField] private Ease restoreEase = Ease.OutQuad;

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;
        [SerializeField] private bool deferPlayOneFrame = true;

        private readonly Dictionary<Transform, ActorVisualState> actorStates = new();
        private Sequence layoutSequence;
        private Coroutine deferCoroutine;

        public struct ActorVisualState
        {
            public Vector3 BaselineLocalPosition;
            public Vector3 BaselineLocalScale;
            public Color BaselineColor;
            public int BaselineSortingOrder;
            public bool HasBaseline;
        }

        public Transform DraggingActor { get; internal set; }

        private void OnDisable()
        {
            ForceReset();
        }

        private void OnValidate()
        {
            layoutDuration = Mathf.Max(0f, layoutDuration);
            restoreDuration = Mathf.Max(0f, restoreDuration);
        }

        public void Enter()
        {
        }

        public void UpdatePresenter()
        {
        }

        public void Exit()
        {
        }

        public void ForceReset()
        {
            StopAndRestore();
        }

        public void RegisterActor(Transform actor, Vector3 baselineLocalPosition, int baselineSortingOrder)
        {
            if (actor == null)
            {
                return;
            }

            SpriteRenderer renderer = GetPrimaryRenderer(actor);
            actorStates[actor] = new ActorVisualState
            {
                BaselineLocalPosition = baselineLocalPosition,
                BaselineLocalScale = actor.localScale,
                BaselineColor = renderer != null ? renderer.color : Color.white,
                BaselineSortingOrder = baselineSortingOrder,
                HasBaseline = true,
            };
        }

        public void UnregisterActor(Transform actor)
        {
            if (actor == null)
            {
                return;
            }

            InvalidateActor(actor, killLayoutIfActive: true);
        }

        public void RestoreAllActorsImmediate(Transform except = null)
        {
            KillLayoutSequence();

            var actors = new List<Transform>(actorStates.Keys);
            for (var i = 0; i < actors.Count; i++)
            {
                Transform actor = actors[i];
                if (actor == null || ReferenceEquals(actor, except))
                {
                    continue;
                }

                RestoreActorVisual(actor, 0f);
            }
        }

        public void PurgeDestroyedActors()
        {
            var stale = new List<Transform>();
            foreach (Transform actor in actorStates.Keys)
            {
                if (actor == null)
                {
                    stale.Add(actor);
                }
            }

            for (var i = 0; i < stale.Count; i++)
            {
                actorStates.Remove(stale[i]);
            }

            if (DraggingActor != null && !actorStates.ContainsKey(DraggingActor))
            {
                DraggingActor = null;
            }
        }

        public void UpdateBaseline(Transform actor, Vector3 baselineLocalPosition, int baselineSortingOrder)
        {
            if (actor == null || !actorStates.TryGetValue(actor, out ActorVisualState state))
            {
                RegisterActor(actor, baselineLocalPosition, baselineSortingOrder);
                return;
            }

            state.BaselineLocalPosition = baselineLocalPosition;
            state.BaselineSortingOrder = baselineSortingOrder;
            actorStates[actor] = state;
        }

        public bool TryGetState(Transform actor, out ActorVisualState state)
        {
            return actorStates.TryGetValue(actor, out state);
        }

        public void Relayout(
            IReadOnlyList<Transform> actors,
            IReadOnlyList<HandCardLayoutTarget> targets,
            float durationOverride = -1f,
            Action onComplete = null)
        {
            if (actors == null || targets == null || actors.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            float duration = durationOverride >= 0f ? durationOverride : layoutDuration;
            KillLayoutSequence();

            if (duration <= 0f)
            {
                ApplyLayoutInstant(actors, targets);
                onComplete?.Invoke();
                return;
            }

            layoutSequence = DOTween.Sequence()
                .SetTarget(this)
                .SetAutoKill(true)
                .Pause();

            if (ignoreTimeScale)
            {
                layoutSequence.SetUpdate(true);
            }

            int pairCount = Mathf.Min(actors.Count, targets.Count);
            for (var i = 0; i < pairCount; i++)
            {
                Transform actor = actors[i];
                HandCardLayoutTarget target = targets[i];
                if (actor == null || ReferenceEquals(actor, DraggingActor))
                {
                    continue;
                }

                UpdateBaseline(actor, target.LocalPosition, target.SortingOrder);

                Tween move = actor
                    .DOLocalMove(target.LocalPosition, duration)
                    .SetEase(layoutEase)
                    .SetTarget(actor);

                Tween sort = SelectionOptionVisual.TweenBaseSortingOrder(actor, target.SortingOrder, duration, layoutEase);
                if (sort != null)
                {
                    if (ignoreTimeScale)
                    {
                        sort.SetUpdate(true);
                    }

                    layoutSequence.Join(move);
                    layoutSequence.Join(sort);
                }
                else
                {
                    layoutSequence.Join(move);
                }

                if (ignoreTimeScale)
                {
                    move.SetUpdate(true);
                }
            }

            layoutSequence.OnComplete(() =>
            {
                layoutSequence = null;
                onComplete?.Invoke();
            });

            if (deferPlayOneFrame && isActiveAndEnabled)
            {
                deferCoroutine = StartCoroutine(PlaySequenceNextFrame(layoutSequence));
            }
            else
            {
                layoutSequence.Restart();
            }
        }

        public void StopAndRestore()
        {
            if (deferCoroutine != null)
            {
                StopCoroutine(deferCoroutine);
                deferCoroutine = null;
            }

            KillLayoutSequence();
            DOTween.Kill(this);

            var actors = new List<Transform>(actorStates.Keys);
            for (var i = 0; i < actors.Count; i++)
            {
                RestoreActorVisual(actors[i], 0f);
            }

            DraggingActor = null;
        }

        internal void EnsureBaseline(Transform actor)
        {
            if (actor == null || actorStates.ContainsKey(actor))
            {
                return;
            }

            RegisterActor(actor, actor.localPosition, SelectionOptionVisual.GetAnchorSortingOrder(actor));
        }

        internal void RestoreActorVisual(Transform actor, float duration)
        {
            if (actor == null || !actorStates.TryGetValue(actor, out ActorVisualState state))
            {
                return;
            }

            KillActorTweens(actor);

            float tweenDuration = duration >= 0f ? duration : restoreDuration;
            Ease ease = restoreEase;

            if (tweenDuration <= 0f)
            {
                actor.localPosition = state.BaselineLocalPosition;
                actor.localScale = state.BaselineLocalScale;
                SelectionOptionVisual.ApplySortingOrder(actor, state.BaselineSortingOrder);

                SpriteRenderer renderer = GetPrimaryRenderer(actor);
                if (renderer != null)
                {
                    renderer.color = state.BaselineColor;
                }

                return;
            }

            Tween move = actor
                .DOLocalMove(state.BaselineLocalPosition, tweenDuration)
                .SetEase(ease)
                .SetTarget(actor);
            ApplyTweenSettings(move);

            SpriteRenderer spriteRenderer = GetPrimaryRenderer(actor);
            if (spriteRenderer != null)
            {
                Tween color = TweenSpriteColor(spriteRenderer, state.BaselineColor, tweenDuration, ease);
                ApplyTweenSettings(color);
            }

            Tween sort = SelectionOptionVisual.TweenBaseSortingOrder(actor, state.BaselineSortingOrder, tweenDuration, ease);
            ApplyTweenSettings(sort);
        }

        internal void KillActorTweens(Transform actor)
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

        internal float LayoutDuration => layoutDuration;

        private void ApplyLayoutInstant(IReadOnlyList<Transform> actors, IReadOnlyList<HandCardLayoutTarget> targets)
        {
            int pairCount = Mathf.Min(actors.Count, targets.Count);
            for (var i = 0; i < pairCount; i++)
            {
                Transform actor = actors[i];
                HandCardLayoutTarget target = targets[i];
                if (actor == null || ReferenceEquals(actor, DraggingActor))
                {
                    continue;
                }

                UpdateBaseline(actor, target.LocalPosition, target.SortingOrder);
                actor.localPosition = target.LocalPosition;
                SelectionOptionVisual.ApplySortingOrder(actor, target.SortingOrder);

                SpriteRenderer renderer = GetPrimaryRenderer(actor);
                if (renderer != null && actorStates.TryGetValue(actor, out ActorVisualState state))
                {
                    renderer.color = state.BaselineColor;
                }
            }
        }

        private void InvalidateActor(Transform actor, bool killLayoutIfActive)
        {
            KillActorTweens(actor);

            if (killLayoutIfActive && layoutSequence != null && layoutSequence.IsActive())
            {
                KillLayoutSequence();
            }

            actorStates.Remove(actor);

            if (ReferenceEquals(DraggingActor, actor))
            {
                DraggingActor = null;
            }
        }

        private void KillLayoutSequence()
        {
            if (deferCoroutine != null)
            {
                StopCoroutine(deferCoroutine);
                deferCoroutine = null;
            }

            if (layoutSequence != null && layoutSequence.IsActive())
            {
                layoutSequence.Kill();
            }

            layoutSequence = null;
        }

        private IEnumerator PlaySequenceNextFrame(Sequence sequence)
        {
            yield return null;
            deferCoroutine = null;

            if (sequence != null && sequence.IsActive())
            {
                sequence.Restart();
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

        internal static Tween TweenSpriteColor(SpriteRenderer renderer, Color endValue, float duration, Ease ease)
        {
            if (renderer == null)
            {
                return null;
            }

            return DOTween
                .To(() => renderer != null ? renderer.color : endValue, value =>
                {
                    if (renderer != null)
                    {
                        renderer.color = value;
                    }
                }, endValue, duration)
                .SetEase(ease)
                .SetTarget(renderer);
        }

        internal static Tween TweenSortingOrder(SpriteRenderer renderer, int endValue, float duration, Ease ease)
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
