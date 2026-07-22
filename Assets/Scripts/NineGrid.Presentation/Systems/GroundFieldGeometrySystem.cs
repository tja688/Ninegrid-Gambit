using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 场地几何 / 飞牌 / 运动执行的权威所有者。场景 View 仅提供锚点与 HitProxy。
    /// </summary>
    public sealed class GroundFieldGeometrySystem : AbstractSystem, IGroundFieldGeometrySystem
    {
        private readonly GroundOccupancyIndex mIndex = new();
        private readonly GroundMotionExecutor mMotion;
        private IGroundFieldView mView;
        private GroundPresentation mPresentation;
        private GroundFieldManagerSingleton mBoundManager;

        public GroundFieldGeometrySystem()
        {
            mMotion = new GroundMotionExecutor(mIndex);
        }

        public bool IsBound => mView != null;

        internal GroundOccupancyIndex Occupancy => mIndex;

        internal GroundPresentation Presentation => mPresentation;

        internal GroundMotionExecutor Motion => mMotion;

        public bool IsBusy => mMotion.IsBusy;

        public bool IsFieldBusy => mMotion.IsFieldBusy;

        public int ActiveDealFlightCount => mMotion.ActiveDealFlightCount;

        public bool HasOccupancyConflictSinceClear => mMotion.HasOccupancyConflictSinceClear;

        public void Bind(IGroundFieldView view)
        {
            if (ReferenceEquals(mView, view) && view != null)
            {
                return;
            }

            UnbindInternal(clearIndex: false);
            mView = view;
            mBoundManager = view as GroundFieldManagerSingleton;
            mMotion.BindView(
                view,
                onFieldMaybeClear: () => mBoundManager?.NotifyFieldMaybeClear(),
                onEmptySlotClicked: slot => mBoundManager?.NotifyEmptySlotClicked(slot));
            mPresentation = view != null ? new GroundPresentation(mMotion, view) : null;
            mBoundManager?.AttachGeometryOwner(this);
            mIndex.ClearConflictFlag();
        }

        public void Unbind()
        {
            UnbindInternal(clearIndex: true);
        }

        public void UnbindIfView(IGroundFieldView view)
        {
            if (ReferenceEquals(mView, view))
            {
                Unbind();
            }
        }

        private void UnbindInternal(bool clearIndex)
        {
            if (mBoundManager != null && ReferenceEquals(mBoundManager.GeometryOwnerOrNull, this))
            {
                mBoundManager.AttachGeometryOwner(null);
            }

            mMotion.UnbindView();
            mPresentation = null;
            mView = null;
            mBoundManager = null;
            if (clearIndex)
            {
                mIndex.Clear();
            }
        }

        public bool TryGetCardAt(int slot, out ManagedCard card)
        {
            return mMotion.TryGetCardAt(slot, out card);
        }

        public bool TryGetSlotOf(int uid, out int slot)
        {
            return mMotion.TryGetSlotOf(uid, out slot);
        }

        public bool IsEmpty(int slot)
        {
            return mMotion.IsEmpty(slot);
        }

        public bool IsPlaceable(int slot)
        {
            return mMotion.IsPlaceable(slot);
        }

        public bool IsDealInFlight(int uid)
        {
            return mMotion.IsDealInFlight(uid);
        }

        public bool RequestPlaceCard(int slot, ManagedCard card, bool skipBusyGuard = false)
        {
            return mMotion.RequestPlaceCard(slot, card, skipBusyGuard);
        }

        public bool RequestPlaceCardAtAnchor(
            int slot,
            ManagedCard card,
            bool skipBusyGuard = false,
            bool snapToAnchor = true,
            float convergeSourceTime = 0.2f)
        {
            return mMotion.RequestPlaceCardAtAnchor(
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
            return mMotion.RequestRelocateOccupancy(uid, toSlot, snapToAnchor, skipBusyGuard);
        }

        public bool ClearSlotOccupancy(int slot, bool skipBusyGuard = false)
        {
            return mMotion.ClearSlotOccupancy(slot, skipBusyGuard);
        }

        public bool TryClearOccupancyForUid(int uid, bool skipBusyGuard = false)
        {
            return mMotion.TryClearOccupancyForUid(uid, skipBusyGuard);
        }

        public void ClearField(bool force = false)
        {
            mMotion.ClearField(force);
        }

        public GroundFieldSnapshot GetSnapshot()
        {
            return mMotion.GetSnapshot();
        }

        public void ClearOccupancyConflictFlag()
        {
            mMotion.ClearOccupancyConflictFlag();
        }

        public bool ConsumeOccupancyConflictFlag()
        {
            return mMotion.ConsumeOccupancyConflictFlag();
        }

        public UniTask ApplyBoardMovesAndHopAsync(
            IReadOnlyList<PostKillCardMove> moves,
            CancellationToken cancellationToken = default,
            bool skipBusyGuard = false,
            CommitmentKind commitment = CommitmentKind.Sync)
        {
            return mMotion.ApplyBoardMovesAndHopAsync(moves, cancellationToken, skipBusyGuard, commitment);
        }

        public UniTask RequestRevealAvatarAsync(ManagedCard avatar, CancellationToken cancellationToken = default)
        {
            return mMotion.RequestRevealAvatarAsync(avatar, cancellationToken);
        }

        public UniTask RotateOuterRingClockwiseAsync(CancellationToken cancellationToken = default)
        {
            return mMotion.RotateOuterRingClockwiseAsync(cancellationToken);
        }

        public UniTask RotateOuterRingWhileBusyAsync(bool clockwise, CancellationToken cancellationToken = default)
        {
            return mMotion.RotateOuterRingWhileBusyAsync(clockwise, cancellationToken);
        }

        public bool RequestMoveCard(int fromSlot, int toSlot, bool animate)
        {
            return mMotion.RequestMoveCard(fromSlot, toSlot, animate);
        }

        public bool TryTakeCardFromField(
            int uid,
            out ManagedCard card,
            bool startExplore = false,
            bool skipBusyGuard = false)
        {
            return mMotion.TryTakeCardFromField(uid, out card, startExplore, skipBusyGuard);
        }

        public bool RequestRemoveFromField(
            int uid,
            bool animate,
            bool skipBusyGuard = false,
            bool startExplore = true)
        {
            return mMotion.RequestRemoveFromField(uid, animate, skipBusyGuard, startExplore);
        }

        public UniTask WaitAllActiveDealFlightsAsync(CancellationToken cancellationToken)
        {
            return mMotion.WaitAllActiveDealFlightsAsync(cancellationToken);
        }

        public void RefreshSlotHitColliders()
        {
            mMotion.RefreshSlotHitColliders();
        }

        public bool TryHandleEmptySlotClick(int slot)
        {
            return mMotion.TryHandleEmptySlotClick(slot);
        }

        public bool TryGetRandomOccupiedCard(out ManagedCard card)
        {
            return mMotion.TryGetRandomOccupiedCard(out card);
        }

        public IReadOnlyList<int> GetEmptyPlaceableSlots()
        {
            return mMotion.GetEmptyPlaceableSlots();
        }

        public bool HasFullOpeningRing()
        {
            return mMotion.HasFullOpeningRing();
        }

        public bool TryFindOccupiedSlotForUid(int uid, out int slot)
        {
            return mMotion.TryFindOccupiedSlotForUid(uid, out slot);
        }

        public bool IsAvatarOrthogonalBattleSlot(int slot)
        {
            return mMotion.IsAvatarOrthogonalBattleSlot(slot);
        }

        public bool TryGetRandomAvatarOrthogonalMonsterSlot(out int slot, out ManagedCard card)
        {
            return mMotion.TryGetRandomAvatarOrthogonalMonsterSlot(out slot, out card);
        }

        public Transform GetGroundAnchor(int slot)
        {
            return mMotion.GetGroundAnchor(slot);
        }

        public DealFlightHandle LaunchDrainDealFlight(
            ManagedCard card,
            int targetSlot,
            Vector3 launchPos,
            DealFlightContext context)
        {
            return mMotion.LaunchDrainDealFlight(card, targetSlot, launchPos, context);
        }

        public UniTask PresentSkeletonFusionAsync(
            SkeletonFusionPresentationRequest request,
            Func<CancellationToken, UniTask> onFusionStarted,
            CancellationToken cancellationToken = default)
        {
            return mPresentation != null
                ? mPresentation.PresentSkeletonFusionAsync(request, onFusionStarted, cancellationToken)
                : UniTask.CompletedTask;
        }

        public bool TryRegisterCardAtSlot(int slot, int uid, bool logConflict = true)
        {
            return mIndex.TryRegister(slot, uid, logConflict);
        }

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            Unbind();
        }
    }
}
