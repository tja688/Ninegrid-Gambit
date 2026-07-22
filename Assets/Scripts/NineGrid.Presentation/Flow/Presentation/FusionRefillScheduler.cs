using System;
using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Flow.Diagnostics;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 融合伴随补牌锁步：Rotate Present 之后按需追加 ResolveFusionRefill → Present。
    /// V4：由静态 Lockstep 收为实例调度器，Resolve 亦可经 Presentation Command 统一入口。
    /// </summary>
    public sealed class FusionRefillScheduler
    {
        public void AppendAfterRotatePresent(
            BattleTimeline timeline,
            Func<bool> hadFusion,
            Action<BattleTimeline> enqueueFusionRefill)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException("timeline");
            }

            if (hadFusion == null)
            {
                throw new ArgumentNullException("hadFusion");
            }

            if (enqueueFusionRefill == null)
            {
                throw new ArgumentNullException("enqueueFusionRefill");
            }

            timeline.Enqueue(new AttackPostHitBranchStep(timeline, hadFusion, enqueueFusionRefill));
        }

        public void EnqueueRefillBatches(
            BattleTimeline timeline,
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel boardPresentChannel,
            int boardSlot,
            IReadOnlyList<int> excludeResultUids,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected)
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

            var sync = architecture.GetSystem<IPresentationSyncSystem>();
            var excludeCopy = CopyUids(excludeResultUids);
            var refillGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(
                    architecture,
                    dispatcher,
                    boardSlot,
                    excludeCopy,
                    onBoardBatchProjected));

            timeline.Enqueue(new ResolveBatchStep(refillGate));
            timeline.Enqueue(new PresentStep(refillGate, boardPresentChannel));
        }

        public CoreCommandDispatchResult ResolveAndProject(
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            int boardSlot,
            IReadOnlyList<int> excludeResultUids,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected)
        {
            if (architecture == null)
            {
                throw new ArgumentNullException("architecture");
            }

            if (dispatcher == null)
            {
                throw new ArgumentNullException("dispatcher");
            }

            var phase = architecture.GetSystem<IPhaseSystem>();
            if (phase.CurrentPhase != GamePhase.InteractionLoop)
            {
                return dispatcher.Send(new ResolveFusionRefillCommand(skipFill: true));
            }

            var deck = architecture.GetModel<DeckModel>();
            var board = architecture.GetModel<BoardModel>();
            if (deck == null
                || deck.DrawPileUids == null
                || deck.DrawPileUids.Count <= 0
                || !FusionRefillPlanner.HasRefillCandidateExcluding(deck, excludeResultUids)
                || !FusionRefillPlanner.HasEmptyBoardSlot(board))
            {
                // 仍走命令打开空批，便于 Present 立刻完成并 ack（与无盘面 delta 一致）。
                return dispatcher.Send(new ResolveFusionRefillCommand(skipFill: true));
            }

            var originalOrder = new List<int>(deck.DrawPileUids);
            var refillOrder = FusionRefillPlanner.BuildRefillDrawOrder(originalOrder, excludeResultUids);
            if (!FusionRefillPlanner.HasRefillCandidateExcludingOrder(refillOrder, excludeResultUids))
            {
                return dispatcher.Send(new ResolveFusionRefillCommand(skipFill: true));
            }

            deck.ReorderDrawPile(refillOrder);
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            CoreCommandDispatchResult dispatch;
            try
            {
                PerfTraceRecorder.Record(
                    "SkeletonFusion",
                    -1,
                    "RefillBatchBegin",
                    new Dictionary<string, string>
                    {
                        ["excludeCount"] = (excludeResultUids != null ? excludeResultUids.Count : 0).ToString(),
                        ["path"] = "director",
                    });
                dispatch = dispatcher.Send(new ResolveFusionRefillCommand());
            }
            finally
            {
                var restored = FusionRefillPlanner.RestoreRemainingOrder(originalOrder, deck.DrawPileUids);
                deck.ReorderDrawPile(restored);
            }

            if (dispatch == null || !dispatch.Accepted)
            {
                return dispatch;
            }

            if (onBoardBatchProjected != null)
            {
                var summary = IntentBatchProjection.Build(architecture, pipeline, startIndex);
                summary.Deals = FusionRefillPlanner.FilterDealsExcluding(summary.Deals, excludeResultUids);
                if (summary.Steps != null && summary.Steps.Length > 0)
                {
                    summary.Steps = FilterDealStepsExcluding(summary.Steps, excludeResultUids);
                }

                onBoardBatchProjected(startIndex, boardSlot, summary);
            }

            PerfTraceRecorder.Record(
                "SkeletonFusion",
                -1,
                "RefillBatchEnd",
                new Dictionary<string, string> { ["path"] = "director" });

            return dispatch;
        }

        private static BoardPresentationStep[] FilterDealStepsExcluding(
            BoardPresentationStep[] steps,
            IReadOnlyList<int> excludeResultUids)
        {
            var list = new List<BoardPresentationStep>(steps.Length);
            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                if (step.Kind == BoardPresentationStepKind.Deal && step.Deals != null)
                {
                    step.Deals = FusionRefillPlanner.FilterDealsExcluding(step.Deals, excludeResultUids);
                    if (step.Deals.Length == 0)
                    {
                        continue;
                    }
                }

                list.Add(step);
            }

            return list.Count > 0 ? list.ToArray() : Array.Empty<BoardPresentationStep>();
        }

        private static List<int> CopyUids(IReadOnlyList<int> source)
        {
            var copy = new List<int>(source != null ? source.Count : 0);
            if (source == null)
            {
                return copy;
            }

            for (var i = 0; i < source.Count; i++)
            {
                if (source[i] > 0 && !copy.Contains(source[i]))
                {
                    copy.Add(source[i]);
                }
            }

            return copy;
        }
    }
}
