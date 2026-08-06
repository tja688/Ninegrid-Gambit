using System;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// Presentation pacing for Core-owned board stabilization. Each refill slice is
    /// resolved, presented, and acknowledged before Core is asked whether another is due.
    /// </summary>
    public sealed class BoardStabilizationScheduler
    {
        public void Append(
            BattleTimeline timeline,
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel boardPresentChannel,
            int boardSlot,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected,
            Action<BattleTimeline> onStable = null)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException("timeline");
            }

            if (architecture == null)
            {
                throw new ArgumentNullException("architecture");
            }

            if (dispatcher == null)
            {
                throw new ArgumentNullException("dispatcher");
            }

            if (boardPresentChannel == null)
            {
                throw new ArgumentNullException("boardPresentChannel");
            }

            new Loop(
                architecture,
                dispatcher,
                boardPresentChannel,
                boardSlot,
                onBoardBatchProjected,
                onStable).EnqueueCheck(timeline);
        }

        private sealed class Loop
        {
            private const int MaxSlices = 64;
            private readonly IArchitecture mArchitecture;
            private readonly CoreCommandDispatcher mDispatcher;
            private readonly IPresentChannel mBoardPresentChannel;
            private readonly int mBoardSlot;
            private readonly Action<int, int, PostKillBoardPresentationResult> mOnBoardBatchProjected;
            private readonly Action<BattleTimeline> mOnStable;
            private int mSlices;

            public Loop(
                IArchitecture architecture,
                CoreCommandDispatcher dispatcher,
                IPresentChannel boardPresentChannel,
                int boardSlot,
                Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected,
                Action<BattleTimeline> onStable)
            {
                mArchitecture = architecture;
                mDispatcher = dispatcher;
                mBoardPresentChannel = boardPresentChannel;
                mBoardSlot = boardSlot;
                mOnBoardBatchProjected = onBoardBatchProjected;
                mOnStable = onStable;
            }

            public void EnqueueCheck(BattleTimeline timeline)
            {
                timeline.Enqueue(new TimelineBranchStep(
                    timeline,
                    () => mArchitecture.GetSystem<IBoardStabilizationSystem>().NeedsRefill,
                    EnqueueSlice,
                    Complete));
            }

            private void EnqueueSlice(BattleTimeline timeline)
            {
                if (mSlices++ >= MaxSlices)
                {
                    throw new InvalidOperationException("Board stabilization exceeded the presentation slice budget.");
                }

                var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
                var gate = PresentationSyncBatchGate.FromSync(
                    sync,
                    ResolveAndProject,
                    slice: "BoardStabilization");
                timeline.Enqueue(new ResolveBatchStep(gate));
                timeline.Enqueue(new PresentStep(
                    gate,
                    mBoardPresentChannel,
                    channelName: "BoardStabilization",
                    choreoKind: "Refill"));
                EnqueueCheck(timeline);
            }

            private void Complete(BattleTimeline timeline)
            {
                mArchitecture.GetSystem<IBoardStabilizationSystem>().Complete();
                if (mOnStable != null)
                {
                    mOnStable(timeline);
                }
            }

            private CoreCommandDispatchResult ResolveAndProject()
            {
                var pipeline = mArchitecture.GetSystem<IActionPipelineSystem>();
                var startIndex = pipeline.EventLog.Entries.Count;
                var dispatch = mDispatcher.Send(new ResolveBoardStabilizationCommand());
                if (dispatch == null || !dispatch.Accepted)
                {
                    return dispatch;
                }

                if (mOnBoardBatchProjected != null)
                {
                    mOnBoardBatchProjected(
                        startIndex,
                        mBoardSlot,
                        IntentBatchProjection.Build(mArchitecture, pipeline, startIndex));
                }

                return dispatch;
            }
        }
    }
}
