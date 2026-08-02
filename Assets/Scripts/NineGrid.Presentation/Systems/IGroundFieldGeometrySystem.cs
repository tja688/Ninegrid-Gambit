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
    /// 场地几何注册与收敛的单一 QF 所有者。
    /// 逻辑占格权威仍在 Core BoardModel；本 System 持有几何镜像与运动执行。
    /// 读取走 Query，写入走 Place/Vacate/Relocate/Clear Command；导演经封闭 <see cref="GroundPresentation"/>。
    /// </summary>
    public interface IGroundFieldGeometrySystem : ISystem
    {
        bool IsBound { get; }

        void Bind(IGroundFieldView view);

        void Unbind();

        void UnbindIfView(IGroundFieldView view);

        bool IsBusy { get; }

        bool IsFieldBusy { get; }

        int ActiveDealFlightCount { get; }

        bool TryGetCardAt(int slot, out ManagedCard card);

        bool TryGetSlotOf(int uid, out int slot);

        bool IsEmpty(int slot);

        bool IsPlaceable(int slot);

        bool IsDealInFlight(int uid);

        /// <summary>移除前取消该 uid 的补牌飞牌（同步卸 ActiveCount + EndChoreo）。</summary>
        bool CancelDealFlightForUid(int uid, string reason = null);

        bool RequestPlaceCard(int slot, ManagedCard card, bool skipBusyGuard = false);

        bool RequestPlaceCardAtAnchor(
            int slot,
            ManagedCard card,
            bool skipBusyGuard = false,
            bool snapToAnchor = true,
            float convergeSourceTime = 0.2f);

        bool RequestRelocateOccupancy(
            int uid,
            int toSlot,
            bool snapToAnchor,
            bool skipBusyGuard = false);

        bool ClearSlotOccupancy(int slot, bool skipBusyGuard = false);

        bool TryClearOccupancyForUid(int uid, bool skipBusyGuard = false);

        void ClearField(bool force = false);

        GroundFieldSnapshot GetSnapshot();

        bool HasOccupancyConflictSinceClear { get; }

        void ClearOccupancyConflictFlag();

        bool ConsumeOccupancyConflictFlag();

        UniTask ApplyBoardMovesAndHopAsync(
            IReadOnlyList<PostKillCardMove> moves,
            CancellationToken cancellationToken = default,
            bool skipBusyGuard = false,
            CommitmentKind commitment = CommitmentKind.Sync);

        UniTask RequestRevealAvatarAsync(ManagedCard avatar, CancellationToken cancellationToken = default);

        /// <summary>Avatar 邻格 hop：更新表现占格并播放旋转同款跳跃（允许落点含格5）。</summary>
        UniTask HopAvatarToSlotAsync(
            int fromSlot,
            int toSlot,
            CancellationToken cancellationToken = default);

        UniTask RotateOuterRingClockwiseAsync(CancellationToken cancellationToken = default);

        UniTask RotateOuterRingWhileBusyAsync(bool clockwise, CancellationToken cancellationToken = default);

        bool RequestMoveCard(int fromSlot, int toSlot, bool animate);

        bool TryTakeCardFromField(
            int uid,
            out ManagedCard card,
            bool startExplore = false,
            bool skipBusyGuard = false);

        bool RequestRemoveFromField(
            int uid,
            bool animate,
            bool skipBusyGuard = false,
            bool startExplore = true);

        void VacateSlotForExplore(
            int slot,
            ManagedCard card,
            bool playRemoveAnim = false,
            bool skipBusyGuard = false,
            bool startExplore = false);

        GroundFieldLayoutSettings LayoutSettings { get; }

        UniTask WaitAllActiveDealFlightsAsync(CancellationToken cancellationToken);

        void RefreshSlotHitColliders();

        bool TryHandleEmptySlotClick(int slot);

        bool TryGetRandomOccupiedCard(out ManagedCard card);

        IReadOnlyList<int> GetEmptyPlaceableSlots();

        bool HasFullOpeningRing();

        bool TryFindOccupiedSlotForUid(int uid, out int slot);

        bool IsAvatarOrthogonalBattleSlot(int slot);

        bool TryGetRandomAvatarOrthogonalMonsterSlot(out int slot, out ManagedCard card);

        Transform GetGroundAnchor(int slot);

        DealFlightHandle LaunchDrainDealFlight(
            ManagedCard card,
            int targetSlot,
            Vector3 launchPos,
            DealFlightContext context);

        UniTask PresentSkeletonFusionAsync(
            SkeletonFusionPresentationRequest request,
            System.Func<CancellationToken, UniTask> onFusionStarted,
            CancellationToken cancellationToken = default);

        /// <summary>测试 / 诊断：直接登记几何占格（冲突显式失败）。</summary>
        bool TryRegisterCardAtSlot(int slot, int uid, bool logConflict = true);
    }
}
