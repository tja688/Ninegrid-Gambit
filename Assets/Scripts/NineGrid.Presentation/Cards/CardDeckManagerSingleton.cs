using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards.Convergence;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 牌组管理器单例：编排卡组 Standby / Entry / InGame 三模式，以及向 Ground 发牌。
    /// 净土域：内部布局黑盒；仅暴露 C 阶段 Evict/Admit（速度恒 0）。
    /// V6 compat shell — Cards/Hand 解析优先 <see cref="CardEntityLifecycleHook"/>。
    /// </summary>
    public sealed class CardDeckManagerSingleton : MonoBehaviour, IHandoffEndpoint
    {
        private const string MainSortingLayerName = "Main";
        private const string RecycleBackgroundSortingLayerName = "BG";

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
        private bool _recycleBackgroundSuppressed;
        private ManagedCard _hoveredDeckCard;
        private readonly HashSet<int> _returnInFlightUids = new();
        private readonly Dictionary<int, UniTaskCompletionSource> _returnSettledWaiters = new();

        /// <summary>入组索引：随机落点（非空时排除最左 slot 0）。</summary>
        public const int RandomInsertIndex = -1;

        public CardDeckMode CurrentMode { get; private set; } = CardDeckMode.Standby;

        public int DeckCount => _slotContainer?.Count ?? _pendingEntryCards.Count;

        public bool IsBusy => _isBusy;

        public CardDeckLayoutSettings LayoutSettings => layoutSettings;

        /// <summary>
        /// 场地回库途中（fieldExit → AddAnchor → ripple 完成前）。Sync 不得征用。
        /// 视图已释放时自动清登记，避免 uid 粘住。
        /// </summary>
        public bool IsReturnInFlight(int uid)
        {
            if (uid <= 0 || !_returnInFlightUids.Contains(uid))
            {
                return false;
            }

            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            if (cardManager != null && !cardManager.TryGet(uid, out _))
            {
                ClearReturnInFlight(uid, "deckReturn.abort");
                return false;
            }

            return true;
        }

        /// <summary>等待单卡回库（fieldExit → insert → ripple）结束；未在途立即返回。</summary>
        public UniTask WaitReturnSettledAsync(int uid, CancellationToken cancellationToken = default)
        {
            if (uid <= 0 || !IsReturnInFlight(uid))
            {
                return UniTask.CompletedTask;
            }

            if (!_returnSettledWaiters.TryGetValue(uid, out var tcs))
            {
                tcs = new UniTaskCompletionSource();
                _returnSettledWaiters[uid] = tcs;
            }

            // End 可能与注册竞态：再确认一次，避免挂死。
            if (!IsReturnInFlight(uid))
            {
                CompleteReturnSettledWaiters(uid);
                return UniTask.CompletedTask;
            }

            return tcs.Task.AttachExternalCancellation(cancellationToken);
        }

        /// <summary>等待多卡回库全部结束。</summary>
        public UniTask WaitReturnsSettledAsync(
            IReadOnlyList<int> uids,
            CancellationToken cancellationToken = default)
        {
            if (uids == null || uids.Count == 0)
            {
                return UniTask.CompletedTask;
            }

            var pending = new List<UniTask>(uids.Count);
            for (var i = 0; i < uids.Count; i++)
            {
                var uid = uids[i];
                if (uid <= 0 || !IsReturnInFlight(uid))
                {
                    continue;
                }

                pending.Add(WaitReturnSettledAsync(uid, cancellationToken));
            }

            if (pending.Count == 0)
            {
                return UniTask.CompletedTask;
            }

            return UniTask.WhenAll(pending);
        }

        private void BeginReturnInFlight(int uid, string phase)
        {
            if (uid <= 0)
            {
                return;
            }

            _returnInFlightUids.Add(uid);
            TraceDeckReturn(uid, phase);
        }

        private void EndReturnInFlight(int uid, string phase)
        {
            ClearReturnInFlight(uid, phase);
        }

        private void ClearReturnInFlight(int uid, string phase)
        {
            if (uid <= 0)
            {
                return;
            }

            if (_returnInFlightUids.Remove(uid))
            {
                TraceDeckReturn(uid, phase);
            }

            CompleteReturnSettledWaiters(uid);
        }

        private void CompleteReturnSettledWaiters(int uid)
        {
            if (!_returnSettledWaiters.TryGetValue(uid, out var tcs))
            {
                return;
            }

            _returnSettledWaiters.Remove(uid);
            tcs.TrySetResult();
        }

        private static void TraceDeckReturn(int uid, string phase, params string[] extraPairs)
        {
            ChoreoTraceSink.SafeExploreTrace(uid, phase, -1, -1, extraPairs);
        }

        /// <summary>
        /// 净土域 C 阶段交接：速度恒填 0。域级快照；卡级见 <see cref="EvictCard"/>。
        /// </summary>
        public HandoffState Evict() => HandoffState.AtRest(Vector3.zero);

        /// <summary>净土域 C 阶段：承接位置，忽略速度。</summary>
        public void Admit(in HandoffState state)
        {
            // 牌库布局黑盒内部不动；C 阶段仅接受接口契约。
        }

        /// <summary>单卡离开牌库域：C 阶段速度恒 0。</summary>
        public HandoffState EvictCard(ManagedCard card)
        {
            if (card?.Transform == null)
            {
                return HandoffState.AtRest(Vector3.zero);
            }

            return HandoffState.AtRest(card.Transform.localPosition);
        }

        /// <summary>单卡进入牌库域：C 阶段忽略速度，仅对齐局部位姿。</summary>
        public void AdmitCard(ManagedCard card, in HandoffState state)
        {
            if (card?.Transform == null)
            {
                return;
            }

            card.Transform.localPosition = state.LocalPosition;
        }

        private void Awake()
        {
            _slotContainer = new CardDeckSlotContainer(layoutSettings);
            ResolveSceneReferences();
            CacheAnchors();
            InitializeLayoutOrigin();
        }

        private void OnDestroy()
        {
        }

        private void Update()
        {
            TickDeckHover();
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
                CardEntityLifecycleHook.CardsOrNull()?.SetDisplayMode(card, CardDisplayMode.CardDeckMode);
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
        /// <param name="awaitMove">为 true 时等到本张飞牌动态就位结束；批量交错发牌时应传 false，由调用方 WaitAllSettledAsync。</param>
        public async UniTask<bool> DealCardByUidAsync(
            int uid,
            int groundSlot,
            ManagedCard ensureCard = null,
            bool skipBusyGuard = false,
            bool awaitMove = true,
            CancellationToken cancellationToken = default)
        {
            var (ok, handle) = await DealCardByUidWithFlightAsync(
                uid,
                groundSlot,
                ensureCard,
                skipBusyGuard,
                flightContext: null,
                cancellationToken);
            if (ok && awaitMove && handle != null)
            {
                await handle.WaitSettleAsync(cancellationToken);
            }

            return ok;
        }

        /// <summary>
        /// 发牌并返回飞牌句柄，供批量交错起飞 + WaitAllSettledAsync 聚合等待。
        /// </summary>
        public async UniTask<(bool ok, DealFlightHandle handle)> DealCardByUidWithFlightAsync(
            int uid,
            int groundSlot,
            ManagedCard ensureCard = null,
            bool skipBusyGuard = false,
            DealFlightContext? flightContext = null,
            CancellationToken cancellationToken = default)
        {
            if (!EnsureInGameForDeal())
            {
                return (false, null);
            }

            // 回库途中禁止抢跑发牌（空堆同 UID 时尤其关键）。
            if (IsReturnInFlight(uid))
            {
                await WaitReturnSettledAsync(uid, cancellationToken);
            }

            if (!TryFindDeckSlotByUid(uid, out var deckSlot))
            {
                if (ensureCard == null || ensureCard.Uid != uid)
                {
                    Debug.LogWarning(
                        $"[CardDeckManager] 卡组中未找到 Uid={uid}，且无 ensureCard，无法就位到格 {groundSlot}。");
                    return (false, null);
                }

                var field = ResolveFieldManager();
                if (field != null && field.TryGetSlotOf(uid, out var occupiedSlot))
                {
                    if (occupiedSlot == groundSlot)
                    {
                        return (true, null);
                    }

                    // 错位：迁到目标格，避免「已在场地」假失败留下 Core/Pres 分叉。
                    if (field.IsPlaceable(groundSlot)
                        && field.RequestRelocateOccupancy(
                            uid,
                            groundSlot,
                            snapToAnchor: true,
                            skipBusyGuard: true))
                    {
                        Debug.LogWarning(
                            $"[CardDeckManager] Uid={uid} 已在场地 slot={occupiedSlot}，迁至格 {groundSlot}。");
                        return (true, null);
                    }

                    Debug.LogWarning(
                        $"[CardDeckManager] Uid={uid} 已在场地 slot={occupiedSlot}，无法就位到格 {groundSlot}。");
                    return (false, null);
                }

                await AddCardAtInternalAsync(RandomInsertIndex, ensureCard, cancellationToken);
                if (!TryFindDeckSlotByUid(uid, out deckSlot))
                {
                    Debug.LogWarning(
                        $"[CardDeckManager] 入组后仍未找到 Uid={uid}，无法就位到格 {groundSlot}。");
                    return (false, null);
                }
            }

            if (!TryDealCard(
                    deckSlot,
                    groundSlot,
                    skipBusyGuard,
                    flightContext,
                    out var handle))
            {
                return (false, null);
            }

            return (true, handle);
        }

        /// <summary>
        /// 卡组槽中是否已有指定 Uid。
        /// </summary>
        public bool ContainsUid(int uid)
        {
            return TryFindDeckSlotByUid(uid, out _);
        }

        /// <summary>
        /// 解析卡组域权威 sortingOrder（左高右低）；供 FlightSortingChannel 掉回目标序时委托。
        /// </summary>
        internal bool TryResolveSortingOrder(ManagedCard card, out int sortingOrder)
        {
            sortingOrder = 0;
            if (card == null || _slotContainer == null)
            {
                return false;
            }

            if (!TryFindDeckSlotByUid(card.Uid, out var slotIndex))
            {
                return false;
            }

            sortingOrder = _slotContainer.ComputeSortingOrder(slotIndex);
            return true;
        }

        /// <summary>
        /// 刷新卡组 sorting（含 SortingLayer Propagate）；供 CardManager 在 CardDeckMode 切换时委托。
        /// 对齐手牌 EnsureHandSorting：禁止入槽后再被 DisplayMode 默认序 -30 打回。
        /// </summary>
        internal void EnsureDeckSorting(ManagedCard card)
        {
            if (card == null || _slotContainer == null)
            {
                return;
            }

            if (TryFindDeckSlotByUid(card.Uid, out var slotIndex))
            {
                _slotContainer.ApplySortingOrder(card, slotIndex);
            }
            else
            {
                _slotContainer.ApplySortingOrders();
            }
        }

        /// <summary>
        /// 回收区 UI 激活期间：卡组卡临时下沉到 BG Sorting Layer，使 CardRecycleNotice 压住卡组，
        /// 同时手牌/遗物拖拽仍留在 Main 压住 Notice。关闭时恢复 Main。
        /// </summary>
        public void SetRecycleBackgroundSuppressed(bool active)
        {
            if (_recycleBackgroundSuppressed == active)
            {
                return;
            }

            _recycleBackgroundSuppressed = active;
            if (_slotContainer == null)
            {
                return;
            }

            _slotContainer.SetSortingLayerName(
                active ? RecycleBackgroundSortingLayerName : MainSortingLayerName);
            _slotContainer.ApplySortingOrders();
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
        /// 卸下 InGame 卡组槽内全部视图；顺带中止回库 in-flight，避免 uid 粘住。
        /// 不改 Mode、不 Release（由调用方决定）。清关选房用。
        /// </summary>
        public List<ManagedCard> DetachAllInGameCards()
        {
            if (_returnInFlightUids.Count > 0)
            {
                var pending = new List<int>(_returnInFlightUids);
                _returnInFlightUids.Clear();
                for (var i = 0; i < pending.Count; i++)
                {
                    CompleteReturnSettledWaiters(pending[i]);
                }
            }

            var detached = new List<ManagedCard>();
            if (_slotContainer == null)
            {
                return detached;
            }

            while (_slotContainer.Count > 0)
            {
                if (!_slotContainer.TryRemoveAt(0, out var card, out _) || card == null)
                {
                    break;
                }

                if (card.Transform != null)
                {
                    CardDeckTween.KillMotion(card.Transform, "Deck.DetachAll", card.Uid);
                }

                detached.Add(card);
            }

            _isBusy = false;
            return detached;
        }

        /// <summary>
        /// 清卡组槽并回到 Standby，供局内重新开局前复位。不销毁卡视图（由 CardManager 统一释放）。
        /// </summary>
        public void ResetToStandby()
        {
            _pendingEntryCards.Clear();
            if (_returnInFlightUids.Count > 0)
            {
                var pending = new List<int>(_returnInFlightUids);
                _returnInFlightUids.Clear();
                for (var i = 0; i < pending.Count; i++)
                {
                    CompleteReturnSettledWaiters(pending[i]);
                }
            }
            else
            {
                _returnSettledWaiters.Clear();
            }

            if (_recycleBackgroundSuppressed)
            {
                _recycleBackgroundSuppressed = false;
                _slotContainer?.SetSortingLayerName(MainSortingLayerName);
            }

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
        /// 从遗物/技能锚点飞入卡组槽位（仅 InGame）：起点缩小 + 位移 + ScaleAppear。
        /// <paramref name="originAnchor"/> 为 null 时改经 <c>CardDeckAddAnchors</c> 入组（局内洗入 / 离开机关等须走此路径）。
        /// </summary>
        public UniTask<bool> AddCardAtFromOriginAsync(
            int slotIndex,
            ManagedCard card,
            Transform originAnchor,
            CancellationToken cancellationToken = default)
        {
            return AddCardAtFromOriginInternalAsync(slotIndex, card, originAnchor, cancellationToken);
        }

        /// <summary>
        /// 清理 Ground 上已发卡牌并释放槽位占用（测试/重置用）。委托 GroundFieldView。
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
        /// 开局/遗物锚点发牌：卡组 withdraw 或 Spawn 后，从 origin 飞入手牌（仅 InGame）。
        /// </summary>
        public async UniTask<bool> DealCardToHandAsync(
            int uid,
            string defId,
            Transform originAnchor,
            ManagedCard ensureCard = null,
            bool skipBusyGuard = false,
            CancellationToken cancellationToken = default)
        {
            if (!EnsureInGameForDeal())
            {
                return false;
            }

            var handManager = CardEntityLifecycleHook.HandOrNull();
            if (handManager == null)
            {
                Debug.LogWarning($"[CardDeckManager] DealCardToHand uid={uid} 失败：无 CardHandManager。");
                return false;
            }

            if (!skipBusyGuard && !handManager.CanAcceptCard)
            {
                Debug.LogWarning($"[CardDeckManager] DealCardToHand uid={uid} 跳过：手牌已满。");
                return false;
            }

            if (skipBusyGuard && handManager.HandCount >= handManager.MaxHandSlots)
            {
                Debug.LogWarning($"[CardDeckManager] DealCardToHand uid={uid} 跳过：手牌已满。");
                return false;
            }

            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            ManagedCard card = ensureCard != null && ensureCard.Uid == uid ? ensureCard : null;
            if (card == null)
            {
                cardManager?.TryGet(uid, out card);
            }

            if (TryFindDeckSlotByUid(uid, out var deckSlot))
            {
                if (!_slotContainer.TryRemoveAt(deckSlot, out card, out var rippleMoves))
                {
                    Debug.LogWarning($"[CardDeckManager] DealCardToHand uid={uid} 失败：卡组槽移除失败。");
                    return false;
                }

                await CardDeckTween.MoveRippleAsync(rippleMoves, layoutSettings.moveDuration, cancellationToken);
            }
            else if (card == null && cardManager != null)
            {
                card = cardManager.SpawnView(
                    uid,
                    defId,
                    initialMode: CardDisplayMode.GroundCardMode,
                    kind: CardPresentationKindResolver.FromDefId(defId));
            }

            if (card == null || card.Transform == null)
            {
                Debug.LogWarning($"[CardDeckManager] DealCardToHand uid={uid} 失败：无可用视图。");
                return false;
            }

            var origin = originAnchor != null ? originAnchor : GetDeckAnchor(0);
            if (origin != null)
            {
                card.Transform.position = origin.position;
            }

            var finalScale = card.Transform.localScale;
            if (finalScale.sqrMagnitude <= 0.0001f)
            {
                finalScale = Vector3.one;
            }

            card.Transform.localScale = Vector3.zero;

            cardManager?.SetDisplayMode(card, CardDisplayMode.GroundCardMode);
            CardOpacityUtility.ResetAlpha(card);

            var moveDuration = layoutSettings != null ? layoutSettings.moveDuration : 0.2f;
            var scaleTask = CardDeckTween.ScaleAppearAsync(
                card.Transform,
                finalScale,
                moveDuration,
                cancellationToken);
            var ok = await handManager.PullFromGroundAsync(
                card,
                skipBusyGuard: skipBusyGuard,
                cancellationToken: cancellationToken);
            await scaleTask;
            if (ok)
            {
                try
                {
                    FlowFieldTraceSink.HandLifecycle?.Invoke(
                        card.Uid,
                        "acquire",
                        true,
                        "OpeningDeal");
                }
                catch
                {
                    // ignore
                }
            }

            return ok;
        }

        /// <summary>
        /// 发牌视觉起点兜底：卡组最左侧槽位锚点。
        /// </summary>
        public bool TryGetDefaultDealOrigin(out Transform anchor)
        {
            anchor = GetDeckAnchor(0);
            return anchor != null;
        }

        /// <summary>
        /// 将卡牌退回牌组（探求失败回滚，仅 InGame）。发射后不管：垂直上飞离画后经 AddAnchors 随机 ripple 入组（非空不进最左）。
        /// </summary>
        public bool TryReturnCardToDeckFront(ManagedCard card, out IReadOnlyList<CardDeckRippleMove> rippleMoves)
        {
            rippleMoves = Array.Empty<CardDeckRippleMove>();
            return LaunchReturnFieldCardToDeck(card);
        }

        /// <summary>
        /// 场地卡垂直上飞离画后插入卡组（发射后不管，可与旋转/换位并行）。
        /// <paramref name="insertIndex"/> 为 <see cref="RandomInsertIndex"/> 时随机落点；显式索引在非空时不得为最左 slot 0。
        /// </summary>
        public bool LaunchReturnFieldCardToDeck(ManagedCard card, int insertIndex = RandomInsertIndex)
        {
            if (!EnsureInGameForDeal() || card == null || card.Transform == null)
            {
                return false;
            }

            if (ContainsUid(card.Uid))
            {
                if (card.DisplayMode == CardDisplayMode.GroundCardMode)
                {
                    TryDetachByUid(card.Uid, out _);
                }
                else
                {
                    return true;
                }
            }

            if (!TryClaimCardForDeck(card, nameof(LaunchReturnFieldCardToDeck)))
            {
                return false;
            }

            var field = ResolveFieldManager();
            if (field != null && field.TryGetSlotOf(card.Uid, out var slot))
            {
                field.ClearSlotOccupancy(slot, skipBusyGuard: true);
            }

            BeginReturnInFlight(card.Uid, "deckReturn.claim");
            SlotFrameConvergence.SanitizeForSanctuary(card, "Deck.Return.Sanitize");
            CardEntityLifecycleHook.CardsOrNull()?.SetDisplayMode(card, CardDisplayMode.CardDeckMode);

            var fieldLayout = field?.LayoutSettings;
            var exitY = fieldLayout != null ? fieldLayout.fieldExitYThreshold : 7f;
            var exitDuration = fieldLayout != null ? fieldLayout.fieldExitDuration : 0.35f;

            TraceDeckReturn(
                card.Uid,
                "deckReturn.fieldExit",
                "exitY",
                exitY.ToString("0.##"),
                "fromX",
                card.Transform.position.x.ToString("0.##"),
                "fromY",
                card.Transform.position.y.ToString("0.##"));

            CardDeckTween.LaunchFieldExitThenDeckInsert(
                card.Transform,
                exitY,
                exitDuration,
                () => CompleteFieldReturnDeckInsert(card, insertIndex),
                uid: card.Uid);

            return true;
        }

        private void CompleteFieldReturnDeckInsert(ManagedCard card, int requestedIndex)
        {
            if (card == null || card.Transform == null || CurrentMode != CardDeckMode.InGame)
            {
                if (card != null)
                {
                    EndReturnInFlight(card.Uid, "deckReturn.abort");
                }

                return;
            }

            // Settlement / ResetToStandby 已清 _returnInFlightUids 后再 Kill 场离 tween 时，
            // OnKill→CompleteOnce 不得再 Insert 把牌塞回卡组。
            if (!_returnInFlightUids.Contains(card.Uid))
            {
                return;
            }

            if (ContainsUid(card.Uid))
            {
                EndReturnInFlight(card.Uid, "deckReturn.rippleEnd");
                return;
            }

            if (!TryClaimCardForDeck(card, nameof(CompleteFieldReturnDeckInsert)))
            {
                EndReturnInFlight(card.Uid, "deckReturn.abort");
                return;
            }

            CardEntityLifecycleHook.CardsOrNull()?.SetDisplayMode(card, CardDisplayMode.CardDeckMode);
            InsertViaAddAnchorAsync(requestedIndex, card, CancellationToken.None).Forget();
        }

        /// <summary>
        /// 解析入组槽位：随机时非空排除 index 0；显式 0 在非空时抬到 1；牌组空时唯一合法为 0。
        /// </summary>
        private int ResolveDeckInsertIndex(int requestedIndex)
        {
            var count = _slotContainer?.Count ?? 0;
            if (count == 0)
            {
                return 0;
            }

            if (requestedIndex < 0)
            {
                return UnityEngine.Random.Range(1, count + 1);
            }

            if (requestedIndex <= 0)
            {
                return 1;
            }

            return requestedIndex;
        }

        /// <summary>
        /// 直接入牌组第二段：CardDeckAddAnchors 落点 → TryInsertAt → ripple 归位。
        /// </summary>
        private async UniTask<bool> InsertViaAddAnchorAsync(
            int requestedIndex,
            ManagedCard card,
            CancellationToken cancellationToken)
        {
            if (card == null || _slotContainer == null)
            {
                if (card != null)
                {
                    EndReturnInFlight(card.Uid, "deckReturn.abort");
                }

                return false;
            }

            var returning = _returnInFlightUids.Contains(card.Uid);
            try
            {
                var slotIndex = ResolveDeckInsertIndex(requestedIndex);
                var clampedSlot = Mathf.Clamp(slotIndex, 0, Mathf.Max(0, layoutSettings.maxSlots - 1));
                var addAnchor = GetAddAnchor(clampedSlot);
                if (addAnchor != null && card.Transform != null)
                {
                    card.Transform.position = addAnchor.position;
                }

                if (returning)
                {
                    TraceDeckReturn(
                        card.Uid,
                        "deckReturn.addAnchor",
                        "slot",
                        slotIndex.ToString(),
                        "x",
                        (addAnchor != null ? addAnchor.position.x : 0f).ToString("0.##"),
                        "y",
                        (addAnchor != null ? addAnchor.position.y : 0f).ToString("0.##"));
                }

                if (!_slotContainer.TryInsertAt(slotIndex, card, out var rippleMoves))
                {
                    Debug.LogWarning("[CardDeckManager] 插入卡牌失败（卡牌无效或索引非法）。");
                    if (returning)
                    {
                        EndReturnInFlight(card.Uid, "deckReturn.abort");
                    }

                    return false;
                }

                await CardDeckTween.MoveRippleAsync(rippleMoves, layoutSettings.moveDuration, cancellationToken);
                if (returning)
                {
                    EndReturnInFlight(card.Uid, "deckReturn.rippleEnd");
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                if (returning)
                {
                    EndReturnInFlight(card.Uid, "deckReturn.abort");
                }

                throw;
            }
            catch
            {
                if (returning)
                {
                    EndReturnInFlight(card.Uid, "deckReturn.abort");
                }

                throw;
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

        private bool TryDealCard(
            int deckSlotIndex,
            int groundSlot,
            bool skipBusyGuard,
            DealFlightContext? flightContext,
            out DealFlightHandle flightHandle)
        {
            flightHandle = null;
            if (!EnsureInGameForDeal())
            {
                return false;
            }

            var field = ResolveFieldManager();
            if (field == null)
            {
                Debug.LogWarning("[CardDeckManager] 未找到 GroundFieldView。");
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
                ReportDealTrace(uid: 0, groundSlot, placeable: false, ok: false, rollback: false, reason: "placeDenied");
                return false;
            }

            if (!_slotContainer.TryGetCardAt(deckSlotIndex, out var card) || card == null)
            {
                Debug.LogWarning($"[CardDeckManager] 卡组槽位为空: {deckSlotIndex}");
                return false;
            }

            var dealUid = card.Uid;
            ReportDealAttempt(dealUid, groundSlot);

            // 变更前校验：无 View 的僵尸句柄不得入场，避免 RequestPlaceCard 后读 Transform NRE。
            if (card.View == null || card.Transform == null)
            {
                Debug.LogWarning(
                    $"[CardDeckManager] 发牌中止：Uid={dealUid} View/Transform 为空（nullView）。");
                ReportDealTrace(dealUid, groundSlot, placeable: true, ok: false, rollback: false, reason: "nullView");
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
                    CardEntityLifecycleHook.CardsOrNull()?.Release(removed, "Deck.DealRollbackNoAnchor");
                }
                else
                {
                    CardDeckTween.MoveRippleAsync(rollbackRipple, layoutSettings.moveDuration).Forget();
                }

                ReportDealTrace(dealUid, groundSlot, placeable: true, ok: false, rollback: true, reason: "noAnchor");
                return false;
            }

            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            cardManager?.SetDisplayMode(removed, CardDisplayMode.GroundCardMode);
            if (!field.RequestPlaceCard(groundSlot, removed, skipBusyGuard))
            {
                Debug.LogWarning(
                    $"[CardDeckManager] 场地拒收发牌 uid={removed.Uid} slot={groundSlot}，回滚入组。");
                if (!_slotContainer.TryInsertAt(deckSlotIndex, removed, out var rollbackRipple))
                {
                    CardEntityLifecycleHook.CardsOrNull()?.Release(removed, "Deck.DealRollbackPlaceDenied");
                }
                else
                {
                    CardEntityLifecycleHook.CardsOrNull()?.SetDisplayMode(removed, CardDisplayMode.CardDeckMode);
                    CardDeckTween.MoveRippleAsync(rollbackRipple, layoutSettings.moveDuration).Forget();
                }

                ReportDealTrace(dealUid, groundSlot, placeable: true, ok: false, rollback: true, reason: "placeDenied");
                return false;
            }

            // 占格后二次校验：若 View 在登记后失效，清占格并回滚，禁止抛 NRE。
            if (removed.View == null || removed.Transform == null)
            {
                Debug.LogWarning(
                    $"[CardDeckManager] 发牌回滚：Uid={dealUid} 占格后 View 仍为空，清占格并回滚入组。");
                field.ClearSlotOccupancy(groundSlot, skipBusyGuard: true);
                if (!_slotContainer.TryInsertAt(deckSlotIndex, removed, out var rollbackRipple))
                {
                    CardEntityLifecycleHook.CardsOrNull()?.Release(removed, "Deck.DealRollbackNullViewAfterPlace");
                }
                else
                {
                    CardEntityLifecycleHook.CardsOrNull()?.SetDisplayMode(removed, CardDisplayMode.CardDeckMode);
                    CardDeckTween.MoveRippleAsync(rollbackRipple, layoutSettings.moveDuration).Forget();
                }

                ReportDealTrace(dealUid, groundSlot, placeable: true, ok: false, rollback: true, reason: "nullView");
                return false;
            }

            CardDeckTween.MoveRippleAsync(rippleMoves, layoutSettings.moveDuration).Forget();
            var launchPos = removed.Transform.position;
            var context = flightContext ?? BuildDefaultFlightContext(field);
            flightHandle = field.LaunchDrainDealFlight(removed, groundSlot, launchPos, context);
            if (flightHandle == null)
            {
                NineGrid.Cards.Convergence.SlotFrameConvergence.BeginDealFromLaunch(
                    removed,
                    launchPos,
                    groundAnchor.position,
                    layoutSettings.moveDuration);
            }

            ReportDealTrace(dealUid, groundSlot, placeable: true, ok: true, rollback: false, reason: string.Empty);
            return true;
        }

        private static DealFlightContext BuildDefaultFlightContext(GroundFieldView field)
        {
            return new DealFlightContext(
                field.IsFieldBusy,
                activeFlightCount: 1,
                pendingRotateSteps: 0);
        }

        private bool TryDealCard(int deckSlotIndex, int groundSlot, bool skipBusyGuard = false)
        {
            return TryDealCard(deckSlotIndex, groundSlot, skipBusyGuard, null, out _);
        }

        private static void ReportDealAttempt(int uid, int slot)
        {
            try
            {
                FlowFieldTraceSink.DealAttempt?.Invoke(uid, slot, "TryDealCard");
            }
            catch
            {
                // ignore
            }
        }

        private static void ReportDealTrace(
            int uid,
            int slot,
            bool placeable,
            bool ok,
            bool rollback,
            string reason)
        {
            try
            {
                FlowFieldTraceSink.DealResult?.Invoke(
                    uid,
                    slot,
                    placeable,
                    ok,
                    rollback,
                    "TryDealCard",
                    reason ?? string.Empty);
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
            await AddCardAtFromOriginInternalAsync(slotIndex, card, null, cancellationToken);
        }

        private async UniTask<bool> AddCardAtFromOriginInternalAsync(
            int slotIndex,
            ManagedCard card,
            Transform originAnchor,
            CancellationToken cancellationToken)
        {
            if (CurrentMode != CardDeckMode.InGame)
            {
                Debug.LogWarning("[CardDeckManager] AddCardAt 仅在 InGame 模式可用。");
                return false;
            }

            if (_isBusy || card == null)
            {
                return false;
            }

            var field = ResolveFieldManager();
            if (field != null && field.TryGetSlotOf(card.Uid, out var staleSlot))
            {
                field.ClearSlotOccupancy(staleSlot, skipBusyGuard: true);
                if (card.DisplayMode == CardDisplayMode.GroundCardMode)
                {
                    if (!TryClaimCardForDeck(card, nameof(AddCardAtFromOriginInternalAsync)))
                    {
                        return false;
                    }

                    return LaunchReturnFieldCardToDeck(card, slotIndex);
                }

                try
                {
                    RegistryTraceSink.RecordSuspectGroundRelease?.Invoke(
                        card.Uid,
                        $"AddCardAt.staleOccupancy:{card.DisplayMode}",
                        nameof(AddCardAtFromOriginInternalAsync),
                        staleSlot);
                }
                catch
                {
                    // ignore
                }
            }

            if (!TryClaimCardForDeck(card, nameof(AddCardAtFromOriginInternalAsync)))
            {
                return false;
            }

            _isBusy = true;
            try
            {
                CardEntityLifecycleHook.CardsOrNull()?.SetDisplayMode(card, CardDisplayMode.CardDeckMode);

                if (originAnchor == null)
                {
                    return await InsertViaAddAnchorAsync(slotIndex, card, cancellationToken);
                }

                var resolvedIndex = ResolveDeckInsertIndex(slotIndex);
                var clampedSlot = Mathf.Clamp(resolvedIndex, 0, Mathf.Max(0, layoutSettings.maxSlots - 1));
                var deckAnchor = GetDeckAnchor(clampedSlot);
                if (originAnchor != null && card.Transform != null)
                {
                    card.Transform.position = originAnchor.position;
                }

                var finalScale = card.Transform != null ? card.Transform.localScale : Vector3.one;
                if (finalScale.sqrMagnitude <= 0.0001f)
                {
                    finalScale = Vector3.one;
                }

                if (card.Transform != null)
                {
                    card.Transform.localScale = Vector3.zero;
                }

                if (!_slotContainer.TryInsertAt(resolvedIndex, card, out var originRippleMoves))
                {
                    Debug.LogWarning("[CardDeckManager] 插入卡牌失败（卡牌无效或索引非法）。");
                    return false;
                }

                var moveDuration = layoutSettings.moveDuration;
                if (deckAnchor != null && card.Transform != null)
                {
                    CardDeckTween.MoveToWorld(card.Transform, deckAnchor.position, moveDuration);
                }

                var scaleTask = card.Transform != null
                    ? CardDeckTween.ScaleAppearAsync(card.Transform, finalScale, moveDuration, cancellationToken)
                    : UniTask.CompletedTask;
                var rippleTask = CardDeckTween.MoveRippleAsync(originRippleMoves, moveDuration, cancellationToken);
                await UniTask.WhenAll(scaleTask, rippleTask);
                if (deckAnchor != null && card.Transform != null)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(moveDuration), cancellationToken: cancellationToken);
                }

                return true;
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

        private static GroundFieldView ResolveFieldManager()
        {
            return GroundFieldGeometryHook.FieldOrNull();
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

        /// <summary>
        /// 入组/回堆前校验：禁止手牌与 Core ItemSlots 的视图被卡组槽吸纳。
        /// </summary>
        private bool TryClaimCardForDeck(ManagedCard card, string caller)
        {
            if (card == null)
            {
                return false;
            }

            if (ContainsUid(card.Uid))
            {
                return true;
            }

            if (IsHandHeldPresentation(card))
            {
                Debug.LogWarning(
                    $"[CardDeckManager] {caller} 拒绝：uid={card.Uid} 仍在手牌/拖拽（mode={card.DisplayMode}）。");
                return false;
            }

            if (CardZoneOwnershipHook.CoreSaysItemSlots(card.Uid))
            {
                Debug.LogWarning(
                    $"[CardDeckManager] {caller} 拒绝：uid={card.Uid} Core=ItemSlots。");
                return false;
            }

            return true;
        }

        private static bool IsHandHeldPresentation(ManagedCard card)
        {
            if (card == null)
            {
                return false;
            }

            var hand = CardEntityLifecycleHook.HandOrNull();
            if (hand != null && hand.ContainsUid(card.Uid))
            {
                return true;
            }

            return card.DisplayMode == CardDisplayMode.HandCardMode
                   || card.DisplayMode == CardDisplayMode.DragCardMode;
        }

        private void TickDeckHover()
        {
            if (CurrentMode != CardDeckMode.InGame || _isBusy || _slotContainer == null)
            {
                return;
            }

            if (_hoveredDeckCard != null && (!ContainsUid(_hoveredDeckCard.Uid) || !IsLiveDeckCard(_hoveredDeckCard)))
            {
                _hoveredDeckCard = null;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            if (!WorldPointerUtility.TryGetPointerScreen(out var pointerScreen))
            {
                return;
            }

            var pointerWorld = ScreenToWorldOnPlane(
                pointerScreen,
                camera,
                ResolveDeckHoverPlaneZ());
            var resolved = ResolveDeckHoverTarget(pointerWorld.x, pointerWorld.y);
            ApplyDeckHoverTarget(resolved);
        }

        private void ApplyDeckHoverTarget(ManagedCard card)
        {
            if (card != null && !IsLiveDeckCardAtSlotZero(card))
            {
                card = null;
            }

            if (_hoveredDeckCard == card)
            {
                return;
            }

            _hoveredDeckCard = card;
            if (card != null && string.IsNullOrEmpty(card.DefId))
            {
                _hoveredDeckCard = null;
                return;
            }

            if (card != null)
            {
                InteractionAudioCues.PulseCard(
                    InteractionAudioCues.DeckCardHover,
                    "CardDeckManagerSingleton.ApplyDeckHoverTarget",
                    card.DefId);
            }
        }

        private ManagedCard ResolveDeckHoverTarget(float pointerX, float pointerY)
        {
            if (!_slotContainer.TryGetCardAt(0, out var card) || card == null || !IsLiveDeckCard(card))
            {
                return null;
            }

            if (card.Transform == null)
            {
                return null;
            }

            var position = card.Transform.position;
            var halfSize = layoutSettings.deckHoverHitBoxSize * 0.5f;
            if (pointerX < position.x - halfSize.x || pointerX > position.x + halfSize.x
                || pointerY < position.y - halfSize.y || pointerY > position.y + halfSize.y)
            {
                return null;
            }

            return card;
        }

        private float ResolveDeckHoverPlaneZ()
        {
            var anchor = GetDeckAnchor(0);
            if (anchor != null)
            {
                return anchor.position.z;
            }

            return layoutSettings.layoutBaseZ;
        }

        private bool IsLiveDeckCardAtSlotZero(ManagedCard card)
        {
            if (!IsLiveDeckCard(card))
            {
                return false;
            }

            return _slotContainer.TryGetCardAt(0, out var slotZero) && slotZero == card;
        }

        private static bool IsLiveDeckCard(ManagedCard card)
        {
            if (card == null)
            {
                return false;
            }

            if (card.View == null)
            {
                return false;
            }

            return card.DisplayMode == CardDisplayMode.CardDeckMode;
        }

        private static Vector3 ScreenToWorldOnPlane(Vector3 screenPosition, Camera camera, float worldZ)
        {
            var point = camera.ScreenToWorldPoint(
                new Vector3(
                    screenPosition.x,
                    screenPosition.y,
                    camera.WorldToScreenPoint(new Vector3(0f, 0f, worldZ)).z));
            point.z = worldZ;
            return point;
        }
    }
}
