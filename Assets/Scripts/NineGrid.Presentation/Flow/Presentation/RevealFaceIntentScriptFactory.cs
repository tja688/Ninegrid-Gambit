using System;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 主动翻开剧本：RevealFace → 互动计数 → 盘面稳定化 → Rotate → 盘面稳定化 → 敌方行动。
    /// </summary>
    public sealed class RevealFaceIntentScriptFactory : IIntentScriptFactory
    {
        private readonly IArchitecture mArchitecture;
        private readonly CoreCommandDispatcher mDispatcher;
        private readonly IPresentChannel mPresentChannel;
        private readonly Action<int, int, PostKillBoardPresentationResult> mOnBatchProjected;
        private readonly Action<int, int, int, PostKillBoardPresentationResult> mOnCounterBatchProjected;
        private readonly BoardStabilizationScheduler mStabilization;
        private readonly EnemyActionPhaseScheduler mEnemyAction;
        private readonly IPresentChannel mCounterPresentChannel;

        public RevealFaceIntentScriptFactory(
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel presentChannel,
            Action<int, int, PostKillBoardPresentationResult> onBatchProjected = null,
            BoardStabilizationScheduler stabilization = null,
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
            mStabilization = stabilization ?? new BoardStabilizationScheduler();
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
            var revealGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(slotIndex, () => mDispatcher.Send(new RevealFaceCommand(slot))));
            var interactGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(
                    slotIndex,
                    () => mDispatcher.Send(new AdvanceInteractionCountCommand())),
                slice: "RevealInteractionAdvance");

            timeline.Enqueue(new ResolveBatchStep(revealGate));
            timeline.Enqueue(new PresentStep(revealGate, mPresentChannel));
            timeline.Enqueue(new ResolveBatchStep(interactGate));
            timeline.Enqueue(new PresentStep(interactGate, mPresentChannel));
            mStabilization.Append(
                timeline,
                mArchitecture,
                mDispatcher,
                mPresentChannel,
                slotIndex,
                mOnBatchProjected,
                t => EnqueueRotateThenSettle(t, slotIndex));
        }

        private void EnqueueRotateThenSettle(BattleTimeline timeline, int boardSlot)
        {
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            var rotateGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(
                    boardSlot,
                    () => mDispatcher.Send(new ResolvePostKillRotateCommand())));
            timeline.Enqueue(new ResolveBatchStep(rotateGate));
            timeline.Enqueue(new PresentStep(rotateGate, mPresentChannel));
            mStabilization.Append(
                timeline,
                mArchitecture,
                mDispatcher,
                mPresentChannel,
                boardSlot,
                mOnBatchProjected,
                t => AppendEnemyActionPhase(t, boardSlot));
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
            Func<CoreCommandDispatchResult> resolve)
        {
            var pipeline = mArchitecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var dispatch = resolve();
            if (dispatch == null || !dispatch.Accepted)
            {
                return dispatch;
            }

            if (mOnBatchProjected != null)
            {
                mOnBatchProjected(startIndex, boardSlot, IntentBatchProjection.Build(mArchitecture, pipeline, startIndex));
            }

            return dispatch;
        }
    }
}
