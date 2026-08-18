using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards.Convergence;
using NineGrid.Core;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地场景视图适配：锚点、布局、HitProxy、骷髅 Fusion。
    /// 占格 / busy / Clock / 飞牌 / 运动由 <see cref="GroundFieldGeometrySystem"/> 持有。
    /// </summary>
    public sealed class GroundFieldView : MonoBehaviour, IGroundFieldViewBinding
    {
        [Header("Scene Anchors")]
        [Tooltip("场景 Anchors/GroundAnchors。留空时 Awake 按名称 GroundAnchors 查找。")]
        [SerializeField] private Transform groundAnchorsRoot;

        [Header("Layout")]
        [Tooltip("场地布局与动效参数。")]
        [SerializeField] private GroundFieldLayoutSettings layoutSettings = new();

        [Header("Deck Presentation")]
        [Tooltip("骷髅军团牌组专用表现管理器。留空时 Awake 在场景中查找 SkeletonDeckPresentationManager。")]
        [SerializeField] private SkeletonDeckPresentationManager skeletonDeckPresentation;

        private readonly List<Transform> _groundAnchors = new();
        private readonly BoxCollider2D[] _slotHitColliders = new BoxCollider2D[GroundSlotTopology.MaxSlot + 1];
        private GroundFieldHitSurface _fieldHitSurface;
        private GroundFieldGeometrySystem _owner;

        public event Action<int> EmptySlotClicked;

        public event Action FieldMaybeClearSignal;

        public GroundFieldLayoutSettings LayoutSettings => layoutSettings;

        public CancellationToken DestroyToken => this.GetCancellationTokenOnDestroy();

        public GroundFieldGeometrySystem GeometryOwnerOrNull => _owner;

        public bool IsBusy => Geometry != null && Geometry.IsBusy;

        public bool IsFieldBusy => Geometry != null && Geometry.IsFieldBusy;

        public int ActiveDealFlightCount => Geometry != null ? Geometry.ActiveDealFlightCount : 0;

        public bool HasOccupancyConflictSinceClear =>
            Geometry != null && Geometry.HasOccupancyConflictSinceClear;

        private IGroundFieldGeometrySystem Geometry =>
            _owner
            ?? NineGridArchitecture.Interface?.GetSystem<IGroundFieldGeometrySystem>();

        public void AttachGeometryOwner(GroundFieldGeometrySystem owner)
        {
            _owner = owner;
        }

        public void NotifyFieldMaybeClear()
        {
            FieldMaybeClearSignal?.Invoke();
        }

        public void NotifyEmptySlotClicked(int slot)
        {
            EmptySlotClicked?.Invoke(slot);
        }

        public bool TryClaimSlot(int slot, SlotClaimant claimant)
        {
            return Geometry != null && Geometry.TryClaimSlot(slot, claimant);
        }

        public bool ReleaseSlotClaim(int slot, object owner)
        {
            return Geometry != null && Geometry.ReleaseSlotClaim(slot, owner);
        }

        public void ReleaseAllClaimsForOwner(object owner)
        {
            Geometry?.ReleaseAllClaimsForOwner(owner);
        }

        public bool TryGetSlotClaimant(int slot, out SlotClaimant claimant)
        {
            if (Geometry != null)
            {
                return Geometry.TryGetSlotClaimant(slot, out claimant);
            }

            claimant = null;
            return false;
        }

        private void Awake()
        {
            ResolveSceneReferences();
            CacheAnchors();
            EnsureHitProxies();
            // ADR-0023：九框恒开；合法性交 IntentIntake，禁止规则型启停。
            RefreshAllSlotHits(_ => true);
            ResolveSkeletonDeckPresentation();
            GroundFieldGeometryHook.RequestWire(this);
            ExploreInputHook.RequestWire(this);
            BoardWalkInputHook.RequestWire(this);
            AvatarBoardFacingHook.RequestWire(this);
        }

        private void OnDestroy()
        {
            _owner?.UnbindIfView(this);
        }

        public Transform GetGroundAnchor(int slot)
        {
            return TryGetAnchor(slot, out var anchor) ? anchor : null;
        }

        public bool TryGetAnchor(int slot, out Transform anchor)
        {
            anchor = null;
            if (!GroundSlotTopology.IsValidSlot(slot))
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

        public void EnsureHitProxies()
        {
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                if (!TryGetAnchor(slot, out var anchor))
                {
                    continue;
                }

                var collider = anchor.GetComponent<BoxCollider2D>();
                if (collider == null)
                {
                    collider = anchor.gameObject.AddComponent<BoxCollider2D>();
                }

                // 场景是尺寸权威（ADR-0023）：只挂接 / enable，永不写 size / offset。
                collider.isTrigger = false;
                _slotHitColliders[slot] = collider;

                // 清掉遗留逐格 Router 注册壳，避免与场地面双注册。
                var legacy = anchor.GetComponent<GroundSlotHitProxy>();
                if (legacy != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(legacy);
                    }
                    else
                    {
                        DestroyImmediate(legacy);
                    }
                }
            }

            _fieldHitSurface = GetComponent<GroundFieldHitSurface>();
            if (_fieldHitSurface == null)
            {
                _fieldHitSurface = gameObject.AddComponent<GroundFieldHitSurface>();
            }

            _fieldHitSurface.BindSlotColliders(_slotHitColliders);
        }

        /// <summary>世界点落入哪一格命中框（交棒手牌拖拽落点等，ADR-0023 / #103）。</summary>
        public bool TryResolveSlotAtWorld(Vector2 worldXY, out int slot)
        {
            if (_fieldHitSurface == null)
            {
                EnsureHitProxies();
            }

            if (_fieldHitSurface == null)
            {
                slot = 0;
                return false;
            }

            return _fieldHitSurface.TryResolveSlotAtWorld(worldXY, out slot);
        }

        public void RefreshSlotHit(int slot, bool hitEnabled)
        {
            if (!GroundSlotTopology.IsValidSlot(slot))
            {
                return;
            }

            var collider = _slotHitColliders[slot];
            if (collider == null && TryGetAnchor(slot, out var anchor))
            {
                collider = anchor.GetComponent<BoxCollider2D>();
                _slotHitColliders[slot] = collider;
            }

            if (collider != null)
            {
                collider.enabled = hitEnabled;
            }
        }

        public void RefreshAllSlotHits(Func<int, bool> hitEnabledForSlot)
        {
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                var enabled = hitEnabledForSlot != null && hitEnabledForSlot(slot);
                RefreshSlotHit(slot, enabled);
            }
        }

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

        // --- thin forwarders ---

        public GroundFieldSnapshot GetSnapshot()
        {
            return Geometry?.GetSnapshot() ?? new GroundFieldSnapshot(null, 0);
        }

        public bool TryGetCardAt(int slot, out ManagedCard card)
        {
            if (Geometry != null)
            {
                return Geometry.TryGetCardAt(slot, out card);
            }

            card = null;
            return false;
        }

        public bool TryGetSlotOf(int uid, out int slot)
        {
            if (Geometry != null)
            {
                return Geometry.TryGetSlotOf(uid, out slot);
            }

            slot = 0;
            return false;
        }

        public bool TryFindOccupiedSlotForUid(int uid, out int slot)
        {
            if (Geometry != null)
            {
                return Geometry.TryFindOccupiedSlotForUid(uid, out slot);
            }

            slot = 0;
            return false;
        }

        public bool TryClearOccupancyForUid(int uid, bool skipBusyGuard = false)
        {
            return Geometry != null && Geometry.TryClearOccupancyForUid(uid, skipBusyGuard);
        }

        public bool IsEmpty(int slot)
        {
            return Geometry != null && Geometry.IsEmpty(slot);
        }

        public bool IsPlaceable(int slot)
        {
            return Geometry != null && Geometry.IsPlaceable(slot);
        }

        public IReadOnlyList<int> GetEmptyPlaceableSlots()
        {
            return Geometry?.GetEmptyPlaceableSlots() ?? Array.Empty<int>();
        }

        public bool HasFullOpeningRing()
        {
            return Geometry != null && Geometry.HasFullOpeningRing();
        }

        public bool TryGetRandomOccupiedCard(out ManagedCard card)
        {
            if (Geometry != null)
            {
                return Geometry.TryGetRandomOccupiedCard(out card);
            }

            card = null;
            return false;
        }

        public bool RequestPlaceCard(int slot, ManagedCard card, bool skipBusyGuard = false)
        {
            return Geometry != null && Geometry.RequestPlaceCard(slot, card, skipBusyGuard);
        }

        public bool RequestPlaceCardAtAnchor(
            int slot,
            ManagedCard card,
            bool skipBusyGuard = false,
            bool snapToAnchor = true,
            float convergeSourceTime = 0.2f)
        {
            return Geometry != null
                   && Geometry.RequestPlaceCardAtAnchor(
                       slot,
                       card,
                       skipBusyGuard,
                       snapToAnchor,
                       convergeSourceTime);
        }

        public bool RequestRelocateOccupancy(
            int uid,
            int toSlot,
            bool snapToAnchor,
            bool skipBusyGuard = false)
        {
            return Geometry != null
                   && Geometry.RequestRelocateOccupancy(uid, toSlot, snapToAnchor, skipBusyGuard);
        }

        public UniTask ApplyBoardMovesAndHopAsync(
            IReadOnlyList<PostKillCardMove> moves,
            CancellationToken cancellationToken = default,
            bool skipBusyGuard = false,
            CommitmentKind commitment = CommitmentKind.Sync)
        {
            return Geometry != null
                ? Geometry.ApplyBoardMovesAndHopAsync(moves, cancellationToken, skipBusyGuard, commitment)
                : UniTask.CompletedTask;
        }

        public UniTask RequestRevealAvatarAsync(ManagedCard avatar, CancellationToken cancellationToken = default)
        {
            return Geometry != null
                ? Geometry.RequestRevealAvatarAsync(avatar, cancellationToken)
                : UniTask.CompletedTask;
        }

        public UniTask HopAvatarToSlotAsync(
            int fromSlot,
            int toSlot,
            CancellationToken cancellationToken = default)
        {
            return Geometry != null
                ? Geometry.HopAvatarToSlotAsync(fromSlot, toSlot, cancellationToken)
                : UniTask.CompletedTask;
        }

        public bool RequestMoveCard(int fromSlot, int toSlot, bool animate)
        {
            return Geometry != null && Geometry.RequestMoveCard(fromSlot, toSlot, animate);
        }

        public bool TryTakeCardFromField(
            int uid,
            out ManagedCard card,
            bool startExplore = false,
            bool skipBusyGuard = false)
        {
            if (Geometry != null)
            {
                return Geometry.TryTakeCardFromField(uid, out card, startExplore, skipBusyGuard);
            }

            card = null;
            return false;
        }

        public bool RequestRemoveFromField(
            int uid,
            bool animate,
            bool skipBusyGuard = false,
            bool startExplore = true)
        {
            return Geometry != null
                   && Geometry.RequestRemoveFromField(uid, animate, skipBusyGuard, startExplore);
        }

        internal void VacateSlotForExplore(
            int slot,
            ManagedCard card,
            bool playRemoveAnim,
            bool skipBusyGuard = false,
            bool startExplore = true)
        {
            ResolveMotion()?.VacateSlotForExplore(slot, card, playRemoveAnim, skipBusyGuard, startExplore);
        }

        internal bool PlaceForExplore(int slot, ManagedCard card)
        {
            var motion = ResolveMotion();
            return motion != null && motion.PlaceForExplore(slot, card);
        }

        internal bool TryGetExploreAnchorPosition(int slot, out Vector3 position)
        {
            var motion = ResolveMotion();
            if (motion != null)
            {
                return motion.TryGetExploreAnchorPosition(slot, out position);
            }

            position = default;
            return false;
        }

        private GroundMotionExecutor ResolveMotion()
        {
            if (_owner != null)
            {
                return _owner.Motion;
            }

            return (NineGridArchitecture.Interface?.GetSystem<IGroundFieldGeometrySystem>()
                    as GroundFieldGeometrySystem)?.Motion;
        }

        internal DealFlightHandle LaunchDrainDealFlight(
            ManagedCard card,
            int targetSlot,
            Vector3 launchPos,
            DealFlightContext context)
        {
            return Geometry?.LaunchDrainDealFlight(card, targetSlot, launchPos, context);
        }

        public bool IsDealInFlight(int uid)
        {
            return Geometry != null && Geometry.IsDealInFlight(uid);
        }

        public bool CancelDealFlightForUid(int uid, string reason = null)
        {
            return Geometry != null && Geometry.CancelDealFlightForUid(uid, reason);
        }

        public UniTask WaitAllActiveDealFlightsAsync(CancellationToken cancellationToken)
        {
            return Geometry != null
                ? Geometry.WaitAllActiveDealFlightsAsync(cancellationToken)
                : UniTask.CompletedTask;
        }

        public static async UniTask WaitDealFlightsSettledAsync(
            IReadOnlyList<DealFlightHandle> handles,
            CancellationToken cancellationToken)
        {
            await GroundMotionExecutor.WaitDealFlightsSettledAsync(handles, cancellationToken);
        }

        public void ClearField(bool force = false)
        {
            Geometry?.ClearField(force);
        }

        public void OnEmptySlotClicked(int slot)
        {
            TryHandleEmptySlotClick(slot);
        }

        public bool TryHandleEmptySlotClick(int slot)
        {
            return Geometry != null && Geometry.TryHandleEmptySlotClick(slot);
        }

        public bool ClearSlotOccupancy(int slot, bool skipBusyGuard = false)
        {
            return Geometry != null && Geometry.ClearSlotOccupancy(slot, skipBusyGuard);
        }

        public bool IsAvatarOrthogonalBattleSlot(int slot)
        {
            if (Geometry != null)
            {
                return Geometry.IsAvatarOrthogonalBattleSlot(slot);
            }

            var board = NineGridArchitecture.Current?.GetModel<BoardModel>();
            var avatarSlot = board != null && board.AvatarSlot.Value.IsBoardSlot
                ? board.AvatarSlot.Value.Index
                : GroundSlotTopology.AvatarReservedSlot;
            return GroundSlotTopology.AreOrthogonal(slot, avatarSlot);
        }

        public bool TryGetRandomAvatarOrthogonalMonsterSlot(out int slot, out ManagedCard card)
        {
            if (Geometry != null)
            {
                return Geometry.TryGetRandomAvatarOrthogonalMonsterSlot(out slot, out card);
            }

            slot = 0;
            card = null;
            return false;
        }

        public UniTask RotateOuterRingClockwiseAsync(CancellationToken cancellationToken = default)
        {
            return Geometry != null
                ? Geometry.RotateOuterRingClockwiseAsync(cancellationToken)
                : UniTask.CompletedTask;
        }

        internal UniTask RotateOuterRingClockwiseWhileBusyAsync(CancellationToken cancellationToken = default)
        {
            return RotateOuterRingWhileBusyAsync(true, cancellationToken);
        }

        public UniTask RotateOuterRingWhileBusyAsync(bool clockwise, CancellationToken cancellationToken = default)
        {
            return Geometry != null
                ? Geometry.RotateOuterRingWhileBusyAsync(clockwise, cancellationToken)
                : UniTask.CompletedTask;
        }

        public void ClearOccupancyConflictFlag()
        {
            Geometry?.ClearOccupancyConflictFlag();
        }

        public bool ConsumeOccupancyConflictFlag()
        {
            return Geometry != null && Geometry.ConsumeOccupancyConflictFlag();
        }

        public void RefreshSlotHitColliders()
        {
            Geometry?.RefreshSlotHitColliders();
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
            _groundAnchors.AddRange(
                CardSlotAnchorUtility.GetSortedSlotTransforms(groundAnchorsRoot, GroundSlotTopology.MaxSlot));
        }
    }
}
