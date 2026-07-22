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
    /// 攻击垂直切片剧本：CombatHit → Present →（击杀）Fill → Present → Rotate → Present →（融合）Refill；
    /// 未击杀 → 反击 CombatHit → Present（主线续写，非 Forget 旁路）。
    /// </summary>
    public sealed class AttackIntentScriptFactory : IIntentScriptFactory
    {
        private readonly IArchitecture mArchitecture;
        private readonly CoreCommandDispatcher mDispatcher;
        private readonly IPresentChannel mHitPresentChannel;
        private readonly IPresentChannel mBoardPresentChannel;
        private readonly IPresentChannel mCounterPresentChannel;
        /// <summary>startIndex, clickedBoardSlot, resolvedCombatUid, projection</summary>
        private readonly Action<int, int, int, PostKillBoardPresentationResult> mOnHitBatchProjected;
        private readonly Action<int, int, PostKillBoardPresentationResult> mOnBoardBatchProjected;
        /// <summary>startIndex, attackerBoardSlot, attackerUid, projection</summary>
        private readonly Action<int, int, int, PostKillBoardPresentationResult> mOnCounterBatchProjected;
        private readonly FusionRefillScheduler mFusionRefill;
        private bool mLastHitKilledTarget;
        private bool mLastRotateHadFusion;
        private int mLastResolvedCombatUid;
        private readonly List<int> mFusionExcludeResultUids = new List<int>(2);

        public AttackIntentScriptFactory(
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel hitPresentChannel,
            IPresentChannel boardPresentChannel,
            IPresentChannel counterPresentChannel = null,
            Action<int, int, int, PostKillBoardPresentationResult> onHitBatchProjected = null,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected = null,
            Action<int, int, int, PostKillBoardPresentationResult> onCounterBatchProjected = null,
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
            mCounterPresentChannel = counterPresentChannel;
            mOnHitBatchProjected = onHitBatchProjected;
            mOnBoardBatchProjected = onBoardBatchProjected;
            mOnCounterBatchProjected = onCounterBatchProjected;
            mFusionRefill = fusionRefill ?? new FusionRefillScheduler();
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
            mLastResolvedCombatUid = 0;
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
                t => EnqueueCounterAftermath(t, slotIndex)));
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
            mFusionRefill.AppendAfterRotatePresent(
                timeline,
                () => mLastRotateHadFusion,
                t => mFusionRefill.EnqueueRefillBatches(
                    t,
                    mArchitecture,
                    mDispatcher,
                    mBoardPresentChannel,
                    boardSlot,
                    mFusionExcludeResultUids,
                    mOnBoardBatchProjected));
        }

        private void EnqueueCounterAftermath(BattleTimeline timeline, int boardSlot)
        {
            if (mCounterPresentChannel == null)
            {
                return;
            }

            var monsterUid = mLastResolvedCombatUid;
            if (monsterUid <= 0)
            {
                var board = mArchitecture.GetModel<BoardModel>();
                monsterUid = board.GetCardUid(SlotId.Board(boardSlot));
            }

            var avatarUid = mArchitecture.GetModel<BoardModel>().AvatarUid.Value;
            if (monsterUid <= 0 || avatarUid <= 0)
            {
                return;
            }

            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            var counterGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveCounterAndProject(boardSlot, monsterUid, avatarUid),
                slice: "AttackCounter");
            timeline.Enqueue(new ResolveBatchStep(counterGate));
            timeline.Enqueue(new PresentStep(counterGate, mCounterPresentChannel, "CounterHit"));
        }

        private CoreCommandDispatchResult ResolveHitAndProject(int boardSlot, int attackerUid, int targetUid)
        {
            var pipeline = mArchitecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var dispatch = mDispatcher.Send(new CombatHitCommand(attackerUid, targetUid));
            if (dispatch == null || !dispatch.Accepted)
            {
                mLastHitKilledTarget = false;
                mLastResolvedCombatUid = 0;
                return dispatch;
            }

            mLastResolvedCombatUid = targetUid;
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

        private CoreCommandDispatchResult ResolveCounterAndProject(
            int attackerBoardSlot,
            int monsterUid,
            int avatarUid)
        {
            var pipeline = mArchitecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var dispatch = mDispatcher.Send(new CombatHitCommand(monsterUid, avatarUid));
            if (dispatch == null || !dispatch.Accepted)
            {
                return dispatch;
            }

            var projection = IntentBatchProjection.Build(mArchitecture, pipeline, startIndex);
            if (mOnCounterBatchProjected != null)
            {
                mOnCounterBatchProjected(startIndex, attackerBoardSlot, monsterUid, projection);
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
