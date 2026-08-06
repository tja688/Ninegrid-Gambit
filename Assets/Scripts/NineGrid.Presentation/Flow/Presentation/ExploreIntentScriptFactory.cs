using System;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 空格 explore 剧本：ClickEmpty → 互动计数 → 盘面稳定化 → Rotate → 盘面稳定化 → 敌方行动。
    /// 非 explore kind 不入队（由 RoutingIntentScriptFactory 分发）。
    /// </summary>
    public sealed class ExploreIntentScriptFactory : IIntentScriptFactory
    {
        private readonly IArchitecture mArchitecture;
        private readonly CoreCommandDispatcher mDispatcher;
        private readonly IPresentChannel mPresentChannel;
        private readonly Action<int, int, PostKillBoardPresentationResult> mOnBatchProjected;
        private readonly Action<int, int, int, PostKillBoardPresentationResult> mOnCounterBatchProjected;
        private readonly BoardStabilizationScheduler mStabilization;
        private readonly EnemyActionPhaseScheduler mEnemyAction;
        private readonly IPresentChannel mCounterPresentChannel;

        public ExploreIntentScriptFactory(
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

            if (!string.Equals(intent.Kind, InputIntentKinds.Explore, StringComparison.Ordinal))
            {
                return;
            }

            var slotIndex = intent.TargetId;
            var slot = SlotId.Board(slotIndex);
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            var clickGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(slotIndex, () => mDispatcher.Send(new ClickEmptyCommand(slot))));
            // 点空格属九宫格互动：计数与补牌解耦（#75）；计数须独立开批以免 OnInteract 效果漏进 Fill 窗。
            var interactGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(
                    slotIndex,
                    () => mDispatcher.Send(new AdvanceInteractionCountCommand())),
                slice: "ExploreInteractionAdvance");

            timeline.Enqueue(new ResolveBatchStep(clickGate));
            timeline.Enqueue(new PresentStep(clickGate, mPresentChannel));
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
