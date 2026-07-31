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
    /// 主动翻开剧本：RevealFace → Present → 互动计数 → Fill → Present → Rotate → Present → 敌方行动。
    /// </summary>
    public sealed class RevealFaceIntentScriptFactory : IIntentScriptFactory
    {
        private readonly IArchitecture mArchitecture;
        private readonly CoreCommandDispatcher mDispatcher;
        private readonly IPresentChannel mPresentChannel;
        private readonly Action<int, int, PostKillBoardPresentationResult> mOnBatchProjected;
        private readonly Action<int, int, int, PostKillBoardPresentationResult> mOnCounterBatchProjected;
        private readonly FusionRefillScheduler mFusionRefill;
        private readonly EnemyActionPhaseScheduler mEnemyAction;
        private readonly IPresentChannel mCounterPresentChannel;
        private bool mLastRotateHadFusion;
        private readonly List<int> mFusionExcludeResultUids = new List<int>(2);

        public RevealFaceIntentScriptFactory(
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel presentChannel,
            Action<int, int, PostKillBoardPresentationResult> onBatchProjected = null,
            FusionRefillScheduler fusionRefill = null,
            IPresentChannel counterPresentChannel = null,
            Action<int, int, int, PostKillBoardPresentationResult> onCounterBatchProjected = null,
            EnemyActionPhaseScheduler enemyAction = null)
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
            mCounterPresentChannel = counterPresentChannel;
            mOnCounterBatchProjected = onCounterBatchProjected;
            mEnemyAction = enemyAction ?? new EnemyActionPhaseScheduler();
        }

        public void BuildScript(InputIntent intent, BattleTimeline timeline)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException("timeline");
            }

            if (!string.Equals(intent.Kind, InputIntentKinds.RevealFace, StringComparison.Ordinal))
            {
                return;
            }

            var slotIndex = intent.TargetId;
            var slot = SlotId.Board(slotIndex);
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            mLastRotateHadFusion = false;
            mFusionExcludeResultUids.Clear();

            var revealGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(slotIndex, () => mDispatcher.Send(new RevealFaceCommand(slot)), trackFusion: false));
            var interactGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(
                    slotIndex,
                    () => mDispatcher.Send(new AdvanceInteractionCountCommand()),
                    trackFusion: false),
                slice: "RevealInteractionAdvance");
            var fillGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(
                    slotIndex,
                    () => mDispatcher.Send(new ResolvePostKillFillCommand()),
                    trackFusion: true));
            var rotateGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(slotIndex, () => mDispatcher.Send(new ResolvePostKillRotateCommand()), trackFusion: true));

            timeline.Enqueue(new ResolveBatchStep(revealGate));
            timeline.Enqueue(new PresentStep(revealGate, mPresentChannel));
            timeline.Enqueue(new ResolveBatchStep(interactGate));
            timeline.Enqueue(new PresentStep(interactGate, mPresentChannel));
            timeline.Enqueue(new ResolveBatchStep(fillGate));
            timeline.Enqueue(new PresentStep(fillGate, mPresentChannel));
            timeline.Enqueue(new ResolveBatchStep(rotateGate));
            timeline.Enqueue(new PresentStep(rotateGate, mPresentChannel));
            mFusionRefill.AppendAfterRotatePresent(
                timeline,
                () => mLastRotateHadFusion,
                t =>
                {
                    mFusionRefill.EnqueueRefillBatches(
                        t,
                        mArchitecture,
                        mDispatcher,
                        mPresentChannel,
                        slotIndex,
                        mFusionExcludeResultUids,
                        mOnBatchProjected);
                    AppendEnemyActionPhase(t, slotIndex);
                },
                t => AppendEnemyActionPhase(t, slotIndex));
        }

        private void AppendEnemyActionPhase(BattleTimeline timeline, int boardSlot)
        {
            mEnemyAction.Append(
                timeline,
                mArchitecture,
                mDispatcher,
                mPresentChannel,
                mCounterPresentChannel,
                mOnBatchProjected,
                mOnCounterBatchProjected,
                boardSlot);
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
                if (FusionRefillPlanner.TryCollectResultUids(
                        pipeline.EventLog.Entries,
                        startIndex,
                        mFusionExcludeResultUids,
                        clearInto: false))
                {
                    mLastRotateHadFusion = true;
                }
            }

            if (mOnBatchProjected != null)
            {
                mOnBatchProjected(startIndex, boardSlot, IntentBatchProjection.Build(mArchitecture, pipeline, startIndex));
            }

            return dispatch;
        }
    }
}
