using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Feedback;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 手牌 Hover 聚焦与拖拽跟手、释放区域着色。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandCardDragPresenter : MonoBehaviour, IInteractivePresenter
    {
        [Header("References")]
        [SerializeField] private HandLayoutPresenter layoutPresenter;
        [SerializeField] private CardShakeCue rejectCue;

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

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;

        private Transform focusedActor;
        private bool dragTintKnown;
        private bool dragTintInZone;
        private Vector3 dragPointerOffset;
        private bool dragPointerOffsetKnown;

        private HandLayoutPresenter Layout => layoutPresenter != null
            ? layoutPresenter
            : layoutPresenter = GetComponent<HandLayoutPresenter>();

        private void OnDisable()
        {
            ForceReset();
        }

        private void OnValidate()
        {
            focusDuration = Mathf.Max(0f, focusDuration);
            focusSortingOrderFloor = Mathf.Max(0, focusSortingOrderFloor);
            dragSortingOrderFloor = Mathf.Max(0, dragSortingOrderFloor);
            zoneTintDuration = Mathf.Max(0.01f, zoneTintDuration);
        }

        public void Enter()
        {
        }

        public void UpdatePresenter()
        {
        }

        public void Exit()
        {
            if (focusedActor != null)
            {
                StopFocus(focusedActor, null, immediate: true);
            }
        }

        public void ForceReset()
        {
            focusedActor = null;
            dragTintKnown = false;
            dragTintInZone = false;
            dragPointerOffsetKnown = false;

            if (Layout != null)
            {
                Layout.DraggingActor = null;
            }
        }

        public void PlayFocus(Transform focused, IReadOnlyList<Transform> others)
        {
            if (focused == null || Layout == null)
            {
                return;
            }

            StopFocus(focused, others, immediate: true);
            focusedActor = focused;
            Layout.EnsureBaseline(focused);

            if (!Layout.TryGetState(focused, out HandLayoutPresenter.ActorVisualState state))
            {
                return;
            }

            Vector3 focusPosition = state.BaselineLocalPosition + focusLocalOffset;
            Layout.KillActorTweens(focused);

            Tween move = focused
                .DOLocalMove(focusPosition, focusDuration)
                .SetEase(focusEase)
                .SetTarget(focused);
            ApplyTweenSettings(move);

            SpriteRenderer focusedRenderer = GetPrimaryRenderer(focused);
            if (focusedRenderer != null)
            {
                int boostedOrder = Mathf.Max(
                    state.BaselineSortingOrder + focusSortingBoost,
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

                Layout.EnsureBaseline(other);
                Layout.KillActorTweens(other);

                SpriteRenderer renderer = GetPrimaryRenderer(other);
                if (renderer == null || !Layout.TryGetState(other, out HandLayoutPresenter.ActorVisualState otherState))
                {
                    continue;
                }

                Color targetColor = otherState.BaselineColor;
                targetColor.a = dimmedAlpha;
                Tween fade = HandLayoutPresenter.TweenSpriteColor(renderer, targetColor, focusDuration, focusEase);
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

            if (restoreFocused && Layout != null)
            {
                Layout.RestoreActorVisual(focused, duration);
            }

            if (others == null || Layout == null)
            {
                return;
            }

            for (var i = 0; i < others.Count; i++)
            {
                Layout.RestoreActorVisual(others[i], duration);
            }
        }

        public void BeginDrag(Transform actor, Vector3 pointerWorldPosition)
        {
            if (actor == null || Layout == null)
            {
                return;
            }

            Layout.DraggingActor = actor;
            dragTintKnown = false;
            dragTintInZone = false;
            dragPointerOffset = actor.position - pointerWorldPosition + dragWorldOffset;
            dragPointerOffsetKnown = true;
            Layout.EnsureBaseline(actor);
            Layout.KillActorTweens(actor);

            if (Layout.TryGetState(actor, out HandLayoutPresenter.ActorVisualState state))
            {
                int boostedOrder = Mathf.Max(
                    state.BaselineSortingOrder + focusSortingBoost + 1,
                    dragSortingOrderFloor);
                SelectionOptionVisual.ApplySortingOrder(actor, boostedOrder);
            }
        }

        public void UpdateDrag(Transform actor, Vector3 pointerWorldPosition, bool inZone)
        {
            if (actor == null)
            {
                return;
            }

            actor.position = pointerWorldPosition + (dragPointerOffsetKnown ? dragPointerOffset : dragWorldOffset);

            SpriteRenderer renderer = GetPrimaryRenderer(actor);
            if (renderer == null || Layout == null)
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
            Color baseline = Layout.TryGetState(actor, out HandLayoutPresenter.ActorVisualState state)
                ? state.BaselineColor
                : renderer.color;
            targetTint.a = baseline.a;

            renderer.DOKill();
            Tween tint = HandLayoutPresenter.TweenSpriteColor(renderer, targetTint, zoneTintDuration, Ease.Linear);
            ApplyTweenSettings(tint);
        }

        public void EndDrag()
        {
            if (Layout != null)
            {
                Layout.DraggingActor = null;
            }

            dragTintKnown = false;
            dragTintInZone = false;
            dragPointerOffsetKnown = false;
        }

        public void PlayReject(Transform actor)
        {
            if (actor == null || Layout == null)
            {
                return;
            }

            Layout.KillActorTweens(actor);
            Layout.EnsureBaseline(actor);

            if (rejectCue != null)
            {
                rejectCue.Play(new CueInvocation(actor));
                return;
            }

            Tween shake = actor
                .DOShakePosition(0.25f, new Vector3(0.08f, 0.08f, 0f), vibrato: 12, randomness: 45f, fadeOut: true)
                .SetTarget(actor);
            ApplyTweenSettings(shake);
            shake.OnComplete(() => Layout.RestoreActorVisual(actor, 0f));
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

        private static SpriteRenderer GetPrimaryRenderer(Transform actor)
        {
            return actor != null ? actor.GetComponent<SpriteRenderer>() : null;
        }
    }
}
