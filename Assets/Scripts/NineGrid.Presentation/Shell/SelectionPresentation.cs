using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Shell
{
    /// <summary>
    /// 选择双路由黑盒：
    /// General — 通关帮助卡三选一 / 道具选项 / 其他选项（扇形悬停、入场、确认、落屏）；
    /// RoomChoice — 房间二选一（缩放悬停、入离场）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelectionPresentation : MonoBehaviour
    {
        public readonly struct GeneralOption
        {
            public GeneralOption(
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

        public readonly struct RoomOption
        {
            public RoomOption(Transform transform, Vector3 baseLocalScale, int baselineSortingOrder)
            {
                Transform = transform;
                BaseLocalScale = baseLocalScale;
                BaselineSortingOrder = baselineSortingOrder;
            }

            public Transform Transform { get; }
            public Vector3 BaseLocalScale { get; }
            public int BaselineSortingOrder { get; }
        }

        public readonly struct RoomMove
        {
            public RoomMove(Transform card, Transform start, Vector3 homeWorldPosition)
            {
                Card = card;
                Start = start;
                HomeWorldPosition = homeWorldPosition;
            }

            public Transform Card { get; }
            public Transform Start { get; }
            public Vector3 HomeWorldPosition { get; }
        }

        [Header("General Hover")]
        [SerializeField, Min(0f)] private float generalPushOffset = 1.6f;
        [SerializeField, Min(0f)] private float generalLiftY = 0.28f;
        [SerializeField, Min(0.05f)] private float generalHoverDuration = 0.4f;
        [SerializeField, Min(0f)] private float generalSiblingDelayStep = 0.03f;
        [SerializeField, Min(0f)] private float generalHoverOvershoot = 1.4f;
        [SerializeField, Min(0)] private int generalFocusSortingBoost = 20;

        [Header("General Entrance")]
        [SerializeField, Min(0f)] private float generalEntryDelay = 0.42f;
        [SerializeField, Min(0f)] private float generalEntryStagger = 0.08f;
        [SerializeField, Min(0.1f)] private float generalEntryDuration = 0.7f;
        [SerializeField] private Ease generalEntryEase = Ease.OutElastic;

        [Header("General Confirm")]
        [SerializeField, Min(0f)] private float generalConfirmLiftY = 0.28f;
        [SerializeField, Min(0.05f)] private float generalConfirmLiftDuration = 0.5f;
        [SerializeField, Min(0f)] private float generalConfirmOvershoot = 1.4f;
        [SerializeField, Min(0)] private int generalConfirmSortingBoost = 40;
        [SerializeField, Min(0f)] private float generalConfirmMoveDelay = 0.12f;
        [SerializeField, Min(0.05f)] private float generalConfirmMoveDuration = 0.5f;

        [Header("General Fall Off")]
        [SerializeField, Min(0f)] private float fallLaunchUpward = 3.4f;
        [SerializeField, Min(0.1f)] private float fallGravity = 24f;
        [SerializeField, Min(0f)] private float fallHorizontalReach = 4.2f;
        [SerializeField, Min(0f)] private float fallHorizontalReachStep = 0.9f;
        [SerializeField, Min(0f)] private float fallSpinMin = 55f;
        [SerializeField, Min(0f)] private float fallSpinMax = 145f;
        [SerializeField, Min(0f)] private float fallStaggerStep = 0.035f;
        [SerializeField, Min(0f)] private float fallBelowScreenPadding = 1.6f;
        [SerializeField, Min(0.01f)] private float fallCardHalfHeight = 0.55f;

        [Header("Room Hover")]
        [SerializeField, Min(1f)] private float roomHoverScale = 1.1f;
        [SerializeField, Min(0.05f)] private float roomHoverDuration = 0.25f;
        [SerializeField, Min(0f)] private float roomHoverOvershoot = 1.2f;
        [SerializeField, Min(0)] private int roomFocusSortingBoost = 10;

        [Header("Room In")]
        [SerializeField, Min(0f)] private float roomInStaggerDelay = 0.08f;
        [SerializeField, Min(0.05f)] private float roomInMoveDuration = 0.55f;
        [SerializeField] private Ease roomInMoveEase = Ease.OutBack;
        [SerializeField, Min(0f)] private float roomInOvershoot = 1.1f;

        [Header("Room Out")]
        [SerializeField, Min(0.05f)] private float roomOutMoveDuration = 0.45f;
        [SerializeField] private Ease roomOutMoveEase = Ease.InBack;
        [SerializeField, Min(0f)] private float roomOutOvershoot = 1f;
        [SerializeField] private bool roomOutHideUnselectedImmediately = true;

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;
        [SerializeField] private bool deferPlayOneFrame = true;
        [SerializeField] private Camera worldCamera;

        private readonly List<GeneralOption> generalOptions = new();
        private readonly List<RoomOption> roomOptions = new();
        private readonly List<Tween> activeTweens = new();
        private readonly List<Transform> activeGeneralEntranceOptions = new();
        private readonly List<Tween> activeFallTweens = new();

        private int generalHoveredIndex = -1;
        private int roomHoveredIndex = -1;
        private Sequence activeSequence;
        private Coroutine deferCoroutine;

        public bool IsPlaying { get; private set; }
        public float ExpectedDuration { get; private set; }

        private void OnDisable()
        {
            StopAllPlayback();
        }

        public void SetGeneralOptions(IReadOnlyList<GeneralOption> options)
        {
            StopGeneralHover();
            generalOptions.Clear();
            if (options == null)
            {
                return;
            }

            for (var i = 0; i < options.Count; i++)
            {
                generalOptions.Add(options[i]);
            }
        }

        public void SetRoomOptions(IReadOnlyList<RoomOption> options)
        {
            StopRoomHover();
            roomOptions.Clear();
            if (options == null)
            {
                return;
            }

            for (var i = 0; i < options.Count; i++)
            {
                roomOptions.Add(options[i]);
            }
        }

        public void PlayGeneralHover(int index)
        {
            if (index < 0 || index >= generalOptions.Count)
            {
                PlayGeneralReset();
                return;
            }

            if (index == generalHoveredIndex)
            {
                return;
            }

            generalHoveredIndex = index;
            KillActiveTweens();

            for (var i = 0; i < generalOptions.Count; i++)
            {
                GeneralOption actor = generalOptions[i];
                if (actor.Transform == null)
                {
                    continue;
                }

                if (i == index)
                {
                    SelectionOptionVisual.ApplySortingOrder(
                        actor.Transform,
                        actor.BaselineSortingOrder + generalOptions.Count + generalFocusSortingBoost);
                    TrackTween(actor.Transform
                        .DOLocalMove(actor.BaseLocalPosition + new Vector3(0f, generalLiftY, 0f), generalHoverDuration)
                        .SetEase(Ease.OutBack, generalHoverOvershoot));
                    TrackTween(actor.Transform
                        .DOLocalRotate(Vector3.zero, generalHoverDuration)
                        .SetEase(Ease.OutBack, generalHoverOvershoot));
                    continue;
                }

                SelectionOptionVisual.ApplySortingOrder(actor.Transform, actor.BaselineSortingOrder);
                float direction = i < index ? -1f : 1f;
                int distance = Mathf.Abs(index - i);
                Vector3 targetPos = actor.BaseLocalPosition + new Vector3(direction * generalPushOffset, 0f, 0f);
                float delay = generalSiblingDelayStep * distance;

                TrackTween(actor.Transform
                    .DOLocalMove(targetPos, generalHoverDuration)
                    .SetDelay(delay)
                    .SetEase(Ease.OutBack, generalHoverOvershoot));
                TrackTween(actor.Transform
                    .DOLocalRotate(new Vector3(0f, 0f, actor.BaseLocalRotationZ), generalHoverDuration)
                    .SetDelay(delay)
                    .SetEase(Ease.OutBack, generalHoverOvershoot));
            }
        }

        public void PlayGeneralReset()
        {
            if (generalHoveredIndex < 0)
            {
                return;
            }

            generalHoveredIndex = -1;
            KillActiveTweens();

            for (var i = 0; i < generalOptions.Count; i++)
            {
                GeneralOption actor = generalOptions[i];
                if (actor.Transform == null)
                {
                    continue;
                }

                SelectionOptionVisual.ApplySortingOrder(actor.Transform, actor.BaselineSortingOrder);
                TrackTween(actor.Transform
                    .DOLocalMove(actor.BaseLocalPosition, generalHoverDuration)
                    .SetEase(Ease.OutBack, generalHoverOvershoot));
                TrackTween(actor.Transform
                    .DOLocalRotate(new Vector3(0f, 0f, actor.BaseLocalRotationZ), generalHoverDuration)
                    .SetEase(Ease.OutBack, generalHoverOvershoot));
            }
        }

        public void ForceGeneralReset()
        {
            KillActiveTweens();
            generalHoveredIndex = -1;

            for (var i = 0; i < generalOptions.Count; i++)
            {
                GeneralOption actor = generalOptions[i];
                if (actor.Transform == null)
                {
                    continue;
                }

                actor.Transform.localPosition = actor.BaseLocalPosition;
                actor.Transform.localRotation = Quaternion.Euler(0f, 0f, actor.BaseLocalRotationZ);
                SelectionOptionVisual.ApplySortingOrder(actor.Transform, actor.BaselineSortingOrder);
            }
        }

        public void PlayGeneralEntrance(IReadOnlyList<Transform> options, Action onFinished = null)
        {
            if (!isActiveAndEnabled || options == null || options.Count == 0)
            {
                onFinished?.Invoke();
                return;
            }

            StopSequencePlayback();
            activeGeneralEntranceOptions.Clear();

            for (var i = 0; i < options.Count; i++)
            {
                Transform option = options[i];
                if (option == null)
                {
                    continue;
                }

                option.localScale = Vector3.zero;
                activeGeneralEntranceOptions.Add(option);
            }

            if (activeGeneralEntranceOptions.Count == 0)
            {
                onFinished?.Invoke();
                return;
            }

            ExpectedDuration = generalEntryDelay
                + ((activeGeneralEntranceOptions.Count - 1) * generalEntryStagger)
                + generalEntryDuration;
            activeSequence = BuildGeneralEntranceSequence(onFinished);
            IsPlaying = true;
            StartDeferredSequence(activeSequence);
        }

        public void PlayGeneralConfirm(
            Transform selected,
            Transform targetSlot,
            int baselineSortingOrder,
            int optionCount,
            Action onComplete = null)
        {
            if (selected == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopSequencePlayback();
            SelectionOptionVisual.ApplySortingOrder(
                selected,
                baselineSortingOrder + optionCount + generalConfirmSortingBoost);

            activeSequence = DOTween.Sequence().SetTarget(this);
            ApplyTimeScale(activeSequence);
            activeSequence.Append(
                selected.DOLocalMove(selected.localPosition + new Vector3(0f, generalConfirmLiftY, 0f), generalConfirmLiftDuration)
                    .SetEase(Ease.OutBack, generalConfirmOvershoot));
            activeSequence.Join(
                selected.DOLocalRotate(Vector3.zero, generalConfirmLiftDuration)
                    .SetEase(Ease.OutBack, generalConfirmOvershoot));

            if (targetSlot != null)
            {
                activeSequence.AppendInterval(generalConfirmMoveDelay);
                activeSequence.Append(
                    selected.DOLocalRotate(Vector3.zero, generalConfirmMoveDuration)
                        .SetEase(Ease.OutBack, generalConfirmOvershoot));
                activeSequence.Join(
                    selected.DOMove(targetSlot.position, generalConfirmMoveDuration)
                        .SetEase(Ease.OutBack, generalConfirmOvershoot));
            }

            IsPlaying = true;
            activeSequence.OnComplete(() =>
            {
                IsPlaying = false;
                onComplete?.Invoke();
            });
            activeSequence.Play();
        }

        public void PlayGeneralFallOff(
            Transform option,
            int optionIndex,
            int selectedIndex,
            float baseLocalRotationZ,
            Action onComplete = null)
        {
            if (option == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopFallTweenFor(option);

            Vector3 start = option.position;
            float awayDirection = optionIndex < selectedIndex ? -1f : 1f;
            int distanceFromSelected = Mathf.Abs(optionIndex - selectedIndex);
            float horizontalOffset = awayDirection
                * (fallHorizontalReach + distanceFromSelected * fallHorizontalReachStep);
            Vector3 end = ResolveFallTarget(start);
            end.x = start.x + horizontalOffset;

            float drop = Mathf.Max(0.1f, start.y - end.y);
            float launchUp = fallLaunchUpward;
            float duration = (launchUp + Mathf.Sqrt(launchUp * launchUp + 2f * fallGravity * drop)) / fallGravity;
            float velocityX = horizontalOffset / duration;
            float startRotZ = option.localEulerAngles.z;
            float spinZ = baseLocalRotationZ + awayDirection * ResolveFallSpin(distanceFromSelected);
            float stagger = fallStaggerStep * distanceFromSelected;

            Tween tween = DOTween.To(() => 0f, progress =>
                {
                    if (option == null)
                    {
                        return;
                    }

                    float elapsed = progress * duration;
                    option.position = new Vector3(
                        start.x + velocityX * elapsed,
                        start.y + launchUp * elapsed - 0.5f * fallGravity * elapsed * elapsed,
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
                activeFallTweens.Remove(tween);
                if (option != null)
                {
                    option.gameObject.SetActive(false);
                }

                onComplete?.Invoke();
            });

            activeFallTweens.Add(tween);
        }

        public void PlayRoomHover(int index)
        {
            if (index < 0 || index >= roomOptions.Count)
            {
                PlayRoomReset();
                return;
            }

            if (index == roomHoveredIndex)
            {
                return;
            }

            roomHoveredIndex = index;
            KillActiveTweens();

            Vector3 hover = Vector3.one * roomHoverScale;
            for (var i = 0; i < roomOptions.Count; i++)
            {
                RoomOption actor = roomOptions[i];
                if (actor.Transform == null)
                {
                    continue;
                }

                bool focused = i == index;
                SelectionOptionVisual.ApplySortingOrder(
                    actor.Transform,
                    actor.BaselineSortingOrder + (focused ? roomFocusSortingBoost : 0));
                Vector3 targetScale = focused ? Vector3.Scale(actor.BaseLocalScale, hover) : actor.BaseLocalScale;
                TrackTween(actor.Transform
                    .DOScale(targetScale, roomHoverDuration)
                    .SetEase(Ease.OutBack, roomHoverOvershoot));
            }
        }

        public void PlayRoomReset()
        {
            if (roomHoveredIndex < 0)
            {
                return;
            }

            roomHoveredIndex = -1;
            KillActiveTweens();

            for (var i = 0; i < roomOptions.Count; i++)
            {
                RoomOption actor = roomOptions[i];
                if (actor.Transform == null)
                {
                    continue;
                }

                SelectionOptionVisual.ApplySortingOrder(actor.Transform, actor.BaselineSortingOrder);
                TrackTween(actor.Transform
                    .DOScale(actor.BaseLocalScale, roomHoverDuration)
                    .SetEase(Ease.OutBack, roomHoverOvershoot));
            }
        }

        public void ForceRoomReset()
        {
            KillActiveTweens();
            roomHoveredIndex = -1;

            for (var i = 0; i < roomOptions.Count; i++)
            {
                RoomOption actor = roomOptions[i];
                if (actor.Transform == null)
                {
                    continue;
                }

                actor.Transform.localScale = actor.BaseLocalScale;
                SelectionOptionVisual.ApplySortingOrder(actor.Transform, actor.BaselineSortingOrder);
            }
        }

        public void PlayRoomEntrance(IReadOnlyList<RoomMove> moves, Action onFinished = null)
        {
            if (!isActiveAndEnabled || moves == null || moves.Count == 0)
            {
                onFinished?.Invoke();
                return;
            }

            StopSequencePlayback();

            var activeCards = new List<Transform>();
            for (var i = 0; i < moves.Count; i++)
            {
                RoomMove move = moves[i];
                if (move.Card == null)
                {
                    continue;
                }

                move.Card.position = move.Start != null ? move.Start.position : move.Card.position;
                activeCards.Add(move.Card);
            }

            if (activeCards.Count == 0)
            {
                onFinished?.Invoke();
                return;
            }

            ExpectedDuration = ((activeCards.Count - 1) * roomInStaggerDelay) + roomInMoveDuration;
            activeSequence = DOTween.Sequence().SetTarget(this);
            ApplyTimeScale(activeSequence);

            var index = 0;
            for (var i = 0; i < moves.Count; i++)
            {
                RoomMove move = moves[i];
                if (move.Card == null)
                {
                    continue;
                }

                float delay = index * roomInStaggerDelay;
                activeSequence.Insert(
                    delay,
                    move.Card.DOMove(move.HomeWorldPosition, roomInMoveDuration)
                        .SetEase(roomInMoveEase, roomInOvershoot));
                index++;
            }

            IsPlaying = true;
            activeSequence.OnComplete(() =>
            {
                IsPlaying = false;
                onFinished?.Invoke();
            });
            StartDeferredSequence(activeSequence);
        }

        public void PlayRoomExit(
            Transform selectedCard,
            Transform selectedEnd,
            Transform unselectedCard,
            Action onFinished = null)
        {
            if (!isActiveAndEnabled || selectedCard == null || selectedEnd == null)
            {
                onFinished?.Invoke();
                return;
            }

            StopSequencePlayback();
            activeSequence = DOTween.Sequence().SetTarget(this);
            ApplyTimeScale(activeSequence);

            if (unselectedCard != null && roomOutHideUnselectedImmediately)
            {
                unselectedCard.gameObject.SetActive(false);
            }

            activeSequence.Insert(
                0f,
                selectedCard.DOMove(selectedEnd.position, roomOutMoveDuration)
                    .SetEase(roomOutMoveEase, roomOutOvershoot));

            IsPlaying = true;
            activeSequence.OnComplete(() =>
            {
                IsPlaying = false;
                onFinished?.Invoke();
            });
            StartDeferredSequence(activeSequence);
        }

        public void StopAllPlayback()
        {
            StopSequencePlayback();
            StopGeneralHover();
            StopRoomHover();
            StopFallTweens();

            for (var i = 0; i < activeGeneralEntranceOptions.Count; i++)
            {
                Transform option = activeGeneralEntranceOptions[i];
                if (option != null)
                {
                    option.localScale = Vector3.one;
                }
            }

            activeGeneralEntranceOptions.Clear();
            IsPlaying = false;
            ExpectedDuration = 0f;
        }

        private Sequence BuildGeneralEntranceSequence(Action onFinished)
        {
            var sequence = DOTween.Sequence().SetTarget(this);
            ApplyTimeScale(sequence);

            for (var i = 0; i < activeGeneralEntranceOptions.Count; i++)
            {
                Transform option = activeGeneralEntranceOptions[i];
                if (option == null)
                {
                    continue;
                }

                float delay = generalEntryDelay + (i * generalEntryStagger);
                sequence.Insert(
                    delay,
                    option.DOScale(Vector3.one, generalEntryDuration).SetEase(generalEntryEase));
            }

            sequence.OnComplete(() =>
            {
                IsPlaying = false;
                onFinished?.Invoke();
            });

            return sequence;
        }

        private void StartDeferredSequence(Sequence sequence)
        {
            if (deferPlayOneFrame)
            {
                deferCoroutine = StartCoroutine(PlayNextFrame(sequence));
            }
            else
            {
                sequence?.Play();
            }
        }

        private IEnumerator PlayNextFrame(Sequence sequence)
        {
            yield return null;
            deferCoroutine = null;
            if (sequence != null && sequence.IsActive())
            {
                sequence.Play();
            }
        }

        private void StopSequencePlayback()
        {
            if (deferCoroutine != null)
            {
                StopCoroutine(deferCoroutine);
                deferCoroutine = null;
            }

            if (activeSequence != null && activeSequence.IsActive())
            {
                activeSequence.Kill(false);
            }

            activeSequence = null;
            DOTween.Kill(this);
        }

        private void StopGeneralHover()
        {
            KillActiveTweens();
            generalHoveredIndex = -1;
        }

        private void StopRoomHover()
        {
            KillActiveTweens();
            roomHoveredIndex = -1;
        }

        private void StopFallTweens()
        {
            for (var i = activeFallTweens.Count - 1; i >= 0; i--)
            {
                Tween tween = activeFallTweens[i];
                if (tween != null && tween.IsActive())
                {
                    tween.Kill(false);
                }
            }

            activeFallTweens.Clear();
        }

        private void StopFallTweenFor(Transform option)
        {
            if (option != null)
            {
                DOTween.Kill(option);
            }

            for (var i = activeFallTweens.Count - 1; i >= 0; i--)
            {
                Tween tween = activeFallTweens[i];
                if (tween == null || !tween.IsActive())
                {
                    activeFallTweens.RemoveAt(i);
                }
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

        private void ApplyTimeScale(Sequence sequence)
        {
            if (ignoreTimeScale)
            {
                sequence.SetUpdate(true);
            }
        }

        private float ResolveFallSpin(int distanceFromSelected)
        {
            return fallSpinMax > fallSpinMin
                ? fallSpinMin + ((distanceFromSelected * 37) % 100) / 100f * (fallSpinMax - fallSpinMin)
                : fallSpinMin;
        }

        private Vector3 ResolveFallTarget(Vector3 start)
        {
            Camera camera = worldCamera != null ? worldCamera : Camera.main;
            if (camera == null)
            {
                return start + new Vector3(0f, -12f, 0f);
            }

            float depth = Mathf.Abs(start.z - camera.transform.position.z);
            Vector3 belowScreen = camera.ViewportToWorldPoint(new Vector3(0.5f, -0.2f, depth));
            float targetY = belowScreen.y - fallBelowScreenPadding - fallCardHalfHeight;
            return new Vector3(start.x, targetY, start.z);
        }
    }
}
