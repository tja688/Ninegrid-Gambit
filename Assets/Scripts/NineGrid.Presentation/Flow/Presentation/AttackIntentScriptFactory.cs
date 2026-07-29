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
    /// 怪物先攻时：先 Counter（怪→玩家），Avatar 未败再 Hit（玩家→怪），击杀则走 Fill/Rotate。
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
        private readonly EnemyActionPhaseScheduler mEnemyAction;
        private bool mLastHitKilledTarget;
        private bool mLastAvatarDefeated;
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
            FusionRefillScheduler fusionRefill = null,
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
            mEnemyAction = enemyAction ?? new EnemyActionPhaseScheduler();
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
            mLastAvatarDefeated = false;
            mLastRotateHadFusion = false;
            mLastResolvedCombatUid = 0;
            mFusionExcludeResultUids.Clear();

            if (phase.MonsterStrikesFirst(attackerUid, targetUid))
            {
                BuildMonsterFirstScript(timeline, sync, slotIndex, attackerUid, targetUid);
                return;
            }

            BuildPlayerFirstScript(timeline, sync, slotIndex, attackerUid, targetUid);
        }

        private void BuildPlayerFirstScript(
            BattleTimeline timeline,
            IPresentationSyncSystem sync,
            int slotIndex,
            int attackerUid,
            int targetUid)
        {
            var hitGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveHitAndProject(slotIndex, attackerUid, targetUid));
            timeline.Enqueue(new ResolveBatchStep(hitGate));
            timeline.Enqueue(new PresentStep(hitGate, mHitPresentChannel));
            timeline.Enqueue(new AttackPostHitBranchStep(
                timeline,
                () => mLastHitKilledTarget,
                t => EnqueueKillAftermath(t, slotIndex),
                t =>
                {
                    if (!mLastAvatarDefeated)
                    {
                        EnqueueCounterAftermath(t, slotIndex, targetUid, attackerUid);
                    }
                    else
                    {
                        EnqueueNonKillInteractionAdvance(t, slotIndex);
                    }
                }));
        }

        private void BuildMonsterFirstScript(
            BattleTimeline timeline,
            IPresentationSyncSystem sync,
            int slotIndex,
            int avatarUid,
            int monsterUid)
        {
            if (mCounterPresentChannel == null)
            {
                // 无反击通道时退化为玩家先打，避免丢交战。
                BuildPlayerFirstScript(timeline, sync, slotIndex, avatarUid, monsterUid);
                return;
            }

            mLastResolvedCombatUid = monsterUid;
            var firstStrikeGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveCounterAndProject(slotIndex, monsterUid, avatarUid),
                slice: "AttackCounter");
            timeline.Enqueue(new ResolveBatchStep(firstStrikeGate));
            timeline.Enqueue(new PresentStep(firstStrikeGate, mCounterPresentChannel, "CounterHit"));
            timeline.Enqueue(new TimelineBranchStep(
                timeline,
                () => !mLastAvatarDefeated,
                t => EnqueuePlayerReplyAfterFirstStrike(t, slotIndex, avatarUid, monsterUid),
                t => EnqueueNonKillInteractionAdvance(t, slotIndex)));
        }

        private void EnqueuePlayerReplyAfterFirstStrike(
            BattleTimeline timeline,
            int boardSlot,
            int avatarUid,
            int monsterUid)
        {
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            var hitGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveHitAndProject(boardSlot, avatarUid, monsterUid));
            timeline.Enqueue(new ResolveBatchStep(hitGate));
            timeline.Enqueue(new PresentStep(hitGate, mHitPresentChannel));
            timeline.Enqueue(new AttackPostHitBranchStep(
                timeline,
                () => mLastHitKilledTarget,
                t => EnqueueKillAftermath(t, boardSlot),
                t => EnqueueNonKillInteractionAdvance(t, boardSlot)));
        }

        private void EnqueueKillAftermath(BattleTimeline timeline, int boardSlot)
        {
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            // 九宫格互动：击杀路径在补牌前推进互动计数（与 Fill 解耦，#75）。
            // 计数走 SendCommand（不开 Present 批），Fill 仍走 Dispatcher 开批。
            var fillGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(
                    boardSlot,
                    () =>
                    {
                        mArchitecture.SendCommand(new AdvanceInteractionCountCommand());
                        return mDispatcher.Send(new ResolvePostKillFillCommand());
                    },
                    trackFusion: true));
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
                t =>
                {
                    mFusionRefill.EnqueueRefillBatches(
                        t,
                        mArchitecture,
                        mDispatcher,
                        mBoardPresentChannel,
                        boardSlot,
                        mFusionExcludeResultUids,
                        mOnBoardBatchProjected);
                    AppendEnemyActionPhase(t, boardSlot);
                },
                t => AppendEnemyActionPhase(t, boardSlot));
        }

        private void EnqueueNonKillInteractionAdvance(BattleTimeline timeline, int boardSlot)
        {
            // 未击杀无补牌批可挂载计数：静默推进，不另开 Present（InteractionChanged 为 Beat.None）。
            // 战败后仍计一次互动（ADR-0012），但不得再挂敌方行动续拍。
            timeline.Enqueue(new InteractionCountAdvanceStep(mArchitecture));
            if (!mLastAvatarDefeated)
            {
                AppendEnemyActionPhase(timeline, boardSlot);
            }
        }

        private void AppendEnemyActionPhase(BattleTimeline timeline, int boardSlot)
        {
            mEnemyAction.Append(
                timeline,
                mArchitecture,
                mDispatcher,
                mBoardPresentChannel,
                mCounterPresentChannel,
                mOnBoardBatchProjected,
                mOnCounterBatchProjected,
                boardSlot);
        }

        private void EnqueueCounterAftermath(
            BattleTimeline timeline,
            int boardSlot,
            int monsterUid,
            int avatarUid)
        {
            if (mCounterPresentChannel == null)
            {
                EnqueueNonKillInteractionAdvance(timeline, boardSlot);
                return;
            }

            if (monsterUid <= 0)
            {
                monsterUid = mLastResolvedCombatUid;
            }

            if (monsterUid <= 0)
            {
                monsterUid = mArchitecture.GetModel<BoardModel>().GetCardUid(SlotId.Board(boardSlot));
            }

            if (avatarUid <= 0)
            {
                avatarUid = mArchitecture.GetModel<BoardModel>().AvatarUid.Value;
            }

            if (monsterUid <= 0 || avatarUid <= 0)
            {
                EnqueueNonKillInteractionAdvance(timeline, boardSlot);
                return;
            }

            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            var counterGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveCounterAndProject(boardSlot, monsterUid, avatarUid),
                slice: "AttackCounter");
            timeline.Enqueue(new ResolveBatchStep(counterGate));
            timeline.Enqueue(new PresentStep(counterGate, mCounterPresentChannel, "CounterHit"));
            // 反击解算后才知道是否战败：不可在 Present 前无条件挂敌方行动。
            timeline.Enqueue(new TimelineBranchStep(
                timeline,
                () => !mLastAvatarDefeated,
                t => EnqueueNonKillInteractionAdvance(t, boardSlot),
                t => t.Enqueue(new InteractionCountAdvanceStep(mArchitecture))));
        }

        private sealed class InteractionCountAdvanceStep : ITimelineStep
        {
            private readonly IArchitecture mArch;
            private bool mDone;

            public InteractionCountAdvanceStep(IArchitecture architecture)
            {
                mArch = architecture;
            }

            public TimelineStepStatus Tick(float deltaTime)
            {
                if (mDone)
                {
                    return TimelineStepStatus.Finished;
                }

                mDone = true;
                mArch.SendCommand(new AdvanceInteractionCountCommand());
                return TimelineStepStatus.Finished;
            }
        }

        private CoreCommandDispatchResult ResolveHitAndProject(int boardSlot, int attackerUid, int targetUid)
        {
            var pipeline = mArchitecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var dispatch = mDispatcher.Send(new CombatHitCommand(attackerUid, targetUid));
            if (dispatch == null || !dispatch.Accepted)
            {
                mLastHitKilledTarget = false;
                mLastAvatarDefeated = false;
                mLastResolvedCombatUid = 0;
                return dispatch;
            }

            mLastResolvedCombatUid = targetUid;
            mLastHitKilledTarget = IntentBatchProjection.ContainsCardKilled(pipeline, startIndex, targetUid);
            var projection = IntentBatchProjection.Build(mArchitecture, pipeline, startIndex);
            mLastAvatarDefeated = projection.AvatarDefeated;
            if (mOnHitBatchProjected != null)
            {
                mOnHitBatchProjected(
                    startIndex,
                    boardSlot,
                    targetUid,
                    projection);
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

            mLastResolvedCombatUid = monsterUid;
            var projection = IntentBatchProjection.Build(mArchitecture, pipeline, startIndex);
            mLastAvatarDefeated = projection.AvatarDefeated;
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
                // Fill/Rotate 共用 exclude：追加收集，OR 进 flag，避免后一次 Clear 掉前一次融合。
                if (FusionRefillPlanner.TryCollectResultUids(
                        pipeline.EventLog.Entries,
                        startIndex,
                        mFusionExcludeResultUids,
                        clearInto: false))
                {
                    mLastRotateHadFusion = true;
                }
            }

            if (mOnBoardBatchProjected != null)
            {
                mOnBoardBatchProjected(startIndex, boardSlot, IntentBatchProjection.Build(mArchitecture, pipeline, startIndex));
            }

            return dispatch;
        }
    }
}
