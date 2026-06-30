using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Shared;
using UnityEngine;
using UnityEngine.Events;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 牌堆开局发牌：复数 <see cref="CardDeckSubstitutePerformance"/> 飞入，槽间间隔逐张加速。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardDeckDealCardsPerformance : MonoBehaviour
    {
        [Header("Substitute Unit")]
        [SerializeField] private CardDeckSubstitutePerformance substitutePerformance;

        [Header("Deal Stagger (Acceleration)")]
        [Tooltip("首张与第二张之间的间隔（秒，未计 playbackSpeed）。")]
        [SerializeField, Min(0f)] private float startStaggerDelay = 0.1f;
        [Tooltip("末两张之间的间隔；小于 start 即逐张加速。")]
        [SerializeField, Min(0f)] private float endStaggerDelay = 0.04f;

        [Header("Speed")]
        [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;

        [Header("Playback Safety")]
        [SerializeField] private bool deferPlayOneFrame = true;

        [Header("Preview")]
        [SerializeField] private Transform ringSlotRoot;
        [SerializeField] private Transform[] ringSlots;
        [SerializeField, Min(1)] private int previewDealCount = 8;
        [SerializeField] private int[] standardBoardRingIndices = { 0, 1, 2, 5, 8, 7, 6, 3 };
        [SerializeField] private GameObject cardPreviewPrefab;
        [SerializeField] private Transform previewActorsRoot;
        [SerializeField] private int baseSortingOrder = 6;

        [Header("Events")]
        [SerializeField] private UnityEvent onComplete;

        private readonly List<Transform> activeCards = new();
        private readonly List<Vector3> baselineWorldPositions = new();
        private readonly List<Transform> spawnedPreviewActors = new();
        private readonly List<Transform> resolvedRingSlots = new();

        private Sequence activeSequence;
        private Coroutine playCoroutine;
        private bool isPlaying;
        private bool previewActorsOwned;

        public bool IsPlaying => isPlaying;
        public float PlaybackSpeed => playbackSpeed;
        public float TotalDuration => ComputeTotalDuration(activeCards.Count);

        private Transform ResolvedPreviewActorsRoot => previewActorsRoot != null ? previewActorsRoot : transform;

        private void Awake()
        {
            EnsureSubstituteReference();
        }

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
            startStaggerDelay = Mathf.Max(0f, startStaggerDelay);
            endStaggerDelay = Mathf.Max(0f, endStaggerDelay);
            playbackSpeed = Mathf.Max(0.01f, playbackSpeed);
            previewDealCount = Mathf.Max(1, previewDealCount);
        }

        [ContextMenu("Play Preview")]
        public void PlayPreview()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            StopAndRestore();
            if (!TryResolveRingSlots())
            {
                Debug.LogWarning($"[{nameof(CardDeckDealCardsPerformance)}] ring slots could not be resolved.", this);
                return;
            }

            int dealCount = Mathf.Min(previewDealCount, resolvedRingSlots.Count);
            var cards = new List<Transform>(dealCount);
            var slots = new List<Transform>(dealCount);
            Transform parent = ResolvedPreviewActorsRoot;
            Vector3 deckPosition = ResolveDeckWorldPosition(parent);

            for (var i = 0; i < dealCount; i++)
            {
                Transform slot = resolvedRingSlots[i];
                if (slot == null)
                {
                    continue;
                }

                Transform card = CreatePreviewActor(parent, i);
                card.position = deckPosition;
                spawnedPreviewActors.Add(card);
                cards.Add(card);
                slots.Add(slot);
            }

            if (cards.Count == 0)
            {
                return;
            }

            previewActorsOwned = true;
            PlayRuntime(cards, slots, ownsPreviewActors: true);
        }

        public void Play(Transform[] cards, Transform[] slots)
        {
            Play((IReadOnlyList<Transform>)cards, slots);
        }

        public void Play(IReadOnlyList<Transform> cards, IReadOnlyList<Transform> slots)
        {
            PlayRuntime(cards, slots, ownsPreviewActors: false);
        }

        public float ComputeTotalDuration(int cardCount)
        {
            if (cardCount <= 0)
            {
                return 0f;
            }

            EnsureSubstituteReference();
            float speed = ResolvePlaybackSpeed();
            float scaledMoveDuration = substitutePerformance != null
                ? substitutePerformance.MoveDuration / speed
                : 0.3f / speed;
            float timeline = 0f;

            if (cardCount > 1)
            {
                for (var i = 0; i < cardCount - 1; i++)
                {
                    timeline += ResolveStaggerDelay(i, cardCount) / speed;
                }
            }

            return timeline + scaledMoveDuration;
        }

        [ContextMenu("Stop And Restore")]
        public void StopAndRestore()
        {
            StopPlaybackOnly();

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
            isPlaying = false;
            previewActorsOwned = false;
        }

        private void PlayRuntime(
            IReadOnlyList<Transform> cards,
            IReadOnlyList<Transform> slots,
            bool ownsPreviewActors)
        {
            if (!isActiveAndEnabled || cards == null || slots == null || cards.Count == 0)
            {
                return;
            }

            EnsureSubstituteReference();

            StopPlaybackOnly();

            previewActorsOwned = ownsPreviewActors;
            activeCards.Clear();
            baselineWorldPositions.Clear();

            int pairCount = Mathf.Min(cards.Count, slots.Count);
            for (var i = 0; i < pairCount; i++)
            {
                Transform card = cards[i];
                Transform slot = slots[i];
                if (card == null || slot == null)
                {
                    continue;
                }

                activeCards.Add(card);
                baselineWorldPositions.Add(card.position);
                substitutePerformance.PrepareCardAtDeck(card);
            }

            if (activeCards.Count == 0)
            {
                previewActorsOwned = false;
                return;
            }

            activeSequence = BuildSequence(activeCards, slots);
            isPlaying = true;

            if (deferPlayOneFrame)
            {
                playCoroutine = StartCoroutine(PlayNextFrame(activeSequence));
            }
            else
            {
                StartSequence(activeSequence);
            }
        }

        private Sequence BuildSequence(IReadOnlyList<Transform> cards, IReadOnlyList<Transform> slots)
        {
            var sequence = DOTween.Sequence()
                .SetTarget(this)
                .SetAutoKill(true)
                .Pause();

            substitutePerformance.ApplySequenceSettings(sequence);
            sequence.timeScale = ResolvePlaybackSpeed();

            int pairCount = Mathf.Min(cards.Count, slots.Count);
            float insertTime = 0f;
            for (var i = 0; i < pairCount; i++)
            {
                Transform card = cards[i];
                Transform slot = slots[i];
                if (card == null || slot == null)
                {
                    continue;
                }

                Tween move = substitutePerformance.CreateMoveTween(card, slot);
                if (move != null)
                {
                    sequence.Insert(insertTime, move);
                }

                if (i < pairCount - 1)
                {
                    insertTime += ResolveStaggerDelay(i, pairCount);
                }
            }

            sequence.OnComplete(HandleSequenceComplete);
            sequence.OnKill(HandleSequenceKilled);
            return sequence;
        }

        private float ResolveStaggerDelay(int index, int totalCount)
        {
            if (totalCount <= 1)
            {
                return 0f;
            }

            float t = totalCount <= 1 ? 0f : index / (float)(totalCount - 1);
            return Mathf.Lerp(startStaggerDelay, endStaggerDelay, t);
        }

        private float ResolvePlaybackSpeed()
        {
            float substituteSpeed = substitutePerformance != null ? substitutePerformance.PlaybackSpeed : 1f;
            return Mathf.Max(0.01f, playbackSpeed * substituteSpeed);
        }

        private Vector3 ResolveDeckWorldPosition(Transform parent)
        {
            EnsureSubstituteReference();
            return substitutePerformance != null
                ? substitutePerformance.ResolveDeckWorldPosition(parent)
                : parent.position;
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
            isPlaying = false;
            activeSequence = null;
            onComplete?.Invoke();
        }

        private void HandleSequenceKilled()
        {
            if (activeSequence != null && !activeSequence.IsComplete())
            {
                isPlaying = false;
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
            actor.name = $"PreviewDeal_{index + 1}";
            return actor;
        }

        private void TeardownPreviewActors()
        {
            SelectionOptionVisual.DestroyActors(spawnedPreviewActors);
            previewActorsOwned = false;
        }

        private bool TryResolveRingSlots()
        {
            resolvedRingSlots.Clear();

            if (ringSlots != null && ringSlots.Length > 0)
            {
                for (var i = 0; i < ringSlots.Length; i++)
                {
                    if (ringSlots[i] != null)
                    {
                        resolvedRingSlots.Add(ringSlots[i]);
                    }
                }

                return resolvedRingSlots.Count > 0;
            }

            if (ringSlotRoot == null)
            {
                return false;
            }

            if (standardBoardRingIndices != null && standardBoardRingIndices.Length > 0)
            {
                for (var i = 0; i < standardBoardRingIndices.Length; i++)
                {
                    int childIndex = standardBoardRingIndices[i];
                    if (childIndex < 0 || childIndex >= ringSlotRoot.childCount)
                    {
                        continue;
                    }

                    resolvedRingSlots.Add(ringSlotRoot.GetChild(childIndex));
                }

                if (resolvedRingSlots.Count > 0)
                {
                    return true;
                }
            }

            for (var i = 0; i < ringSlotRoot.childCount; i++)
            {
                resolvedRingSlots.Add(ringSlotRoot.GetChild(i));
            }

            return resolvedRingSlots.Count > 0;
        }

        private void EnsureSubstituteReference()
        {
            if (substitutePerformance == null)
            {
                substitutePerformance = GetComponent<CardDeckSubstitutePerformance>();
            }

            if (substitutePerformance == null)
            {
                substitutePerformance = gameObject.AddComponent<CardDeckSubstitutePerformance>();
            }
        }
    }
}
