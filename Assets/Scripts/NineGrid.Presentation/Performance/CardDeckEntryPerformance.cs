using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 牌堆入场：多张卡从当前位置依次滑入槽位锚点。
    /// 烘焙自 CardDeckEntryPerformance Timeline（delay 0.1 递增 × 20，duration 0.3，OutQuint）。
    /// </summary>
    [MovedFrom("NineGrid.Presentation.AtomicRepresentationTools")]
    [DisallowMultipleComponent]
    public sealed class CardDeckEntryPerformance : MonoBehaviour
    {
        [Header("Anchors")]
        [SerializeField] private Transform slotRoot;
        [SerializeField] private Transform[] slotAnchors;

        [Header("Timing (Timeline)")]
        [SerializeField, Min(0f)] private float staggerDelay = 0.1f;
        [SerializeField, Min(0.01f)] private float moveDuration = 0.3f;
        [SerializeField] private Ease moveEase = Ease.OutQuint;

        [Header("Playback Safety")]
        [Tooltip("实例化/布置卡牌的同一帧不启动 Sequence，等下一帧再 Play，避免首帧 deltaTime 尖峰吃掉前几张 tween。")]
        [SerializeField] private bool deferPlayOneFrame = true;
        [SerializeField] private bool ignoreTimeScale;

        [Header("Preview (Optional)")]
        [SerializeField] private Transform[] previewCards;

        [Header("Events")]
        [SerializeField] private UnityEvent onComplete;

        private readonly List<Vector3> baselineWorldPositions = new();
        private readonly List<Transform> activeCards = new();
        private Sequence activeSequence;
        private Coroutine playCoroutine;
        private bool isPlaying;

        public bool IsPlaying => isPlaying;
        public float TotalDuration => ComputeTotalDuration(activeCards.Count);

        private Transform ResolvedSlotRoot => slotRoot != null ? slotRoot : transform;

        private void OnDisable()
        {
            StopAndRestore();
        }

        private void OnValidate()
        {
            staggerDelay = Mathf.Max(0f, staggerDelay);
            moveDuration = Mathf.Max(0.01f, moveDuration);
        }

        [ContextMenu("Play Preview")]
        public void PlayPreview()
        {
            Play(previewCards);
        }

        public void Play()
        {
            Play(previewCards);
        }

        public void Play(Transform[] cards)
        {
            Play((IReadOnlyList<Transform>)cards);
        }

        public void Play(IReadOnlyList<Transform> cards)
        {
            Play(cards, null);
        }

        public void Play(IReadOnlyList<Transform> cards, IReadOnlyList<Transform> slots)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (cards == null || cards.Count == 0)
            {
                return;
            }

            IReadOnlyList<Transform> resolvedSlots = slots ?? ResolveSlotAnchors();
            if (resolvedSlots.Count < cards.Count)
            {
                Debug.LogWarning(
                    $"[{nameof(CardDeckEntryPerformance)}] slot count ({resolvedSlots.Count}) < card count ({cards.Count}).",
                    this);
            }

            StopPlaybackOnly();

            activeCards.Clear();
            baselineWorldPositions.Clear();

            int pairCount = Mathf.Min(cards.Count, resolvedSlots.Count);
            for (var i = 0; i < pairCount; i++)
            {
                Transform card = cards[i];
                if (card == null)
                {
                    continue;
                }

                activeCards.Add(card);
                baselineWorldPositions.Add(card.position);
            }

            if (activeCards.Count == 0)
            {
                return;
            }

            activeSequence = BuildSequence(activeCards, resolvedSlots);
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

        [ContextMenu("Stop And Restore")]
        public void StopAndRestore()
        {
            StopPlaybackOnly();

            for (var i = 0; i < activeCards.Count; i++)
            {
                Transform card = activeCards[i];
                if (card == null)
                {
                    continue;
                }

                card.position = baselineWorldPositions[i];
            }

            activeCards.Clear();
            baselineWorldPositions.Clear();
            isPlaying = false;
        }

        public float ComputeTotalDuration(int cardCount)
        {
            if (cardCount <= 0)
            {
                return 0f;
            }

            return (cardCount - 1) * staggerDelay + moveDuration;
        }

        private Sequence BuildSequence(IReadOnlyList<Transform> cards, IReadOnlyList<Transform> slots)
        {
            var sequence = DOTween.Sequence()
                .SetTarget(this)
                .SetAutoKill(true)
                .Pause();

            if (ignoreTimeScale)
            {
                sequence.SetUpdate(true);
            }

            int pairCount = Mathf.Min(cards.Count, slots.Count);
            for (var i = 0; i < pairCount; i++)
            {
                Transform card = cards[i];
                Transform slot = slots[i];
                if (card == null || slot == null)
                {
                    continue;
                }

                Tween move = card
                    .DOMove(slot.position, moveDuration)
                    .SetEase(moveEase)
                    .SetTarget(this);

                if (ignoreTimeScale)
                {
                    move.SetUpdate(true);
                }

                sequence.Insert(staggerDelay * i, move);
            }

            sequence.OnComplete(HandleSequenceComplete);
            sequence.OnKill(HandleSequenceKilled);
            return sequence;
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

            foreach (Transform card in activeCards)
            {
                if (card != null)
                {
                    DOTween.Kill(card);
                }
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

        private IReadOnlyList<Transform> ResolveSlotAnchors()
        {
            if (slotAnchors != null && slotAnchors.Length > 0)
            {
                return slotAnchors;
            }

            Transform root = ResolvedSlotRoot;
            int childCount = root.childCount;
            if (childCount == 0)
            {
                return Array.Empty<Transform>();
            }

            var resolved = new Transform[childCount];
            for (var i = 0; i < childCount; i++)
            {
                resolved[i] = root.GetChild(i);
            }

            return resolved;
        }
    }
}
