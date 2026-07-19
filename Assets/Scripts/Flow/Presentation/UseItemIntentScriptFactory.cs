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
    /// 用牌/帮助卡垂直切片：ApplyUseItem → Present →（击杀）Fill → Present → Rotate → Present →（融合）Refill。
    /// 未击杀不入 Fill/Rotate；未识别 kind 不入队。
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
        private bool mLastUseKilledTarget;
        private bool mLastRotateHadFusion;
        private readonly List<int> mFusionExcludeResultUids = new List<int>(2);

        public UseItemIntentScriptFactory(
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel usePresentChannel,
            IPresentChannel boardPresentChannel,
            Action<int, int, PostKillBoardPresentationResult> onUseBatchProjected = null,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected = null,
            Action onResolvedWithoutKill = null)
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
            mLastRotateHadFusion = false;
            mFusionExcludeResultUids.Clear();

            var useGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveUseAndProject(boardSlot, itemUid, selected, option));
            timeline.Enqueue(new ResolveBatchStep(useGate));
            timeline.Enqueue(new PresentStep(useGate, mUsePresentChannel));
            timeline.Enqueue(new AttackPostHitBranchStep(
                timeline,
                () => mLastUseKilledTarget,
                t => EnqueueKillAftermath(t, boardSlot),
                mOnResolvedWithoutKill));
        }

        private void EnqueueKillAftermath(BattleTimeline timeline, int boardSlot)
        {
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            var fillGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(boardSlot, () => mDispatcher.Send(new ResolvePostKillFillCommand()), trackFusion: false));
            var rotateGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(boardSlot, () => mDispatcher.Send(new ResolvePostKillRotateCommand()), trackFusion: true));

            timeline.Enqueue(new ResolveBatchStep(fillGate));
            timeline.Enqueue(new PresentStep(fillGate, mBoardPresentChannel));
            timeline.Enqueue(new ResolveBatchStep(rotateGate));
            timeline.Enqueue(new PresentStep(rotateGate, mBoardPresentChannel));
            FusionRefillLockstep.AppendAfterRotatePresent(
                timeline,
                () => mLastRotateHadFusion,
                t => FusionRefillLockstep.EnqueueRefillBatches(
                    t,
                    mArchitecture,
                    mDispatcher,
                    mBoardPresentChannel,
                    boardSlot,
                    mFusionExcludeResultUids,
                    mOnBoardBatchProjected));
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

            if (mOnBoardBatchProjected != null)
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
