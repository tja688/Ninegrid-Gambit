using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Contracts;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Flow.Deck
{
    /// <summary>
    /// 牌堆入场：多张卡从当前位置依次滑入槽位锚点。
    /// 烘焙自 CardDeckEntryPerformance Timeline（delay 0.1 递增 × 20，duration 0.3，OutQuint）。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "CardDeckEntryPerformance")]
    public sealed class CardDeckEntryFlow : MonoBehaviour, IDirectedFlow
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

        [Header("Preview")]
        [Tooltip("预览用卡牌模板；为 null 时生成纯色 Sprite 占位。")]
        [SerializeField] private GameObject cardPreviewPrefab;
        [Tooltip("动态生成的预览卡父节点；为 null 时使用本物体 Transform。")]
        [SerializeField] private Transform previewActorsRoot;
        [Tooltip("预览卡起始位置；为 null 时使用 previewDeckLocalOffset。")]
        [SerializeField] private Transform previewDeckOrigin;
        [SerializeField] private Vector3 previewDeckLocalOffset = new(9.4375f, 3f, 0f);
        [Tooltip("0 = 自动匹配槽位数量。")]
        [SerializeField, Min(0)] private int previewActorCount;

        [Header("Events")]
        [SerializeField] private UnityEvent onComplete;

        private static Sprite fallbackPreviewSprite;

        private readonly List<Vector3> baselineWorldPositions = new();
        private readonly List<Transform> activeCards = new();
        private readonly List<Transform> spawnedPreviewActors = new();
        private Sequence activeSequence;
        private Coroutine playCoroutine;
        private bool previewActorsOwned;

        public bool IsPlaying { get; private set; }
        public float ExpectedDuration => TotalDuration;
        public float TotalDuration => ComputeTotalDuration(activeCards.Count);

        private Transform ResolvedSlotRoot => slotRoot != null ? slotRoot : transform;
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
            staggerDelay = Mathf.Max(0f, staggerDelay);
            moveDuration = Mathf.Max(0.01f, moveDuration);
        }

        [ContextMenu("Play Preview")]
        public void PlayPreview()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            StopAndRestore();
            IReadOnlyList<Transform> previewActors = EnsurePreviewActors();
            if (previewActors.Count == 0)
            {
                Debug.LogWarning(
                    $"[{nameof(CardDeckEntryFlow)}] preview actors could not be created.",
                    this);
                return;
            }

            PlayRuntime(previewActors, null, ownsPreviewActors: true);
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
            PlayRuntime(cards, slots, ownsPreviewActors: false);
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

            activeCards.Clear();
            baselineWorldPositions.Clear();
            IsPlaying = false;
        }

        public float ComputeTotalDuration(int cardCount)
        {
            if (cardCount <= 0)
            {
                return 0f;
            }

            return (cardCount - 1) * staggerDelay + moveDuration;
        }

        private void PlayRuntime(
            IReadOnlyList<Transform> cards,
            IReadOnlyList<Transform> slots,
            bool ownsPreviewActors)
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
                    $"[{nameof(CardDeckEntryFlow)}] slot count ({resolvedSlots.Count}) < card count ({cards.Count}).",
                    this);
            }

            StopPlaybackOnly();

            previewActorsOwned = ownsPreviewActors;
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
                previewActorsOwned = false;
                return;
            }

            activeSequence = BuildSequence(activeCards, resolvedSlots);
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

        private IReadOnlyList<Transform> EnsurePreviewActors()
        {
            TeardownPreviewActors();

            IReadOnlyList<Transform> slots = ResolveSlotAnchors();
            int actorCount = previewActorCount > 0 ? previewActorCount : slots.Count;
            if (actorCount <= 0)
            {
                actorCount = 1;
            }

            Transform parent = ResolvedPreviewActorsRoot;
            Vector3 startWorldPosition = ResolvePreviewDeckWorldPosition(parent);

            for (var i = 0; i < actorCount; i++)
            {
                Transform actor = CreatePreviewActor(parent, i);
                if (actor == null)
                {
                    continue;
                }

                actor.position = startWorldPosition;
                spawnedPreviewActors.Add(actor);
            }

            return spawnedPreviewActors;
        }

        private void TeardownPreviewActors()
        {
            for (var i = spawnedPreviewActors.Count - 1; i >= 0; i--)
            {
                Transform actor = spawnedPreviewActors[i];
                if (actor == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(actor.gameObject);
                }
                else
                {
                    DestroyImmediate(actor.gameObject);
                }
            }

            spawnedPreviewActors.Clear();
            previewActorsOwned = false;
        }

        private Vector3 ResolvePreviewDeckWorldPosition(Transform parent)
        {
            if (previewDeckOrigin != null)
            {
                return previewDeckOrigin.position;
            }

            return parent.TransformPoint(previewDeckLocalOffset);
        }

        private Transform CreatePreviewActor(Transform parent, int index)
        {
            if (cardPreviewPrefab != null)
            {
                GameObject instance = Instantiate(cardPreviewPrefab, parent);
                instance.name = $"PreviewCard_{index + 1}";
                return instance.transform;
            }

            var stub = new GameObject($"PreviewCard_{index + 1}");
            stub.transform.SetParent(parent, worldPositionStays: false);

            var spriteRenderer = stub.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = GetFallbackPreviewSprite();
            spriteRenderer.sortingOrder = 3;

            return stub.transform;
        }

        private static Sprite GetFallbackPreviewSprite()
        {
            if (fallbackPreviewSprite != null)
            {
                return fallbackPreviewSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, new Color(0.82f, 0.76f, 0.66f, 1f));
            texture.Apply();

            fallbackPreviewSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                100f);
            return fallbackPreviewSprite;
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

            var resolved = new List<Transform>(childCount);
            for (var i = 0; i < childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (!IsDeckPileSlotAnchor(child.name))
                {
                    continue;
                }

                resolved.Add(child);
            }

            return resolved;
        }

        private static bool IsDeckPileSlotAnchor(string anchorName)
        {
            if (string.IsNullOrEmpty(anchorName) || !anchorName.StartsWith("slot", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return int.TryParse(anchorName.Substring(4), out int slotNumber) && slotNumber >= 1;
        }
    }
}
