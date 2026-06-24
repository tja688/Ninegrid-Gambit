using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Performance;
using NineGrid.Presentation.Registry;
using QFramework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace NineGrid.Presentation.FSM
{
    public enum ItemCardInteractionState
    {
        Idle,
        Hover,
        Drag,
        Watching,
    }

    [Serializable]
    public sealed class HandCardSlotBinding
    {
        public Transform anchor;
        public Transform actor;
        public int cardUid;
    }

    /// <summary>
    /// 道具卡交互 FSM：Hover/Drag 本地反馈；MVP 不发 UseItemCommand（场地主导 V0.6）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemCardInteractionFsm : MonoBehaviour, IController, IFlowShellManagedInteraction
    {
        private const int MaxHandCards = 5;
        private const float MaxResponsiveDragThresholdPixels = 2f;

        [Header("References")]
        [SerializeField] private ItemCardInteractPerformance interactPerformance;
        [SerializeField] private InputLockGate inputLockGate;
        [SerializeField] private BoardItemInteractionCoordinator coordinator;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private Collider applyZoneCollider;
        [SerializeField] private Transform actorsRoot;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private TableNineActorFactory actorFactory;
        [SerializeField] private Transform[] referenceAnchors = Array.Empty<Transform>();

        [Header("Layout")]
        [SerializeField] private HandCardLayoutSolver layoutSolver = new();

        [Header("Input")]
        [SerializeField, Min(0f)] private float dragStartThresholdPixels = 1.5f;
        [SerializeField] private bool pointerInputEnabled = true;

        [Header("Hover Intent Proxy")]
        [SerializeField] private bool useLinearHoverProxy = true;
        [SerializeField, Min(0.1f)] private float hoverProxyCardHeight = 2.0625f;
        [SerializeField, Min(0f)] private float hoverProxyHorizontalPadding = 0.08f;
        [SerializeField, Min(0f)] private float hoverProxyVerticalPadding = 0.35f;

        [Header("Formal Mode")]
        [SerializeField] private bool resolveUidsFromDeckOnStart = true;

        [Header("Demo Mode (Play Mode)")]
        [SerializeField] private bool demoModeEnabled;
        [SerializeField, Min(1)] private int demoMaxCards = MaxHandCards;

        [Header("Events")]
        [SerializeField] private UnityEvent onStateChanged;
        [SerializeField] private UnityEvent<int> onUseItemRequested;
        [SerializeField] private UnityEvent<string> onUseItemRejected;

        private readonly List<HandCardEntry> handCards = new();
        private readonly List<HandCardLayoutTarget> layoutBuffer = new();
        private readonly List<Transform> actorListBuffer = new();
        private readonly List<Transform> othersBuffer = new();

        private ItemCardInteractionState state = ItemCardInteractionState.Idle;
        private HandCardEntry hoveredEntry;
        private HandCardEntry draggedEntry;
        private Vector2 pressScreenPosition;
        private bool pointerPressed;
        private bool isWatching;
        private int deckVersionSnapshot = -1;

        private struct HandCardEntry
        {
            public int CardUid;
            public Transform Actor;
            public bool IsDemo;
        }

        public ItemCardInteractionState State => state;
        public bool DemoModeEnabled => demoModeEnabled;

        public void SetFlowShellInteractionEnabled(bool enabled)
        {
            pointerInputEnabled = enabled;
            if (!enabled)
            {
                interactPerformance?.StopAndRestore();
                SetState(ItemCardInteractionState.Idle);
                ClearPointerState();
            }
        }

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
        }

        private void Awake()
        {
            if (actorsRoot == null)
            {
                actorsRoot = transform;
            }

            layoutSolver.SetReferenceAnchors(referenceAnchors);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (inputLockGate != null)
            {
                inputLockGate.OnWatchingChanged += HandleWatchingChanged;
                HandleWatchingChanged(inputLockGate.IsWatching);
            }

            this.RegisterEvent<Evt_ActionRejected>(HandleActionRejected);

            if (!demoModeEnabled)
            {
                this.GetModel<DeckModel>().Version.Register(OnDeckVersionChanged);
            }

            if (!demoModeEnabled && resolveUidsFromDeckOnStart)
            {
                SyncFromDeck(force: true);
            }
            else
            {
                RelayoutHand(instant: true);
            }
        }

        private void OnDisable()
        {
            if (inputLockGate != null)
            {
                inputLockGate.OnWatchingChanged -= HandleWatchingChanged;
            }

            this.UnRegisterEvent<Evt_ActionRejected>(HandleActionRejected);

            if (!demoModeEnabled)
            {
                this.GetModel<DeckModel>().Version.UnRegister(OnDeckVersionChanged);
            }

            interactPerformance?.StopAndRestore();
            SetState(ItemCardInteractionState.Idle);
            ClearPointerState();
        }

        private void Update()
        {
            if (!pointerInputEnabled)
            {
                return;
            }

            if (demoModeEnabled)
            {
                HandleDemoHotkeys();
            }

            if (!Application.isPlaying)
            {
                return;
            }

            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }

            if (inputCamera == null)
            {
                return;
            }

            HandlePointerInput();
        }

        private void HandleDemoHotkeys()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                DemoFillToCount(demoMaxCards);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                DemoAddOneCard();
            }
        }

        [ContextMenu("Demo Fill Max Cards")]
        public void DemoFillToCount(int count)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ClearHandActors();
            count = Mathf.Clamp(count, 0, demoMaxCards);
            layoutSolver.BuildLayout(count, layoutBuffer);

            for (var i = 0; i < count; i++)
            {
                HandCardLayoutTarget target = layoutBuffer[i];
                Transform actor = SpawnActor(target.LocalPosition, target.SortingOrder, cardUid: 0, isDemo: true);
                handCards.Add(new HandCardEntry
                {
                    CardUid = 0,
                    Actor = actor,
                    IsDemo = true,
                });
            }

            RegisterActorsWithPerformance();
            RelayoutHand(instant: true);
        }

        [ContextMenu("Demo Add One Card")]
        public void DemoAddOneCard()
        {
            if (!Application.isPlaying || handCards.Count >= demoMaxCards)
            {
                return;
            }

            int newCount = handCards.Count + 1;
            layoutSolver.BuildLayout(newCount, layoutBuffer);

            actorListBuffer.Clear();
            for (var i = 0; i < handCards.Count; i++)
            {
                actorListBuffer.Add(handCards[i].Actor);
            }

            var existingTargets = new List<HandCardLayoutTarget>(newCount - 1);
            for (var i = 0; i < newCount - 1; i++)
            {
                existingTargets.Add(layoutBuffer[i]);
            }

            interactPerformance.Relayout(actorListBuffer, existingTargets, durationOverride: -1f);

            HandCardLayoutTarget spawnTarget = layoutBuffer[newCount - 1];
            Transform actor = SpawnActor(spawnTarget.LocalPosition, spawnTarget.SortingOrder, cardUid: 0, isDemo: true);
            handCards.Add(new HandCardEntry
            {
                CardUid = 0,
                Actor = actor,
                IsDemo = true,
            });

            interactPerformance.RegisterActor(actor, spawnTarget.LocalPosition, spawnTarget.SortingOrder);
        }

        private void OnDeckVersionChanged(int _)
        {
            SyncFromDeck(force: false);
        }

        public void SyncFromDeck(bool force)
        {
            if (demoModeEnabled)
            {
                return;
            }

            DeckModel deck = this.GetModel<DeckModel>();
            deckVersionSnapshot = deck.Version.Value;

            IReadOnlyList<int> uids = deck.ItemSlotUids;
            if (!force && handCards.Count == uids.Count)
            {
                bool same = true;
                for (var i = 0; i < uids.Count; i++)
                {
                    if (handCards[i].CardUid != uids[i])
                    {
                        same = false;
                        break;
                    }
                }

                if (same)
                {
                    return;
                }
            }

            ClearHandActors();

            layoutSolver.BuildLayout(uids.Count, layoutBuffer);
            for (var i = 0; i < uids.Count; i++)
            {
                HandCardLayoutTarget target = layoutBuffer[i];
                Transform actor = SpawnActor(target.LocalPosition, target.SortingOrder, uids[i], isDemo: false);
                handCards.Add(new HandCardEntry
                {
                    CardUid = uids[i],
                    Actor = actor,
                    IsDemo = false,
                });
            }

            RegisterActorsWithPerformance();
            RelayoutHand(instant: true);
        }

        private void HandlePointerInput()
        {
            if (IsPointerOverUi())
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                pressScreenPosition = Input.mousePosition;
                pointerPressed = true;

                HandCardEntry hit = ResolveHandCardIntent(Input.mousePosition);
                if (hit.Actor != null && !isWatching)
                {
                    hoveredEntry = hit;
                    EnterHover(hit);
                }
            }

            if (Input.GetMouseButton(0) && pointerPressed)
            {
                if (isWatching)
                {
                    return;
                }

                if (state == ItemCardInteractionState.Hover && hoveredEntry.Actor != null)
                {
                    float dragDistance = Vector2.Distance(pressScreenPosition, Input.mousePosition);
                    if (dragDistance >= EffectiveDragStartThresholdPixels)
                    {
                        BeginDrag(hoveredEntry);
                        UpdateDragAtScreenPosition(Input.mousePosition);
                    }
                }
                else if (state == ItemCardInteractionState.Drag && draggedEntry.Actor != null)
                {
                    UpdateDragAtScreenPosition(Input.mousePosition);
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (pointerPressed && state == ItemCardInteractionState.Drag && draggedEntry.Actor != null && !isWatching)
                {
                    ReleaseDrag(draggedEntry);
                }

                pointerPressed = false;
            }

            if (!pointerPressed && !isWatching)
            {
                HandCardEntry hit = ResolveHandCardIntent(Input.mousePosition);
                if (hit.Actor != null)
                {
                    if (hoveredEntry.Actor != hit.Actor)
                    {
                        if (hoveredEntry.Actor != null)
                        {
                            ExitHover();
                        }

                        hoveredEntry = hit;
                        EnterHover(hit);
                    }
                }
                else if (hoveredEntry.Actor != null)
                {
                    ExitHover();
                    hoveredEntry = default;
                }
            }
        }

        private void EnterHover(HandCardEntry entry)
        {
            if (entry.Actor == null || interactPerformance == null)
            {
                return;
            }

            BuildOthersBuffer(entry.Actor);
            interactPerformance.PlayFocus(entry.Actor, othersBuffer);
            SetState(isWatching ? ItemCardInteractionState.Watching : ItemCardInteractionState.Hover);
        }

        private void ExitHover()
        {
            if (hoveredEntry.Actor == null || interactPerformance == null)
            {
                return;
            }

            BuildOthersBuffer(hoveredEntry.Actor);
            interactPerformance.StopFocus(hoveredEntry.Actor, othersBuffer);
            SetState(isWatching ? ItemCardInteractionState.Watching : ItemCardInteractionState.Idle);
        }

        private void BeginDrag(HandCardEntry entry)
        {
            draggedEntry = entry;
            BuildOthersBuffer(entry.Actor);
            interactPerformance.StopFocus(entry.Actor, othersBuffer, immediate: true, restoreFocused: false);
            interactPerformance.BeginDrag(entry.Actor, ScreenToWorld(Input.mousePosition));
            coordinator?.NotifyDragBegan(entry.CardUid);
            SetState(ItemCardInteractionState.Drag);
        }

        private void UpdateDragAtScreenPosition(Vector2 screenPosition)
        {
            if (draggedEntry.Actor == null || interactPerformance == null)
            {
                return;
            }

            Vector3 world = ScreenToWorld(screenPosition);
            bool inZone = IsInApplyZone(world);
            interactPerformance.UpdateDrag(draggedEntry.Actor, world, inZone);
        }

        private void ReleaseDrag(HandCardEntry entry)
        {
            coordinator?.NotifyDragEnded();

            int index = FindEntryIndex(entry.Actor);
            if (index < 0)
            {
                interactPerformance.EndDrag();
                draggedEntry = default;
                hoveredEntry = default;
                SetState(ItemCardInteractionState.Idle);
                return;
            }

            layoutSolver.BuildLayout(handCards.Count, layoutBuffer);
            HandCardLayoutTarget target = layoutBuffer[index];
            interactPerformance.PlayReturn(entry.Actor, target.LocalPosition, target.SortingOrder, () =>
            {
                interactPerformance.EndDrag();
                interactPerformance.UpdateBaseline(entry.Actor, target.LocalPosition, target.SortingOrder);
                draggedEntry = default;
                hoveredEntry = default;
                SetState(ItemCardInteractionState.Idle);
            });
        }

        private void HandleActionRejected(Evt_ActionRejected evt)
        {
            if (demoModeEnabled || evt.Command != GameCommandKind.UseItem)
            {
                return;
            }

            onUseItemRejected?.Invoke(evt.Reason);

            for (var i = 0; i < handCards.Count; i++)
            {
                if (handCards[i].CardUid != evt.CardUid)
                {
                    continue;
                }

                interactPerformance.PlayReject(handCards[i].Actor);
                break;
            }

            SetState(isWatching ? ItemCardInteractionState.Watching : ItemCardInteractionState.Idle);
        }

        private void HandleWatchingChanged(bool watching)
        {
            isWatching = watching;
            if (watching)
            {
                if (state == ItemCardInteractionState.Drag)
                {
                    if (draggedEntry.Actor != null)
                    {
                        int index = FindEntryIndex(draggedEntry.Actor);
                        if (index >= 0)
                        {
                            layoutSolver.BuildLayout(handCards.Count, layoutBuffer);
                            HandCardLayoutTarget target = layoutBuffer[index];
                            draggedEntry.Actor.localPosition = target.LocalPosition;
                            interactPerformance.UpdateBaseline(draggedEntry.Actor, target.LocalPosition, target.SortingOrder);
                        }
                    }

                    interactPerformance.EndDrag();
                    coordinator?.NotifyDragEnded();
                    draggedEntry = default;
                }

                if (state == ItemCardInteractionState.Hover)
                {
                    ExitHover();
                    hoveredEntry = default;
                }

                SetState(ItemCardInteractionState.Watching);
                return;
            }

            if (state == ItemCardInteractionState.Watching)
            {
                SetState(ItemCardInteractionState.Idle);
            }
        }

        private void RelayoutHand(bool instant, float durationOverride = -1f)
        {
            if (handCards.Count == 0 || interactPerformance == null)
            {
                return;
            }

            layoutSolver.BuildLayout(handCards.Count, layoutBuffer);
            actorListBuffer.Clear();
            for (var i = 0; i < handCards.Count; i++)
            {
                actorListBuffer.Add(handCards[i].Actor);
            }

            if (instant)
            {
                interactPerformance.Relayout(actorListBuffer, layoutBuffer, durationOverride: 0f);
            }
            else
            {
                interactPerformance.Relayout(actorListBuffer, layoutBuffer, durationOverride: durationOverride);
            }
        }

        private Transform SpawnActor(Vector3 localPosition, int sortingOrder, int cardUid, bool isDemo)
        {
            Transform actor;
            if (!isDemo && cardUid > 0 && TrySpawnFromFactory(cardUid, out actor))
            {
                actor.SetParent(actorsRoot, false);
                actor.localPosition = localPosition;
                actor.localRotation = Quaternion.identity;
                actor.localScale = Vector3.one;
                SelectionOptionVisual.ApplySortingOrder(actor, sortingOrder);
                EnsurePickCollider(actor, actor.GetComponentInChildren<SpriteRenderer>());
                return actor;
            }

            GameObject instance;
            if (cardPrefab != null)
            {
                instance = Instantiate(cardPrefab, actorsRoot);
            }
            else
            {
                instance = new GameObject(isDemo ? "DemoHandCard" : $"HandCard_{cardUid}");
                instance.transform.SetParent(actorsRoot, false);
                var renderer = instance.AddComponent<SpriteRenderer>();
                renderer.sprite = null;
            }

            actor = instance.transform;
            actor.localPosition = localPosition;
            actor.localRotation = Quaternion.identity;
            actor.localScale = Vector3.one;

            SpriteRenderer spriteRenderer = actor.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = sortingOrder;
            }

            EnsurePickCollider(actor, spriteRenderer);
            return actor;
        }

        private bool TrySpawnFromFactory(int cardUid, out Transform actor)
        {
            actor = null;
            EnsureActorFactory();
            if (actorFactory == null)
            {
                return false;
            }

            actor = actorFactory.Acquire(cardUid, ViewActorZone.Hand, GetArchitecture());
            return actor != null;
        }

        private void EnsureActorFactory()
        {
            if (actorFactory == null)
            {
                actorFactory = GetComponentInParent<TableNineActorFactory>();
                if (actorFactory == null)
                {
                    actorFactory = FindFirstObjectByType<TableNineActorFactory>();
                }
            }
        }

        private static void EnsurePickCollider(Transform actor, SpriteRenderer spriteRenderer)
        {
            if (actor.GetComponent<Collider2D>() != null)
            {
                return;
            }

            var box = actor.gameObject.AddComponent<BoxCollider2D>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                box.size = spriteRenderer.sprite.bounds.size;
            }
            else
            {
                box.size = new Vector2(1.625f, 2.0625f);
            }
        }

        private void RegisterActorsWithPerformance()
        {
            if (interactPerformance == null)
            {
                return;
            }

            for (var i = 0; i < handCards.Count; i++)
            {
                Transform actor = handCards[i].Actor;
                if (actor == null)
                {
                    continue;
                }

                HandCardLayoutTarget target = i < layoutBuffer.Count
                    ? layoutBuffer[i]
                    : new HandCardLayoutTarget { LocalPosition = actor.localPosition, SortingOrder = 0 };
                interactPerformance.RegisterActor(actor, target.LocalPosition, target.SortingOrder);
            }
        }

        private void ClearHandActors()
        {
            interactPerformance?.RestoreAllActorsImmediate();

            for (var i = 0; i < handCards.Count; i++)
            {
                HandCardEntry entry = handCards[i];
                Transform actor = entry.Actor;
                if (actor == null)
                {
                    continue;
                }

                interactPerformance?.UnregisterActor(actor);
                if (!entry.IsDemo && entry.CardUid > 0)
                {
                    EnsureActorFactory();
                    if (actorFactory != null)
                    {
                        actorFactory.Release(entry.CardUid);
                        continue;
                    }
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

            interactPerformance?.PurgeDestroyedActors();
            handCards.Clear();
        }

        private HandCardEntry ResolveHandCardIntent(Vector2 screenPosition)
        {
            if (useLinearHoverProxy)
            {
                HandCardEntry proxyHit = ResolveLinearHandCardIntent(screenPosition);
                if (proxyHit.Actor != null)
                {
                    return proxyHit;
                }
            }

            return RaycastHandCard(screenPosition);
        }

        private HandCardEntry ResolveLinearHandCardIntent(Vector2 screenPosition)
        {
            if (inputCamera == null || actorsRoot == null || handCards.Count == 0)
            {
                return default;
            }

            Vector3 world = ScreenToWorld(screenPosition);
            Vector3 local = actorsRoot.InverseTransformPoint(world);

            layoutSolver.BuildLayout(handCards.Count, layoutBuffer);
            if (layoutBuffer.Count == 0)
            {
                return default;
            }

            float centerY = layoutBuffer[0].LocalPosition.y;
            float halfHeight = hoverProxyCardHeight * 0.5f + hoverProxyVerticalPadding;
            if (Mathf.Abs(local.y - centerY) > halfHeight)
            {
                return default;
            }

            float halfWidth = layoutSolver.CardWidth * 0.5f;
            for (var i = 0; i < layoutBuffer.Count && i < handCards.Count; i++)
            {
                float currentX = layoutBuffer[i].LocalPosition.x;
                float leftBoundary = i == 0
                    ? currentX - halfWidth - hoverProxyHorizontalPadding
                    : (layoutBuffer[i - 1].LocalPosition.x + currentX) * 0.5f;
                float rightBoundary = i == layoutBuffer.Count - 1
                    ? currentX + halfWidth + hoverProxyHorizontalPadding
                    : (currentX + layoutBuffer[i + 1].LocalPosition.x) * 0.5f;

                if (local.x >= leftBoundary && local.x <= rightBoundary)
                {
                    return handCards[i];
                }
            }

            return default;
        }

        private HandCardEntry RaycastHandCard(Vector2 screenPosition)
        {
            if (inputCamera == null)
            {
                return default;
            }

            Vector3 world = ScreenToWorld(screenPosition);
            Collider2D[] hits = Physics2D.OverlapPointAll(world);
            if (hits == null || hits.Length == 0)
            {
                return default;
            }

            var bestHit = default(HandCardEntry);
            var bestSortingOrder = int.MinValue;
            for (var i = 0; i < handCards.Count; i++)
            {
                Transform actor = handCards[i].Actor;
                if (actor == null)
                {
                    continue;
                }

                for (var j = 0; j < hits.Length; j++)
                {
                    if (hits[j] == null || hits[j].transform != actor)
                    {
                        continue;
                    }

                    SpriteRenderer renderer = actor.GetComponent<SpriteRenderer>();
                    int sortingOrder = renderer != null ? renderer.sortingOrder : 0;
                    if (sortingOrder > bestSortingOrder)
                    {
                        bestSortingOrder = sortingOrder;
                        bestHit = handCards[i];
                    }
                }
            }

            return bestHit;
        }

        private bool IsInApplyZone(Vector3 worldPosition)
        {
            return applyZoneCollider != null && applyZoneCollider.bounds.Contains(worldPosition);
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            float zDepth = Mathf.Abs(inputCamera.transform.position.z);
            Vector3 screen = new(screenPosition.x, screenPosition.y, zDepth);
            Vector3 world = inputCamera.ScreenToWorldPoint(screen);
            world.z = 0f;
            return world;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void BuildOthersBuffer(Transform focused)
        {
            othersBuffer.Clear();
            for (var i = 0; i < handCards.Count; i++)
            {
                Transform actor = handCards[i].Actor;
                if (actor != null && actor != focused)
                {
                    othersBuffer.Add(actor);
                }
            }
        }

        private int FindEntryIndex(Transform actor)
        {
            for (var i = 0; i < handCards.Count; i++)
            {
                if (handCards[i].Actor == actor)
                {
                    return i;
                }
            }

            return -1;
        }

        private void SetState(ItemCardInteractionState newState)
        {
            if (state == newState)
            {
                return;
            }

            state = newState;
            onStateChanged?.Invoke();
        }

        private void ClearPointerState()
        {
            pointerPressed = false;
            hoveredEntry = default;
            draggedEntry = default;
        }

        private float EffectiveDragStartThresholdPixels => Mathf.Min(
            Mathf.Max(0f, dragStartThresholdPixels),
            MaxResponsiveDragThresholdPixels);

        private void OnValidate()
        {
            dragStartThresholdPixels = Mathf.Max(0f, dragStartThresholdPixels);
            hoverProxyCardHeight = Mathf.Max(0.1f, hoverProxyCardHeight);
            hoverProxyHorizontalPadding = Mathf.Max(0f, hoverProxyHorizontalPadding);
            hoverProxyVerticalPadding = Mathf.Max(0f, hoverProxyVerticalPadding);
            demoMaxCards = Mathf.Clamp(demoMaxCards, 1, MaxHandCards);
            layoutSolver.SetReferenceAnchors(referenceAnchors);
        }
    }
}
