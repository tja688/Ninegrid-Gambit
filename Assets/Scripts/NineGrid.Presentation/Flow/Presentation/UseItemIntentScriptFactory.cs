using System;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 用牌/帮助卡垂直切片：ApplyUseItem → Present → 盘面稳定化；
    /// 有击杀时在首次稳定后 Rotate，再稳定一次。
    /// 未识别 kind 不入队。
    /// </summary>
    public sealed class UseItemIntentScriptFactory : IIntentScriptFactory
    {
        private readonly IArchitecture mArchitecture;
        private readonly CoreCommandDispatcher mDispatcher;
        private readonly IPresentChannel mUsePresentChannel;
        private readonly IPresentChannel mBoardPresentChannel;
        private readonly Action<int, int, PostKillBoardPresentationResult> mOnUseBatchProjected;
        private readonly Action<int, int, PostKillBoardPresentationResult> mOnBoardBatchProjected;
        private readonly Action mOnResolvedWithoutKill;
        private readonly BoardStabilizationScheduler mStabilization;
        private bool mLastUseKilledTarget;

        public UseItemIntentScriptFactory(
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel usePresentChannel,
            IPresentChannel boardPresentChannel,
            Action<int, int, PostKillBoardPresentationResult> onUseBatchProjected = null,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected = null,
            Action onResolvedWithoutKill = null,
            BoardStabilizationScheduler stabilization = null)
        {
            if (architecture == null)
            {
                throw new ArgumentNullException("architecture");
            }

            if (dispatcher == null)
            {
                throw new ArgumentNullException("dispatcher");
            }

            if (usePresentChannel == null)
            {
                throw new ArgumentNullException("usePresentChannel");
            }

            if (boardPresentChannel == null)
            {
                throw new ArgumentNullException("boardPresentChannel");
            }

            mArchitecture = architecture;
            mDispatcher = dispatcher;
            mUsePresentChannel = usePresentChannel;
            mBoardPresentChannel = boardPresentChannel;
            mOnUseBatchProjected = onUseBatchProjected;
            mOnBoardBatchProjected = onBoardBatchProjected;
            mOnResolvedWithoutKill = onResolvedWithoutKill;
            mStabilization = stabilization ?? new BoardStabilizationScheduler();
        }

        public void BuildScript(InputIntent intent, BattleTimeline timeline)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException("timeline");
            }

            if (!string.Equals(intent.Kind, InputIntentKinds.UseItem, StringComparison.Ordinal))
            {
                return;
            }

            // ADR-0032：非战斗相位使用走 NonCombatUseItemIntentScriptFactory（无交战通道、非锁步冲刷）。
            var phase = mArchitecture.GetSystem<IPhaseSystem>();
            if (phase == null || phase.CurrentPhase != GamePhase.InteractionLoop)
            {
                return;
            }

            var itemUid = intent.TargetId;
            if (itemUid <= 0)
            {
                return;
            }

            var selected = intent.SelectedCardUids;
            var option = intent.SelectedOption;
            var boardSlot = ResolvePrimaryBoardSlot(selected);
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            mLastUseKilledTarget = false;
            var useGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveUseAndProject(boardSlot, itemUid, selected, option));
            timeline.Enqueue(new ResolveBatchStep(useGate));
            timeline.Enqueue(new PresentStep(useGate, mUsePresentChannel));
            timeline.Enqueue(new AttackPostHitBranchStep(
                timeline,
                () => mLastUseKilledTarget,
                t => EnqueueKillAftermath(t, boardSlot),
                t => EnqueueNonKillAftermath(t, boardSlot)));
        }

        private void EnqueueNonKillAftermath(BattleTimeline timeline, int boardSlot)
        {
            if (mOnResolvedWithoutKill != null)
            {
                mOnResolvedWithoutKill();
            }

            mStabilization.Append(
                timeline,
                mArchitecture,
                mDispatcher,
                mBoardPresentChannel,
                boardSlot,
                mOnBoardBatchProjected);
        }

        private void EnqueueKillAftermath(BattleTimeline timeline, int boardSlot)
        {
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();

            // ADR-0026：清关后跳过补牌/融合，只走旋转批收场（与 Attack 路径一致）。
            if (mArchitecture.GetSystem<IDeckSystem>().IsNodeCleared())
            {
                var clearGate = PresentationSyncBatchGate.FromSync(
                    sync,
                    () => ResolveAndProject(
                        boardSlot,
                        () => mDispatcher.Send(new ResolvePostKillRotateCommand()),
                        project: true),
                    slice: "LeaveTrapClear");
                timeline.Enqueue(new ResolveBatchStep(clearGate));
                timeline.Enqueue(new PresentStep(clearGate, mBoardPresentChannel));
                return;
            }

            mStabilization.Append(
                timeline,
                mArchitecture,
                mDispatcher,
                mBoardPresentChannel,
                boardSlot,
                mOnBoardBatchProjected,
                t => EnqueueRotateThenSettle(t, boardSlot));
        }

        private void EnqueueRotateThenSettle(BattleTimeline timeline, int boardSlot)
        {
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            var rotateGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(
                    boardSlot,
                    () => mDispatcher.Send(new ResolvePostKillRotateCommand()),
                    project: true));
            timeline.Enqueue(new ResolveBatchStep(rotateGate));
            timeline.Enqueue(new PresentStep(rotateGate, mBoardPresentChannel));
            mStabilization.Append(
                timeline,
                mArchitecture,
                mDispatcher,
                mBoardPresentChannel,
                boardSlot,
                mOnBoardBatchProjected);
        }

        private CoreCommandDispatchResult ResolveUseAndProject(
            int boardSlot,
            int itemUid,
            int[] selected,
            string option)
        {
            var pipeline = mArchitecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var dispatch = mDispatcher.Send(new ApplyUseItemCommand(itemUid, selected, option));
            if (dispatch == null || !dispatch.Accepted)
            {
                mLastUseKilledTarget = false;
                return dispatch;
            }

            mLastUseKilledTarget = ContainsAnyCardKilled(pipeline, startIndex);
            if (mOnUseBatchProjected != null)
            {
                mOnUseBatchProjected(startIndex, boardSlot, IntentBatchProjection.Build(mArchitecture, pipeline, startIndex));
            }

            return dispatch;
        }

        private CoreCommandDispatchResult ResolveAndProject(
            int boardSlot,
            Func<CoreCommandDispatchResult> resolve,
            bool project)
        {
            var pipeline = mArchitecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var dispatch = resolve();
            if (dispatch == null || !dispatch.Accepted)
            {
                return dispatch;
            }

            if (project && mOnBoardBatchProjected != null)
            {
                mOnBoardBatchProjected(startIndex, boardSlot, IntentBatchProjection.Build(mArchitecture, pipeline, startIndex));
            }

            return dispatch;
        }

        private static bool ContainsAnyCardKilled(IActionPipelineSystem pipeline, int startIndex)
        {
            var entries = pipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.CardKilled)
                {
                    return true;
                }
            }

            return false;
        }

        private int ResolvePrimaryBoardSlot(int[] selected)
        {
            if (selected == null || selected.Length == 0)
            {
                return 0;
            }

            var registry = mArchitecture.GetModel<CardRegistry>();
            CardInstance card;
            if (!registry.TryGet(selected[0], out card) || card == null)
            {
                return 0;
            }

            var slot = card.Slot.Value;
            return slot.IsBoardSlot ? slot.Index : 0;
        }
    }
}
