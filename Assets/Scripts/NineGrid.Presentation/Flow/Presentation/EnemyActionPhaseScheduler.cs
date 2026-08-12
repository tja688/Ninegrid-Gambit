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
    /// 敌方行动阶段导演分拍：报名 → 逐条（单向打击走 Counter）→ 收尾（ADR-0012 / #81）。
    /// </summary>
    public sealed class EnemyActionPhaseScheduler
    {
        private readonly BoardStabilizationScheduler mStabilization;
        private bool mLastStrikeHadDamage;
        private bool mLastAvatarDefeated;
        private int mLastStrikerUid;
        private int mLastStrikerSlot;

        public EnemyActionPhaseScheduler(BoardStabilizationScheduler stabilization = null)
        {
            mStabilization = stabilization ?? new BoardStabilizationScheduler();
        }

        public void Append(
            BattleTimeline timeline,
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel boardPresentChannel,
            IPresentChannel counterPresentChannel,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected,
            Action<int, int, int, PostKillBoardPresentationResult> onCounterBatchProjected,
            int boardSlotHint)
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

            if (!HasParticipatingEnemy(architecture))
            {
                mStabilization.Append(
                    timeline,
                    architecture,
                    dispatcher,
                    boardPresentChannel,
                    boardSlotHint,
                    onBoardBatchProjected);
                return;
            }

            EnqueueRegisterThenVolley(
                timeline,
                architecture,
                dispatcher,
                boardPresentChannel,
                counterPresentChannel,
                onBoardBatchProjected,
                onCounterBatchProjected,
                boardSlotHint);
        }

        private void EnqueueRegisterThenVolley(
            BattleTimeline timeline,
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel boardPresentChannel,
            IPresentChannel counterPresentChannel,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected,
            Action<int, int, int, PostKillBoardPresentationResult> onCounterBatchProjected,
            int boardSlotHint)
        {
            var sync = architecture.GetSystem<IPresentationSyncSystem>();
            var registerGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveBoardAndProject(
                    architecture,
                    dispatcher,
                    boardSlotHint,
                    () => dispatcher.Send(new RegisterEnemyActionPhaseCommand()),
                    onBoardBatchProjected),
                slice: "EnemyActionRegister");
            timeline.Enqueue(new ResolveBatchStep(registerGate));
            timeline.Enqueue(new PresentStep(registerGate, boardPresentChannel, "EnemyActionRegister"));
            timeline.Enqueue(new TimelineBranchStep(
                timeline,
                () => HasPendingEnemyAction(architecture),
                t => EnqueueNextStrikeOrFinale(
                    t,
                    architecture,
                    dispatcher,
                    boardPresentChannel,
                    counterPresentChannel,
                    onBoardBatchProjected,
                    onCounterBatchProjected,
                    boardSlotHint),
                t => EnqueueFinale(
                    t,
                    architecture,
                    dispatcher,
                    boardPresentChannel,
                    onBoardBatchProjected,
                    boardSlotHint)));
        }

        private void EnqueueNextStrikeOrFinale(
            BattleTimeline timeline,
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel boardPresentChannel,
            IPresentChannel counterPresentChannel,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected,
            Action<int, int, int, PostKillBoardPresentationResult> onCounterBatchProjected,
            int boardSlotHint)
        {
            var sync = architecture.GetSystem<IPresentationSyncSystem>();
            var nextGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveNextAndProject(
                    architecture,
                    dispatcher,
                    boardSlotHint,
                    onBoardBatchProjected,
                    onCounterBatchProjected),
                slice: "EnemyActionStrike");
            timeline.Enqueue(new ResolveBatchStep(nextGate));
            timeline.Enqueue(new TimelineBranchStep(
                timeline,
                () => mLastStrikeHadDamage && counterPresentChannel != null,
                t =>
                {
                    // 敌方开火同战斗通道：伤人触发的 FaceUp（如 rise_up）延到命中后再翻。
                    t.Enqueue(new PresentStep(
                        nextGate,
                        counterPresentChannel,
                        "CounterHit",
                        flushFaceUpBeforeBegin: false));
                    EnqueueContinueAfterStrike(
                        t,
                        architecture,
                        dispatcher,
                        boardPresentChannel,
                        counterPresentChannel,
                        onBoardBatchProjected,
                        onCounterBatchProjected,
                        boardSlotHint);
                },
                t =>
                {
                    t.Enqueue(new PresentStep(nextGate, boardPresentChannel, "EnemyActionMiss"));
                    EnqueueContinueAfterStrike(
                        t,
                        architecture,
                        dispatcher,
                        boardPresentChannel,
                        counterPresentChannel,
                        onBoardBatchProjected,
                        onCounterBatchProjected,
                        boardSlotHint);
                }));
        }

        private void EnqueueContinueAfterStrike(
            BattleTimeline timeline,
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel boardPresentChannel,
            IPresentChannel counterPresentChannel,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected,
            Action<int, int, int, PostKillBoardPresentationResult> onCounterBatchProjected,
            int boardSlotHint)
        {
            timeline.Enqueue(new TimelineBranchStep(
                timeline,
                () => !mLastAvatarDefeated && HasPendingEnemyAction(architecture),
                t => EnqueueNextStrikeOrFinale(
                    t,
                    architecture,
                    dispatcher,
                    boardPresentChannel,
                    counterPresentChannel,
                    onBoardBatchProjected,
                    onCounterBatchProjected,
                    boardSlotHint),
                t => EnqueueFinale(
                    t,
                    architecture,
                    dispatcher,
                    boardPresentChannel,
                    onBoardBatchProjected,
                    boardSlotHint)));
        }

        private void EnqueueFinale(
            BattleTimeline timeline,
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel boardPresentChannel,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected,
            int boardSlotHint)
        {
            var sync = architecture.GetSystem<IPresentationSyncSystem>();
            var finaleGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveBoardAndProject(
                    architecture,
                    dispatcher,
                    boardSlotHint,
                    () => dispatcher.Send(new ResolveEnemyActionFinaleCommand()),
                    onBoardBatchProjected),
                slice: "EnemyActionFinale");
            timeline.Enqueue(new ResolveBatchStep(finaleGate));
            timeline.Enqueue(new PresentStep(finaleGate, boardPresentChannel, "EnemyActionFinale"));
            mStabilization.Append(
                timeline,
                architecture,
                dispatcher,
                boardPresentChannel,
                boardSlotHint,
                onBoardBatchProjected);
        }

        private CoreCommandDispatchResult ResolveNextAndProject(
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            int boardSlotHint,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected,
            Action<int, int, int, PostKillBoardPresentationResult> onCounterBatchProjected)
        {
            mLastStrikeHadDamage = false;
            mLastAvatarDefeated = false;
            mLastStrikerUid = 0;
            mLastStrikerSlot = boardSlotHint;

            var phase = architecture.GetSystem<IPhaseSystem>();
            var pending = phase.PendingEnemyActionUids;
            if (pending != null && pending.Count > 0)
            {
                mLastStrikerUid = pending[0];
                var board = architecture.GetModel<BoardModel>();
                var registry = architecture.GetModel<CardRegistry>();
                CardInstance monster;
                if (registry.TryGet(mLastStrikerUid, out monster) && monster != null && monster.Slot.Value.IsBoardSlot)
                {
                    mLastStrikerSlot = monster.Slot.Value.Index;
                }
                else if (board != null)
                {
                    mLastStrikerSlot = boardSlotHint;
                }
            }

            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var dispatch = dispatcher.Send(new ResolveNextEnemyActionCommand());
            if (dispatch == null || !dispatch.Accepted)
            {
                return dispatch;
            }

            var projection = IntentBatchProjection.Build(architecture, pipeline, startIndex);
            mLastAvatarDefeated = projection.AvatarDefeated;
            mLastStrikeHadDamage = ContainsDamageDealt(pipeline.EventLog.Entries, startIndex);

            if (mLastStrikeHadDamage && onCounterBatchProjected != null)
            {
                // 标注 battlelog op reason：齐射伤害批与交战反击批区分开。
                NineGrid.Flow.Diagnostics.CombatHitTraceContext.PendingReason = "EnemyVolleyStrike";
                onCounterBatchProjected(startIndex, mLastStrikerSlot, mLastStrikerUid, projection);
            }
            else if (onBoardBatchProjected != null)
            {
                onBoardBatchProjected(startIndex, boardSlotHint, projection);
            }

            return dispatch;
        }

        private static CoreCommandDispatchResult ResolveBoardAndProject(
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            int boardSlot,
            Func<CoreCommandDispatchResult> resolve,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected)
        {
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var dispatch = resolve();
            if (dispatch == null || !dispatch.Accepted)
            {
                return dispatch;
            }

            if (onBoardBatchProjected != null)
            {
                onBoardBatchProjected(
                    startIndex,
                    boardSlot,
                    IntentBatchProjection.Build(architecture, pipeline, startIndex));
            }

            return dispatch;
        }

        private static bool HasParticipatingEnemy(IArchitecture architecture)
        {
            var board = architecture.GetModel<BoardModel>();
            var registry = architecture.GetModel<CardRegistry>();
            if (board == null || registry == null)
            {
                return false;
            }

            foreach (var uid in board.BoardCardUids())
            {
                CardInstance card;
                if (!registry.TryGet(uid, out card)
                    || card == null
                    || card.Kind != CardKind.Monster)
                {
                    continue;
                }

                if (CardRhythmRules.HasActiveRhythm(card))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPendingEnemyAction(IArchitecture architecture)
        {
            var pending = architecture.GetSystem<IPhaseSystem>().PendingEnemyActionUids;
            return pending != null && pending.Count > 0;
        }

        private static bool ContainsDamageDealt(IReadOnlyList<CoreGameEvent> entries, int startIndex)
        {
            if (entries == null)
            {
                return false;
            }

            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.DamageDealt && entries[i].Amount > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
