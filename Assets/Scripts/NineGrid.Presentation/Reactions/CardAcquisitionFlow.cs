using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Core;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Shared;
using NineGrid.Presentation.Visuals;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Reactions
{
    /// <summary>
    /// 卡牌获取：场上卡飞入手牌槽位；已有手牌同步让位重排。
    /// 缓动手感委托 <see cref="HandLayoutPresenter"/> / <see cref="HandCardReturnPresenter"/>。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "CardAcquisitionPerformance")]
    public sealed class CardAcquisitionFlow : MonoBehaviour, IPlannedReaction
    {
        [Header("Hand Presenters")]
        [SerializeField] private HandLayoutPresenter layoutPresenter;
        [SerializeField] private HandCardReturnPresenter returnPresenter;

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

        private Coroutine deferCoroutine;
        private bool isPlaying;
        private bool previewActorsOwned;

        public bool IsPlaying => isPlaying;

        public float TotalDuration => Mathf.Max(
            layoutPresenter != null ? layoutPresenter.LayoutDuration : 0f,
            returnPresenter != null ? returnPresenter.ReturnDuration : 0f);

        private HandLayoutPresenter Layout => layoutPresenter != null
            ? layoutPresenter
            : layoutPresenter = GetComponent<HandLayoutPresenter>();

        private HandCardReturnPresenter Return => returnPresenter != null
            ? returnPresenter
            : returnPresenter = GetComponent<HandCardReturnPresenter>();

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

            isPlaying = true;

            if (deferPlayOneFrame)
            {
                deferCoroutine = StartCoroutine(PlayDeferred(
                    acquiredCard,
                    targetLocalPosition,
                    targetSortingOrder,
                    existingHandActors,
                    existingLayoutTargets,
                    onComplete));
            }
            else
            {
                PlayImmediate(
                    acquiredCard,
                    targetLocalPosition,
                    targetSortingOrder,
                    existingHandActors,
                    existingLayoutTargets,
                    onComplete);
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
                Debug.LogWarning($"[{nameof(CardAcquisitionFlow)}] preview layout is empty.", this);
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

        private IEnumerator PlayDeferred(
            Transform acquiredCard,
            Vector3 targetLocalPosition,
            int targetSortingOrder,
            IReadOnlyList<Transform> existingHandActors,
            IReadOnlyList<HandCardLayoutTarget> existingLayoutTargets,
            Action onComplete)
        {
            yield return null;
            deferCoroutine = null;

            PlayImmediate(
                acquiredCard,
                targetLocalPosition,
                targetSortingOrder,
                existingHandActors,
                existingLayoutTargets,
                onComplete);
        }

        private void PlayImmediate(
            Transform acquiredCard,
            Vector3 targetLocalPosition,
            int targetSortingOrder,
            IReadOnlyList<Transform> existingHandActors,
            IReadOnlyList<HandCardLayoutTarget> existingLayoutTargets,
            Action onComplete)
        {
            var pending = 0;
            var completed = false;

            void TryFinish()
            {
                if (completed || pending > 0)
                {
                    return;
                }

                completed = true;
                isPlaying = false;
                onComplete?.Invoke();
                this.onComplete?.Invoke();
            }

            void BeginTrack()
            {
                pending++;
            }

            void EndTrack()
            {
                pending--;
                TryFinish();
            }

            if (existingHandActors != null
                && existingLayoutTargets != null
                && existingHandActors.Count > 0
                && Layout != null)
            {
                BeginTrack();
                Layout.Relayout(existingHandActors, existingLayoutTargets, onComplete: EndTrack);
            }

            if (Return != null)
            {
                BeginTrack();
                Return.PlayReturn(acquiredCard, targetLocalPosition, targetSortingOrder, EndTrack);
            }

            TryFinish();
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
