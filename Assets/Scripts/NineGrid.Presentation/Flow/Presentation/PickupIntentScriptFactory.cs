using System;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// Pickup 缓冲 flush 后的表现通知；Hand 可注册以承接入手动画。
    /// </summary>
    public static class PickupIntentFlushHook
    {
        public static Action<int, PickupItemPresentationResult> Notify;
    }

    /// <summary>
    /// Pickup 锁步剧本（ADR-0001 / ADR-0012）：
    /// 拾卡（<see cref="ApplyPickupCardCommand"/>，只拾卡）→ Present（盘面 delta + 效果打击）→
    /// 互动计数 → 盘面稳定化 → 旋转 → 再稳定化 → 敌方行动分拍 → 收尾回调（教程 Step7 通知）。
    ///
    /// 旧单体路径（<c>PhaseSystem.ApplyPickupItem</c> 整拍）会在 Core 命令内同步结算整个互动链
    /// （含敌方齐射伤害），伤害没有表演批次 = 观感瞬间出伤；本剧本把互动链逐批交给导演锁步表演。
    /// 手牌入手动画经 <see cref="PickupIntentFlushHook"/> 在拾卡批解算时启动（持主线租约，
    /// 排在 Present 之后、互动链之前）。
    /// </summary>
    public sealed class PickupIntentScriptFactory : IIntentScriptFactory
    {
        private readonly IArchitecture mArchitecture;
        private readonly CoreCommandDispatcher mDispatcher;
        private readonly IPresentChannel mBoardPresentChannel;
        private readonly Action<int, int, PostKillBoardPresentationResult> mOnBoardBatchProjected;
        private readonly Action<int, int, int, PostKillBoardPresentationResult> mOnCounterBatchProjected;
        private readonly IPresentChannel mCounterPresentChannel;
        private readonly BoardStabilizationScheduler mStabilization;
        private readonly EnemyActionPhaseScheduler mEnemyAction;
        private readonly Action<int> mOnItemPickedUp;

        public PickupIntentScriptFactory(
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel boardPresentChannel,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected,
            Action<int> onItemPickedUp = null,
            IPresentChannel counterPresentChannel = null,
            Action<int, int, int, PostKillBoardPresentationResult> onCounterBatchProjected = null,
            BoardStabilizationScheduler stabilization = null,
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

            if (boardPresentChannel == null)
            {
                throw new ArgumentNullException("boardPresentChannel");
            }

            mArchitecture = architecture;
            mDispatcher = dispatcher;
            mBoardPresentChannel = boardPresentChannel;
            mOnBoardBatchProjected = onBoardBatchProjected;
            mOnItemPickedUp = onItemPickedUp;
            mCounterPresentChannel = counterPresentChannel;
            mOnCounterBatchProjected = onCounterBatchProjected;
            mStabilization = stabilization ?? new BoardStabilizationScheduler();
            mEnemyAction = enemyAction ?? new EnemyActionPhaseScheduler();
        }

        public void BuildScript(InputIntent intent, BattleTimeline timeline)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException("timeline");
            }

            if (!string.Equals(intent.Kind, InputIntentKinds.Pickup, StringComparison.Ordinal))
            {
                return;
            }

            var slotIndex = intent.TargetId;
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();

            // 拾卡分拍：只写 Core 拾取（不动互动链），Present 消费盘面 delta 与效果打击。
            var pickupGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolvePickupAndProject(slotIndex),
                slice: "PickupApply");
            timeline.Enqueue(new ResolveBatchStep(pickupGate));
            timeline.Enqueue(new PresentStep(pickupGate, mBoardPresentChannel, channelName: "PickupPresent"));

            // 拾取属九宫格互动：计数独立开批（与攻击/探索同构，OnInteract 效果不进 Fill 窗）。
            var interactGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(
                    slotIndex,
                    () => mDispatcher.Send(new AdvanceInteractionCountCommand())),
                slice: "PickupInteractionAdvance");
            timeline.Enqueue(new ResolveBatchStep(interactGate));
            timeline.Enqueue(new PresentStep(interactGate, mBoardPresentChannel));

            mStabilization.Append(
                timeline,
                mArchitecture,
                mDispatcher,
                mBoardPresentChannel,
                slotIndex,
                mOnBoardBatchProjected,
                t => EnqueueRotateThenSettle(t, slotIndex));
            // 收尾回调：整个互动链（含敌方齐射）表演完后触发（教程 Step6→Step7 通知）。
            if (mOnItemPickedUp != null)
            {
                timeline.Enqueue(new PickupPresentedStep(mOnItemPickedUp, slotIndex));
            }
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
            timeline.Enqueue(new PresentStep(rotateGate, mBoardPresentChannel));
            mStabilization.Append(
                timeline,
                mArchitecture,
                mDispatcher,
                mBoardPresentChannel,
                boardSlot,
                mOnBoardBatchProjected,
                t => mEnemyAction.Append(
                    t,
                    mArchitecture,
                    mDispatcher,
                    mBoardPresentChannel,
                    mCounterPresentChannel,
                    mOnBoardBatchProjected,
                    mOnCounterBatchProjected,
                    boardSlot));
        }

        private CoreCommandDispatchResult ResolvePickupAndProject(int slotIndex)
        {
            var pipeline = mArchitecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var dispatch = mDispatcher.Send(new ApplyPickupCardCommand(SlotId.Board(slotIndex)));
            if (dispatch == null || !dispatch.Accepted)
            {
                return dispatch;
            }

            var projection = IntentBatchProjection.Build(mArchitecture, pipeline, startIndex);
            if (mOnBoardBatchProjected != null)
            {
                mOnBoardBatchProjected(startIndex, slotIndex, projection);
            }

            // 通知手牌侧承接入手动画：经主线租约 Hold 排在 Present 之后、互动链之前。
            var notify = PickupIntentFlushHook.Notify;
            if (notify != null)
            {
                notify(slotIndex, BuildFlushSummary(startIndex, projection));
            }

            return dispatch;
        }

        private PickupItemPresentationResult BuildFlushSummary(
            int startIndex,
            PostKillBoardPresentationResult projection)
        {
            var summary = new PickupItemPresentationResult
            {
                Accepted = true,
                Reason = "director",
                RoutedToDirector = true,
                EventLogStartIndex = startIndex,
                Steps = projection.Steps ?? Array.Empty<BoardPresentationStep>(),
                Moves = projection.Moves ?? Array.Empty<PostKillCardMove>(),
                Deals = projection.Deals ?? Array.Empty<PostKillCardDeal>(),
                RemovedUids = projection.RemovedUids ?? Array.Empty<int>(),
            };

            var registry = mArchitecture.GetModel<CardRegistry>();
            var stepProjection = BoardPresentationStepProjector.Project(
                mArchitecture.GetSystem<IActionPipelineSystem>().EventLog.Entries,
                startIndex,
                registry);
            var pickedUid = stepProjection.PickedUid;
            summary.CardUid = pickedUid;
            if (pickedUid > 0 && registry.TryGet(pickedUid, out var card) && card != null)
            {
                summary.AcquiredToHand = card.Zone.Value == ZoneId.ItemSlots;
                summary.RemovedWithoutHand = card.Zone.Value == ZoneId.Removed;
            }

            return summary;
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

            if (mOnBoardBatchProjected != null)
            {
                mOnBoardBatchProjected(
                    startIndex,
                    boardSlot,
                    IntentBatchProjection.Build(mArchitecture, pipeline, startIndex));
            }

            return dispatch;
        }

        /// <summary>拾取互动链完整表演后的单次收尾回调（教程 Step7 通知等）。</summary>
        private sealed class PickupPresentedStep : ITimelineStep
        {
            private readonly Action<int> mCallback;
            private readonly int mSlot;
            private bool mDone;

            public PickupPresentedStep(Action<int> callback, int slot)
            {
                mCallback = callback;
                mSlot = slot;
            }

            public TimelineStepStatus Tick(float deltaTime)
            {
                if (mDone)
                {
                    return TimelineStepStatus.Finished;
                }

                mDone = true;
                mCallback?.Invoke(mSlot);
                return TimelineStepStatus.Finished;
            }
        }
    }
}
