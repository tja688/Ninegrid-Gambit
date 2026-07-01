using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Shared;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Flow.Board
{
    /// <summary>
    /// 棋盘旋转单格跳：每张卡 scale punch + 位移至目标槽位。
    /// 烘焙自 NineGrid Rotation Horizontal/Vertical Timeline（scale 0.25×2 + move 0.5，settle delay 0.25）。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "BoardRotatePerformance")]
    public sealed class BoardRotateFlow : MonoBehaviour, IDirectedFlow
    {
        [Header("Timing (Timeline)")]
        [SerializeField, Min(0f)] private float settleDelay = 0.25f;
        [SerializeField, Min(0.01f)] private float scaleDuration = 0.25f;
        [SerializeField, Min(0.01f)] private float moveDuration = 0.5f;
        [SerializeField] private AnimationCurve scaleEaseCurve = new(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.30620915f, 1.0264964f, 2.3131902f, 2.3131902f),
            new Keyframe(1f, 1f, 0f, 0f));
        [SerializeField] private Ease moveEase = Ease.InOutQuart;
        [SerializeField, Min(1f)] private float punchScale = 1.1f;

        [Header("Playback Safety")]
        [SerializeField] private bool deferPlayOneFrame = true;
        [SerializeField] private bool ignoreTimeScale;

        [Header("Preview")]
        [SerializeField] private GameObject cardPreviewPrefab;
        [SerializeField] private Transform previewActorsRoot;
        [SerializeField] private Transform slotRoot;
        [SerializeField] private Transform[] slotAnchors;
        [Tooltip("0 = 自动匹配槽位数量。")]
        [SerializeField, Min(0)] private int previewActorCount;

        [Header("Events")]
        [SerializeField] private UnityEvent onComplete;

        private static Sprite fallbackPreviewSprite;

        private readonly List<Transform> activeActors = new();
        private readonly List<Vector3> baselineWorldPositions = new();
        private readonly List<Vector3> baselineLocalScales = new();
        private readonly List<Transform> spawnedPreviewActors = new();

        private Sequence activeSequence;
        private Coroutine playCoroutine;
        private bool previewActorsOwned;

        public bool IsPlaying { get; private set; }
        public float ExpectedDuration => TotalDuration;
        public float TotalDuration => Mathf.Max(moveDuration, settleDelay + scaleDuration);

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
            settleDelay = Mathf.Max(0f, settleDelay);
            scaleDuration = Mathf.Max(0.01f, scaleDuration);
            moveDuration = Mathf.Max(0.01f, moveDuration);
            punchScale = Mathf.Max(1f, punchScale);
        }

        [ContextMenu("Play Preview")]
        public void PlayPreview()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            StopAndRestore();
            if (!TryResolveOuterRingAnchors(out List<Transform> slots))
            {
                Debug.LogWarning($"[{nameof(BoardRotateFlow)}] outer ring slots could not be resolved.", this);
                return;
            }

            IReadOnlyList<Transform> previewActors = EnsurePreviewActors(slots);
            if (previewActors.Count == 0)
            {
                Debug.LogWarning($"[{nameof(BoardRotateFlow)}] preview actors could not be created.", this);
                return;
            }

            var targets = new List<Transform>(previewActors.Count);
            BoardRingPath.BuildClockwiseStepTargets(slots, step: 1, targets);

            PlayRuntime(previewActors, targets, ownsPreviewActors: true);
        }

        public void Play(Transform[] actors, Transform[] targetSlots)
        {
            Play((IReadOnlyList<Transform>)actors, targetSlots);
        }

        public void Play(IReadOnlyList<Transform> actors, IReadOnlyList<Transform> targetSlots)
        {
            PlayRuntime(actors, targetSlots, ownsPreviewActors: false);
        }

        /// <summary>
        /// 环上推进一步：actor[i] 从当前位置跳至 ringSlots[(i + step + N) % N]。
        /// </summary>
        public void PlayRingStep(IReadOnlyList<Transform> actors, IReadOnlyList<Transform> ringSlots, int step = 1)
        {
            if (actors == null || ringSlots == null || actors.Count == 0 || ringSlots.Count == 0)
            {
                return;
            }

            int ringCount = ringSlots.Count;
            var targets = new Transform[actors.Count];
            int direction = step >= 0 ? 1 : -1;
            for (var i = 0; i < actors.Count; i++)
            {
                int targetIndex = (i + direction + ringCount) % ringCount;
                if (targetIndex < 0)
                {
                    targetIndex += ringCount;
                }

                targets[i] = ringSlots[targetIndex];
            }

            Play(actors, targets);
        }

        public void PlayRingStep(
            IReadOnlyList<Transform> actors,
            IReadOnlyList<Transform> ringSlots,
            bool clockwise)
        {
            PlayRingStep(actors, ringSlots, clockwise ? 1 : -1);
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
                for (var i = 0; i < activeActors.Count; i++)
                {
                    Transform actor = activeActors[i];
                    if (actor == null)
                    {
                        continue;
                    }

                    actor.position = baselineWorldPositions[i];
                    actor.localScale = baselineLocalScales[i];
                }
            }

            activeActors.Clear();
            baselineWorldPositions.Clear();
            baselineLocalScales.Clear();
            IsPlaying = false;
        }

        private void PlayRuntime(
            IReadOnlyList<Transform> actors,
            IReadOnlyList<Transform> targetSlots,
            bool ownsPreviewActors)
        {
            if (!isActiveAndEnabled || actors == null || targetSlots == null)
            {
                return;
            }

            int pairCount = Mathf.Min(actors.Count, targetSlots.Count);
            if (pairCount == 0)
            {
                return;
            }

            StopPlaybackOnly();

            previewActorsOwned = ownsPreviewActors;
            activeActors.Clear();
            baselineWorldPositions.Clear();
            baselineLocalScales.Clear();

            for (var i = 0; i < pairCount; i++)
            {
                Transform actor = actors[i];
                Transform target = targetSlots[i];
                if (actor == null || target == null)
                {
                    continue;
                }

                activeActors.Add(actor);
                baselineWorldPositions.Add(actor.position);
                baselineLocalScales.Add(actor.localScale);
            }

            if (activeActors.Count == 0)
            {
                previewActorsOwned = false;
                return;
            }

            activeSequence = BuildSequence(activeActors, targetSlots);
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

        private Sequence BuildSequence(IReadOnlyList<Transform> actors, IReadOnlyList<Transform> targetSlots)
        {
            var sequence = DOTween.Sequence()
                .SetTarget(this)
                .SetAutoKill(true)
                .Pause();

            if (ignoreTimeScale)
            {
                sequence.SetUpdate(true);
            }

            int pairCount = Mathf.Min(actors.Count, targetSlots.Count);
            for (var i = 0; i < pairCount; i++)
            {
                Transform actor = actors[i];
                Transform target = targetSlots[i];
                if (actor == null || target == null)
                {
                    continue;
                }

                AppendCardJump(sequence, actor, target.position);
            }

            sequence.OnComplete(HandleSequenceComplete);
            sequence.OnKill(HandleSequenceKilled);
            return sequence;
        }

        private void AppendCardJump(Sequence sequence, Transform actor, Vector3 targetWorldPosition)
        {
            Vector3 punchScaleVector = Vector3.one * punchScale;

            Tween punch = actor
                .DOScale(punchScaleVector, scaleDuration)
                .SetEase(scaleEaseCurve)
                .SetTarget(this);

            Tween settle = actor
                .DOScale(Vector3.one, scaleDuration)
                .SetDelay(settleDelay)
                .SetEase(scaleEaseCurve)
                .SetTarget(this);

            Tween move = actor
                .DOMove(targetWorldPosition, moveDuration)
                .SetEase(moveEase)
                .SetTarget(this);

            if (ignoreTimeScale)
            {
                punch.SetUpdate(true);
                settle.SetUpdate(true);
                move.SetUpdate(true);
            }

            sequence.Insert(0f, punch);
            sequence.Insert(settleDelay, settle);
            sequence.Insert(0f, move);
        }

        private IReadOnlyList<Transform> EnsurePreviewActors(IReadOnlyList<Transform> slots)
        {
            TeardownPreviewActors();

            int actorCount = previewActorCount > 0 ? previewActorCount : slots.Count;
            actorCount = Mathf.Min(actorCount, slots.Count);
            if (actorCount <= 0)
            {
                actorCount = 1;
            }

            Transform parent = ResolvedPreviewActorsRoot;
            for (var i = 0; i < actorCount; i++)
            {
                Transform slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                Transform actor = CreatePreviewActor(parent, i);
                if (actor == null)
                {
                    continue;
                }

                actor.position = slot.position;
                actor.localScale = Vector3.one;
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

        private Transform CreatePreviewActor(Transform parent, int index)
        {
            if (cardPreviewPrefab != null)
            {
                GameObject instance = Instantiate(cardPreviewPrefab, parent);
                instance.name = $"PreviewBoardCard_{index + 1}";
                return instance.transform;
            }

            var stub = new GameObject($"PreviewBoardCard_{index + 1}");
            stub.transform.SetParent(parent, worldPositionStays: false);

            var spriteRenderer = stub.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = GetFallbackPreviewSprite();
            spriteRenderer.sortingOrder = 5;

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

            foreach (Transform actor in activeActors)
            {
                if (actor != null)
                {
                    DOTween.Kill(actor);
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

            if (TryResolveOuterRingAnchors(out List<Transform> outerRing))
            {
                return outerRing;
            }

            Transform root = slotRoot != null ? slotRoot : transform;
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

        private bool TryResolveOuterRingAnchors(out List<Transform> outerRing)
        {
            Transform root = slotRoot != null ? slotRoot : transform;
            return BoardRingPath.TryResolveOuterRingAnchors(root, outerRing = new List<Transform>(8));
        }
    }
}
