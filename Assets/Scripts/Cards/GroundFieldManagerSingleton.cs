using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地卡管理器单例：9 格占用权威、放置申请、外圈旋转表演、方位查询与空槽点击。
    /// 基础交战编排已抽至 <see cref="FieldBattleManagerSingleton"/>。
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

        private readonly int[] _uidBySlot = new int[GroundSlotTopology.MaxSlot + 1];
        private readonly Dictionary<int, int> _slotByUid = new();
        private readonly List<Transform> _groundAnchors = new();
        private readonly GroundSlotHitProxy[] _slotHitProxies = new GroundSlotHitProxy[GroundSlotTopology.MaxSlot + 1];
        private GroundEmptySlotExploreRunner _exploreRunner;
        private bool _isBusy;
        private CancellationTokenSource _fieldAnimCts;

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

        /// <summary>
        /// 场地自身忙碌，或交战管理器忙碌（保持原有 IsBusy 语义，供 hover/点击门禁使用）。
        /// </summary>
        public bool IsBusy
        {
            get
            {
                var battle = FieldBattleManagerSingleton.Instance;
                return _isBusy || (battle != null && battle.IsBusy);
            }
        }

        /// <summary>
        /// 仅场地自身忙碌（不含交战），供交战管理器判断是否可开打。
        /// </summary>
        public bool IsFieldBusy => _isBusy;

        public GroundFieldLayoutSettings LayoutSettings => layoutSettings;

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
            _exploreRunner = new GroundEmptySlotExploreRunner(this, this.GetCancellationTokenOnDestroy());
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

            return CardManagerSingleton.Instance.TryGet(_uidBySlot[slot], out card);
        }

        public bool TryGetSlotOf(int uid, out int slot)
        {
            return _slotByUid.TryGetValue(uid, out slot);
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
            return CardManagerSingleton.Instance.TryGet(uid, out card);
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

            RegisterCardAtSlot(slot, card.Uid);
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
                CardDeckTween.KillMotion(card.Transform);
                card.Transform.position = anchor.position;
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
                    CardDeckTween.KillMotion(same.Transform);
                    same.Transform.position = sameAnchor.position;
                }

                return true;
            }

            if (!IsEmpty(toSlot))
            {
                Debug.LogWarning($"[GroundFieldManager] 目标格已占用，无法迁移 uid={uid} → slot={toSlot}");
                return false;
            }

            UnregisterCardAtSlot(fromSlot);
            RegisterCardAtSlot(toSlot, uid);
            RefreshSlotHitCollider(fromSlot);
            RefreshSlotHitCollider(toSlot);

            if (snapToAnchor
                && CardManagerSingleton.Instance.TryGet(uid, out var card)
                && card?.Transform != null
                && TryGetAnchor(toSlot, out var anchor)
                && anchor != null)
            {
                CardDeckTween.KillMotion(card.Transform);
                card.Transform.position = anchor.position;
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

            RegisterCardAtSlot(slot, avatar.Uid);
            var cardManager = CardManagerSingleton.Instance;
            cardManager.SetDisplayMode(avatar, CardDisplayMode.GroundCardMode);
            avatar.Transform.position = anchor.position;
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
            if (!CardManagerSingleton.Instance.TryGet(uid, out var card) || card?.Transform == null)
            {
                return false;
            }

            UnregisterCardAtSlot(fromSlot);
            RegisterCardAtSlot(toSlot, uid);

            if (animate)
            {
                MoveCardAnimatedAsync(card, fromSlot, toSlot, EnsureFieldAnimToken()).Forget();
            }
            else if (TryGetAnchor(toSlot, out var anchor))
            {
                card.Transform.position = anchor.position;
                CardManagerSingleton.Instance.RefreshDisplayMode(card);
            }

            RefreshSlotHitCollider(fromSlot);
            RefreshSlotHitCollider(toSlot);
            return true;
        }

        /// <summary>
        /// 将卡从场地表移除但不销毁视图，供手牌管理器接管。
        /// </summary>
        public bool TryTakeCardFromField(int uid, out ManagedCard card, bool startExplore = false)
        {
            card = null;
            if (IsBusy)
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
                UnregisterCardAtSlot(slot);
                RefreshSlotHitCollider(slot);
                if (startExplore)
                {
                    _exploreRunner?.StartExplore(slot);
                }

                return false;
            }

            VacateSlotForExplore(slot, card, playRemoveAnim: false, skipBusyGuard: false, startExplore: startExplore);
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
                UnregisterCardAtSlot(slot);
                RefreshSlotHitCollider(slot);
                if (startExplore)
                {
                    _exploreRunner?.StartExplore(slot);
                }

                return false;
            }

            VacateSlotForExplore(slot, card, animate, skipBusyGuard, startExplore);
            if (!animate)
            {
                CardManagerSingleton.Instance.Release(uid);
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
                _exploreRunner?.StartExplore(slot);
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

            RegisterCardAtSlot(slot, card.Uid);
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

            _exploreRunner?.CancelAll();

            // 强制清场时只清占格表，卡视图交给外层 ReleaseAll，避免与手牌/卡组重复 Release。
            if (!force)
            {
                var cardManager = CardManagerSingleton.TryGetInstance();
                if (cardManager != null)
                {
                    for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
                    {
                        var uid = _uidBySlot[slot];
                        if (uid != 0)
                        {
                            cardManager.Release(uid);
                        }
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

            Debug.Log($"[GroundFieldManager] 空槽点击 → Core ClickEmpty: slot={slot}");
            EmptySlotClicked?.Invoke(slot);

            var postKill = CombatHitSink.RequestClickEmpty(slot);
            if (!postKill.Accepted)
            {
                return false;
            }

            CombatHitSink.RequestDrainPostKillBoard(postKill).Forget();
            return true;
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

        public Transform GetGroundAnchor(int slot)
        {
            return TryGetAnchor(slot, out var anchor) ? anchor : null;
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

            if (!skipBusyGuard)
            {
                _isBusy = true;
            }

            try
            {
                var cardManager = CardManagerSingleton.Instance;
                var hopPlans = new List<(ManagedCard card, int fromSlot, int toSlot)>(moves.Count);

                // 先收集可播 hop 的计划（死者已 Vacate 的 uid 跳过）。
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

                    if (!cardManager.TryGet(move.Uid, out var card) || card?.Transform == null)
                    {
                        continue;
                    }

                    // 表现侧已无此 uid 占格（击杀 Vacate）→ 跳过。
                    if (!_slotByUid.ContainsKey(move.Uid))
                    {
                        continue;
                    }

                    hopPlans.Add((card, move.FromSlot, move.ToSlot));
                }

                if (hopPlans.Count == 0)
                {
                    return;
                }

                // 原子换格：先清本批所有占格，再登记目标，避免环上目标格仍被旧卡占用。
                for (var i = 0; i < hopPlans.Count; i++)
                {
                    var uid = hopPlans[i].card.Uid;
                    if (_slotByUid.TryGetValue(uid, out var occupied))
                    {
                        UnregisterCardAtSlot(occupied);
                    }
                }

                for (var i = 0; i < hopPlans.Count; i++)
                {
                    var plan = hopPlans[i];
                    RegisterCardAtSlot(plan.toSlot, plan.card.Uid);
                }

                var moveTasks = new List<UniTask>(hopPlans.Count);
                for (var i = 0; i < hopPlans.Count; i++)
                {
                    var plan = hopPlans[i];
                    moveTasks.Add(
                        AnimateCardHopToSlotAsync(plan.card, plan.fromSlot, plan.toSlot, cancellationToken));
                }

                await UniTask.WhenAll(moveTasks);
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

            try
            {
                var ring = GroundSlotTopology.ClockwiseRing;
                var uids = new int[ring.Count];
                for (var i = 0; i < ring.Count; i++)
                {
                    uids[i] = _uidBySlot[ring[i]];
                }

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
                    RegisterCardAtSlot(toSlot, uids[i]);
                }

                _exploreRunner?.OnRingShifted(clockwise);

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
                        continue;
                    }

                    if (!_slotByUid.TryGetValue(uid, out var toSlot))
                    {
                        continue;
                    }

                    var fromSlot = ring[i];
                    moveTasks.Add(AnimateCardHopToSlotAsync(card, fromSlot, toSlot, cancellationToken));
                }

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
            }
            finally
            {
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
                || !TryGetAnchor(fromSlot, out var fromAnchor)
                || !TryGetAnchor(toSlot, out var toAnchor))
            {
                return;
            }

            CardManagerSingleton.Instance.RefreshDisplayMode(card);
            var start = fromAnchor.position;
            var end = toAnchor.position;
            var mid = ComputeHopMidpoint(start, end);

            await CardDeckTween.MoveHopToWorldAsync(
                card.Transform,
                start,
                mid,
                end,
                layoutSettings.moveDuration,
                layoutSettings.hopPeakScaleIntensity,
                layoutSettings.hopLandScaleIntensity,
                cancellationToken,
                onComplete: () => CardManagerSingleton.Instance.RefreshDisplayMode(card));
        }

        private Vector3 ComputeHopMidpoint(Vector3 start, Vector3 end)
        {
            var linearMid = Vector3.Lerp(start, end, 0.5f);
            if (layoutSettings.hopArcHeight <= 0f)
            {
                return linearMid;
            }

            return linearMid + Vector3.up * layoutSettings.hopArcHeight;
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
                        CardManagerSingleton.TryGetInstance()?.Release(card.Uid);
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

                CardManagerSingleton.TryGetInstance()?.Release(card.Uid);
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

        private void RegisterCardAtSlot(int slot, int uid)
        {
            var previousUid = _uidBySlot[slot];
            if (previousUid != 0 && previousUid != uid)
            {
                _slotByUid.Remove(previousUid);
            }

            if (_slotByUid.TryGetValue(uid, out var previousSlot) && previousSlot != slot)
            {
                _uidBySlot[previousSlot] = 0;
            }

            _uidBySlot[slot] = uid;
            _slotByUid[uid] = slot;
        }

        private void UnregisterCardAtSlot(int slot)
        {
            var uid = _uidBySlot[slot];
            if (uid != 0)
            {
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

        private void CacheAnchors()
        {
            _groundAnchors.Clear();
            _groundAnchors.AddRange(CardSlotAnchorUtility.GetSortedSlotTransforms(groundAnchorsRoot, GroundSlotTopology.MaxSlot));
        }
    }
}
