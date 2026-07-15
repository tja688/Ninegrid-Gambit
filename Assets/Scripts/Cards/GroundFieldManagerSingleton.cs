using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地卡管理器单例：9 格占用权威、放置申请、外圈旋转表演、方位查询与空槽点击。
    /// 格位位移由 L2 五次收敛编排（不写 world position 做动画）；基础交战编排已抽至 <see cref="FieldBattleManagerSingleton"/>。
    /// </summary>
    public sealed class GroundFieldManagerSingleton : MonoBehaviour
    {
        private static GroundFieldManagerSingleton _instance;

        [Header("Scene Anchors")]
        [Tooltip("场景 Anchors/GroundAnchors。留空时 Awake 按名称 GroundAnchors 查找。")]
        [SerializeField] private Transform groundAnchorsRoot;

        [Header("Layout")]
        [Tooltip("场地布局与动效参数。")]
        [SerializeField] private GroundFieldLayoutSettings layoutSettings = new();

        [Header("Deck Presentation")]
        [Tooltip("骷髅军团牌组专用表现管理器。留空时 Awake 在场景中查找 SkeletonDeckPresentationManager。")]
        [SerializeField] private SkeletonDeckPresentationManager skeletonDeckPresentation;

        private readonly int[] _uidBySlot = new int[GroundSlotTopology.MaxSlot + 1];
        private readonly Dictionary<int, int> _slotByUid = new();
        private readonly List<Transform> _groundAnchors = new();
        private readonly GroundSlotHitProxy[] _slotHitProxies = new GroundSlotHitProxy[GroundSlotTopology.MaxSlot + 1];
        private readonly PresentationClock _presentationClock = new();
        private BeatGrid _beatGrid;
        private LeaseArbiter _leaseArbiter;
        private GroundSlotDealFlightCoordinator _dealFlightCoordinator;
        private bool _isBusy;
        private CancellationTokenSource _fieldAnimCts;
        private bool _occupancyConflictSinceClear;

        public static GroundFieldManagerSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<GroundFieldManagerSingleton>();
                }

                return _instance;
            }
        }

        /// <summary>仅查找，不创建。审计/销毁期用。</summary>
        public static GroundFieldManagerSingleton TryGetInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            return FindFirstObjectByType<GroundFieldManagerSingleton>();
        }

        /// <summary>
        /// 场地自身忙碌，或交战管理器忙碌（保持原有 IsBusy 语义，供 hover/点击门禁使用）。
        /// </summary>
        public bool IsBusy
        {
            get
            {
                if (CombatHitSink.ChoiceOverlayActive || CombatHitSink.PresentationLocked)
                {
                    return true;
                }

                var battle = FieldBattleManagerSingleton.Instance;
                return _isBusy || (battle != null && battle.IsBusy);
            }
        }

        /// <summary>
        /// 仅场地自身忙碌（不含交战），供交战管理器判断是否可开打。
        /// </summary>
        public bool IsFieldBusy => _isBusy;

        public GroundFieldLayoutSettings LayoutSettings => layoutSettings;

        /// <summary>
        /// 骷髅合体表现：参与卡互撞重叠 → 闪白替换结果卡 → 场地离场入组。
        /// <paramref name="onFusionStarted"/> 在参与卡脱离场地格位后调用（用于触发补牌）。
        /// </summary>
        public async UniTask PresentSkeletonFusionAsync(
            SkeletonFusionPresentationRequest request,
            Func<CancellationToken, UniTask> onFusionStarted,
            CancellationToken cancellationToken = default)
        {
            ResolveSkeletonDeckPresentation();
            if (skeletonDeckPresentation == null)
            {
                Debug.LogWarning("[GroundFieldManager] 未装配 SkeletonDeckPresentationManager，跳过骷髅合体表现。");
                if (onFusionStarted != null)
                {
                    await onFusionStarted(cancellationToken);
                }

                return;
            }

            await skeletonDeckPresentation.PresentFusionAsync(
                request,
                onFusionStarted,
                cancellationToken);
        }

        public int ActiveDealFlightCount => _dealFlightCoordinator?.ActiveCount ?? 0;

        public event Action<int> EmptySlotClicked;

        /// <summary>
        /// 表现侧：非 Avatar 格上已无存活牌时抛出（不引用 Flow/Core；由局内管理器订阅并核对内核通关）。
        /// </summary>
        public event Action FieldMaybeClearSignal;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            ClearSlotTable();
            ResolveSceneReferences();
            CacheAnchors();
            EnsureSlotHitProxies();
            RefreshAllSlotHitColliders();
            _beatGrid = new BeatGrid(_presentationClock);
            _leaseArbiter = new LeaseArbiter(reason =>
                Debug.LogWarning("[GroundFieldManager] " + reason));
            _dealFlightCoordinator = new GroundSlotDealFlightCoordinator(this, this.GetCancellationTokenOnDestroy());
            ResolveSkeletonDeckPresentation();
        }

        private void OnDestroy()
        {
            CancelFieldAnimations();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public GroundFieldSnapshot GetSnapshot()
        {
            var slots = new GroundFieldSlotSnapshot[GroundSlotTopology.MaxSlot];
            var occupied = 0;
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                var uid = _uidBySlot[slot];
                var isEmpty = uid == 0;
                if (!isEmpty)
                {
                    occupied++;
                }

                slots[slot - 1] = new GroundFieldSlotSnapshot(
                    slot,
                    uid,
                    isEmpty,
                    GroundSlotTopology.IsAvatarReserved(slot));
            }

            return new GroundFieldSnapshot(slots, occupied);
        }

        public bool TryGetCardAt(int slot, out ManagedCard card)
        {
            card = null;
            if (!IsValidSlot(slot) || _uidBySlot[slot] == 0)
            {
                return false;
            }

            var uid = _uidBySlot[slot];
            if (CardManagerSingleton.Instance.TryGet(uid, out card))
            {
                return true;
            }

            CardPresentationProbe.RegistryMiss(
                uid,
                "Ground.TryGetCardAt",
                "slot=" + slot.ToString(CultureInfo.InvariantCulture));
            CardManagerSingleton.Instance?.AuditRegistryIntegrity("Ground.TryGetCardAt");
            return false;
        }

        public bool TryGetSlotOf(int uid, out int slot)
        {
            return _slotByUid.TryGetValue(uid, out slot);
        }

        /// <summary>
        /// 查找 uid 占用的场地格（含 _slotByUid/_uidBySlot 短暂不一致时的自愈扫描）。
        /// </summary>
        public bool TryFindOccupiedSlotForUid(int uid, out int slot)
        {
            slot = 0;
            if (uid <= 0)
            {
                return false;
            }

            if (TryGetSlotOf(uid, out slot))
            {
                return true;
            }

            for (var s = GroundSlotTopology.MinSlot; s <= GroundSlotTopology.MaxSlot; s++)
            {
                if (_uidBySlot[s] != uid)
                {
                    continue;
                }

                slot = s;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 按 uid 清占格（不 Release 视图）。Release 前守卫用，避免「占格在、视图无」幽灵格。
        /// </summary>
        public bool TryClearOccupancyForUid(int uid, bool skipBusyGuard = false)
        {
            if (!TryFindOccupiedSlotForUid(uid, out var slot))
            {
                return false;
            }

            return ClearSlotOccupancy(slot, skipBusyGuard);
        }

        public bool IsEmpty(int slot)
        {
            return IsValidSlot(slot) && _uidBySlot[slot] == 0;
        }

        public bool IsPlaceable(int slot)
        {
            return IsValidSlot(slot)
                   && !GroundSlotTopology.IsAvatarReserved(slot)
                   && _uidBySlot[slot] == 0;
        }

        public IReadOnlyList<int> GetEmptyPlaceableSlots()
        {
            var result = new List<int>(8);
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                if (IsPlaceable(slot))
                {
                    result.Add(slot);
                }
            }

            return result;
        }

        public bool HasFullOpeningRing()
        {
            for (var i = 0; i < GroundSlotTopology.ClockwiseRing.Count; i++)
            {
                if (_uidBySlot[GroundSlotTopology.ClockwiseRing[i]] == 0)
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryGetRandomOccupiedCard(out ManagedCard card)
        {
            card = null;
            var candidates = new List<int>();
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                if (_uidBySlot[slot] != 0)
                {
                    candidates.Add(_uidBySlot[slot]);
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            var uid = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            if (!CardManagerSingleton.Instance.TryGetTracked(uid, "Ground.TryGetRandomOccupiedCard", out card))
            {
                return false;
            }

            return true;
        }

        public bool RequestPlaceCard(int slot, ManagedCard card, bool skipBusyGuard = false)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundFieldManager] 当前忙碌，无法接受放置申请。");
                return false;
            }

            if (card == null)
            {
                return false;
            }

            if (!IsPlaceable(slot))
            {
                Debug.LogWarning($"[GroundFieldManager] 格位不可放置: slot={slot}");
                return false;
            }

            if (!TryRegisterCardAtSlot(slot, card.Uid))
            {
                return false;
            }

            CardManagerSingleton.Instance.SetDisplayMode(card, CardDisplayMode.GroundCardMode);
            RefreshSlotHitCollider(slot);
            return true;
        }

        /// <summary>
        /// 登记到格位并把 Transform 落到锚点（战后 Sync / 补牌兜底用）。不播移动动画。
        /// </summary>
        public bool RequestPlaceCardAtAnchor(
            int slot,
            ManagedCard card,
            bool skipBusyGuard = false,
            bool snapToAnchor = true)
        {
            if (!RequestPlaceCard(slot, card, skipBusyGuard))
            {
                return false;
            }

            if (snapToAnchor
                && card?.Transform != null
                && TryGetAnchor(slot, out var anchor)
                && anchor != null)
            {
                SlotFrameConvergence.SnapHome(card, anchor.position, "Ground.Place.Snap", card.Uid);
                CardPresentationProbe.SnapSet(
                    card.Uid,
                    anchor.position,
                    "Ground.Place.Snap",
                    slot: slot,
                    killedTween: true,
                    reason: "placeAtAnchor");
                CardManagerSingleton.Instance.RefreshDisplayMode(card);
            }

            return true;
        }

        /// <summary>
        /// 仅迁移占格（不销毁视图）：从当前格挪到目标格，可选瞬移到锚点。战后 Sync 安全网用。
        /// </summary>
        public bool RequestRelocateOccupancy(
            int uid,
            int toSlot,
            bool snapToAnchor,
            bool skipBusyGuard = false)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundFieldManager] 当前忙碌，无法迁移占格。");
                return false;
            }

            if (!IsValidSlot(toSlot) || GroundSlotTopology.IsAvatarReserved(toSlot))
            {
                return false;
            }

            if (!_slotByUid.TryGetValue(uid, out var fromSlot))
            {
                return false;
            }

            if (fromSlot == toSlot)
            {
                if (snapToAnchor
                    && CardManagerSingleton.Instance.TryGet(uid, out var same)
                    && same?.Transform != null
                    && TryGetAnchor(toSlot, out var sameAnchor)
                    && sameAnchor != null)
                {
                    SlotFrameConvergence.SnapHome(same, sameAnchor.position, "Ground.Relocate.Snap", uid);
                    CardPresentationProbe.SnapSet(
                        uid,
                        sameAnchor.position,
                        "Ground.Relocate.Snap",
                        slot: toSlot,
                        killedTween: true,
                        reason: "relocateSameSlot");
                }

                return true;
            }

            if (!IsEmpty(toSlot))
            {
                Debug.LogWarning($"[GroundFieldManager] 目标格已占用，无法迁移 uid={uid} → slot={toSlot}");
                return false;
            }

            UnregisterCardAtSlot(fromSlot);
            if (!TryRegisterCardAtSlot(toSlot, uid))
            {
                // 回滚：目标格被占时恢复原格，避免 uid 从占格表消失却留下视图。
                TryRegisterCardAtSlot(fromSlot, uid);
                return false;
            }

            RefreshSlotHitCollider(fromSlot);
            RefreshSlotHitCollider(toSlot);

            if (snapToAnchor
                && CardManagerSingleton.Instance.TryGet(uid, out var card)
                && card?.Transform != null
                && TryGetAnchor(toSlot, out var anchor)
                && anchor != null)
            {
                SlotFrameConvergence.SnapHome(card, anchor.position, "Ground.Relocate.Snap", uid);
                CardPresentationProbe.SnapSet(
                    uid,
                    anchor.position,
                    "Ground.Relocate.Snap",
                    slot: toSlot,
                    killedTween: true,
                    reason: "relocate");
                CardManagerSingleton.Instance.RefreshDisplayMode(card);
            }

            return true;
        }

        /// <summary>
        /// 按 Core CardMoved 列表更新占格并并行 hop（不整圈盲转）。交战忙碌时可 skipBusyGuard。
        /// </summary>
        public UniTask ApplyBoardMovesAndHopAsync(
            IReadOnlyList<PostKillCardMove> moves,
            CancellationToken cancellationToken = default,
            bool skipBusyGuard = false)
        {
            return ApplyBoardMovesAndHopInternalAsync(moves, cancellationToken, skipBusyGuard);
        }

        /// <summary>
        /// Avatar 专用入场：登记到格5并播缩放出现。不走 <see cref="IsPlaceable"/>，不锁 busy，便于与开局外圈发牌并行。
        /// </summary>
        public UniTask RequestRevealAvatarAsync(ManagedCard avatar, CancellationToken cancellationToken = default)
        {
            return RequestRevealAvatarInternalAsync(avatar, cancellationToken);
        }

        private async UniTask RequestRevealAvatarInternalAsync(
            ManagedCard avatar,
            CancellationToken cancellationToken)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[GroundFieldManager] 当前忙碌，无法揭示 Avatar。");
                return;
            }

            if (avatar == null)
            {
                Debug.LogWarning("[GroundFieldManager] Avatar 卡为空，无法揭示。");
                return;
            }

            var slot = GroundSlotTopology.AvatarReservedSlot;
            if (!IsEmpty(slot))
            {
                Debug.LogWarning($"[GroundFieldManager] Avatar 格位已被占用: slot={slot}");
                return;
            }

            if (!TryGetAnchor(slot, out var anchor) || anchor == null)
            {
                Debug.LogWarning($"[GroundFieldManager] Avatar 锚点缺失: slot={slot}");
                return;
            }

            if (!TryRegisterCardAtSlot(slot, avatar.Uid))
            {
                Debug.LogWarning($"[GroundFieldManager] Avatar 登记失败: slot={slot}");
                return;
            }

            var cardManager = CardManagerSingleton.Instance;
            cardManager.SetDisplayMode(avatar, CardDisplayMode.GroundCardMode);
            SlotFrameConvergence.SnapHome(avatar, anchor.position, "Ground.AvatarReveal", avatar.Uid);
            RefreshSlotHitCollider(slot);

            var finalScale = CardDisplayModeVisuals.GetBaseLocalScale(CardDisplayMode.GroundCardMode);
            avatar.Transform.localScale = Vector3.zero;

            var duration = layoutSettings != null ? layoutSettings.avatarRevealDuration : 0.28f;
            try
            {
                await RunViewTweenAsync(
                    CardViewTween.ScaleAppear(avatar.Transform, finalScale, duration),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (avatar.Transform != null)
                {
                    avatar.Transform.localScale = finalScale;
                }

                throw;
            }

            if (avatar.Transform != null)
            {
                cardManager.RefreshDisplayMode(avatar);
            }
        }

        public bool RequestMoveCard(int fromSlot, int toSlot, bool animate)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[GroundFieldManager] 当前忙碌，无法移动卡牌。");
                return false;
            }

            if (!IsValidSlot(fromSlot) || !IsValidSlot(toSlot) || fromSlot == toSlot)
            {
                return false;
            }

            if (_uidBySlot[fromSlot] == 0 || _uidBySlot[toSlot] != 0)
            {
                return false;
            }

            var uid = _uidBySlot[fromSlot];
            if (!CardManagerSingleton.Instance.TryGetTracked(
                    uid,
                    "Ground.RequestMoveCard",
                    out var card,
                    "fromSlot=" + fromSlot.ToString(CultureInfo.InvariantCulture))
                || card?.Transform == null)
            {
                return false;
            }

            UnregisterCardAtSlot(fromSlot);
            if (!TryRegisterCardAtSlot(toSlot, uid))
            {
                TryRegisterCardAtSlot(fromSlot, uid);
                return false;
            }

            if (animate)
            {
                MoveCardAnimatedAsync(card, fromSlot, toSlot, EnsureFieldAnimToken()).Forget();
            }
            else if (TryGetAnchor(toSlot, out var anchor))
            {
                SlotFrameConvergence.SnapHome(card, anchor.position, "Ground.Move.Snap", uid);
                CardManagerSingleton.Instance.RefreshDisplayMode(card);
            }

            RefreshSlotHitCollider(fromSlot);
            RefreshSlotHitCollider(toSlot);
            return true;
        }

        /// <summary>
        /// 将卡从场地表移除但不销毁视图，供手牌管理器接管。
        /// </summary>
        public bool TryTakeCardFromField(
            int uid,
            out ManagedCard card,
            bool startExplore = false,
            bool skipBusyGuard = false)
        {
            card = null;
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundFieldManager] 当前忙碌，无法取走卡牌。");
                return false;
            }

            if (!_slotByUid.TryGetValue(uid, out var slot))
            {
                return false;
            }

            if (!CardManagerSingleton.Instance.TryGet(uid, out card))
            {
                CardPresentationProbe.RegistryMiss(
                    uid,
                    "Ground.TryTakeCardFromField",
                    "slot=" + slot.ToString(CultureInfo.InvariantCulture) + ",healVacate=1");
                UnregisterCardAtSlot(slot, "TryTakeCardFromField.missView");
                RefreshSlotHitCollider(slot);
                if (startExplore)
                {
                    _dealFlightCoordinator?.StartExplore(slot);
                }

                return false;
            }

            VacateSlotForExplore(slot, card, playRemoveAnim: false, skipBusyGuard: true, startExplore: startExplore);
            return true;
        }

        public bool RequestRemoveFromField(
            int uid,
            bool animate,
            bool skipBusyGuard = false,
            bool startExplore = true)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundFieldManager] 当前忙碌，无法移除卡牌。");
                return false;
            }

            if (!_slotByUid.TryGetValue(uid, out var slot))
            {
                return false;
            }

            if (!CardManagerSingleton.Instance.TryGet(uid, out var card))
            {
                CardPresentationProbe.RegistryMiss(
                    uid,
                    "Ground.RequestRemoveFromField",
                    "slot=" + slot.ToString(CultureInfo.InvariantCulture) + ",healVacate=1");
                UnregisterCardAtSlot(slot, "RequestRemoveFromField.missView");
                RefreshSlotHitCollider(slot);
                if (startExplore)
                {
                    _dealFlightCoordinator?.StartExplore(slot);
                }

                return false;
            }

            VacateSlotForExplore(slot, card, animate, skipBusyGuard, startExplore);
            if (!animate)
            {
                CardManagerSingleton.Instance.Release(uid, "Ground.RemoveImmediate");
            }

            return true;
        }

        internal void VacateSlotForExplore(
            int slot,
            ManagedCard card,
            bool playRemoveAnim,
            bool skipBusyGuard = false,
            bool startExplore = true)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundFieldManager] 当前忙碌，无法清格探求。");
                return;
            }

            if (!IsValidSlot(slot) || _uidBySlot[slot] == 0)
            {
                return;
            }

            UnregisterCardAtSlot(slot);
            RefreshSlotHitCollider(slot);
            if (startExplore)
            {
                _dealFlightCoordinator?.StartExplore(slot);
            }

            if (card == null)
            {
                return;
            }

            if (playRemoveAnim)
            {
                RemoveCardAnimatedAsync(card, slot, EnsureFieldAnimToken()).Forget();
            }
        }

        internal bool PlaceForExplore(int slot, ManagedCard card)
        {
            if (card == null || !IsPlaceable(slot))
            {
                return false;
            }

            if (!TryRegisterCardAtSlot(slot, card.Uid))
            {
                return false;
            }

            CardManagerSingleton.Instance.SetDisplayMode(card, CardDisplayMode.GroundCardMode);
            RefreshSlotHitCollider(slot);
            return true;
        }

        internal bool TryGetExploreAnchorPosition(int slot, out Vector3 position)
        {
            position = default;
            if (!TryGetAnchor(slot, out var anchor) || anchor == null)
            {
                return false;
            }

            position = anchor.position;
            return true;
        }

        /// <summary>
        /// 启动 Drain 补牌 L2 收敛飞牌（逻辑占格后视觉收敛到格锚）。
        /// </summary>
        internal DealFlightHandle LaunchDrainDealFlight(
            ManagedCard card,
            int targetSlot,
            Vector3 launchPos,
            DealFlightContext context)
        {
            return _dealFlightCoordinator?.LaunchDrainFlight(card, targetSlot, launchPos, context);
        }

        internal bool IsDealInFlight(int uid)
        {
            return _dealFlightCoordinator != null && _dealFlightCoordinator.IsInFlight(uid);
        }

        public static async UniTask WaitDealFlightsSettledAsync(
            IReadOnlyList<DealFlightHandle> handles,
            CancellationToken cancellationToken)
        {
            await GroundSlotDealFlightCoordinator.WaitAllSettledAsync(handles, cancellationToken);
        }

        /// <summary>
        /// 清场。生命周期清理（回主菜单/重开）应传 <paramref name="force"/>，
        /// 否则忙碌态会直接放弃，留下占格与 _isBusy 残留。
        /// </summary>
        public void ClearField(bool force = false)
        {
            if (!force && IsBusy)
            {
                Debug.LogWarning("[GroundFieldManager] 当前忙碌，无法清场。");
                return;
            }

            if (force)
            {
                FieldBattleManagerSingleton.Instance?.CancelBattleWork();
                CancelFieldAnimations();
                _isBusy = false;
            }

            _dealFlightCoordinator?.CancelAll();

            // 非 force：先 Vacate 再 Release，与 RequestRemoveFromField 契约一致，避免幽灵占格。
            if (!force)
            {
                var cardManager = CardManagerSingleton.TryGetInstance();
                if (cardManager != null)
                {
                    for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
                    {
                        var uid = _uidBySlot[slot];
                        if (uid == 0)
                        {
                            continue;
                        }

                        UnregisterCardAtSlot(slot, "ClearField");
                        cardManager.Release(uid, "Ground.ClearField");
                    }
                }
            }

            ClearSlotTable();
            RefreshAllSlotHitColliders();
        }

        private void CancelFieldAnimations()
        {
            if (_fieldAnimCts == null)
            {
                return;
            }

            _fieldAnimCts.Cancel();
            _fieldAnimCts.Dispose();
            _fieldAnimCts = null;
        }

        private CancellationToken EnsureFieldAnimToken()
        {
            if (_fieldAnimCts == null)
            {
                _fieldAnimCts = new CancellationTokenSource();
            }

            return _fieldAnimCts.Token;
        }

        public void OnEmptySlotClicked(int slot)
        {
            TryHandleEmptySlotClick(slot);
        }

        public bool TryHandleEmptySlotClick(int slot)
        {
            if (IsBusy)
            {
                return false;
            }

            if (!IsEmpty(slot) || !IsAvatarOrthogonalBattleSlot(slot))
            {
                return false;
            }

            if (!CombatHitSink.TryBeginPresentationLock("ClickEmpty"))
            {
                return false;
            }

            Debug.Log($"[GroundFieldManager] 空槽点击 → Core ClickEmpty: slot={slot}");
            EmptySlotClicked?.Invoke(slot);

            var postKill = CombatHitSink.RequestClickEmpty(slot);
            if (!postKill.Accepted)
            {
                CombatHitSink.EndPresentationLock("ClickEmpty-rejected");
                return false;
            }

            RunEmptySlotDrainAsync(postKill).Forget();
            return true;
        }

        private async UniTaskVoid RunEmptySlotDrainAsync(PostKillBoardPresentationResult postKill)
        {
            try
            {
                await CombatHitSink.RequestDrainPostKillBoard(postKill);
            }
            catch (System.OperationCanceledException)
            {
            }
            finally
            {
                CombatHitSink.EndPresentationLock("ClickEmpty-drain");
            }
        }

        /// <summary>
        /// 仅清占格登记，不销毁视图、不启动探求。Sync 两阶段卸格用。
        /// </summary>
        public bool ClearSlotOccupancy(int slot, bool skipBusyGuard = false)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundFieldManager] 当前忙碌，无法清占格。");
                return false;
            }

            if (!IsValidSlot(slot) || _uidBySlot[slot] == 0)
            {
                return false;
            }

            UnregisterCardAtSlot(slot);
            RefreshSlotHitCollider(slot);
            return true;
        }

        public bool IsAvatarOrthogonalBattleSlot(int slot)
        {
            return GroundSlotTopology.AreOrthogonal(slot, GroundSlotTopology.AvatarReservedSlot);
        }

        /// <summary>
        /// 在 Avatar 四向相邻格中随机挑选一张存活卡牌（用于怪物反击测试）。
        /// </summary>
        public bool TryGetRandomAvatarOrthogonalMonsterSlot(out int slot, out ManagedCard card)
        {
            slot = 0;
            card = null;

            var avatarSlot = GroundSlotTopology.AvatarReservedSlot;
            var neighbors = GroundSlotTopology.GetNeighbors(avatarSlot, GroundSlotRelation.Orthogonal);
            var candidates = new List<int>(neighbors.Count);
            for (var i = 0; i < neighbors.Count; i++)
            {
                var neighborSlot = neighbors[i];
                if (!TryGetCardAt(neighborSlot, out var neighborCard) || neighborCard.IsFieldDead)
                {
                    continue;
                }

                candidates.Add(neighborSlot);
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            slot = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return TryGetCardAt(slot, out card);
        }

        public UniTask RotateOuterRingClockwiseAsync(CancellationToken cancellationToken = default)
        {
            return RotateOuterRingInternalAsync(true, cancellationToken);
        }

        /// <summary>
        /// 交战流程内嵌套旋转：跳过场地忙碌门禁（由交战管理器持有外层忙碌锁）。
        /// </summary>
        internal UniTask RotateOuterRingClockwiseWhileBusyAsync(CancellationToken cancellationToken = default)
        {
            return RotateOuterRingInternalAsync(true, cancellationToken, skipBusyGuard: true);
        }

        /// <summary>
        /// 交战流程内嵌套旋转：跳过场地忙碌门禁，可指定顺/逆时针。
        /// </summary>
        public UniTask RotateOuterRingWhileBusyAsync(bool clockwise, CancellationToken cancellationToken = default)
        {
            return RotateOuterRingInternalAsync(clockwise, cancellationToken, skipBusyGuard: true);
        }

        public Transform GetGroundAnchor(int slot)
        {
            return TryGetAnchor(slot, out var anchor) ? anchor : null;
        }

        /// <summary>
        /// 自上次 <see cref="ClearOccupancyConflictFlag"/> 以来是否发生过占格冲突。
        /// Drain/拾取路径用此决定是否强制 SyncBoardOccupancyFromCore。
        /// </summary>
        public bool HasOccupancyConflictSinceClear => _occupancyConflictSinceClear;

        public void ClearOccupancyConflictFlag()
        {
            _occupancyConflictSinceClear = false;
        }

        public bool ConsumeOccupancyConflictFlag()
        {
            var had = _occupancyConflictSinceClear;
            _occupancyConflictSinceClear = false;
            return had;
        }

        private async UniTask ApplyBoardMovesAndHopInternalAsync(
            IReadOnlyList<PostKillCardMove> moves,
            CancellationToken cancellationToken,
            bool skipBusyGuard)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundFieldManager] 当前忙碌，无法应用盘面移动。");
                return;
            }

            if (moves == null || moves.Count == 0)
            {
                return;
            }

            if (moves.Count == 2
                && TryGetCrossSwapPair(moves, out var swapA, out var swapB))
            {
                await ApplyCrossSwapMovesInternalAsync(swapA, swapB, cancellationToken, skipBusyGuard);
                return;
            }

            if (TryClassifyOuterRingRotation(moves, out var clockwise))
            {
                await RotateOuterRingWhileBusyAsync(clockwise, cancellationToken);
                return;
            }

            await ApplyGeneralBoardMovesInternalAsync(moves, cancellationToken, skipBusyGuard);
        }

        private bool TryClassifyOuterRingRotation(
            IReadOnlyList<PostKillCardMove> moves,
            out bool clockwise)
        {
            clockwise = true;
            if (moves == null || moves.Count == 0)
            {
                return false;
            }

            var ring = GroundSlotTopology.ClockwiseRing;
            var ringOccupied = 0;
            for (var i = 0; i < ring.Count; i++)
            {
                if (_uidBySlot[ring[i]] != 0)
                {
                    ringOccupied++;
                }
            }

            if (ringOccupied == 0 || moves.Count != ringOccupied)
            {
                return false;
            }

            bool? direction = null;
            var fromSlots = new HashSet<int>(moves.Count);
            for (var i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                if (move.Uid <= 0
                    || !IsValidSlot(move.FromSlot)
                    || !IsValidSlot(move.ToSlot)
                    || move.FromSlot == move.ToSlot)
                {
                    return false;
                }

                if (!GroundSlotTopology.IsOuterRing(move.FromSlot)
                    || !GroundSlotTopology.IsOuterRing(move.ToSlot))
                {
                    return false;
                }

                if (_uidBySlot[move.FromSlot] != move.Uid)
                {
                    return false;
                }

                var fromIdx = GroundSlotTopology.GetClockwiseRingIndex(move.FromSlot);
                var toIdx = GroundSlotTopology.GetClockwiseRingIndex(move.ToSlot);
                if (fromIdx < 0 || toIdx < 0)
                {
                    return false;
                }

                var isClockwise = toIdx == (fromIdx + 1) % ring.Count;
                var isCounterClockwise = toIdx == (fromIdx + ring.Count - 1) % ring.Count;
                if (!isClockwise && !isCounterClockwise)
                {
                    return false;
                }

                var moveClockwise = isClockwise;
                if (!direction.HasValue)
                {
                    direction = moveClockwise;
                }
                else if (direction.Value != moveClockwise)
                {
                    return false;
                }

                if (!fromSlots.Add(move.FromSlot))
                {
                    return false;
                }
            }

            for (var i = 0; i < ring.Count; i++)
            {
                var slot = ring[i];
                if (_uidBySlot[slot] != 0 && !fromSlots.Contains(slot))
                {
                    return false;
                }
            }

            clockwise = direction ?? true;
            FlowFieldTraceSink.RotateClassify?.Invoke(true, clockwise, moves.Count, ringOccupied);
            return true;
        }

        private async UniTask ApplyGeneralBoardMovesInternalAsync(
            IReadOnlyList<PostKillCardMove> moves,
            CancellationToken cancellationToken,
            bool skipBusyGuard)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundFieldManager] 当前忙碌，无法应用盘面移动。");
                return;
            }

            if (moves == null || moves.Count == 0)
            {
                return;
            }

            if (!skipBusyGuard)
            {
                _isBusy = true;
            }

            try
            {
                var cardManager = CardManagerSingleton.Instance;
                var hopPlans = new List<(ManagedCard card, int fromSlot, int toSlot)>(moves.Count);
                var expectedToSlotByUid = new Dictionary<int, int>(moves.Count);
                var movingUids = new HashSet<int>(moves.Count);
                var expectedMoveCount = 0;

                for (var i = 0; i < moves.Count; i++)
                {
                    var move = moves[i];
                    if (move.Uid <= 0
                        || !IsValidSlot(move.FromSlot)
                        || !IsValidSlot(move.ToSlot)
                        || move.FromSlot == move.ToSlot)
                    {
                        continue;
                    }

                    expectedMoveCount++;
                    expectedToSlotByUid[move.Uid] = move.ToSlot;
                    movingUids.Add(move.Uid);

                    if (!cardManager.TryGet(move.Uid, out var card) || card?.Transform == null)
                    {
                        continue;
                    }

                    if (!_slotByUid.ContainsKey(move.Uid))
                    {
                        continue;
                    }

                    hopPlans.Add((card, move.FromSlot, move.ToSlot));
                }

                if (hopPlans.Count == 0 && expectedMoveCount == 0)
                {
                    return;
                }

                try
                {
                    var planSb = new System.Text.StringBuilder(hopPlans.Count * 12);
                    for (var i = 0; i < hopPlans.Count; i++)
                    {
                        var p = hopPlans[i];
                        if (planSb.Length > 0)
                        {
                            planSb.Append(';');
                        }

                        planSb.Append(p.card.Uid)
                            .Append(':')
                            .Append(p.fromSlot)
                            .Append('\u2192')
                            .Append(p.toSlot);
                    }

                    FlowFieldTraceSink.HopPlan?.Invoke(planSb.ToString());
                }
                catch
                {
                    // ignore
                }

                for (var i = 0; i < hopPlans.Count; i++)
                {
                    var p = hopPlans[i];
                    var fromPos = p.card.Transform != null ? p.card.Transform.position : Vector3.zero;
                    var toAnchor = GetGroundAnchor(p.toSlot);
                    var toPos = toAnchor != null ? toAnchor.position : fromPos;
                    CardPresentationProbe.MotionPlan(
                        p.card.Uid,
                        p.fromSlot,
                        p.toSlot,
                        fromPos,
                        toPos,
                        "Ground.HopPlan",
                        reason: "hop");
                }

                if (hopPlans.Count < expectedMoveCount)
                {
                    Debug.LogWarning(
                        $"[GroundFieldManager] 盘面 hop 不完整：可播={hopPlans.Count}/{expectedMoveCount}，"
                        + "漏移卡按目标格落位或留给 Sync，禁止原格回登。");
                }

                foreach (var uid in movingUids)
                {
                    if (_slotByUid.TryGetValue(uid, out var occupiedSlot))
                    {
                        UnregisterCardAtSlot(occupiedSlot, "General.Vacate");
                    }
                }

                var registeredUids = new HashSet<int>(hopPlans.Count);
                for (var i = 0; i < hopPlans.Count; i++)
                {
                    var plan = hopPlans[i];
                    if (TryRegisterCardAtSlot(plan.toSlot, plan.card.Uid, logConflict: false))
                    {
                        registeredUids.Add(plan.card.Uid);
                    }
                }

                foreach (var kv in expectedToSlotByUid)
                {
                    if (registeredUids.Contains(kv.Key))
                    {
                        continue;
                    }

                    if (!cardManager.TryGet(kv.Key, out var card) || card?.Transform == null)
                    {
                        continue;
                    }

                    TryRegisterCardAtSlot(kv.Value, kv.Key, logConflict: false);
                }

                if (hopPlans.Count > 0)
                {
                    var moveTasks = new List<UniTask>(hopPlans.Count);
                    for (var i = 0; i < hopPlans.Count; i++)
                    {
                        var plan = hopPlans[i];
                        if (!_slotByUid.TryGetValue(plan.card.Uid, out var registeredSlot)
                            || registeredSlot != plan.toSlot)
                        {
                            continue;
                        }

                        if (IsDealInFlight(plan.card.Uid))
                        {
                            _dealFlightCoordinator?.TryRedirectFlightToSlot(
                                plan.card.Uid,
                                plan.toSlot,
                                layoutSettings.moveDuration);
                        }

                        moveTasks.Add(
                            AnimateCardHopToSlotAsync(plan.card, plan.fromSlot, plan.toSlot, cancellationToken));
                    }

                    if (moveTasks.Count > 0)
                    {
                        await UniTask.WhenAll(moveTasks);
                    }
                }

                RefreshAllSlotHitColliders();
            }
            finally
            {
                if (!skipBusyGuard)
                {
                    _isBusy = false;
                }
            }
        }

        private async UniTask RotateOuterRingInternalAsync(
            bool clockwise,
            CancellationToken cancellationToken,
            bool skipBusyGuard = false)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundFieldManager] 当前忙碌，无法旋转。");
                return;
            }

            if (!skipBusyGuard)
            {
                _isBusy = true;
            }

            PerfTraceSink.OpenBeat?.Invoke("BoardChoreo", 0);
            var choreoSeqId = ChoreoTraceSink.SafeBeginChoreo(
                "rotate",
                "skipBusyGuard", skipBusyGuard ? "1" : "0",
                "clockwise", clockwise ? "1" : "0");
            var plannedAnim = 0;
            var actualAnim = 0;
            var outcome = "ok";

            try
            {
                var ring = GroundSlotTopology.ClockwiseRing;
                var uids = new int[ring.Count];
                var planSb = new StringBuilder(ring.Count * 16);
                for (var i = 0; i < ring.Count; i++)
                {
                    uids[i] = _uidBySlot[ring[i]];
                    if (uids[i] == 0)
                    {
                        continue;
                    }

                    plannedAnim++;
                    var toIndex = clockwise
                        ? (i + 1) % ring.Count
                        : (i + ring.Count - 1) % ring.Count;
                    if (planSb.Length > 0)
                    {
                        planSb.Append(';');
                    }

                    planSb.Append(uids[i])
                        .Append(':')
                        .Append(ring[i])
                        .Append('\u2192')
                        .Append(ring[toIndex]);
                }

                CardPresentationProbe.RingShift(
                    "plan",
                    planSb.ToString(),
                    "Ground.RingShift",
                    clockwise,
                    skipBusyGuard,
                    plannedAnim,
                    0,
                    choreoSeqId);

                for (var i = 0; i < ring.Count; i++)
                {
                    var slot = ring[i];
                    var uid = _uidBySlot[slot];
                    if (uid != 0)
                    {
                        _slotByUid.Remove(uid);
                    }

                    _uidBySlot[slot] = 0;
                }

                CardPresentationProbe.RingShift(
                    "vacated",
                    planSb.ToString(),
                    "Ground.RingShift",
                    clockwise,
                    skipBusyGuard,
                    plannedAnim,
                    0,
                    choreoSeqId);

                for (var i = 0; i < ring.Count; i++)
                {
                    if (uids[i] == 0)
                    {
                        continue;
                    }

                    var toIndex = clockwise
                        ? (i + 1) % ring.Count
                        : (i + ring.Count - 1) % ring.Count;
                    var toSlot = ring[toIndex];
                    if (!TryRegisterCardAtSlot(toSlot, uids[i]))
                    {
                        Debug.LogError(
                            $"[GroundFieldManager] 外圈旋转登记失败 uid={uids[i]} → slot={toSlot}");
                        CardPresentationProbe.RegistryMiss(
                            uids[i],
                            "Ground.RingShift.RegisterFail",
                            "toSlot=" + toSlot.ToString(CultureInfo.InvariantCulture));
                    }
                }

                CardPresentationProbe.RingShift(
                    "registered",
                    planSb.ToString(),
                    "Ground.RingShift",
                    clockwise,
                    skipBusyGuard,
                    plannedAnim,
                    0,
                    choreoSeqId);

                ChoreoTraceSink.SafeExploreTrace(
                    -1,
                    "ringShift",
                    -1,
                    -1,
                    "clockwise", clockwise ? "1" : "0");

                _dealFlightCoordinator?.OnRingShifted(clockwise);

                SyncPresentationClock();
                var sourceTime = layoutSettings != null ? layoutSettings.moveDuration : 0.35f;
                var beatId = _beatGrid != null ? _beatGrid.OpenBeat(sourceTime) : -1;
                if (beatId > 0)
                {
                    _beatGrid.PlaceBarrier(beatId, _presentationClock.Now + sourceTime);
                }

                var moveTasks = new List<UniTask>();
                for (var i = 0; i < ring.Count; i++)
                {
                    var uid = uids[i];
                    if (uid == 0)
                    {
                        continue;
                    }

                    if (!CardManagerSingleton.Instance.TryGet(uid, out var card) || card?.Transform == null)
                    {
                        CardPresentationProbe.RegistryMiss(
                            uid,
                            "Ground.RingShift.AnimateSkip",
                            "fromSlot=" + ring[i].ToString(CultureInfo.InvariantCulture));
                        continue;
                    }

                    if (!_slotByUid.TryGetValue(uid, out var toSlot))
                    {
                        continue;
                    }

                    var fromSlot = ring[i];
                    if (beatId > 0)
                    {
                        _beatGrid.Register(beatId, committed: true);
                    }

                    moveTasks.Add(AnimateCardHopToSlotAsync(card, fromSlot, toSlot, cancellationToken));
                    actualAnim++;
                }

                CardPresentationProbe.RingShift(
                    "animate",
                    planSb.ToString(),
                    "Ground.RingShift",
                    clockwise,
                    skipBusyGuard,
                    plannedAnim,
                    actualAnim,
                    choreoSeqId);

                if (moveTasks.Count > 0)
                {
                    await UniTask.WhenAll(moveTasks);
                }
                else
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(layoutSettings.emptyRotateDuration),
                        cancellationToken: cancellationToken);
                }

                RefreshAllSlotHitColliders();
                CardManagerSingleton.Instance?.AuditRegistryIntegrity("Ground.RingRotate.End");
            }
            catch (OperationCanceledException)
            {
                outcome = "cancel";
                throw;
            }
            catch (Exception)
            {
                outcome = "error";
                throw;
            }
            finally
            {
                ChoreoTraceSink.SafeEndChoreo(outcome, plannedAnim, actualAnim);
                PerfTraceSink.CloseBeat?.Invoke();
                if (!skipBusyGuard)
                {
                    _isBusy = false;
                }
            }
        }

        private async UniTask AnimateCardHopToSlotAsync(
            ManagedCard card,
            int fromSlot,
            int toSlot,
            CancellationToken cancellationToken)
        {
            if (card?.Transform == null
                || !TryGetAnchor(toSlot, out var toAnchor)
                || toAnchor == null)
            {
                return;
            }

            CardManagerSingleton.Instance.RefreshDisplayMode(card);
            var sourceTime = layoutSettings != null ? layoutSettings.moveDuration : 0.35f;

            // 补牌飞行中：只 Redirect L2 目标，由飞牌探针等待同一驱动器完成。
            if (IsDealInFlight(card.Uid)
                && _dealFlightCoordinator != null
                && _dealFlightCoordinator.TryRedirectFlightToSlot(card.Uid, toSlot, sourceTime))
            {
                if (SlotFrameConvergence.TryGetDriver(card, out var inFlightDriver))
                {
                    await SlotFrameConvergence.AwaitDriverAsync(inFlightDriver, cancellationToken);
                }

                return;
            }

            IssueSyncL2Lease(card.Uid, sourceTime);
            try
            {
                await SlotFrameConvergence.ConvergeVisualToWorldAsync(
                    card,
                    toAnchor.position,
                    sourceTime,
                    cancellationToken,
                    snapHomeOnComplete: true);
            }
            finally
            {
                ReleaseSyncL2Lease(card.Uid);
            }

            CardManagerSingleton.Instance.RefreshDisplayMode(card);
        }

        private void SyncPresentationClock()
        {
            _presentationClock.Seek(Time.time);
        }

        private void IssueSyncL2Lease(int cardId, float sourceTime)
        {
            if (_leaseArbiter == null || cardId <= 0)
            {
                return;
            }

            SyncPresentationClock();
            var now = _presentationClock.Now;
            var key = new LeaseKey(cardId, TowerLayer.SlotFrame);
            var request = LeaseRequest.Sync(key, now, now + sourceTime, committed: true);
            var result = _leaseArbiter.TryAcquire(request, () =>
            {
                if (CardManagerSingleton.Instance != null
                    && CardManagerSingleton.Instance.TryGet(cardId, out var card)
                    && SlotFrameConvergence.TryGetDriver(card, out var driver)
                    && SlotFrameConvergence.TryGetTower(card, out var tower)
                    && tower.SlotFrame != null)
                {
                    return new HandoffState(tower.SlotFrame.localPosition, driver.SampleVelocity());
                }

                return HandoffState.AtRest(Vector3.zero);
            });

            if (!result.IsWriteAllowed)
            {
                Debug.LogWarning(
                    $"[GroundFieldManager] L2 sync lease denied uid={cardId} verdict={result.Verdict}");
            }
        }

        private void ReleaseSyncL2Lease(int cardId)
        {
            if (_leaseArbiter == null || cardId <= 0)
            {
                return;
            }

            var key = new LeaseKey(cardId, TowerLayer.SlotFrame);
            if (_leaseArbiter.TryGetActiveLeaseId(key, out var leaseId))
            {
                _leaseArbiter.MarkFulfilled(key);
                _leaseArbiter.Release(key, leaseId);
            }
        }

        private static bool TryGetCrossSwapPair(
            IReadOnlyList<PostKillCardMove> moves,
            out PostKillCardMove first,
            out PostKillCardMove second)
        {
            first = default;
            second = default;
            if (moves == null || moves.Count != 2)
            {
                return false;
            }

            var a = moves[0];
            var b = moves[1];
            if (a.Uid <= 0 || b.Uid <= 0 || a.Uid == b.Uid)
            {
                return false;
            }

            if (a.FromSlot == b.ToSlot && a.ToSlot == b.FromSlot)
            {
                first = a;
                second = b;
                return true;
            }

            return false;
        }

        private async UniTask ApplyCrossSwapMovesInternalAsync(
            PostKillCardMove moveA,
            PostKillCardMove moveB,
            CancellationToken cancellationToken,
            bool skipBusyGuard)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundFieldManager] 当前忙碌，无法应用换位。");
                return;
            }

            var cardManager = CardManagerSingleton.Instance;
            if (cardManager == null)
            {
                return;
            }

            if (!cardManager.TryGet(moveA.Uid, out var cardA)
                || !cardManager.TryGet(moveB.Uid, out var cardB)
                || cardA?.Transform == null
                || cardB?.Transform == null)
            {
                Debug.LogWarning("[GroundFieldManager] 换位缺少视图，回退 Sync。");
                return;
            }

            if (!TryGetAnchor(moveA.ToSlot, out var anchorA)
                || !TryGetAnchor(moveB.ToSlot, out var anchorB))
            {
                return;
            }

            if (!skipBusyGuard)
            {
                _isBusy = true;
            }

            try
            {
                if (_slotByUid.TryGetValue(moveA.Uid, out var slotA)
                    && _slotByUid.TryGetValue(moveB.Uid, out var slotB))
                {
                    _uidBySlot[slotA] = 0;
                    _uidBySlot[slotB] = 0;
                    _slotByUid.Remove(moveA.Uid);
                    _slotByUid.Remove(moveB.Uid);
                    TryRegisterCardAtSlot(moveA.ToSlot, moveA.Uid);
                    TryRegisterCardAtSlot(moveB.ToSlot, moveB.Uid);
                }

                var duration = layoutSettings != null ? layoutSettings.swapMoveDuration : 0.3f;
                CardManagerSingleton.Instance.RefreshDisplayMode(cardA);
                CardManagerSingleton.Instance.RefreshDisplayMode(cardB);

                if (IsDealInFlight(moveA.Uid))
                {
                    _dealFlightCoordinator?.TryRedirectFlightToSlot(moveA.Uid, moveA.ToSlot, duration);
                }

                if (IsDealInFlight(moveB.Uid))
                {
                    _dealFlightCoordinator?.TryRedirectFlightToSlot(moveB.Uid, moveB.ToSlot, duration);
                }

                IssueSyncL2Lease(moveA.Uid, duration);
                IssueSyncL2Lease(moveB.Uid, duration);
                try
                {
                    await UniTask.WhenAll(
                        SlotFrameConvergence.ConvergeVisualToWorldAsync(
                            cardA,
                            anchorA.position,
                            duration,
                            cancellationToken,
                            snapHomeOnComplete: true),
                        SlotFrameConvergence.ConvergeVisualToWorldAsync(
                            cardB,
                            anchorB.position,
                            duration,
                            cancellationToken,
                            snapHomeOnComplete: true));
                }
                finally
                {
                    ReleaseSyncL2Lease(moveA.Uid);
                    ReleaseSyncL2Lease(moveB.Uid);
                }

                CardManagerSingleton.Instance.RefreshDisplayMode(cardA);
                CardManagerSingleton.Instance.RefreshDisplayMode(cardB);
                RefreshAllSlotHitColliders();
            }
            finally
            {
                if (!skipBusyGuard)
                {
                    _isBusy = false;
                }
            }
        }

        private static UniTask AwaitTweenAsync(Tween tween, CancellationToken cancellationToken)
        {
            if (tween == null || !tween.IsActive())
            {
                return UniTask.CompletedTask;
            }

            var tcs = new UniTaskCompletionSource();
            tween.OnComplete(() => tcs.TrySetResult());
            tween.OnKill(() => tcs.TrySetResult());

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    if (tween.IsActive())
                    {
                        tween.Kill(complete: false);
                    }

                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            return tcs.Task;
        }

        private UniTask MoveCardAnimatedAsync(
            ManagedCard card,
            int fromSlot,
            int toSlot,
            CancellationToken cancellationToken)
        {
            return AnimateCardHopToSlotAsync(card, fromSlot, toSlot, cancellationToken);
        }

        private async UniTask RemoveCardAnimatedAsync(ManagedCard card, int slot, CancellationToken cancellationToken)
        {
            try
            {
                if (card?.Transform == null)
                {
                    if (card != null)
                    {
                        CardManagerSingleton.TryGetInstance()?.Release(card.Uid, "Ground.RemoveAnimatedNoTransform");
                    }

                    return;
                }

                if (card.TryGetEffectManager(out var effectManager))
                {
                    await effectManager.PlayDeathAsync(slot, cancellationToken: cancellationToken);
                }
                else
                {
                    var initialScale = card.Transform.localScale;
                    await RunViewTweenAsync(
                        CardViewTween.ScaleDisappear(
                            card.Transform,
                            initialScale,
                            layoutSettings.removeDisappearDuration),
                        cancellationToken);
                }

                CardManagerSingleton.TryGetInstance()?.Release(card.Uid, "Ground.RemoveAnimatedComplete");
            }
            catch (OperationCanceledException)
            {
                // 清场取消：视图由外层 ReleaseAll 处理。
            }
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

        /// <summary>
        /// 登记占格。目标格已有其他 uid 时失败并告警，禁止静默挤占留下游离视图。
        /// 同 uid 迁格：先清旧格再写新格。
        /// </summary>
        private bool TryRegisterCardAtSlot(int slot, int uid, bool logConflict = true)
        {
            if (!IsValidSlot(slot) || uid <= 0)
            {
                return false;
            }

            var previousUid = _uidBySlot[slot];
            if (previousUid != 0 && previousUid != uid)
            {
                _occupancyConflictSinceClear = true;
                if (logConflict)
                {
                    Debug.LogError(
                        $"[GroundFieldManager] 禁止静默挤占：slot={slot} 已有 uid={previousUid}，拒绝登记 uid={uid}。"
                        + " 调用方须先 Vacate/Clear 旧占格。");
                }

                try
                {
                    FlowFieldTraceSink.OccupancyConflict?.Invoke(
                        slot,
                        previousUid,
                        uid,
                        logConflict ? "TryRegisterCardAtSlot" : "TryRegisterCardAtSlot.Silent");
                }
                catch
                {
                    // ignore
                }

                return false;
            }

            if (_slotByUid.TryGetValue(uid, out var previousSlot) && previousSlot != slot)
            {
                _uidBySlot[previousSlot] = 0;
            }

            _uidBySlot[slot] = uid;
            _slotByUid[uid] = slot;
            return true;
        }

        private void UnregisterCardAtSlot(int slot, string caller = null)
        {
            var uid = _uidBySlot[slot];
            if (uid != 0)
            {
                CardPresentationProbe.Vacate(uid, slot, "Ground.Vacate", caller);
                try
                {
                    FlowFieldTraceSink.OccupancyVacate?.Invoke(slot, uid, caller ?? "UnregisterCardAtSlot");
                }
                catch
                {
                    // ignore
                }

                _slotByUid.Remove(uid);
            }

            _uidBySlot[slot] = 0;
            TryRaiseFieldMaybeClearSignal();
        }

        private void TryRaiseFieldMaybeClearSignal()
        {
            if (HasLivingNonAvatarCard())
            {
                return;
            }

            FieldMaybeClearSignal?.Invoke();
        }

        private bool HasLivingNonAvatarCard()
        {
            var cardManager = CardManagerSingleton.Instance;
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                if (slot == GroundSlotTopology.AvatarReservedSlot)
                {
                    continue;
                }

                var uid = _uidBySlot[slot];
                if (uid == 0)
                {
                    continue;
                }

                if (cardManager != null &&
                    cardManager.TryGet(uid, out var card) &&
                    card != null &&
                    card.IsFieldDead)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void ClearSlotTable()
        {
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                _uidBySlot[slot] = 0;
            }

            _slotByUid.Clear();
        }

        private void EnsureSlotHitProxies()
        {
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                if (!TryGetAnchor(slot, out var anchor))
                {
                    continue;
                }

                var proxy = anchor.GetComponent<GroundSlotHitProxy>();
                if (proxy == null)
                {
                    var collider = anchor.GetComponent<BoxCollider2D>();
                    if (collider == null)
                    {
                        collider = anchor.gameObject.AddComponent<BoxCollider2D>();
                    }

                    proxy = anchor.gameObject.AddComponent<GroundSlotHitProxy>();
                }

                proxy.Configure(slot, layoutSettings.slotHitBoxSize);
                _slotHitProxies[slot] = proxy;
            }
        }

        private void RefreshAllSlotHitColliders()
        {
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                RefreshSlotHitCollider(slot);
            }
        }

        private void RefreshSlotHitCollider(int slot)
        {
            var proxy = _slotHitProxies[slot];
            if (proxy == null)
            {
                return;
            }

            proxy.SetHitEnabled(
                IsEmpty(slot)
                && !GroundSlotTopology.IsAvatarReserved(slot)
                && IsAvatarOrthogonalBattleSlot(slot));
        }

        private bool TryGetAnchor(int slot, out Transform anchor)
        {
            anchor = null;
            if (!IsValidSlot(slot))
            {
                return false;
            }

            var index = CardSlotAnchorUtility.SlotToAnchorIndex(slot);
            if (index < 0 || index >= _groundAnchors.Count)
            {
                return false;
            }

            anchor = _groundAnchors[index];
            return anchor != null;
        }

        private static bool IsValidSlot(int slot)
        {
            return GroundSlotTopology.IsValidSlot(slot);
        }

        private void ResolveSceneReferences()
        {
            if (groundAnchorsRoot != null)
            {
                return;
            }

            var anchors = GameObject.Find("Anchors");
            if (anchors != null)
            {
                groundAnchorsRoot = anchors.transform.Find("GroundAnchors");
            }
        }

        private void ResolveSkeletonDeckPresentation()
        {
            if (skeletonDeckPresentation != null)
            {
                return;
            }

            skeletonDeckPresentation = SkeletonDeckPresentationManager.TryGetInstance();
        }

        private void CacheAnchors()
        {
            _groundAnchors.Clear();
            _groundAnchors.AddRange(CardSlotAnchorUtility.GetSortedSlotTransforms(groundAnchorsRoot, GroundSlotTopology.MaxSlot));
        }
    }
}
