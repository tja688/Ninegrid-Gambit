using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Core;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Shared;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Flow.Deck
{
    /// <summary>
    /// 牌堆补牌：单张卡从牌堆原点飞入场地空槽。
    /// 手感对齐 <see cref="CardDeckEntryFlow"/>（duration 0.3，OutQuint）。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "CardDeckSubstitutePerformance")]
    public sealed class CardDeckSubstituteFlow : MonoBehaviour, IDirectedFlow
    {
        [Header("Deck Origin")]
        [SerializeField] private Transform deckOrigin;
        [SerializeField] private Vector3 deckLocalOffset = new(9.4375f, 3f, 0f);

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float moveDuration = 0.3f;
        [SerializeField] private Ease moveEase = Ease.OutQuint;

        [Tooltip("播放前将卡瞬移到牌堆原点。")]
        [SerializeField] private bool snapToDeckOnPlay = true;

        [Header("Speed")]
        [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;

        [Header("Playback Safety")]
        [SerializeField] private bool deferPlayOneFrame = true;
        [SerializeField] private bool ignoreTimeScale;

        [Header("Preview")]
        [SerializeField] private Transform slotRoot;
        [SerializeField] private Transform[] slotAnchors;
        [SerializeField] private GameObject cardPreviewPrefab;
        [SerializeField] private Transform previewActorsRoot;
        [SerializeField] private int baseSortingOrder = 6;

        [Header("Events")]
        [SerializeField] private UnityEvent onComplete;

        private readonly List<Transform> activeCards = new();
        private readonly List<Vector3> baselineWorldPositions = new();
        private readonly List<Transform> spawnedPreviewActors = new();

        private Sequence activeSequence;
        private Coroutine playCoroutine;
        private bool previewActorsOwned;
        private Transform hiddenVacantActor;
        private bool hiddenVacantActorWasActive = true;

        public bool IsPlaying { get; private set; }
        public float ExpectedDuration => TotalDuration;
        public float MoveDuration => moveDuration;
        public Ease MoveEase => moveEase;
        public float PlaybackSpeed => playbackSpeed;
        public bool IgnoreTimeScale => ignoreTimeScale;
        public float ScaledMoveDuration => moveDuration / Mathf.Max(0.01f, playbackSpeed);
        public float TotalDuration => ScaledMoveDuration;

        private Transform ResolvedPreviewActorsRoot => previewActorsRoot != null ? previewActorsRoot : transform;

        private void OnDisable()
        {
            StopAndRestore();
        }

        private void OnDestroy()
        {
            TeardownPreviewActors();
        }

        private void OnValidate()
        {
            moveDuration = Mathf.Max(0.01f, moveDuration);
            playbackSpeed = Mathf.Max(0.01f, playbackSpeed);
        }

        [ContextMenu("Play Preview")]
        public void PlayPreview()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            StopAndRestore();
            IReadOnlyList<Transform> slots = ResolveSlotAnchors();
            if (slots.Count == 0)
            {
                Debug.LogWarning($"[{nameof(CardDeckSubstituteFlow)}] no slot anchors resolved.", this);
                return;
            }

            Transform card = CreatePreviewActor(ResolvedPreviewActorsRoot, 0);
            if (card == null)
            {
                return;
            }

            spawnedPreviewActors.Add(card);
            previewActorsOwned = true;
            card.position = ResolveDeckWorldPosition(ResolvedPreviewActorsRoot);
            PlayRuntime(card, slots[0], ownsPreviewActors: true);
        }

        /// <summary>
        /// 调试/补位预览：外圈保留已有卡牌，缺位一格，从牌堆飞入补位。
        /// </summary>
        public bool TryPlayGapFillPreview(IViewRegistry registry, SlotId vacantSlot)
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                return false;
            }

            SlotId resolvedVacant = vacantSlot.IsBoardSlot ? vacantSlot : SlotId.Board(2);
            Transform targetSlot = registry != null
                ? registry.ResolveAnchor(resolvedVacant)
                : BoardRingPath.ResolveBoardAnchor(slotRoot, resolvedVacant.Index);
            if (targetSlot == null)
            {
                return false;
            }

            StopAndRestore();
            RestoreHiddenVacantActor();

            if (registry != null)
            {
                hiddenVacantActor = registry.ResolveActor(PresentationFallbackActorUids.BoardCard(resolvedVacant.Index));
                if (hiddenVacantActor != null)
                {
                    hiddenVacantActorWasActive = hiddenVacantActor.gameObject.activeSelf;
                    hiddenVacantActor.gameObject.SetActive(false);
                }
            }

            Transform card = CreatePreviewActor(ResolvedPreviewActorsRoot, 0);
            if (card == null)
            {
                RestoreHiddenVacantActor();
                return false;
            }

            spawnedPreviewActors.Add(card);
            previewActorsOwned = true;
            card.position = ResolveDeckWorldPosition(ResolvedPreviewActorsRoot);
            PlayRuntime(card, targetSlot, ownsPreviewActors: true);
            return true;
        }

        public void Play(Transform card, Transform slot)
        {
            PlayRuntime(card, slot, ownsPreviewActors: false);
        }

        public Tween CreateMoveTween(Transform card, Transform slot)
        {
            if (card == null || slot == null)
            {
                return null;
            }

            Tween move = card
                .DOMove(slot.position, moveDuration)
                .SetEase(moveEase)
                .SetTarget(card);

            ApplyTweenSettings(move);
            return move;
        }

        public void PrepareCardAtDeck(Transform card, Transform parentForLocalOffset = null)
        {
            if (card == null || !snapToDeckOnPlay)
            {
                return;
            }

            card.position = ResolveDeckWorldPosition(parentForLocalOffset != null ? parentForLocalOffset : card.parent);
        }

        public Vector3 ResolveDeckWorldPosition(Transform fallbackParent = null)
        {
            if (deckOrigin != null)
            {
                return deckOrigin.position;
            }

            Transform parent = fallbackParent != null ? fallbackParent : transform;
            return parent.TransformPoint(deckLocalOffset);
        }

        [ContextMenu("Stop And Restore")]
        public void StopAndRestore()
        {
            StopPlaybackOnly();
            RestoreHiddenVacantActor();

            if (previewActorsOwned)
            {
                TeardownPreviewActors();
            }
            else
            {
                RestoreBaselines();
            }

            activeCards.Clear();
            baselineWorldPositions.Clear();
            IsPlaying = false;
            previewActorsOwned = false;
        }

        private void PlayRuntime(Transform card, Transform slot, bool ownsPreviewActors)
        {
            if (!isActiveAndEnabled || card == null || slot == null)
            {
                return;
            }

            StopPlaybackOnly();

            previewActorsOwned = ownsPreviewActors;
            activeCards.Clear();
            baselineWorldPositions.Clear();
            activeCards.Add(card);
            baselineWorldPositions.Add(card.position);

            PrepareCardAtDeck(card);

            activeSequence = BuildSequence(card, slot);
            IsPlaying = true;

            if (deferPlayOneFrame)
            {
                playCoroutine = StartCoroutine(PlayNextFrame(activeSequence));
            }
            else
            {
                StartSequence(activeSequence);
            }
        }

        private Sequence BuildSequence(Transform card, Transform slot)
        {
            var sequence = DOTween.Sequence()
                .SetTarget(this)
                .SetAutoKill(true)
                .Pause();

            ApplySequenceSettings(sequence);

            Tween move = CreateMoveTween(card, slot);
            if (move != null)
            {
                sequence.Append(move);
            }

            sequence.OnComplete(HandleSequenceComplete);
            sequence.OnKill(HandleSequenceKilled);
            return sequence;
        }

        internal void ApplySequenceSettings(Sequence sequence)
        {
            if (sequence == null)
            {
                return;
            }

            if (ignoreTimeScale)
            {
                sequence.SetUpdate(true);
            }

            sequence.timeScale = playbackSpeed;
        }

        internal void ApplyTweenSettings(Tween tween)
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

        private IEnumerator PlayNextFrame(Sequence sequence)
        {
            yield return null;
            playCoroutine = null;

            if (sequence == null || !ReferenceEquals(sequence, activeSequence))
            {
                yield break;
            }

            StartSequence(sequence);
        }

        private void StartSequence(Sequence sequence)
        {
            if (sequence == null)
            {
                return;
            }

            sequence.Restart();
        }

        private void StopPlaybackOnly()
        {
            if (playCoroutine != null)
            {
                StopCoroutine(playCoroutine);
                playCoroutine = null;
            }

            if (activeSequence != null && activeSequence.IsActive())
            {
                activeSequence.OnComplete(null);
                activeSequence.OnKill(null);
                activeSequence.Kill();
            }

            activeSequence = null;
            DOTween.Kill(this);

            for (var i = 0; i < activeCards.Count; i++)
            {
                Transform card = activeCards[i];
                if (card != null)
                {
                    DOTween.Kill(card);
                }
            }
        }

        private void RestoreBaselines()
        {
            for (var i = 0; i < activeCards.Count; i++)
            {
                Transform card = activeCards[i];
                if (card == null)
                {
                    continue;
                }

                card.position = baselineWorldPositions[i];
            }
        }

        private void HandleSequenceComplete()
        {
            IsPlaying = false;
            activeSequence = null;
            onComplete?.Invoke();
        }

        private void HandleSequenceKilled()
        {
            if (activeSequence != null && !activeSequence.IsComplete())
            {
                IsPlaying = false;
                activeSequence = null;
            }
        }

        private Transform CreatePreviewActor(Transform parent, int index)
        {
            Transform actor = SelectionOptionVisual.CreatePreviewCard(
                parent,
                index,
                cardPreviewPrefab,
                Vector3.zero,
                0f,
                baseSortingOrder + index);
            actor.name = $"PreviewSubstitute_{index + 1}";
            return actor;
        }

        private void RestoreHiddenVacantActor()
        {
            if (hiddenVacantActor == null)
            {
                return;
            }

            hiddenVacantActor.gameObject.SetActive(hiddenVacantActorWasActive);
            hiddenVacantActor = null;
        }

        private void TeardownPreviewActors()
        {
            SelectionOptionVisual.DestroyActors(spawnedPreviewActors);
            previewActorsOwned = false;
        }

        private IReadOnlyList<Transform> ResolveSlotAnchors()
        {
            if (slotAnchors != null && slotAnchors.Length > 0)
            {
                return slotAnchors;
            }

            if (slotRoot == null)
            {
                return Array.Empty<Transform>();
            }

            int childCount = slotRoot.childCount;
            if (childCount == 0)
            {
                return Array.Empty<Transform>();
            }

            var resolved = new Transform[childCount];
            for (var i = 0; i < childCount; i++)
            {
                resolved[i] = slotRoot.GetChild(i);
            }

            return resolved;
        }
    }
}
