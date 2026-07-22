using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Cards.Convergence;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 场地表现封闭 API：导演 / Scheduler 经此执行 Rotate/Hop/Deal/Remove/Fusion。
    /// </summary>
    internal sealed class GroundPresentation
    {
        private readonly GroundMotionExecutor _motion;
        private readonly IGroundFieldView _view;

        public GroundPresentation(GroundMotionExecutor motion, IGroundFieldView view)
        {
            _motion = motion ?? throw new ArgumentNullException(nameof(motion));
            _view = view;
        }

        public GroundMotionExecutor Motion => _motion;

        public UniTask RotateOuterRingAsync(bool clockwise, CancellationToken cancellationToken = default)
        {
            return _motion.RotateOuterRingWhileBusyAsync(clockwise, cancellationToken);
        }

        public UniTask RotateOuterRingClockwiseAsync(CancellationToken cancellationToken = default)
        {
            return _motion.RotateOuterRingClockwiseAsync(cancellationToken);
        }

        public UniTask ApplyBoardMovesAndHopAsync(
            IReadOnlyList<PostKillCardMove> moves,
            CancellationToken cancellationToken = default,
            bool skipBusyGuard = false,
            CommitmentKind commitment = CommitmentKind.Sync)
        {
            return _motion.ApplyBoardMovesAndHopAsync(moves, cancellationToken, skipBusyGuard, commitment);
        }

        public UniTask RevealAvatarAsync(ManagedCard avatar, CancellationToken cancellationToken = default)
        {
            return _motion.RequestRevealAvatarAsync(avatar, cancellationToken);
        }

        public DealFlightHandle LaunchDrainDealFlight(
            ManagedCard card,
            int targetSlot,
            UnityEngine.Vector3 launchPos,
            DealFlightContext context)
        {
            return _motion.LaunchDrainDealFlight(card, targetSlot, launchPos, context);
        }

        public bool RequestRemoveFromField(
            int uid,
            bool animate,
            bool skipBusyGuard = false,
            bool startExplore = true)
        {
            return _motion.RequestRemoveFromField(uid, animate, skipBusyGuard, startExplore);
        }

        public UniTask PresentSkeletonFusionAsync(
            SkeletonFusionPresentationRequest request,
            Func<CancellationToken, UniTask> onFusionStarted,
            CancellationToken cancellationToken = default)
        {
            if (_view == null)
            {
                return UniTask.CompletedTask;
            }

            return _view.PresentSkeletonFusionAsync(request, onFusionStarted, cancellationToken);
        }

        public static UniTask WaitDealFlightsSettledAsync(
            IReadOnlyList<DealFlightHandle> handles,
            CancellationToken cancellationToken)
        {
            return GroundMotionExecutor.WaitDealFlightsSettledAsync(handles, cancellationToken);
        }
    }
}
