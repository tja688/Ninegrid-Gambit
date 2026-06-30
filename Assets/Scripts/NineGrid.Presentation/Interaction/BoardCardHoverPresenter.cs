using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Shared;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 场地卡 Hover 本地反馈：烘焙自场景 NineGrid PreChoise / NineGrid Deselect Timeline。
    /// 进入：均匀放大 + Z 轴 wobble（10° → -10° → 基准）；退出：相对缩小 -0.1。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "CardHoverPerformance")]
    public sealed class BoardCardHoverPresenter : MonoBehaviour, IInteractivePresenter
    {
        [Header("Focus Enter (NineGrid PreChoise)")]
        [SerializeField] private float focusScaleDelta = 0.1f;
        [SerializeField, Min(0f)] private float focusScaleDuration = 0.3f;
        [SerializeField, Min(0f)] private float focusWobbleStepDuration = 0.1f;
        [SerializeField, Min(0f)] private float focusWobbleSecondDelay = 0.1f;
        [SerializeField, Min(0f)] private float focusWobbleThirdDelay = 0.2f;
        [SerializeField] private float focusWobbleAngleZ = 10f;
        [SerializeField] private Ease focusEase = Ease.OutQuad;

        [Header("Focus Exit (NineGrid Deselect)")]
        [SerializeField] private float deselectScaleDelta = -0.1f;
        [SerializeField, Min(0f)] private float deselectDuration = 0.1f;
        [SerializeField] private Ease deselectEase = Ease.OutQuad;

        [Header("Reject")]
        [SerializeField, Min(0.01f)] private float rejectShakeDuration = 0.25f;
        [SerializeField] private Vector3 rejectShakeStrength = new(0.08f, 0.08f, 0f);

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;

        [Header("Preview")]
        [SerializeField] private Transform previewActor;
        [SerializeField] private GameObject cardPreviewPrefab;

        private readonly Dictionary<Transform, ActorVisualState> actorStates = new();
        private Transform focusedActor;

        private struct ActorVisualState
        {
            public Vector3 BaselineLocalPosition;
            public Vector3 BaselineLocalScale;
            public Vector3 BaselineLocalEuler;
            public int BaselineSortingOrder;
            public bool HasBaseline;
        }

        private void OnDisable()
        {
            StopAndRestore();
        }

        private void OnValidate()
        {
            focusScaleDuration = Mathf.Max(0f, focusScaleDuration);
            focusWobbleStepDuration = Mathf.Max(0f, focusWobbleStepDuration);
            focusWobbleSecondDelay = Mathf.Max(0f, focusWobbleSecondDelay);
            focusWobbleThirdDelay = Mathf.Max(0f, focusWobbleThirdDelay);
            deselectDuration = Mathf.Max(0f, deselectDuration);
            rejectShakeDuration = Mathf.Max(0.01f, rejectShakeDuration);
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
                PlayDeselect(focusedActor);
            }
            else
            {
                StopAndRestore();
            }
        }

        public void ForceReset()
        {
            StopAndRestore(null);
        }

        public void Play(Transform actor)
        {
            if (actor == null)
            {
                return;
            }

            KillActorTweens(actor);
            EnsureBaseline(actor);
            SnapActorVisual(actor);
            focusedActor = actor;

            ActorVisualState state = actorStates[actor];
            Vector3 baselineEuler = state.BaselineLocalEuler;
            float wobble = focusWobbleAngleZ;

            Sequence sequence = DOTween.Sequence().SetTarget(actor);
            Tween scaleTween = actor
                .DOScale(Vector3.one * focusScaleDelta, focusScaleDuration)
                .SetRelative(true)
                .SetEase(focusEase);
            ApplyTweenSettings(scaleTween);
            sequence.Insert(0f, scaleTween);

            AppendWobbleStep(sequence, actor, baselineEuler + new Vector3(0f, 0f, wobble), 0f);
            AppendWobbleStep(sequence, actor, baselineEuler + new Vector3(0f, 0f, -wobble), focusWobbleSecondDelay);
            AppendWobbleStep(sequence, actor, baselineEuler, focusWobbleThirdDelay);
        }

        public void StopAndRestore(Transform actor = null)
        {
            StopAndRestore(actor, immediate: actor == null);
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
                    SnapActorVisual(actor);
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

        [ContextMenu("Play Preview Hover")]
        private void PlayPreviewHover()
        {
            Transform actor = EnsurePreviewActor();
            if (actor == null)
            {
                return;
            }

            Play(actor);
        }

        [ContextMenu("Play Preview Deselect")]
        private void PlayPreviewDeselect()
        {
            Transform actor = EnsurePreviewActor();
            if (actor == null)
            {
                return;
            }

            Play(actor);
            StopAndRestore(actor);
        }

        private void StopAndRestore(Transform actor, bool immediate)
        {
            if (actor != null)
            {
                if (immediate)
                {
                    SnapActorVisual(actor);
                }
                else
                {
                    PlayDeselect(actor);
                }

                if (ReferenceEquals(focusedActor, actor))
                {
                    focusedActor = null;
                }

                return;
            }

            var actors = new List<Transform>(actorStates.Keys);
            for (var i = 0; i < actors.Count; i++)
            {
                SnapActorVisual(actors[i]);
            }

            focusedActor = null;
        }

        private void PlayDeselect(Transform actor)
        {
            if (actor == null)
            {
                return;
            }

            KillActorTweens(actor);
            EnsureBaseline(actor);

            if (deselectDuration <= 0f || Mathf.Approximately(deselectScaleDelta, 0f))
            {
                SnapActorVisual(actor);
                return;
            }

            Tween scaleTween = actor
                .DOScale(Vector3.one * deselectScaleDelta, deselectDuration)
                .SetRelative(true)
                .SetEase(deselectEase)
                .SetTarget(actor);
            ApplyTweenSettings(scaleTween);
            scaleTween.OnComplete(() =>
            {
                if (actor != null)
                {
                    SnapActorVisual(actor);
                }
            });
        }

        private void AppendWobbleStep(Sequence sequence, Transform actor, Vector3 targetEuler, float atTime)
        {
            Tween rotateTween = actor
                .DOLocalRotate(targetEuler, focusWobbleStepDuration)
                .SetEase(focusEase);
            ApplyTweenSettings(rotateTween);
            sequence.Insert(atTime, rotateTween);
        }

        private void SnapActorVisual(Transform actor)
        {
            if (actor == null || !actorStates.TryGetValue(actor, out ActorVisualState state))
            {
                return;
            }

            KillActorTweens(actor);
            actor.localPosition = state.BaselineLocalPosition;
            actor.localScale = state.BaselineLocalScale;
            actor.localEulerAngles = state.BaselineLocalEuler;
            SelectionOptionVisual.ApplySortingOrder(actor, state.BaselineSortingOrder);
        }

        private void RegisterBaseline(Transform actor, Vector3 baselineLocalPosition, int baselineSortingOrder)
        {
            actorStates[actor] = new ActorVisualState
            {
                BaselineLocalPosition = baselineLocalPosition,
                BaselineLocalScale = actor.localScale,
                BaselineLocalEuler = actor.localEulerAngles,
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

            RegisterBaseline(
                actor,
                actor.localPosition,
                SelectionOptionVisual.GetAnchorSortingOrder(actor));
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

        private Transform EnsurePreviewActor()
        {
            if (previewActor != null)
            {
                return previewActor;
            }

            if (cardPreviewPrefab == null)
            {
                Debug.LogWarning($"{nameof(BoardCardHoverPresenter)} preview actor is not assigned.", this);
                return null;
            }

            GameObject instance = Instantiate(cardPreviewPrefab, transform);
            instance.name = $"{cardPreviewPrefab.name} (Hover Preview)";
            previewActor = instance.transform;
            return previewActor;
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
