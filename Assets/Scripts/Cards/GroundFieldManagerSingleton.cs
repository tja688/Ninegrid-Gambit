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

        [Header("Battle Presentation")]
        [Tooltip("CardAttack 节点上的基础交战适配器；留空时 Awake 在子节点 Performance/CardAttack 自动查找。")]
        [SerializeField] private CardAttackBasicAdapter attackAdapter;

        private readonly int[] _uidBySlot = new int[GroundSlotTopology.MaxSlot + 1];
        private readonly Dictionary<int, int> _slotByUid = new();
        private readonly List<Transform> _groundAnchors = new();
        private readonly GroundSlotHitProxy[] _slotHitProxies = new GroundSlotHitProxy[GroundSlotTopology.MaxSlot + 1];
        private GroundEmptySlotExploreRunner _exploreRunner;
        private bool _isBusy;

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

        public bool IsBusy => _isBusy;

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
            ResolveAttackAdapter();
            CacheAnchors();
            EnsureSlotHitProxies();
            RefreshAllSlotHitColliders();
            _exploreRunner = new GroundEmptySlotExploreRunner(this, this.GetCancellationTokenOnDestroy());
        }

        private void OnDestroy()
        {
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

        public bool RequestPlaceCard(int slot, ManagedCard card)
        {
            if (_isBusy)
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
            if (_isBusy)
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
            if (_isBusy)
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
                MoveCardAnimatedAsync(card, fromSlot, toSlot, CancellationToken.None).Forget();
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
        public bool TryTakeCardFromField(int uid, out ManagedCard card)
        {
            card = null;
            if (_isBusy)
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
                _exploreRunner?.StartExplore(slot);
                return false;
            }

            VacateSlotForExplore(slot, card, playRemoveAnim: false);
            return true;
        }

        public bool RequestRemoveFromField(int uid, bool animate)
        {
            if (_isBusy)
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
                _exploreRunner?.StartExplore(slot);
                return false;
            }

            VacateSlotForExplore(slot, card, animate);
            if (!animate)
            {
                CardManagerSingleton.Instance.Release(uid);
            }

            return true;
        }

        internal void VacateSlotForExplore(int slot, ManagedCard card, bool playRemoveAnim, bool skipBusyGuard = false)
        {
            if (!skipBusyGuard && _isBusy)
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
            _exploreRunner?.StartExplore(slot);

            if (card == null)
            {
                return;
            }

            if (playRemoveAnim)
            {
                RemoveCardAnimatedAsync(card, slot, CancellationToken.None).Forget();
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

        public void ClearField()
        {
            if (_isBusy)
            {
                Debug.LogWarning("[GroundFieldManager] 当前忙碌，无法清场。");
                return;
            }

            _exploreRunner?.CancelAll();

            var cardManager = CardManagerSingleton.Instance;
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                var uid = _uidBySlot[slot];
                if (uid != 0)
                {
                    cardManager.Release(uid);
                }
            }

            ClearSlotTable();
            RefreshAllSlotHitColliders();
        }

        public void OnEmptySlotClicked(int slot)
        {
            TryHandleEmptySlotClick(slot);
        }

        public bool TryHandleEmptySlotClick(int slot)
        {
            if (_isBusy)
            {
                return false;
            }

            if (!IsEmpty(slot) || !IsAvatarOrthogonalBattleSlot(slot))
            {
                return false;
            }

            Debug.Log($"[GroundFieldManager] 空槽点击旋转: slot={slot}");
            EmptySlotClicked?.Invoke(slot);
            RotateOuterRingClockwiseAsync().Forget();
            return true;
        }

        public CardAttackBasicAdapter AttackAdapter => attackAdapter;

        public bool IsAvatarOrthogonalBattleSlot(int slot)
        {
            return GroundSlotTopology.AreOrthogonal(slot, GroundSlotTopology.AvatarReservedSlot);
        }

        public void ArmNextLethalAttack(bool armed = true)
        {
            attackAdapter?.ArmNextLethalAttack(armed);
        }

        public bool TryHandleBattleClick(ManagedCard card)
        {
            if (card == null || _isBusy || attackAdapter == null)
            {
                return false;
            }

            if (card.IsFieldDead)
            {
                return false;
            }

            if (!TryGetSlotOf(card.Uid, out var slot) || !IsAvatarOrthogonalBattleSlot(slot))
            {
                return false;
            }

            RequestBasicAttackAtSlotAsync(slot).Forget();
            return true;
        }

        public UniTask RequestBasicAttackAtSlotAsync(
            int victimSlot,
            bool? lethalOverride = null,
            CancellationToken cancellationToken = default)
        {
            return RequestBasicAttackInternalAsync(victimSlot, lethalOverride, cancellationToken);
        }

        public UniTask RotateOuterRingClockwiseAsync(CancellationToken cancellationToken = default)
        {
            return RotateOuterRingInternalAsync(true, cancellationToken);
        }

        public Transform GetGroundAnchor(int slot)
        {
            return TryGetAnchor(slot, out var anchor) ? anchor : null;
        }

        private async UniTask RotateOuterRingInternalAsync(
            bool clockwise,
            CancellationToken cancellationToken,
            bool skipBusyGuard = false)
        {
            if (!skipBusyGuard && _isBusy)
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
            if (card?.Transform == null)
            {
                CardManagerSingleton.Instance.Release(card.Uid);
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

            CardManagerSingleton.Instance.Release(card.Uid);
        }

        /// <summary>
        /// 即死交战已在 rig 时间轴内触发死亡特效，此处仅等待播完再 Release，避免重复播死亡或 Refresh 诈尸。
        /// </summary>
        private async UniTask FinalizeLethalVictimAsync(ManagedCard card, CancellationToken cancellationToken)
        {
            if (card == null)
            {
                return;
            }

            if (card.TryGetEffectManager(out var effectManager))
            {
                await WaitForEffectIdleAsync(effectManager, cancellationToken);
            }
            else if (card.Transform != null)
            {
                var initialScale = card.Transform.localScale;
                await RunViewTweenAsync(
                    CardViewTween.ScaleDisappear(
                        card.Transform,
                        initialScale,
                        layoutSettings.removeDisappearDuration),
                    cancellationToken);
            }

            if (card.Transform != null)
            {
                CardManagerSingleton.Instance.Release(card.Uid);
            }
        }

        private static async UniTask WaitForEffectIdleAsync(
            CardEffectManager effectManager,
            CancellationToken cancellationToken)
        {
            const float startupGraceSeconds = 0.15f;
            var deadline = Time.time + startupGraceSeconds;
            while (!effectManager.IsPlaying && Time.time < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            while (effectManager.IsPlaying)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
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

        private async UniTask RequestBasicAttackInternalAsync(
            int victimSlot,
            bool? lethalOverride,
            CancellationToken cancellationToken)
        {
            if (_isBusy)
            {
                Debug.LogWarning("[GroundFieldManager] 当前忙碌，无法触发交战。");
                return;
            }

            if (attackAdapter == null)
            {
                Debug.LogWarning("[GroundFieldManager] 未配置 CardAttackBasicAdapter。");
                return;
            }

            if (!IsAvatarOrthogonalBattleSlot(victimSlot) || !TryGetCardAt(victimSlot, out var victim))
            {
                Debug.LogWarning($"[GroundFieldManager] 格位 {victimSlot} 不可触发 Avatar 四向交战。");
                return;
            }

            if (victim.IsFieldDead)
            {
                Debug.LogWarning($"[GroundFieldManager] 格位 {victimSlot} 卡牌已死亡。");
                return;
            }

            var lethal = lethalOverride ?? attackAdapter.ConsumeNextLethalArmed();
            _isBusy = true;
            try
            {
                await attackAdapter.PlayBasicAttackAsync(victim, lethal, cancellationToken);
                if (lethal)
                {
                    CardManagerSingleton.Instance.MarkFieldDead(victim);
                    VacateSlotForExplore(victimSlot, victim, playRemoveAnim: false, skipBusyGuard: true);
                    FinalizeLethalVictimAsync(victim, cancellationToken).Forget();
                }

                await RotateOuterRingInternalAsync(true, cancellationToken, skipBusyGuard: true);
            }
            finally
            {
                _isBusy = false;
            }
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

        private void ResolveAttackAdapter()
        {
            if (attackAdapter != null)
            {
                return;
            }

            attackAdapter = GetComponentInChildren<CardAttackBasicAdapter>(true);
        }

        private void CacheAnchors()
        {
            _groundAnchors.Clear();
            _groundAnchors.AddRange(CardSlotAnchorUtility.GetSortedSlotTransforms(groundAnchorsRoot, GroundSlotTopology.MaxSlot));
        }
    }
}
