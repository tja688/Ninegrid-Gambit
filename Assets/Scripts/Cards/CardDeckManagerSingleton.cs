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

        public bool IsBusy => _isBusy;

        public CardDeckLayoutSettings LayoutSettings => layoutSettings;

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

            for (var i = 0; i < cardsInOrder.Count; i++)
            {
                var card = cardsInOrder[i];
                if (card == null)
                {
                    continue;
                }

                _pendingEntryCards.Add(card);
                PlaceCardInStandby(card, _pendingEntryCards.Count - 1);
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
        /// 按 Uid 从卡组取出并放到指定 Ground 格（1-based，仅 InGame）。对齐内核盘面就位用。
        /// </summary>
        public bool DealCardByUid(int uid, int groundSlot, bool skipBusyGuard = false)
        {
            if (!EnsureInGameForDeal())
            {
                return false;
            }

            if (!TryFindDeckSlotByUid(uid, out var deckSlot))
            {
                Debug.LogWarning($"[CardDeckManager] 卡组中未找到 Uid={uid}，无法就位到格 {groundSlot}（将走兜底放置）。");
                return false;
            }

            return TryDealCard(deckSlot, groundSlot, skipBusyGuard);
        }

        /// <summary>
        /// 按 Uid 发牌到 Ground。若卡不在组内且提供了 <paramref name="ensureCard"/>，
        /// 先经 CardDeckAddAnchors 入组（完整入组缓动），再走与 <see cref="DealCard"/> 相同的飞入轨迹。
        /// </summary>
        /// <param name="awaitMove">为 true 时等到本张 moveDuration 结束；批量交错发牌时应传 false，由调用方在末张后再等一次。</param>
        public async UniTask<bool> DealCardByUidAsync(
            int uid,
            int groundSlot,
            ManagedCard ensureCard = null,
            bool skipBusyGuard = false,
            bool awaitMove = true,
            CancellationToken cancellationToken = default)
        {
            if (!EnsureInGameForDeal())
            {
                return false;
            }

            if (!TryFindDeckSlotByUid(uid, out var deckSlot))
            {
                if (ensureCard == null || ensureCard.Uid != uid)
                {
                    Debug.LogWarning(
                        $"[CardDeckManager] 卡组中未找到 Uid={uid}，且无 ensureCard，无法就位到格 {groundSlot}。");
                    return false;
                }

                var field = ResolveFieldManager();
                if (field != null && field.TryGetSlotOf(uid, out _))
                {
                    Debug.LogWarning(
                        $"[CardDeckManager] Uid={uid} 已在场地，跳过入组发牌到格 {groundSlot}。");
                    return false;
                }

                await AddCardAtInternalAsync(0, ensureCard, cancellationToken);
                if (!TryFindDeckSlotByUid(uid, out deckSlot))
                {
                    Debug.LogWarning(
                        $"[CardDeckManager] 入组后仍未找到 Uid={uid}，无法就位到格 {groundSlot}。");
                    return false;
                }
            }

            if (!TryDealCard(deckSlot, groundSlot, skipBusyGuard))
            {
                return false;
            }

            if (awaitMove && layoutSettings != null && layoutSettings.moveDuration > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(layoutSettings.moveDuration),
                    cancellationToken: cancellationToken);
            }

            return true;
        }

        /// <summary>
        /// 卡组槽中是否已有指定 Uid。
        /// </summary>
        public bool ContainsUid(int uid)
        {
            return TryFindDeckSlotByUid(uid, out _);
        }

        /// <summary>
        /// 按 Uid 从卡组槽卸下视图，不 Release（供未用帮助卡结算等外层自行退场）。
        /// </summary>
        public bool TryDetachByUid(int uid, out ManagedCard card)
        {
            card = null;
            if (!TryFindDeckSlotByUid(uid, out var deckSlot) || _slotContainer == null)
            {
                // Standby 待入场列表也可能持有该 uid。
                for (var i = _pendingEntryCards.Count - 1; i >= 0; i--)
                {
                    if (_pendingEntryCards[i] == null || _pendingEntryCards[i].Uid != uid)
                    {
                        continue;
                    }

                    card = _pendingEntryCards[i];
                    _pendingEntryCards.RemoveAt(i);
                    return card != null;
                }

                return false;
            }

            return _slotContainer.TryRemoveAt(deckSlot, out card, out _);
        }

        /// <summary>
        /// 清卡组槽并回到 Standby，供局内重新开局前复位。不销毁卡视图（由 CardManager 统一释放）。
        /// </summary>
        public void ResetToStandby()
        {
            _pendingEntryCards.Clear();
            _slotContainer?.Clear();
            CurrentMode = CardDeckMode.Standby;
            _isBusy = false;
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
            var count = _slotContainer?.Count ?? 0;
            if (count == 0)
            {
                return false;
            }

            deckSlotIndex = UnityEngine.Random.Range(0, count);
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

        /// <summary>
        /// 从卡组最左侧取牌，不占 Ground 格位（供空牌位探求使用，仅 InGame）。
        /// </summary>
        public bool TryWithdrawFirstCard(out ManagedCard card, out IReadOnlyList<CardDeckRippleMove> rippleMoves)
        {
            card = null;
            rippleMoves = Array.Empty<CardDeckRippleMove>();
            if (!EnsureInGameForDeal())
            {
                return false;
            }

            if (!_slotContainer.TryRemoveAt(0, out var removed, out rippleMoves))
            {
                return false;
            }

            card = removed;
            return card != null;
        }

        /// <summary>
        /// 将卡牌退回卡组最左侧（探求失败回滚，仅 InGame）。发射后不管：垂直上飞离画后自然 ripple 入组。
        /// </summary>
        public bool TryReturnCardToDeckFront(ManagedCard card, out IReadOnlyList<CardDeckRippleMove> rippleMoves)
        {
            rippleMoves = Array.Empty<CardDeckRippleMove>();
            return LaunchReturnFieldCardToDeck(card, 0);
        }

        /// <summary>
        /// 场地卡垂直上飞离画后插入卡组（发射后不管，可与旋转/换位并行）。
        /// </summary>
        public bool LaunchReturnFieldCardToDeck(ManagedCard card, int insertIndex = 0)
        {
            if (!EnsureInGameForDeal() || card == null || card.Transform == null)
            {
                return false;
            }

            if (ContainsUid(card.Uid))
            {
                return true;
            }

            var field = ResolveFieldManager();
            if (field != null && field.TryGetSlotOf(card.Uid, out var slot))
            {
                field.ClearSlotOccupancy(slot, skipBusyGuard: true);
            }

            CardManagerSingleton.Instance.SetDisplayMode(card, CardDisplayMode.CardDeckMode);

            var fieldLayout = field?.LayoutSettings;
            var exitY = fieldLayout != null ? fieldLayout.fieldExitYThreshold : 8f;
            var exitDuration = fieldLayout != null ? fieldLayout.fieldExitDuration : 0.35f;
            var targetInsertIndex = Mathf.Clamp(insertIndex, 0, Mathf.Max(0, layoutSettings.maxSlots - 1));

            CardDeckTween.LaunchFieldExitThenDeckInsert(
                card.Transform,
                exitY,
                exitDuration,
                () => CompleteFieldReturnDeckInsert(card, targetInsertIndex),
                uid: card.Uid);

            return true;
        }

        private void CompleteFieldReturnDeckInsert(ManagedCard card, int insertIndex)
        {
            if (card == null || card.Transform == null || CurrentMode != CardDeckMode.InGame)
            {
                return;
            }

            if (ContainsUid(card.Uid))
            {
                return;
            }

            CardManagerSingleton.Instance.SetDisplayMode(card, CardDisplayMode.CardDeckMode);
            if (_slotContainer.TryInsertAt(insertIndex, card, out var rippleMoves)
                && rippleMoves != null
                && rippleMoves.Count > 0)
            {
                CardDeckTween.MoveRippleAsync(rippleMoves, layoutSettings.moveDuration).Forget();
            }
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

                    var target = ResolveEntryTargetPosition(i);
                    if (!target.HasValue)
                    {
                        Debug.LogWarning($"[CardDeckManager] 缺少 Entry 锚点（视觉槽位上限={layoutSettings.maxSlots}）");
                        continue;
                    }

                    CardDeckTween.MoveToWorld(
                        card.Transform,
                        target.Value,
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
            var count = _slotContainer.Count;
            for (var i = 0; i < count; i++)
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

        private bool TryDealCard(int deckSlotIndex, int groundSlot, bool skipBusyGuard = false)
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

            var placeable = field.IsPlaceable(groundSlot);
            if (!placeable)
            {
                Debug.LogWarning($"[CardDeckManager] Ground 格位已占用: {groundSlot}");
                ReportDealTrace(uid: 0, groundSlot, placeable: false, ok: false, rollback: false);
                return false;
            }

            if (!_slotContainer.TryGetCardAt(deckSlotIndex, out var card) || card == null)
            {
                Debug.LogWarning($"[CardDeckManager] 卡组槽位为空: {deckSlotIndex}");
                return false;
            }

            var dealUid = card.Uid;
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
                    CardManagerSingleton.Instance.Release(removed, "Deck.DealRollbackNoAnchor");
                }
                else
                {
                    CardDeckTween.MoveRippleAsync(rollbackRipple, layoutSettings.moveDuration).Forget();
                }

                ReportDealTrace(dealUid, groundSlot, placeable: true, ok: false, rollback: true);
                return false;
            }

            var cardManager = CardManagerSingleton.Instance;
            cardManager.SetDisplayMode(removed, CardDisplayMode.GroundCardMode);
            if (!field.RequestPlaceCard(groundSlot, removed, skipBusyGuard))
            {
                Debug.LogWarning(
                    $"[CardDeckManager] 场地拒收发牌 uid={removed.Uid} slot={groundSlot}，回滚入组。");
                if (!_slotContainer.TryInsertAt(deckSlotIndex, removed, out var rollbackRipple))
                {
                    CardManagerSingleton.Instance.Release(removed, "Deck.DealRollbackPlaceDenied");
                }
                else
                {
                    CardManagerSingleton.Instance.SetDisplayMode(removed, CardDisplayMode.CardDeckMode);
                    CardDeckTween.MoveRippleAsync(rollbackRipple, layoutSettings.moveDuration).Forget();
                }

                ReportDealTrace(dealUid, groundSlot, placeable: true, ok: false, rollback: true);
                return false;
            }

            CardDeckTween.MoveRippleAsync(rippleMoves, layoutSettings.moveDuration).Forget();
            CardDeckTween.MoveToWorld(
                removed.Transform,
                groundAnchor.position,
                layoutSettings.moveDuration,
                onComplete: () => cardManager.RefreshDisplayMode(removed));
            ReportDealTrace(dealUid, groundSlot, placeable: true, ok: true, rollback: false);
            return true;
        }

        private static void ReportDealTrace(int uid, int slot, bool placeable, bool ok, bool rollback)
        {
            try
            {
                FlowFieldTraceSink.DealResult?.Invoke(
                    uid,
                    slot,
                    placeable,
                    ok,
                    rollback,
                    "TryDealCard");
            }
            catch
            {
                // ignore
            }
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

            var field = ResolveFieldManager();
            if (field != null
                && (card.DisplayMode == CardDisplayMode.GroundCardMode
                    || field.TryGetSlotOf(card.Uid, out _))
                && field.TryGetSlotOf(card.Uid, out _))
            {
                LaunchReturnFieldCardToDeck(card, slotIndex);
                return;
            }

            _isBusy = true;
            try
            {
                CardManagerSingleton.Instance.SetDisplayMode(card, CardDisplayMode.CardDeckMode);

                var addAnchor = GetAddAnchor(Mathf.Clamp(slotIndex, 0, Mathf.Max(0, layoutSettings.maxSlots - 1)));
                if (addAnchor != null && card.Transform != null)
                {
                    card.Transform.position = addAnchor.position;
                }

                if (!_slotContainer.TryInsertAt(slotIndex, card, out var rippleMoves))
                {
                    Debug.LogWarning("[CardDeckManager] 插入卡牌失败（卡牌无效或索引非法）。");
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

        private bool TryFindDeckSlotByUid(int uid, out int deckSlotIndex)
        {
            deckSlotIndex = -1;
            if (uid <= 0 || _slotContainer == null)
            {
                return false;
            }

            var count = _slotContainer.Count;
            for (var i = 0; i < count; i++)
            {
                if (_slotContainer.TryGetCardAt(i, out var card) && card != null && card.Uid == uid)
                {
                    deckSlotIndex = i;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Entry 目标：前 maxSlots 张各占锚点；超出叠在末锚点并加 Z 步进。
        /// </summary>
        private Vector3? ResolveEntryTargetPosition(int cardIndex)
        {
            var maxSlots = Mathf.Max(1, layoutSettings.maxSlots);
            var layoutIndex = Mathf.Min(cardIndex, maxSlots - 1);
            var anchor = GetDeckAnchor(layoutIndex);
            if (anchor == null)
            {
                return null;
            }

            var position = anchor.position;
            if (cardIndex > maxSlots - 1)
            {
                var overflowDepth = cardIndex - (maxSlots - 1);
                position.z += overflowDepth * layoutSettings.overflowStackZStep;
            }

            return position;
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
