using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards
{
    /// <summary>
    /// 手牌管理器单例：最多 5 张，CardHandAnchors 布局，hover / 拖拽 / 回手 / 场地抓取。
    /// </summary>
    public sealed class CardHandManagerSingleton : MonoBehaviour
    {
        private enum DragSource
        {
            Hand = 0,
            Ground = 1,
        }

        private sealed class DragSession
        {
            public ManagedCard Card;
            public DragSource Source;
            public int OriginHandSlot = -1;
            public int OriginGroundSlot = -1;
            public bool WasHovering;
            public bool PointerReleasedInZone;
        }

        private static CardHandManagerSingleton _instance;

        [Header("Scene Anchors")]
        [Tooltip("场景 Anchors/CardHandAnchors。留空时 Awake 按名称 CardHandAnchors 查找。")]
        [SerializeField] private Transform handAnchorsRoot;

        [Tooltip("场景 CardHandAnchors/HandcardApplyZone 的 Collider。留空时作为 handAnchorsRoot 子节点查找。")]
        [SerializeField] private Collider applyZoneCollider;

        [Header("Layout")]
        [Tooltip("手牌布局与动效参数。")]
        [SerializeField] private CardHandLayoutSettings layoutSettings = new();

        private readonly List<Transform> _handAnchors = new();
        private CardHandSlotContainer _slotContainer;
        private DragSession _dragSession;
        private ManagedCard _hoveredCard;
        private readonly List<HandHoverCandidate> _hoverCandidates = new();
        private bool _isBusy;
        private CancellationTokenSource _dragLoopCts;

        private readonly struct HandHoverCandidate
        {
            public readonly int SlotIndex;
            public readonly ManagedCard Card;
            public readonly float LayoutX;
            public readonly float LayoutY;

            public HandHoverCandidate(int slotIndex, ManagedCard card, float layoutX, float layoutY)
            {
                SlotIndex = slotIndex;
                Card = card;
                LayoutX = layoutX;
                LayoutY = layoutY;
            }
        }

        /// <summary>
        /// TODO: Core 逻辑层注入 — 校验手牌释放（目标格位、费用、效果等）。返回 true 表示释放成功。
        /// </summary>
        public event Func<ManagedCard, int?, UniTask<bool>> DragApplyValidator;

        public static CardHandManagerSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<CardHandManagerSingleton>();
                }

                return _instance;
            }
        }

        public CardHandLayoutSettings LayoutSettings => layoutSettings;

        public int HandCount => _slotContainer?.Count ?? 0;

        public bool IsBusy => _isBusy;

        public bool IsDragging => _dragSession != null;

        public bool CanAcceptCard => !IsBusy && !IsDragging && HandCount < layoutSettings.maxSlots;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _slotContainer = new CardHandSlotContainer(layoutSettings);
            ResolveSceneReferences();
            CacheAnchors();
            InitializeLayoutOrigin();
        }

        private void OnDestroy()
        {
            _dragLoopCts?.Cancel();
            _dragLoopCts?.Dispose();

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            TickHandHover();
        }

        public async UniTask<bool> PullFromGroundAsync(
            ManagedCard card,
            int? insertSlot = null,
            CancellationToken cancellationToken = default)
        {
            if (card == null || !CanAcceptCard)
            {
                return false;
            }

            _isBusy = true;
            try
            {
                var cardManager = CardManagerSingleton.Instance;
                cardManager.SetDisplayMode(card, CardDisplayMode.HandCardMode);
                CardOpacityUtility.ResetAlpha(card);

                var slot = insertSlot ?? HandCount;
                if (!_slotContainer.TryInsertAt(slot, card, out var rippleMoves))
                {
                    return false;
                }

                await CardDeckTween.MoveRippleAsync(rippleMoves, layoutSettings.moveDuration, cancellationToken);
                RefreshHandCardDisplay(card);
                SnapHandCardToLayout(card);
                return true;
            }
            finally
            {
                _isBusy = false;
            }
        }

        public bool TryBeginDragFromHand(ManagedCard card)
        {
            if (card == null || IsBusy || IsDragging)
            {
                return false;
            }

            if (card.DisplayMode != CardDisplayMode.HandCardMode)
            {
                return false;
            }

            if (!_slotContainer.TryGetSlotOf(card, out var slotIndex))
            {
                return false;
            }

            var driver = card.View?.GetComponent<CardVisualDriver>();
            var wasHovering = driver != null && driver.CurrentTarget == CardVisualTarget.Hover;

            if (!_slotContainer.TryRemoveAt(slotIndex, out var removed, out var rippleMoves))
            {
                return false;
            }

            CardDeckTween.MoveRippleAsync(rippleMoves, layoutSettings.moveDuration).Forget();
            ClearHandHoverState(removed);

            _dragSession = new DragSession
            {
                Card = removed,
                Source = DragSource.Hand,
                OriginHandSlot = slotIndex,
                WasHovering = wasHovering,
            };

            BeginDragLoop();
            return true;
        }

        public bool TryBeginDragFromGround(ManagedCard card)
        {
            if (card == null || IsBusy || IsDragging || !CanAcceptCard)
            {
                return false;
            }

            if (card.DisplayMode != CardDisplayMode.GroundCardMode)
            {
                return false;
            }

            var field = GroundFieldManagerSingleton.Instance;
            if (field == null || field.IsBusy)
            {
                return false;
            }

            if (!field.TryGetSlotOf(card.Uid, out var groundSlot))
            {
                return false;
            }

            if (!field.TryTakeCardFromField(card.Uid, out var taken) || taken != card)
            {
                return false;
            }

            var driver = card.View?.GetComponent<CardVisualDriver>();
            driver?.SetTarget(CardVisualTarget.Base);

            _dragSession = new DragSession
            {
                Card = card,
                Source = DragSource.Ground,
                OriginGroundSlot = groundSlot,
            };

            BeginDragLoop();
            return true;
        }

        private void TickHandHover()
        {
            if (IsBusy || IsDragging || _slotContainer == null)
            {
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var pointerWorld = ScreenToWorldOnPlane(
                Input.mousePosition,
                camera,
                ResolveHandHoverPlaneZ());
            var resolved = ResolveHandHoverTarget(pointerWorld.x, pointerWorld.y);
            ApplyHandHoverTarget(resolved);
        }

        private float ResolveHandHoverPlaneZ()
        {
            if (_handAnchors.Count > 0 && _handAnchors[0] != null)
            {
                return _handAnchors[0].position.z;
            }

            return handAnchorsRoot != null ? handAnchorsRoot.position.z : 0f;
        }

        private ManagedCard ResolveHandHoverTarget(float pointerX, float pointerY)
        {
            CollectHandHoverCandidates(pointerX, pointerY);
            if (_hoverCandidates.Count == 0)
            {
                return null;
            }

            if (_hoverCandidates.Count == 1)
            {
                return _hoverCandidates[0].Card;
            }

            var rawTarget = ResolveHandHoverBySlotBands(pointerX);
            if (_hoveredCard == null || rawTarget == _hoveredCard)
            {
                return rawTarget;
            }

            return ShouldKeepCurrentHover(pointerX, rawTarget) ? _hoveredCard : rawTarget;
        }

        private void CollectHandHoverCandidates(float pointerX, float pointerY)
        {
            _hoverCandidates.Clear();

            var halfHeight = layoutSettings.handHitBoxSize.y * 0.5f;
            for (var i = 0; i < layoutSettings.maxSlots; i++)
            {
                if (!_slotContainer.TryGetCardAt(i, out var card) || card == null)
                {
                    continue;
                }

                if (card.DisplayMode != CardDisplayMode.HandCardMode)
                {
                    continue;
                }

                var layoutPosition = _slotContainer.GetLayoutPosition(i);
                if (pointerY < layoutPosition.y - halfHeight || pointerY > layoutPosition.y + halfHeight)
                {
                    continue;
                }

                var halfWidth = layoutSettings.handHitBoxSize.x * 0.5f;
                if (pointerX < layoutPosition.x - halfWidth || pointerX > layoutPosition.x + halfWidth)
                {
                    continue;
                }

                _hoverCandidates.Add(new HandHoverCandidate(i, card, layoutPosition.x, layoutPosition.y));
            }

            _hoverCandidates.Sort(static (a, b) => a.LayoutX.CompareTo(b.LayoutX));
        }

        private ManagedCard ResolveHandHoverBySlotBands(float pointerX)
        {
            var count = _hoverCandidates.Count;
            for (var i = 0; i < count; i++)
            {
                var leftBound = i == 0
                    ? float.NegativeInfinity
                    : (_hoverCandidates[i - 1].LayoutX + _hoverCandidates[i].LayoutX) * 0.5f;
                var rightBound = i == count - 1
                    ? float.PositiveInfinity
                    : (_hoverCandidates[i].LayoutX + _hoverCandidates[i + 1].LayoutX) * 0.5f;

                if (pointerX >= leftBound && pointerX < rightBound)
                {
                    return _hoverCandidates[i].Card;
                }
            }

            return _hoverCandidates[count - 1].Card;
        }

        private bool ShouldKeepCurrentHover(float pointerX, ManagedCard rawTarget)
        {
            if (!TryGetHoverCandidate(_hoveredCard, out var current) ||
                !TryGetHoverCandidate(rawTarget, out var next))
            {
                return false;
            }

            if (current.SlotIndex == next.SlotIndex)
            {
                return true;
            }

            var midpoint = (current.LayoutX + next.LayoutX) * 0.5f;
            var hysteresis = Mathf.Max(0f, layoutSettings.hoverSwitchHysteresis);

            if (next.LayoutX > current.LayoutX)
            {
                return pointerX < midpoint + hysteresis;
            }

            return pointerX > midpoint - hysteresis;
        }

        private bool TryGetHoverCandidate(ManagedCard card, out HandHoverCandidate candidate)
        {
            for (var i = 0; i < _hoverCandidates.Count; i++)
            {
                if (_hoverCandidates[i].Card == card)
                {
                    candidate = _hoverCandidates[i];
                    return true;
                }
            }

            candidate = default;
            return false;
        }

        private void ApplyHandHoverTarget(ManagedCard card)
        {
            if (_hoveredCard == card)
            {
                if (card != null)
                {
                    RefreshHandHoverAlphas(card);
                }

                return;
            }

            if (_hoveredCard != null)
            {
                ResetHandCardHoverVisual(_hoveredCard);
            }

            _hoveredCard = card;
            if (card == null)
            {
                ResetAllHandAlphas();
                return;
            }

            var driver = card.View?.GetComponent<CardVisualDriver>();
            driver?.SetTarget(CardVisualTarget.Hover);
            BoostHandCardHoverSorting(card);
            RefreshHandHoverAlphas(card);
        }

        /// <summary>
        /// 手牌在槽位中的权威布局世界坐标；hover 基准与回位均以此为准，避免反复触发累积上浮。
        /// </summary>
        internal bool TryGetHandLayoutWorldPosition(ManagedCard card, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (card == null || _slotContainer == null)
            {
                return false;
            }

            if (!_slotContainer.TryGetSlotOf(card, out var slotIndex))
            {
                return false;
            }

            worldPosition = _slotContainer.GetLayoutPosition(slotIndex);
            return true;
        }

        internal void ResetHandCardHoverVisual(ManagedCard card)
        {
            if (card?.View == null)
            {
                return;
            }

            var driver = card.View.GetComponent<CardVisualDriver>();
            driver?.SetTarget(CardVisualTarget.Base);
            RestoreHandCardSorting(card);
        }

        private void RestoreHandCardSorting(ManagedCard card)
        {
            if (card == null || _slotContainer == null)
            {
                return;
            }

            if (_slotContainer.TryGetSlotOf(card, out var slotIndex))
            {
                _slotContainer.ApplySortingOrder(card, slotIndex);
            }
        }

        private void ApplyAllHandSortingOrders()
        {
            _slotContainer?.ApplySortingOrders();
        }

        private void RefreshHandCardDisplay(ManagedCard card)
        {
            if (card == null)
            {
                return;
            }

            CardManagerSingleton.Instance.RefreshDisplayMode(card);
            ApplyAllHandSortingOrders();
        }

        private void BoostHandCardHoverSorting(ManagedCard card)
        {
            var sortingGroup = card.View?.GetComponent<SortingGroup>();
            if (sortingGroup != null)
            {
                sortingGroup.sortingOrder = layoutSettings.sortingOrderBase + layoutSettings.hoverSortingBoost;
            }
        }

        public bool TryCompleteDragApply()
        {
            if (_dragSession?.Card == null || _dragSession.Source != DragSource.Hand)
            {
                return false;
            }

            if (!_dragSession.PointerReleasedInZone)
            {
                return false;
            }

            CompleteDragApplyAsync().Forget();
            return true;
        }

        public void CancelDragAndReturnToHand()
        {
            if (_dragSession == null)
            {
                return;
            }

            CancelDragAndReturnInternalAsync().Forget();
        }

        private void BeginDragLoop()
        {
            _dragLoopCts?.Cancel();
            _dragLoopCts?.Dispose();
            _dragLoopCts = new CancellationTokenSource();
            RunDragLoopAsync(_dragLoopCts.Token).Forget();
        }

        private async UniTaskVoid RunDragLoopAsync(CancellationToken cancellationToken)
        {
            var session = _dragSession;
            if (session?.Card?.Transform == null)
            {
                return;
            }

            var card = session.Card;
            var cardManager = CardManagerSingleton.Instance;
            cardManager.SetDisplayMode(card, CardDisplayMode.DragCardMode);
            BoostDragSorting(card);

            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[CardHandManager] 未找到 Main Camera，无法拖拽。");
                await FinishDragWithReturnAsync(session);
                return;
            }

            var dragZ = card.Transform.position.z;

            try
            {
                while (Input.GetMouseButton(0))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var world = ScreenToWorldOnPlane(Input.mousePosition, camera, dragZ);
                    card.Transform.position = world;

                    var inZone = IsPointInApplyZone(world);
                    var overGround = IsOverGroundCard(world);
                    CardOpacityUtility.SetAlpha(
                        card,
                        overGround ? layoutSettings.dragAlphaWhenOverGround : 1f);

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                var releaseWorld = ScreenToWorldOnPlane(Input.mousePosition, camera, dragZ);
                session.PointerReleasedInZone = IsPointInApplyZone(releaseWorld);

                if (session.Source == DragSource.Hand)
                {
                    if (!session.PointerReleasedInZone)
                    {
                        await FinishDragWithReturnAsync(session);
                        return;
                    }

                    await CompleteDragApplyInternalAsync(session);
                    return;
                }

                if (session.Source == DragSource.Ground)
                {
                    if (session.PointerReleasedInZone)
                    {
                        await FinishGroundDragToHandAsync(session);
                    }
                    else
                    {
                        await FinishGroundDragWithReturnAsync(session);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (_dragSession == session)
                {
                    await FinishDragWithReturnAsync(session);
                }
            }
        }

        private async UniTask CompleteDragApplyInternalAsync(DragSession session)
        {
            var card = session.Card;
            if (card == null)
            {
                ClearDragSession();
                return;
            }

            var targetSlot = TryResolveGroundSlotUnderPoint(card.Transform.position);
            var validator = DragApplyValidator ?? DefaultDragApplyValidator;
            var approved = await validator(card, targetSlot);

            if (!approved)
            {
                await FinishDragWithReturnAsync(session);
                return;
            }

            _dragSession = null;
            CardOpacityUtility.ResetAlpha(card);
            await VanishCardAfterApplyAsync(card);
        }

        private async UniTaskVoid CompleteDragApplyAsync()
        {
            var session = _dragSession;
            if (session == null)
            {
                return;
            }

            await CompleteDragApplyInternalAsync(session);
        }

        private static async UniTask<bool> DefaultDragApplyValidator(ManagedCard card, int? targetGroundSlot)
        {
            await UniTask.CompletedTask;
            return true;
        }

        private async UniTask VanishCardAfterApplyAsync(ManagedCard card)
        {
            if (card?.Transform == null)
            {
                CardManagerSingleton.Instance.Release(card);
                return;
            }

            _isBusy = true;
            try
            {
                CardManagerSingleton.Instance.SetDisplayMode(card, CardDisplayMode.RemovedMode);
                if (card.TryGetEffectManager(out var effectManager))
                {
                    await effectManager.PlayUseAsync(CardBoardDirection.None, CancellationToken.None);
                }
                else
                {
                    var initialScale = card.Transform.localScale;
                    await RunViewTweenAsync(
                        CardViewTween.ScaleDisappear(
                            card.Transform,
                            initialScale,
                            layoutSettings.applyVanishDuration),
                        CancellationToken.None);
                }
                CardOpacityUtility.ClearCache(card.Uid);
                CardManagerSingleton.Instance.Release(card);
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async UniTask FinishDragWithReturnAsync(DragSession session)
        {
            var card = session?.Card;
            if (card == null)
            {
                ClearDragSession();
                return;
            }

            _isBusy = true;
            try
            {
                CardOpacityUtility.ResetAlpha(card);
                CardManagerSingleton.Instance.SetDisplayMode(card, CardDisplayMode.HandCardMode);

                var slot = session.OriginHandSlot >= 0 ? session.OriginHandSlot : HandCount;
                if (!_slotContainer.TryInsertAt(slot, card, out var rippleMoves))
                {
                    Debug.LogWarning("[CardHandManager] 回手失败，手牌已满。");
                    CardManagerSingleton.Instance.Release(card);
                    ClearDragSession();
                    return;
                }

                await CardDeckTween.MoveRippleAsync(rippleMoves, layoutSettings.moveDuration);
                RefreshHandCardDisplay(card);
                SnapHandCardToLayout(card);
            }
            finally
            {
                _isBusy = false;
                ClearDragSession();
            }
        }

        private void SnapHandCardToLayout(ManagedCard card)
        {
            if (card?.Transform == null)
            {
                return;
            }

            if (!TryGetHandLayoutWorldPosition(card, out var layoutPosition))
            {
                return;
            }

            CardDeckTween.KillMotion(card.Transform);
            card.Transform.position = layoutPosition;

            var driver = card.View.GetComponent<CardVisualDriver>();
            driver?.SnapToDisplayMode();
        }

        private async UniTask CancelDragAndReturnInternalAsync()
        {
            var session = _dragSession;
            if (session == null)
            {
                return;
            }

            _dragLoopCts?.Cancel();

            if (session.Source == DragSource.Hand)
            {
                await FinishDragWithReturnAsync(session);
                return;
            }

            await FinishGroundDragWithReturnAsync(session);
        }

        private async UniTask FinishGroundDragToHandAsync(DragSession session)
        {
            var card = session?.Card;
            if (card == null)
            {
                ClearDragSession();
                return;
            }

            _dragSession = null;
            CardOpacityUtility.ResetAlpha(card);
            await PullFromGroundAsync(card);
        }

        private async UniTask FinishGroundDragWithReturnAsync(DragSession session)
        {
            var card = session?.Card;
            if (card == null)
            {
                ClearDragSession();
                return;
            }

            _isBusy = true;
            try
            {
                CardOpacityUtility.ResetAlpha(card);
                var field = GroundFieldManagerSingleton.Instance;
                var cardManager = CardManagerSingleton.Instance;
                cardManager.SetDisplayMode(card, CardDisplayMode.GroundCardMode);

                var targetSlot = session.OriginGroundSlot;
                if (field != null && field.IsPlaceable(targetSlot))
                {
                    var anchor = field.GetGroundAnchor(targetSlot);
                    if (anchor != null)
                    {
                        CardDeckTween.MoveToWorld(
                            card.Transform,
                            anchor.position,
                            layoutSettings.moveDuration);
                        await UniTask.Delay(
                            TimeSpan.FromSeconds(layoutSettings.moveDuration),
                            cancellationToken: CancellationToken.None);
                    }

                    field.RequestPlaceCard(targetSlot, card);
                }
                else
                {
                    CardManagerSingleton.Instance.Release(card);
                }

                cardManager.RefreshDisplayMode(card);
            }
            finally
            {
                _isBusy = false;
                ClearDragSession();
            }
        }

        private void ClearDragSession()
        {
            _dragSession = null;
        }

        private void ClearHandHoverState(ManagedCard card)
        {
            if (card != null)
            {
                ResetHandCardHoverVisual(card);
            }

            if (_hoveredCard == card)
            {
                _hoveredCard = null;
            }

            ResetAllHandAlphas();
        }

        private void RefreshHandHoverAlphas(ManagedCard hovered)
        {
            CardOpacityUtility.ResetAlpha(hovered);

            for (var i = 0; i < layoutSettings.maxSlots; i++)
            {
                if (!_slotContainer.TryGetCardAt(i, out var card) || card == null || card == hovered)
                {
                    continue;
                }

                CardOpacityUtility.SetAlpha(card, layoutSettings.nonHoveredAlpha);
            }
        }

        private void ResetAllHandAlphas()
        {
            for (var i = 0; i < layoutSettings.maxSlots; i++)
            {
                if (_slotContainer.TryGetCardAt(i, out var card) && card != null)
                {
                    CardOpacityUtility.ResetAlpha(card);
                }
            }
        }

        private void BoostDragSorting(ManagedCard card)
        {
            var sortingGroup = card.View?.GetComponent<SortingGroup>();
            if (sortingGroup != null)
            {
                sortingGroup.sortingOrder =
                    CardDisplayModeVisuals.GetSortingOrder(CardDisplayMode.DragCardMode)
                    + layoutSettings.dragSortingBoost;
            }
        }

        private bool IsPointInApplyZone(Vector3 worldPoint)
        {
            if (applyZoneCollider == null)
            {
                return false;
            }

            return applyZoneCollider.bounds.Contains(worldPoint);
        }

        private static bool IsOverGroundCard(Vector3 worldPoint)
        {
            var hits = Physics2D.OverlapPointAll(worldPoint);
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                var driver = hit.GetComponent<CardVisualDriver>();
                if (driver?.BoundCard != null &&
                    driver.BoundCard.DisplayMode == CardDisplayMode.GroundCardMode)
                {
                    return true;
                }
            }

            return false;
        }

        private int? TryResolveGroundSlotUnderPoint(Vector3 worldPoint)
        {
            var field = GroundFieldManagerSingleton.Instance;
            if (field == null)
            {
                return null;
            }

            var hits = Physics2D.OverlapPointAll(worldPoint);
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                var driver = hit.GetComponent<CardVisualDriver>();
                var bound = driver?.BoundCard;
                if (bound == null || bound.DisplayMode != CardDisplayMode.GroundCardMode)
                {
                    continue;
                }

                if (field.TryGetSlotOf(bound.Uid, out var slot))
                {
                    return slot;
                }
            }

            return null;
        }

        private static Vector3 ScreenToWorldOnPlane(Vector3 screenPosition, Camera camera, float worldZ)
        {
            var point = camera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, camera.WorldToScreenPoint(new Vector3(0f, 0f, worldZ)).z));
            point.z = worldZ;
            return point;
        }

        private static async UniTask RunViewTweenAsync(IEnumerator routine, CancellationToken cancellationToken)
        {
            if (routine == null)
            {
                return;
            }

            while (routine.MoveNext())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        private void ResolveSceneReferences()
        {
            if (handAnchorsRoot == null)
            {
                var anchors = GameObject.Find("Anchors");
                if (anchors != null)
                {
                    handAnchorsRoot = anchors.transform.Find("CardHandAnchors");
                }
            }

            if (applyZoneCollider == null && handAnchorsRoot != null)
            {
                var zone = handAnchorsRoot.Find("HandcardApplyZone");
                if (zone != null)
                {
                    applyZoneCollider = zone.GetComponent<Collider>();
                }
            }
        }

        private void CacheAnchors()
        {
            _handAnchors.Clear();
            _handAnchors.AddRange(CardHandAnchorUtility.GetSortedHandAnchors(handAnchorsRoot, layoutSettings.maxSlots));
            _slotContainer.SetLayoutAnchorPositions(_handAnchors);
        }

        private void InitializeLayoutOrigin()
        {
            var leftX = 0f;
            var baseY = 0f;
            var baseZ = 0f;

            if (_handAnchors.Count > 0 && _handAnchors[0] != null)
            {
                var first = _handAnchors[0].position;
                leftX = first.x;
                baseY = first.y;
                baseZ = first.z;
            }
            else if (handAnchorsRoot != null)
            {
                leftX = handAnchorsRoot.position.x;
                baseY = handAnchorsRoot.position.y;
                baseZ = handAnchorsRoot.position.z;
            }

            _slotContainer.SetLayoutOrigin(leftX, baseY, baseZ);
        }
    }
}
