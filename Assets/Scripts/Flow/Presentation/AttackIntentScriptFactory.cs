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
    /// 攻击垂直切片剧本：CombatHit → Present →（击杀）Fill → Present → Rotate → Present →（融合）Refill。
    /// 未击杀走 onSurvived（旧反击路径）；未识别 kind 不入队。
    /// </summary>
    public sealed class AttackIntentScriptFactory : IIntentScriptFactory
    {
        private readonly IArchitecture mArchitecture;
        private readonly CoreCommandDispatcher mDispatcher;
        private readonly IPresentChannel mHitPresentChannel;
        private readonly IPresentChannel mBoardPresentChannel;
        /// <summary>startIndex, clickedBoardSlot, resolvedCombatUid, projection</summary>
        private readonly Action<int, int, int, PostKillBoardPresentationResult> mOnHitBatchProjected;
        private readonly Action<int, int, PostKillBoardPresentationResult> mOnBoardBatchProjected;
        private readonly Action mOnSurvived;
        private bool mLastHitKilledTarget;
        private bool mLastRotateHadFusion;
        private readonly List<int> mFusionExcludeResultUids = new List<int>(2);

        public AttackIntentScriptFactory(
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel hitPresentChannel,
            IPresentChannel boardPresentChannel,
            Action<int, int, int, PostKillBoardPresentationResult> onHitBatchProjected = null,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected = null,
            Action onSurvived = null)
        {
            if (architecture == null)
            {
                throw new ArgumentNullException("architecture");
            }

            if (dispatcher == null)
            {
                throw new ArgumentNullException("dispatcher");
            }

            if (hitPresentChannel == null)
            {
                throw new ArgumentNullException("hitPresentChannel");
            }

            if (boardPresentChannel == null)
            {
                throw new ArgumentNullException("boardPresentChannel");
            }

            mArchitecture = architecture;
            mDispatcher = dispatcher;
            mHitPresentChannel = hitPresentChannel;
            mBoardPresentChannel = boardPresentChannel;
            mOnHitBatchProjected = onHitBatchProjected;
            mOnBoardBatchProjected = onBoardBatchProjected;
            mOnSurvived = onSurvived;
        }

        public void BuildScript(InputIntent intent, BattleTimeline timeline)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException("timeline");
            }

            if (!string.Equals(intent.Kind, InputIntentKinds.Attack, StringComparison.Ordinal))
            {
                return;
            }

            var slotIndex = intent.TargetId;
            var slot = SlotId.Board(slotIndex);
            var board = mArchitecture.GetModel<BoardModel>();
            var intendedTargetUid = board.GetCardUid(slot);
            if (intendedTargetUid <= 0)
            {
                return;
            }

            var phase = mArchitecture.GetSystem<IPhaseSystem>();
            var targetUid = phase.ResolvePlayerAttackTargetUid(intendedTargetUid);
            if (targetUid <= 0)
            {
                targetUid = intendedTargetUid;
            }

            var attackerUid = board.AvatarUid.Value;
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            mLastHitKilledTarget = false;
            mLastRotateHadFusion = false;
            mFusionExcludeResultUids.Clear();

            var hitGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveHitAndProject(slotIndex, attackerUid, targetUid));
            timeline.Enqueue(new ResolveBatchStep(hitGate));
            timeline.Enqueue(new PresentStep(hitGate, mHitPresentChannel));
            timeline.Enqueue(new AttackPostHitBranchStep(
                timeline,
                () => mLastHitKilledTarget,
                t => EnqueueKillAftermath(t, slotIndex),
                mOnSurvived));
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

        private CoreCommandDispatchResult ResolveHitAndProject(int boardSlot, int attackerUid, int targetUid)
        {
            var pipeline = mArchitecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var dispatch = mDispatcher.Send(new CombatHitCommand(attackerUid, targetUid));
            if (dispatch == null || !dispatch.Accepted)
            {
                mLastHitKilledTarget = false;
                return dispatch;
            }

            mLastHitKilledTarget = IntentBatchProjection.ContainsCardKilled(pipeline, startIndex, targetUid);
            if (mOnHitBatchProjected != null)
            {
                mOnHitBatchProjected(
                    startIndex,
                    boardSlot,
                    targetUid,
                    IntentBatchProjection.Build(mArchitecture, pipeline, startIndex));
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
    }
}
