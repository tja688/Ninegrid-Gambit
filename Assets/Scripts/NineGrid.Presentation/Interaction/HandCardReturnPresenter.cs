using System;
using DG.Tweening;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 手牌回手与确认打出动效。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandCardReturnPresenter : MonoBehaviour, IInteractivePresenter
    {
        [Header("References")]
        [SerializeField] private HandLayoutPresenter layoutPresenter;

        [Header("Return / Confirm")]
        [SerializeField, Min(0.01f)] private float returnDuration = 0.25f;
        [SerializeField] private Ease returnEase = Ease.OutQuad;
        [SerializeField, Min(0.01f)] private float confirmDuration = 0.18f;
        [SerializeField] private Ease confirmEase = Ease.OutQuad;
        [SerializeField] private Vector3 confirmLocalScalePunch = new(0.08f, 0.08f, 0f);

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;

        private HandLayoutPresenter Layout => layoutPresenter != null
            ? layoutPresenter
            : layoutPresenter = GetComponent<HandLayoutPresenter>();

        private void OnValidate()
        {
            returnDuration = Mathf.Max(0.01f, returnDuration);
            confirmDuration = Mathf.Max(0.01f, confirmDuration);
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
        }

        public void PlayReturn(Transform actor, Vector3 targetLocalPosition, int targetSortingOrder, Action onComplete)
        {
            if (actor == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (Layout == null)
            {
                onComplete?.Invoke();
                return;
            }

            Layout.KillActorTweens(actor);
            Layout.UpdateBaseline(actor, targetLocalPosition, targetSortingOrder);

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
            if (renderer != null && Layout.TryGetState(actor, out HandLayoutPresenter.ActorVisualState state))
            {
                Tween color = HandLayoutPresenter.TweenSpriteColor(renderer, state.BaselineColor, returnDuration, returnEase);
                ApplyTweenSettings(color);
                sequence.Join(color);

                Tween sort = SelectionOptionVisual.TweenBaseSortingOrder(actor, targetSortingOrder, returnDuration, returnEase);
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

            if (Layout == null)
            {
                onComplete?.Invoke();
                return;
            }

            Layout.KillActorTweens(actor);
            Layout.EnsureBaseline(actor);

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

        public float ReturnDuration => returnDuration;

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
