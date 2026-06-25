using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Performance.Layout;
using UnityEngine;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 道具卡本地反馈：布局缓动、Hover 聚焦、拖拽跟手、回手/打出、拒绝抖动。
    /// 烘焙自 HandCardPerformance Timeline（focus Y+0.3 / alpha 0.4 / 0.2s OutQuad）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemCardInteractPerformance : MonoBehaviour
    {
        [Header("Focus (Hover)")]
        [SerializeField] private Vector3 focusLocalOffset = new(0f, 0.3f, 0f);
        [SerializeField, Min(0f)] private float focusDuration = 0.2f;
        [SerializeField] private Ease focusEase = Ease.OutQuad;
        [SerializeField, Min(0)] private int focusSortingBoost = 10;
        [SerializeField, Min(0)] private int focusSortingOrderFloor = 1000;
        [SerializeField, Min(0)] private int dragSortingOrderFloor = 1001;
        [SerializeField, Range(0f, 1f)] private float dimmedAlpha = 0.4f;

        [Header("Drag")]
        [SerializeField] private Vector3 dragWorldOffset = new(0f, 0.15f, 0f);
        [SerializeField] private Color inZoneTint = new(0.75f, 1f, 0.75f, 1f);
        [SerializeField] private Color outOfZoneTint = Color.white;
        [SerializeField, Min(0.01f)] private float zoneTintDuration = 0.12f;

        [Header("Layout / Return / Confirm")]
        [SerializeField, Min(0f)] private float layoutDuration = 0.25f;
        [SerializeField] private Ease layoutEase = Ease.OutQuad;
        [SerializeField, Min(0.01f)] private float returnDuration = 0.25f;
        [SerializeField] private Ease returnEase = Ease.OutQuad;
        [SerializeField, Min(0.01f)] private float confirmDuration = 0.18f;
        [SerializeField] private Ease confirmEase = Ease.OutQuad;
        [SerializeField] private Vector3 confirmLocalScalePunch = new(0.08f, 0.08f, 0f);

        [Header("Reject")]
        [SerializeField, Min(0.01f)] private float rejectShakeDuration = 0.25f;
        [SerializeField] private Vector3 rejectShakeStrength = new(0.08f, 0.08f, 0f);

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;
        [SerializeField] private bool deferPlayOneFrame = true;

        private readonly Dictionary<Transform, ActorVisualState> actorStates = new();
        private Transform focusedActor;
        private Transform draggingActor;
        private Sequence layoutSequence;
        private Coroutine deferCoroutine;
        private bool dragTintKnown;
        private bool dragTintInZone;
        private Vector3 dragPointerOffset;
        private bool dragPointerOffsetKnown;

        private struct ActorVisualState
        {
            public Vector3 BaselineLocalPosition;
            public Vector3 BaselineLocalScale;
            public Color BaselineColor;
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
            dragSortingOrderFloor = Mathf.Max(0, dragSortingOrderFloor);
            layoutDuration = Mathf.Max(0f, layoutDuration);
            returnDuration = Mathf.Max(0.01f, returnDuration);
            confirmDuration = Mathf.Max(0.01f, confirmDuration);
            zoneTintDuration = Mathf.Max(0.01f, zoneTintDuration);
            rejectShakeDuration = Mathf.Max(0.01f, rejectShakeDuration);
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

        /// <summary>
        /// 手牌突变前（销毁/整手重刷）立即还原所有演员视觉，避免 dim tween 中途被杀后透明度卡住。
        /// </summary>
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

            if (except == null || !ReferenceEquals(focusedActor, except))
            {
                focusedActor = null;
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

            if (focusedActor == null)
            {
                focusedActor = null;
            }
            else if (!actorStates.ContainsKey(focusedActor))
            {
                focusedActor = null;
            }

            if (draggingActor == null)
            {
                draggingActor = null;
                dragTintKnown = false;
                dragTintInZone = false;
                dragPointerOffsetKnown = false;
            }
            else if (!actorStates.ContainsKey(draggingActor))
            {
                draggingActor = null;
                dragTintKnown = false;
                dragTintInZone = false;
                dragPointerOffsetKnown = false;
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
                if (actor == null || ReferenceEquals(actor, draggingActor))
                {
                    continue;
                }

                UpdateBaseline(actor, target.LocalPosition, target.SortingOrder);

                Tween move = actor
                    .DOLocalMove(target.LocalPosition, duration)
                    .SetEase(layoutEase)
                    .SetTarget(actor);

                SpriteRenderer renderer = GetPrimaryRenderer(actor);
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
                else if (renderer != null)
                {
                    layoutSequence.Join(move);
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

        public void PlayFocus(Transform focused, IReadOnlyList<Transform> others)
        {
            if (focused == null)
            {
                return;
            }

            StopFocus(focused, others, immediate: true);
            focusedActor = focused;
            EnsureBaseline(focused);

            Vector3 focusPosition = actorStates[focused].BaselineLocalPosition + focusLocalOffset;
            KillActorTweens(focused);

            Tween move = focused
                .DOLocalMove(focusPosition, focusDuration)
                .SetEase(focusEase)
                .SetTarget(focused);
            ApplyTweenSettings(move);

            SpriteRenderer focusedRenderer = GetPrimaryRenderer(focused);
            if (focusedRenderer != null)
            {
                int boostedOrder = Mathf.Max(
                    actorStates[focused].BaselineSortingOrder + focusSortingBoost,
                    focusSortingOrderFloor);
                Tween sort = SelectionOptionVisual.TweenBaseSortingOrder(focused, boostedOrder, focusDuration, focusEase);
                ApplyTweenSettings(sort);
            }

            if (others == null)
            {
                return;
            }

            for (var i = 0; i < others.Count; i++)
            {
                Transform other = others[i];
                if (other == null || other == focused)
                {
                    continue;
                }

                EnsureBaseline(other);
                KillActorTweens(other);

                SpriteRenderer renderer = GetPrimaryRenderer(other);
                if (renderer == null)
                {
                    continue;
                }

                Color targetColor = actorStates[other].BaselineColor;
                targetColor.a = dimmedAlpha;
                Tween fade = TweenSpriteColor(renderer, targetColor, focusDuration, focusEase);
                ApplyTweenSettings(fade);
            }
        }

        public void StopFocus(
            Transform focused,
            IReadOnlyList<Transform> others,
            bool immediate = false,
            bool restoreFocused = true)
        {
            focusedActor = null;
            float duration = immediate ? 0f : focusDuration;

            if (restoreFocused)
            {
                RestoreActorVisual(focused, duration);
            }

            if (others == null)
            {
                return;
            }

            for (var i = 0; i < others.Count; i++)
            {
                RestoreActorVisual(others[i], duration);
            }
        }

        public void BeginDrag(Transform actor, Vector3 pointerWorldPosition)
        {
            if (actor == null)
            {
                return;
            }

            draggingActor = actor;
            dragTintKnown = false;
            dragTintInZone = false;
            dragPointerOffset = actor.position - pointerWorldPosition + dragWorldOffset;
            dragPointerOffsetKnown = true;
            EnsureBaseline(actor);
            KillActorTweens(actor);

            int boostedOrder = Mathf.Max(
                actorStates[actor].BaselineSortingOrder + focusSortingBoost + 1,
                dragSortingOrderFloor);
            SelectionOptionVisual.ApplySortingOrder(actor, boostedOrder);
        }

        public void UpdateDrag(Transform actor, Vector3 pointerWorldPosition, bool inZone)
        {
            if (actor == null)
            {
                return;
            }

            actor.position = pointerWorldPosition + (dragPointerOffsetKnown ? dragPointerOffset : dragWorldOffset);

            SpriteRenderer renderer = GetPrimaryRenderer(actor);
            if (renderer == null)
            {
                return;
            }

            if (dragTintKnown && dragTintInZone == inZone)
            {
                return;
            }

            dragTintKnown = true;
            dragTintInZone = inZone;

            Color targetTint = inZone ? inZoneTint : outOfZoneTint;
            Color baseline = actorStates.TryGetValue(actor, out ActorVisualState state)
                ? state.BaselineColor
                : renderer.color;
            targetTint.a = baseline.a;

            renderer.DOKill();
            Tween tint = TweenSpriteColor(renderer, targetTint, zoneTintDuration, Ease.Linear);
            ApplyTweenSettings(tint);
        }

        public void EndDrag()
        {
            draggingActor = null;
            dragTintKnown = false;
            dragTintInZone = false;
            dragPointerOffsetKnown = false;
        }

        public void PlayReturn(Transform actor, Vector3 targetLocalPosition, int targetSortingOrder, Action onComplete)
        {
            if (actor == null)
            {
                onComplete?.Invoke();
                return;
            }

            KillActorTweens(actor);
            UpdateBaseline(actor, targetLocalPosition, targetSortingOrder);

            Sequence sequence = DOTween.Sequence()
                .SetTarget(actor)
                .SetAutoKill(true);

            if (ignoreTimeScale)
            {
                sequence.SetUpdate(true);
            }

            Tween move = actor
                .DOLocalMove(targetLocalPosition, returnDuration)
                .SetEase(returnEase)
                .SetTarget(actor);
            ApplyTweenSettings(move);
            sequence.Append(move);

            SpriteRenderer renderer = GetPrimaryRenderer(actor);
            if (renderer != null)
            {
                Color restoreColor = actorStates[actor].BaselineColor;
                Tween color = TweenSpriteColor(renderer, restoreColor, returnDuration, returnEase);
                ApplyTweenSettings(color);
                sequence.Join(color);

                Tween sort = TweenSortingOrder(renderer, targetSortingOrder, returnDuration, returnEase);
                ApplyTweenSettings(sort);
                sequence.Join(sort);
            }

            sequence.OnComplete(() => onComplete?.Invoke());
        }

        public void PlayConfirm(Transform actor, Action onComplete)
        {
            if (actor == null)
            {
                onComplete?.Invoke();
                return;
            }

            KillActorTweens(actor);
            EnsureBaseline(actor);

            Vector3 punch = confirmLocalScalePunch;
            if (punch == Vector3.zero)
            {
                onComplete?.Invoke();
                return;
            }

            Tween punchTween = actor
                .DOPunchScale(punch, confirmDuration, vibrato: 1, elasticity: 0.5f)
                .SetEase(confirmEase)
                .SetTarget(actor);
            ApplyTweenSettings(punchTween);

            var completed = false;
            void Finish()
            {
                if (completed)
                {
                    return;
                }

                completed = true;
                onComplete?.Invoke();
            }

            punchTween.OnComplete(Finish);
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

            focusedActor = null;
            draggingActor = null;
            dragTintKnown = false;
            dragTintInZone = false;
            dragPointerOffsetKnown = false;
        }

        private void ApplyLayoutInstant(IReadOnlyList<Transform> actors, IReadOnlyList<HandCardLayoutTarget> targets)
        {
            int pairCount = Mathf.Min(actors.Count, targets.Count);
            for (var i = 0; i < pairCount; i++)
            {
                Transform actor = actors[i];
                HandCardLayoutTarget target = targets[i];
                if (actor == null || ReferenceEquals(actor, draggingActor))
                {
                    continue;
                }

                UpdateBaseline(actor, target.LocalPosition, target.SortingOrder);
                actor.localPosition = target.LocalPosition;
                SelectionOptionVisual.ApplySortingOrder(actor, target.SortingOrder);

                SpriteRenderer renderer = GetPrimaryRenderer(actor);
                if (renderer != null)
                {
                    renderer.color = actorStates[actor].BaselineColor;
                }
            }
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
                .DOLocalMove(state.BaselineLocalPosition, duration)
                .SetEase(focusEase)
                .SetTarget(actor);
            ApplyTweenSettings(move);

            SpriteRenderer spriteRenderer = GetPrimaryRenderer(actor);
            if (spriteRenderer != null)
            {
                Tween color = TweenSpriteColor(spriteRenderer, state.BaselineColor, duration, focusEase);
                ApplyTweenSettings(color);
            }

            Tween sort = SelectionOptionVisual.TweenBaseSortingOrder(actor, state.BaselineSortingOrder, duration, focusEase);
            ApplyTweenSettings(sort);
        }

        private void EnsureBaseline(Transform actor)
        {
            if (actor == null)
            {
                return;
            }

            if (actorStates.ContainsKey(actor))
            {
                return;
            }

            SpriteRenderer renderer = GetPrimaryRenderer(actor);
            RegisterActor(actor, actor.localPosition, SelectionOptionVisual.GetAnchorSortingOrder(actor));
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

        private void InvalidateActor(Transform actor, bool killLayoutIfActive)
        {
            KillActorTweens(actor);

            if (killLayoutIfActive && layoutSequence != null && layoutSequence.IsActive())
            {
                KillLayoutSequence();
            }

            actorStates.Remove(actor);

            if (ReferenceEquals(focusedActor, actor))
            {
                focusedActor = null;
            }

            if (ReferenceEquals(draggingActor, actor))
            {
                draggingActor = null;
                dragTintKnown = false;
                dragTintInZone = false;
                dragPointerOffsetKnown = false;
            }
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

        private static Tween TweenSpriteColor(SpriteRenderer renderer, Color endValue, float duration, Ease ease)
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

        private static Tween TweenSortingOrder(SpriteRenderer renderer, int endValue, float duration, Ease ease)
        {
            if (renderer == null)
            {
                return null;
            }

            Tween sort = DOTween
                .To(() => renderer != null ? renderer.sortingOrder : endValue, value =>
                {
                    if (renderer != null)
                    {
                        renderer.sortingOrder = value;
                    }
                }, endValue, duration)
                .SetEase(ease)
                .SetTarget(renderer);
            return sort;
        }

        private static SpriteRenderer GetPrimaryRenderer(Transform actor)
        {
            return actor != null ? actor.GetComponent<SpriteRenderer>() : null;
        }
    }
}
