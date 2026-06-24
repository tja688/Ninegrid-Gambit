using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.FSM;
using UnityEngine;
using UnityEngine.Events;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 卡牌获取：场上卡飞入手牌槽位；已有手牌同步让位重排。
    /// 缓动手感对齐 <see cref="ItemCardInteractPerformance"/>（layout/return 0.25s OutQuad，复用拖拽取消回手）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardAcquisitionPerformance : MonoBehaviour
    {
        [Header("Layout / Return (ItemCardInteract)")]
        [SerializeField, Min(0f)] private float layoutDuration = 0.25f;
        [SerializeField] private Ease layoutEase = Ease.OutQuad;
        [SerializeField, Min(0.01f)] private float returnDuration = 0.25f;
        [SerializeField] private Ease returnEase = Ease.OutQuad;

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;
        [SerializeField] private bool deferPlayOneFrame = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onComplete;

        [Header("Preview")]
        [SerializeField] private Transform handRoot;
        [SerializeField] private Transform sourceAnchor;
        [SerializeField] private HandCardLayoutSolver layoutSolver = new();
        [SerializeField] private GameObject cardPreviewPrefab;
        [SerializeField] private Transform previewActorsRoot;
        [SerializeField, Min(0)] private int previewExistingHandCount = 2;

        private static Sprite fallbackPreviewSprite;

        private readonly List<Transform> activeActors = new();
        private readonly List<Vector3> baselineLocalPositions = new();
        private readonly List<Color> baselineColors = new();
        private readonly List<int> baselineSortingOrders = new();
        private readonly List<HandCardLayoutTarget> layoutBuffer = new();
        private readonly List<Transform> previewSpawnedActors = new();

        private Sequence activeSequence;
        private Coroutine deferCoroutine;
        private bool isPlaying;
        private bool previewActorsOwned;

        public bool IsPlaying => isPlaying;
        public float TotalDuration => Mathf.Max(
            layoutDuration > 0f ? layoutDuration : 0f,
            returnDuration);

        private Transform ResolvedHandRoot => handRoot != null ? handRoot : transform;
        private Transform ResolvedPreviewActorsRoot => previewActorsRoot != null ? previewActorsRoot : ResolvedHandRoot;

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
            layoutDuration = Mathf.Max(0f, layoutDuration);
            returnDuration = Mathf.Max(0.01f, returnDuration);
            previewExistingHandCount = Mathf.Max(0, previewExistingHandCount);
        }

        public void Play(
            Transform acquiredCard,
            Vector3 targetLocalPosition,
            int targetSortingOrder,
            Action onComplete = null)
        {
            Play(acquiredCard, targetLocalPosition, targetSortingOrder, null, null, null, onComplete);
        }

        public void Play(
            Transform acquiredCard,
            Vector3 targetLocalPosition,
            int targetSortingOrder,
            IReadOnlyList<Transform> existingHandActors,
            IReadOnlyList<HandCardLayoutTarget> existingLayoutTargets,
            Action onComplete = null)
        {
            Play(acquiredCard, targetLocalPosition, targetSortingOrder, existingHandActors, existingLayoutTargets, null, onComplete);
        }

        public void Play(
            Transform acquiredCard,
            Vector3 targetLocalPosition,
            int targetSortingOrder,
            IReadOnlyList<Transform> existingHandActors,
            IReadOnlyList<HandCardLayoutTarget> existingLayoutTargets,
            Transform reparentUnder,
            Action onComplete = null)
        {
            if (!isActiveAndEnabled || acquiredCard == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopPlaybackOnly();
            previewActorsOwned = false;
            CacheActorBaselines(acquiredCard, existingHandActors);

            if (reparentUnder != null && acquiredCard.parent != reparentUnder)
            {
                acquiredCard.SetParent(reparentUnder, worldPositionStays: true);
            }

            activeSequence = BuildSequence(
                acquiredCard,
                targetLocalPosition,
                targetSortingOrder,
                existingHandActors,
                existingLayoutTargets,
                onComplete);
            isPlaying = true;

            if (deferPlayOneFrame)
            {
                deferCoroutine = StartCoroutine(PlaySequenceNextFrame(activeSequence));
            }
            else
            {
                activeSequence.Restart();
            }
        }

        [ContextMenu("Play Preview")]
        public void PlayPreview()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            StopAndRestore();
            layoutSolver.ResolveFromReferenceAnchors();

            int existingCount = previewExistingHandCount;
            int totalCount = existingCount + 1;
            layoutSolver.BuildLayout(totalCount, layoutBuffer);
            if (layoutBuffer.Count == 0)
            {
                Debug.LogWarning($"[{nameof(CardAcquisitionPerformance)}] preview layout is empty.", this);
                return;
            }

            Transform parent = ResolvedPreviewActorsRoot;
            previewSpawnedActors.Clear();

            var existingActors = new List<Transform>(existingCount);
            var existingTargets = new List<HandCardLayoutTarget>(existingCount);
            for (var i = 0; i < existingCount; i++)
            {
                HandCardLayoutTarget target = layoutBuffer[i];
                Transform actor = CreatePreviewActor(parent, i, target.LocalPosition, target.SortingOrder);
                previewSpawnedActors.Add(actor);
                existingActors.Add(actor);
                existingTargets.Add(target);
            }

            HandCardLayoutTarget acquiredTarget = layoutBuffer[totalCount - 1];
            Vector3 sourceLocal = ResolvePreviewSourceLocalPosition(parent);
            Transform acquiredCard = CreatePreviewActor(parent, totalCount - 1, sourceLocal, acquiredTarget.SortingOrder + 5);
            previewSpawnedActors.Add(acquiredCard);
            previewActorsOwned = true;

            Play(
                acquiredCard,
                acquiredTarget.LocalPosition,
                acquiredTarget.SortingOrder,
                existingActors,
                existingTargets,
                parent,
                null);
        }

        [ContextMenu("Stop And Restore")]
        public void StopAndRestore()
        {
            StopPlaybackOnly();
            RestoreBaselines();

            if (previewActorsOwned)
            {
                TeardownPreviewActors();
            }

            activeActors.Clear();
            baselineLocalPositions.Clear();
            baselineColors.Clear();
            baselineSortingOrders.Clear();
            isPlaying = false;
            previewActorsOwned = false;
        }

        private Sequence BuildSequence(
            Transform acquiredCard,
            Vector3 targetLocalPosition,
            int targetSortingOrder,
            IReadOnlyList<Transform> existingHandActors,
            IReadOnlyList<HandCardLayoutTarget> existingLayoutTargets,
            Action onComplete)
        {
            var sequence = DOTween.Sequence()
                .SetTarget(this)
                .SetAutoKill(true)
                .Pause();

            if (ignoreTimeScale)
            {
                sequence.SetUpdate(true);
            }

            AppendRelayoutTweens(sequence, existingHandActors, existingLayoutTargets);
            AppendReturnTween(sequence, acquiredCard, targetLocalPosition, targetSortingOrder);

            sequence.OnComplete(() =>
            {
                activeSequence = null;
                isPlaying = false;
                onComplete?.Invoke();
                this.onComplete?.Invoke();
            });

            return sequence;
        }

        private void AppendRelayoutTweens(
            Sequence sequence,
            IReadOnlyList<Transform> actors,
            IReadOnlyList<HandCardLayoutTarget> targets)
        {
            if (actors == null || targets == null || actors.Count == 0 || layoutDuration <= 0f)
            {
                return;
            }

            int pairCount = Mathf.Min(actors.Count, targets.Count);
            for (var i = 0; i < pairCount; i++)
            {
                Transform actor = actors[i];
                HandCardLayoutTarget target = targets[i];
                if (actor == null)
                {
                    continue;
                }

                Tween move = actor
                    .DOLocalMove(target.LocalPosition, layoutDuration)
                    .SetEase(layoutEase)
                    .SetTarget(actor);
                ApplyTweenSettings(move);
                sequence.Join(move);

                SpriteRenderer renderer = GetPrimaryRenderer(actor);
                if (renderer != null)
                {
                    Tween sort = SelectionOptionVisual.TweenBaseSortingOrder(actor, target.SortingOrder, layoutDuration, layoutEase);
                    if (sort != null)
                    {
                        ApplyTweenSettings(sort);
                        sequence.Join(sort);
                    }
                }
            }
        }

        private void AppendReturnTween(
            Sequence sequence,
            Transform actor,
            Vector3 targetLocalPosition,
            int targetSortingOrder)
        {
            KillActorTweens(actor);

            Tween move = actor
                .DOLocalMove(targetLocalPosition, returnDuration)
                .SetEase(returnEase)
                .SetTarget(actor);
            ApplyTweenSettings(move);
            sequence.Join(move);

            SpriteRenderer renderer = GetPrimaryRenderer(actor);
            if (renderer == null)
            {
                return;
            }

            Color restoreColor = ResolveBaselineColor(actor, renderer.color);
            Tween color = TweenSpriteColor(renderer, restoreColor, returnDuration, returnEase);
            ApplyTweenSettings(color);
            sequence.Join(color);

            Tween sort = SelectionOptionVisual.TweenBaseSortingOrder(actor, targetSortingOrder, returnDuration, returnEase);
            ApplyTweenSettings(sort);
            sequence.Join(sort);
        }

        private void CacheActorBaselines(
            Transform acquiredCard,
            IReadOnlyList<Transform> existingHandActors)
        {
            activeActors.Clear();
            baselineLocalPositions.Clear();
            baselineColors.Clear();
            baselineSortingOrders.Clear();

            CacheSingleActorBaseline(acquiredCard);

            if (existingHandActors == null)
            {
                return;
            }

            for (var i = 0; i < existingHandActors.Count; i++)
            {
                CacheSingleActorBaseline(existingHandActors[i]);
            }
        }

        private void CacheSingleActorBaseline(Transform actor)
        {
            if (actor == null)
            {
                return;
            }

            SpriteRenderer renderer = GetPrimaryRenderer(actor);
            activeActors.Add(actor);
            baselineLocalPositions.Add(actor.localPosition);
            baselineColors.Add(renderer != null ? renderer.color : Color.white);
            baselineSortingOrders.Add(SelectionOptionVisual.GetAnchorSortingOrder(actor));
        }

        private Color ResolveBaselineColor(Transform actor, Color fallback)
        {
            int index = activeActors.IndexOf(actor);
            return index >= 0 ? baselineColors[index] : fallback;
        }

        private void RestoreBaselines()
        {
            for (var i = 0; i < activeActors.Count; i++)
            {
                Transform actor = activeActors[i];
                if (actor == null)
                {
                    continue;
                }

                KillActorTweens(actor);
                actor.localPosition = baselineLocalPositions[i];

                SelectionOptionVisual.ApplySortingOrder(actor, baselineSortingOrders[i]);

                SpriteRenderer renderer = GetPrimaryRenderer(actor);
                if (renderer != null)
                {
                    renderer.color = baselineColors[i];
                }
            }
        }

        private Vector3 ResolvePreviewSourceLocalPosition(Transform parent)
        {
            if (sourceAnchor != null)
            {
                return parent.InverseTransformPoint(sourceAnchor.position);
            }

            HandCardLayoutTarget lastTarget = layoutBuffer[layoutBuffer.Count - 1];
            return lastTarget.LocalPosition + new Vector3(0f, 2.5f, 0f);
        }

        private Transform CreatePreviewActor(
            Transform parent,
            int index,
            Vector3 localPosition,
            int sortingOrder)
        {
            Transform actor;
            if (cardPreviewPrefab != null)
            {
                var instance = Instantiate(cardPreviewPrefab, parent);
                instance.name = $"PreviewAcq_{index + 1}";
                actor = instance.transform;
            }
            else
            {
                var stub = new GameObject($"PreviewAcq_{index + 1}");
                stub.transform.SetParent(parent, worldPositionStays: false);
                var spriteRenderer = stub.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = GetFallbackPreviewSprite();
                actor = stub.transform;
            }

            actor.localPosition = localPosition;
            actor.localRotation = Quaternion.identity;
            actor.localScale = Vector3.one;

            SelectionOptionVisual.ApplySortingOrder(actor, sortingOrder);

            return actor;
        }

        private void TeardownPreviewActors()
        {
            for (var i = previewSpawnedActors.Count - 1; i >= 0; i--)
            {
                Transform actor = previewSpawnedActors[i];
                if (actor == null)
                {
                    continue;
                }

                DOTween.Kill(actor);
                Destroy(actor.gameObject);
            }

            previewSpawnedActors.Clear();
            previewActorsOwned = false;
        }

        private void StopPlaybackOnly()
        {
            if (deferCoroutine != null)
            {
                StopCoroutine(deferCoroutine);
                deferCoroutine = null;
            }

            if (activeSequence != null && activeSequence.IsActive())
            {
                activeSequence.Kill();
            }

            activeSequence = null;
            DOTween.Kill(this);

            for (var i = 0; i < activeActors.Count; i++)
            {
                KillActorTweens(activeActors[i]);
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

            return DOTween
                .To(() => renderer != null ? renderer.sortingOrder : endValue, value =>
                {
                    if (renderer != null)
                    {
                        renderer.sortingOrder = value;
                    }
                }, endValue, duration)
                .SetEase(ease)
                .SetTarget(renderer);
        }

        private static SpriteRenderer GetPrimaryRenderer(Transform actor)
        {
            return actor != null ? actor.GetComponent<SpriteRenderer>() : null;
        }

        private static Sprite GetFallbackPreviewSprite()
        {
            if (fallbackPreviewSprite != null)
            {
                return fallbackPreviewSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, new Color(0.72f, 0.82f, 0.68f, 1f));
            texture.Apply();

            fallbackPreviewSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                100f);
            return fallbackPreviewSprite;
        }
    }
}
