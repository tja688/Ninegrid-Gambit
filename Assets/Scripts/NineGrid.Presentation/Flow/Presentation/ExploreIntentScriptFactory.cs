using System;
using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 空格 explore 剧本：ClickEmpty → Present → Fill → Present → Rotate → Present →（融合）Refill → Present。
    /// 非 explore kind 不入队（由 RoutingIntentScriptFactory 分发）。
    /// </summary>
    public sealed class ExploreIntentScriptFactory : IIntentScriptFactory
    {
        private readonly IArchitecture mArchitecture;
        private readonly CoreCommandDispatcher mDispatcher;
        private readonly IPresentChannel mPresentChannel;
        private readonly Action<int, int, PostKillBoardPresentationResult> mOnBatchProjected;
        private readonly FusionRefillScheduler mFusionRefill;
        private bool mLastRotateHadFusion;
        private readonly List<int> mFusionExcludeResultUids = new List<int>(2);

        public ExploreIntentScriptFactory(
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel presentChannel,
            Action<int, int, PostKillBoardPresentationResult> onBatchProjected = null,
            FusionRefillScheduler fusionRefill = null)
        {
            if (architecture == null)
            {
                throw new ArgumentNullException("architecture");
            }

            if (dispatcher == null)
            {
                throw new ArgumentNullException("dispatcher");
            }

            if (presentChannel == null)
            {
                throw new ArgumentNullException("presentChannel");
            }

            mArchitecture = architecture;
            mDispatcher = dispatcher;
            mPresentChannel = presentChannel;
            mOnBatchProjected = onBatchProjected;
            mFusionRefill = fusionRefill ?? new FusionRefillScheduler();
        }

        public void BuildScript(InputIntent intent, BattleTimeline timeline)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException("timeline");
            }

            if (!string.Equals(intent.Kind, InputIntentKinds.Explore, StringComparison.Ordinal))
            {
                return;
            }

            var slotIndex = intent.TargetId;
            var slot = SlotId.Board(slotIndex);
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            mLastRotateHadFusion = false;
            mFusionExcludeResultUids.Clear();

            var clickGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(slotIndex, () => mDispatcher.Send(new ClickEmptyCommand(slot)), trackFusion: false));
            var fillGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(slotIndex, () => mDispatcher.Send(new ResolvePostKillFillCommand()), trackFusion: false));
            var rotateGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(slotIndex, () => mDispatcher.Send(new ResolvePostKillRotateCommand()), trackFusion: true));

            timeline.Enqueue(new ResolveBatchStep(clickGate));
            timeline.Enqueue(new PresentStep(clickGate, mPresentChannel));
            timeline.Enqueue(new ResolveBatchStep(fillGate));
            timeline.Enqueue(new PresentStep(fillGate, mPresentChannel));
            timeline.Enqueue(new ResolveBatchStep(rotateGate));
            timeline.Enqueue(new PresentStep(rotateGate, mPresentChannel));
            mFusionRefill.AppendAfterRotatePresent(
                timeline,
                () => mLastRotateHadFusion,
                t => mFusionRefill.EnqueueRefillBatches(
                    t,
                    mArchitecture,
                    mDispatcher,
                    mPresentChannel,
                    slotIndex,
                    mFusionExcludeResultUids,
                    mOnBatchProjected));
        }

        private CoreCommandDispatchResult ResolveAndProject(
            int boardSlot,
            Func<CoreCommandDispatchResult> resolve,
            bool trackFusion)
        {
            var pipeline = mArchitecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var dispatch = resolve();
            if (dispatch == null || !dispatch.Accepted)
            {
                return dispatch;
            }

            if (trackFusion)
            {
                mLastRotateHadFusion = FusionRefillPlanner.TryCollectResultUids(
                    pipeline.EventLog.Entries,
                    startIndex,
                    mFusionExcludeResultUids);
            }

            if (mOnBatchProjected != null)
            {
                mOnBatchProjected(startIndex, boardSlot, IntentBatchProjection.Build(mArchitecture, pipeline, startIndex));
            }

            return dispatch;
        }
    }
}
