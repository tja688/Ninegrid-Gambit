using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 牌组管理器单例：编排卡组 Standby / Entry / InGame 三模式，以及向 Ground 发牌。
    /// </summary>
    public sealed class CardDeckManagerSingleton : MonoBehaviour
    {
        private static CardDeckManagerSingleton _instance;

        [Header("Scene Anchors")]
        [Tooltip("场景 Anchors/CardDeckAnchors。留空时 Awake 按名称 CardDeckAnchors 查找。")]
        [SerializeField] private Transform deckAnchorsRoot;

        [Tooltip("场景 CardDeckAnchors/CardDeckAddAnchors，增卡镜像入口。留空时作为 deckAnchorsRoot 子节点查找。")]
        [SerializeField] private Transform addAnchorsRoot;

        [Tooltip("场景 CardDeckAnchors/DeckEntryPreparationSlot，Standby 待命堆叠点。")]
        [SerializeField] private Transform entryPreparationSlot;

        [Tooltip("场景 Anchors/GroundAnchors。留空时按名称 GroundAnchors 查找。")]
        [SerializeField] private Transform groundAnchorsRoot;

        [Header("Layout")]
        [Tooltip("卡组布局与动效参数。")]
        [SerializeField] private CardDeckLayoutSettings layoutSettings = new();

        private readonly List<ManagedCard> _pendingEntryCards = new();
        private CardDeckSlotContainer _slotContainer;
        private List<Transform> _deckAnchors = new();
        private List<Transform> _addAnchors = new();
        private List<Transform> _groundAnchors = new();
        private bool _isBusy;

        public static CardDeckManagerSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<CardDeckManagerSingleton>();
                }

                return _instance;
            }
        }

        public CardDeckMode CurrentMode { get; private set; } = CardDeckMode.Standby;

        public int DeckCount => _slotContainer?.Count ?? _pendingEntryCards.Count;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _slotContainer = new CardDeckSlotContainer(layoutSettings);
            ResolveSceneReferences();
            CacheAnchors();
            InitializeLayoutOrigin();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 注入卡组（仅 Standby）。卡牌须已由 CardManagerSingleton 创建。
        /// </summary>
        public void InjectDeck(IReadOnlyList<ManagedCard> cardsInOrder)
        {
            if (CurrentMode != CardDeckMode.Standby)
            {
                Debug.LogWarning("[CardDeckManager] InjectDeck 仅在 Standby 模式可用。");
                return;
            }

            _pendingEntryCards.Clear();
            _slotContainer.Clear();
            ResolveFieldManager()?.ClearField();

            if (cardsInOrder == null || cardsInOrder.Count == 0)
            {
                return;
            }

            var limit = Mathf.Min(cardsInOrder.Count, layoutSettings.maxSlots);
            if (cardsInOrder.Count > limit)
            {
                Debug.LogWarning($"[CardDeckManager] 注入卡牌超过上限 {layoutSettings.maxSlots}，已截断。");
            }

            for (var i = 0; i < limit; i++)
            {
                var card = cardsInOrder[i];
                if (card == null)
                {
                    continue;
                }

                _pendingEntryCards.Add(card);
                PlaceCardInStandby(card, i);
                CardManagerSingleton.Instance.SetDisplayMode(card, CardDisplayMode.CardDeckMode);
            }
        }

        /// <summary>
        /// Standby → Entry → InGame 入场流程。
        /// </summary>
        public UniTask BeginEntryAsync(CancellationToken cancellationToken = default)
        {
            return BeginEntryInternalAsync(cancellationToken);
        }

        /// <summary>
        /// 从指定卡组槽发一张牌到 Ground 格位（1-based，仅 InGame）。
        /// </summary>
        public void DealCard(int deckSlotIndex, int groundSlot)
        {
            TryDealCard(deckSlotIndex, groundSlot);
        }

        /// <summary>
        /// 从卡组最左侧（槽位 0）发一张牌到 Ground 格位（1-based，仅 InGame）。
        /// </summary>
        public bool DealFirstCard(int groundSlot)
        {
            return TryDealCard(0, groundSlot);
        }

        /// <summary>
        /// 批量发牌：按 groundSlots 顺序，每次从卡组最左侧连续取牌（1-based，仅 InGame）。
        /// </summary>
        public void DealCards(IReadOnlyList<int> groundSlots)
        {
            DealCardsInternal(groundSlots).Forget();
        }

        /// <summary>
        /// 开局发牌：按 Ground 1,2,3,6,9,8,7,4 顺序，从卡组左侧连续取牌。
        /// </summary>
        public void DealOpeningRing()
        {
            DealOpeningRingInternal().Forget();
        }

        /// <summary>
        /// 在指定索引插入卡牌（仅 InGame），从 CardDeckAddAnchors 镜像槽入场。
        /// </summary>
        public UniTask AddCardAtAsync(int slotIndex, ManagedCard card, CancellationToken cancellationToken = default)
        {
            return AddCardAtInternalAsync(slotIndex, card, cancellationToken);
        }

        /// <summary>
        /// 清理 Ground 上已发卡牌并释放槽位占用（测试/重置用）。委托 GroundFieldManagerSingleton。
        /// </summary>
        public void ClearGround()
        {
            ResolveFieldManager()?.ClearField();
        }

        public bool TryGetFirstDeckSlot(out int deckSlotIndex)
        {
            deckSlotIndex = -1;
            if (!_slotContainer.TryGetCardAt(0, out _))
            {
                return false;
            }

            deckSlotIndex = 0;
            return true;
        }

        public bool TryGetFirstEmptyGroundSlot(out int groundSlot)
        {
            groundSlot = -1;
            var field = ResolveFieldManager();
            if (field == null)
            {
                return false;
            }

            var emptySlots = field.GetEmptyPlaceableSlots();
            if (emptySlots.Count == 0)
            {
                return false;
            }

            groundSlot = emptySlots[0];
            return true;
        }

        public bool TryGetRandomDeckSlot(out int deckSlotIndex)
        {
            deckSlotIndex = -1;
            var candidates = new List<int>();
            for (var i = 0; i < layoutSettings.maxSlots; i++)
            {
                if (_slotContainer.TryGetCardAt(i, out _))
                {
                    candidates.Add(i);
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            deckSlotIndex = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return true;
        }

        public bool TryGetRandomEmptyGroundSlot(out int groundSlot)
        {
            groundSlot = -1;
            var field = ResolveFieldManager();
            if (field == null)
            {
                return false;
            }

            var emptySlots = field.GetEmptyPlaceableSlots();
            if (emptySlots.Count == 0)
            {
                return false;
            }

            groundSlot = emptySlots[UnityEngine.Random.Range(0, emptySlots.Count)];
            return true;
        }

        private async UniTask BeginEntryInternalAsync(CancellationToken cancellationToken)
        {
            if (_isBusy)
            {
                Debug.LogWarning("[CardDeckManager] 当前忙碌，无法开始入场。");
                return;
            }

            if (CurrentMode != CardDeckMode.Standby || _pendingEntryCards.Count == 0)
            {
                Debug.LogWarning("[CardDeckManager] 无待入场卡牌或模式不正确。");
                return;
            }

            _isBusy = true;
            CurrentMode = CardDeckMode.Entry;

            try
            {
                await CardDeckTween.WaitOneFrameAsync(cancellationToken);

                for (var i = 0; i < _pendingEntryCards.Count; i++)
                {
                    _slotContainer.ApplySortingOrder(_pendingEntryCards[i], i);
                }

                for (var i = 0; i < _pendingEntryCards.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var card = _pendingEntryCards[i];
                    if (card?.Transform == null)
                    {
                        continue;
                    }

                    var anchor = GetDeckAnchor(i);
                    if (anchor == null)
                    {
                        Debug.LogWarning($"[CardDeckManager] 缺少 Entry 锚点 index={i}");
                        continue;
                    }

                    CardDeckTween.MoveToWorld(
                        card.Transform,
                        anchor.position,
                        layoutSettings.moveDuration);

                    if (i < _pendingEntryCards.Count - 1)
                    {
                        await UniTask.Delay(
                            TimeSpan.FromSeconds(layoutSettings.entryDealInterval),
                            cancellationToken: cancellationToken);
                    }
                }

                await UniTask.Delay(
                    TimeSpan.FromSeconds(layoutSettings.moveDuration),
                    cancellationToken: cancellationToken);

                _slotContainer.SetCardsDense(_pendingEntryCards);
                _pendingEntryCards.Clear();
                await SwitchToDynamicLayoutAsync(cancellationToken);
                CurrentMode = CardDeckMode.InGame;
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async UniTask SwitchToDynamicLayoutAsync(CancellationToken cancellationToken)
        {
            var moves = new List<CardDeckRippleMove>();
            for (var i = 0; i < layoutSettings.maxSlots; i++)
            {
                if (!_slotContainer.TryGetCardAt(i, out var card) || card?.Transform == null)
                {
                    continue;
                }

                moves.Add(new CardDeckRippleMove(
                    card,
                    i,
                    i,
                    _slotContainer.GetLayoutPosition(i),
                    delay: i * layoutSettings.rippleDelayPerSlot));
            }

            _slotContainer.ApplySortingOrders();
            await CardDeckTween.MoveRippleAsync(moves, layoutSettings.moveDuration, cancellationToken);
        }

        private bool TryDealCard(int deckSlotIndex, int groundSlot)
        {
            if (!EnsureInGameForDeal())
            {
                return false;
            }

            var field = ResolveFieldManager();
            if (field == null)
            {
                Debug.LogWarning("[CardDeckManager] 未找到 GroundFieldManagerSingleton。");
                return false;
            }

            if (!IsValidGroundSlot(groundSlot))
            {
                Debug.LogWarning($"[CardDeckManager] Ground 格位无效或禁止发牌: {groundSlot}");
                return false;
            }

            if (!field.IsPlaceable(groundSlot))
            {
                Debug.LogWarning($"[CardDeckManager] Ground 格位已占用: {groundSlot}");
                return false;
            }

            if (!_slotContainer.TryGetCardAt(deckSlotIndex, out var card) || card == null)
            {
                Debug.LogWarning($"[CardDeckManager] 卡组槽位为空: {deckSlotIndex}");
                return false;
            }

            if (!_slotContainer.TryRemoveAt(deckSlotIndex, out var removed, out var rippleMoves))
            {
                return false;
            }

            var anchorIndex = CardSlotAnchorUtility.SlotToAnchorIndex(groundSlot);
            var groundAnchor = anchorIndex >= 0 && anchorIndex < _groundAnchors.Count
                ? _groundAnchors[anchorIndex]
                : null;
            if (groundAnchor == null)
            {
                Debug.LogWarning($"[CardDeckManager] Ground 锚点缺失: slot={groundSlot}");
                if (!_slotContainer.TryInsertAt(deckSlotIndex, removed, out var rollbackRipple))
                {
                    CardManagerSingleton.Instance.Release(removed);
                }
                else
                {
                    CardDeckTween.MoveRippleAsync(rollbackRipple, layoutSettings.moveDuration).Forget();
                }

                return false;
            }

            CardDeckTween.MoveRippleAsync(rippleMoves, layoutSettings.moveDuration).Forget();

            var cardManager = CardManagerSingleton.Instance;
            cardManager.SetDisplayMode(removed, CardDisplayMode.GroundCardMode);
            CardDeckTween.MoveToWorld(
                removed.Transform,
                groundAnchor.position,
                layoutSettings.moveDuration,
                onComplete: () => cardManager.RefreshDisplayMode(removed));
            field.RequestPlaceCard(groundSlot, removed);
            return true;
        }

        private async UniTask DealCardsInternal(IReadOnlyList<int> groundSlots)
        {
            if (groundSlots == null)
            {
                return;
            }

            for (var i = 0; i < groundSlots.Count; i++)
            {
                TryDealCard(0, groundSlots[i]);
                if (i < groundSlots.Count - 1)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(layoutSettings.dealInterval));
                }
            }
        }

        private async UniTask DealOpeningRingInternal()
        {
            if (!EnsureInGameForDeal())
            {
                return;
            }

            var field = ResolveFieldManager();
            if (field == null)
            {
                return;
            }

            var ringSlots = CardSlotAnchorUtility.GetOpeningRingSlotIndices();
            for (var i = 0; i < ringSlots.Count; i++)
            {
                var groundSlot = ringSlots[i];
                if (!field.IsPlaceable(groundSlot))
                {
                    continue;
                }

                if (_slotContainer.Count == 0)
                {
                    break;
                }

                TryDealCard(0, groundSlot);
                if (i < ringSlots.Count - 1)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(layoutSettings.dealInterval));
                }
            }
        }

        private async UniTask AddCardAtInternalAsync(int slotIndex, ManagedCard card, CancellationToken cancellationToken)
        {
            if (CurrentMode != CardDeckMode.InGame)
            {
                Debug.LogWarning("[CardDeckManager] AddCardAt 仅在 InGame 模式可用。");
                return;
            }

            if (_isBusy || card == null)
            {
                return;
            }

            _isBusy = true;
            try
            {
                CardManagerSingleton.Instance.SetDisplayMode(card, CardDisplayMode.CardDeckMode);

                var addAnchor = GetAddAnchor(slotIndex);
                if (addAnchor != null && card.Transform != null)
                {
                    card.Transform.position = addAnchor.position;
                }

                if (!_slotContainer.TryInsertAt(slotIndex, card, out var rippleMoves))
                {
                    Debug.LogWarning("[CardDeckManager] 插入卡牌失败，可能已满。");
                    return;
                }

                await CardDeckTween.MoveRippleAsync(rippleMoves, layoutSettings.moveDuration, cancellationToken);
            }
            finally
            {
                _isBusy = false;
            }
        }

        private bool IsValidGroundSlot(int groundSlot)
        {
            if (!CardSlotAnchorUtility.IsPlaceableGroundSlot(groundSlot))
            {
                return false;
            }

            var anchorIndex = CardSlotAnchorUtility.SlotToAnchorIndex(groundSlot);
            return anchorIndex >= 0
                   && anchorIndex < _groundAnchors.Count
                   && _groundAnchors[anchorIndex] != null;
        }

        private static GroundFieldManagerSingleton ResolveFieldManager()
        {
            return GroundFieldManagerSingleton.Instance;
        }

        private void PlaceCardInStandby(ManagedCard card, int stackIndex)
        {
            if (card?.Transform == null || entryPreparationSlot == null)
            {
                return;
            }

            var basePosition = entryPreparationSlot.position;
            card.Transform.position = new Vector3(
                basePosition.x,
                basePosition.y,
                basePosition.z + stackIndex * layoutSettings.standbyStackZStep);
        }

        private bool EnsureInGameForDeal()
        {
            if (CurrentMode != CardDeckMode.InGame)
            {
                Debug.LogWarning("[CardDeckManager] 发牌仅在 InGame 模式可用。");
                return false;
            }

            return true;
        }

        private Transform GetDeckAnchor(int index)
        {
            return index >= 0 && index < _deckAnchors.Count ? _deckAnchors[index] : null;
        }

        private Transform GetAddAnchor(int index)
        {
            return index >= 0 && index < _addAnchors.Count ? _addAnchors[index] : null;
        }

        private void ResolveSceneReferences()
        {
            if (deckAnchorsRoot == null)
            {
                var anchors = GameObject.Find("Anchors");
                if (anchors != null)
                {
                    deckAnchorsRoot = anchors.transform.Find("CardDeckAnchors");
                }
            }

            if (addAnchorsRoot == null && deckAnchorsRoot != null)
            {
                addAnchorsRoot = deckAnchorsRoot.Find("CardDeckAddAnchors");
            }

            if (entryPreparationSlot == null && deckAnchorsRoot != null)
            {
                entryPreparationSlot = FindChildTrimmed(deckAnchorsRoot, "DeckEntryPreparationSlot");
            }

            if (groundAnchorsRoot == null)
            {
                var anchors = GameObject.Find("Anchors");
                if (anchors != null)
                {
                    groundAnchorsRoot = anchors.transform.Find("GroundAnchors");
                }
            }
        }

        private void CacheAnchors()
        {
            _deckAnchors = CardSlotAnchorUtility.GetSortedSlotTransforms(deckAnchorsRoot, layoutSettings.maxSlots);
            _addAnchors = CardSlotAnchorUtility.GetSortedSlotTransforms(addAnchorsRoot, layoutSettings.maxSlots);
            _groundAnchors = CardSlotAnchorUtility.GetSortedSlotTransforms(groundAnchorsRoot, 9);
            _slotContainer.SetLayoutAnchorPositions(_deckAnchors);
        }

        private void InitializeLayoutOrigin()
        {
            var leftX = 0f;
            var baseY = layoutSettings.layoutBaseY;
            var baseZ = layoutSettings.layoutBaseZ;

            if (_deckAnchors.Count > 0 && _deckAnchors[0] != null)
            {
                var slot1 = _deckAnchors[0].position;
                leftX = slot1.x;
                if (Mathf.Approximately(baseY, 0f))
                {
                    baseY = slot1.y;
                }

                if (Mathf.Approximately(baseZ, 0f))
                {
                    baseZ = slot1.z;
                }
            }
            else if (deckAnchorsRoot != null)
            {
                leftX = deckAnchorsRoot.position.x;
                if (Mathf.Approximately(baseY, 0f))
                {
                    baseY = deckAnchorsRoot.position.y;
                }

                if (Mathf.Approximately(baseZ, 0f))
                {
                    baseZ = deckAnchorsRoot.position.z;
                }
            }

            _slotContainer.SetLayoutOrigin(leftX, baseY, baseZ);
        }

        private static Transform FindChildTrimmed(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child != null && string.Equals(child.name.Trim(), childName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }
    }
}
