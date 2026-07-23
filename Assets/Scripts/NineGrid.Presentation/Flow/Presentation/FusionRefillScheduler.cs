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
    /// 融合伴随补牌锁步：Rotate/Use Present 之后按需追加 ResolveFusionRefill → Present。
    /// V4：由静态 Lockstep 收为实例调度器，Resolve 亦可经 Presentation Command 统一入口。
    /// </summary>
    public sealed class FusionRefillScheduler
    {
        private static int sArmedChainId = -1;
        private static int sScheduledChainId = -1;

        /// <summary>当前连锁已挂「可能入队 FusionRefill」的剧本门（AppendAfter*）。</summary>
        public static bool IsRefillGateArmed
        {
            get
            {
                var chain = DirectorTrace.CurrentChainId;
                return chain > 0 && sArmedChainId == chain;
            }
        }

        /// <summary>当前连锁已实际入队 FusionRefill Resolve/Present。</summary>
        public static bool IsRefillScheduled
        {
            get
            {
                var chain = DirectorTrace.CurrentChainId;
                return chain > 0 && sScheduledChainId == chain;
            }
        }

        public static void ResetScheduleMarks()
        {
            sArmedChainId = -1;
            sScheduledChainId = -1;
        }

        public static void ArmRefillGate()
        {
            var chain = DirectorTrace.CurrentChainId;
            if (chain > 0)
            {
                sArmedChainId = chain;
            }
        }

        public static void DisarmRefillGate()
        {
            sArmedChainId = -1;
        }

        public static void MarkRefillScheduled()
        {
            var chain = DirectorTrace.CurrentChainId;
            if (chain > 0)
            {
                sScheduledChainId = chain;
            }
        }

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

            ArmRefillGate();
            timeline.Enqueue(new AttackPostHitBranchStep(
                timeline,
                () =>
                {
                    var hit = hadFusion();
                    if (!hit)
                    {
                        DisarmRefillGate();
                    }

                    return hit;
                },
                t =>
                {
                    if (IsRefillScheduled)
                    {
                        DisarmRefillGate();
                        return;
                    }

                    enqueueFusionRefill(t);
                    MarkRefillScheduled();
                    DisarmRefillGate();
                }));
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
            timeline.Enqueue(new PresentStep(
                refillGate,
                boardPresentChannel,
                channelName: "FusionRefill",
                choreoKind: "Refill"));
            MarkRefillScheduled();
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
            var deck = architecture.GetModel<DeckModel>();
            var board = architecture.GetModel<BoardModel>();
            var emptyCount = FusionRefillPlanner.CountEmptyBoardSlots(board);
            var drawCount = deck != null && deck.DrawPileUids != null ? deck.DrawPileUids.Count : 0;
            var excludeCount = excludeResultUids != null ? excludeResultUids.Count : 0;
            var hasNonExcludeCandidate = FusionRefillPlanner.HasRefillCandidateExcluding(deck, excludeResultUids);

            if (phase.CurrentPhase != GamePhase.InteractionLoop)
            {
                return SkipFill(
                    dispatcher,
                    reason: "phase",
                    emptyCount,
                    drawCount,
                    excludeCount,
                    hasNonExcludeCandidate);
            }

            if (deck == null || deck.DrawPileUids == null || drawCount <= 0)
            {
                return SkipFill(
                    dispatcher,
                    reason: "noDraw",
                    emptyCount,
                    drawCount,
                    excludeCount,
                    hasNonExcludeCandidate);
            }

            if (emptyCount <= 0)
            {
                return SkipFill(
                    dispatcher,
                    reason: "noEmpty",
                    emptyCount,
                    drawCount,
                    excludeCount,
                    hasNonExcludeCandidate);
            }

            // 有空槽且牌堆非空：即使候选全被 exclude，也不得静默空批——回退为不过滤补牌。
            var effectiveExclude = excludeResultUids;
            var usedExcludeFallback = false;
            if (!hasNonExcludeCandidate)
            {
                effectiveExclude = Array.Empty<int>();
                usedExcludeFallback = true;
                RecordDecision(
                    "RefillExcludeFallback",
                    skipFill: false,
                    reason: "excludeOnly",
                    emptyCount,
                    drawCount,
                    excludeCount,
                    hasNonExcludeCandidate: false);
            }

            var originalOrder = new List<int>(deck.DrawPileUids);
            var refillOrder = FusionRefillPlanner.BuildRefillDrawOrder(originalOrder, effectiveExclude);
            if (!FusionRefillPlanner.HasRefillCandidateExcludingOrder(refillOrder, effectiveExclude)
                && refillOrder.Count <= 0)
            {
                return SkipFill(
                    dispatcher,
                    reason: "noCandidate",
                    emptyCount,
                    drawCount,
                    excludeCount,
                    hasNonExcludeCandidate);
            }

            deck.ReorderDrawPile(refillOrder);
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            CoreCommandDispatchResult dispatch;
            try
            {
                RecordDecision(
                    "RefillBatchBegin",
                    skipFill: false,
                    reason: usedExcludeFallback ? "excludeFallback" : "fill",
                    emptyCount,
                    drawCount,
                    excludeCount,
                    hasNonExcludeCandidate || usedExcludeFallback);
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
                var filteredDeals = FusionRefillPlanner.FilterDealsExcluding(summary.Deals, effectiveExclude);
                if ((filteredDeals == null || filteredDeals.Length == 0)
                    && summary.Deals != null
                    && summary.Deals.Length > 0)
                {
                    RecordDecision(
                        "RefillDealFilterEmpty",
                        skipFill: false,
                        reason: "filteredEmpty",
                        emptyCount,
                        drawCount,
                        excludeCount,
                        hasNonExcludeCandidate);
                    // 投影仍保留 Core 已发出的 deals，避免 Present 空批瞬 ack。
                }
                else
                {
                    summary.Deals = filteredDeals;
                }

                if (summary.Steps != null && summary.Steps.Length > 0)
                {
                    var filteredSteps = FilterDealStepsExcluding(summary.Steps, effectiveExclude);
                    if (filteredSteps.Length == 0 && HasAnyDealStep(summary.Steps))
                    {
                        // 同上：滤空则保留原步骤，保证飞牌栅栏可跑。
                    }
                    else
                    {
                        summary.Steps = filteredSteps;
                    }
                }

                onBoardBatchProjected(startIndex, boardSlot, summary);
            }

            RecordDecision(
                "RefillBatchEnd",
                skipFill: false,
                reason: usedExcludeFallback ? "excludeFallback" : "fill",
                emptyCount,
                drawCount,
                excludeCount,
                hasNonExcludeCandidate || usedExcludeFallback);
            return dispatch;
        }

        private static CoreCommandDispatchResult SkipFill(
            CoreCommandDispatcher dispatcher,
            string reason,
            int emptyCount,
            int drawCount,
            int excludeCount,
            bool hasNonExcludeCandidate)
        {
            RecordDecision(
                "RefillBatchSkip",
                skipFill: true,
                reason,
                emptyCount,
                drawCount,
                excludeCount,
                hasNonExcludeCandidate);
            return dispatcher.Send(new ResolveFusionRefillCommand(skipFill: true));
        }

        private static void RecordDecision(
            string site,
            bool skipFill,
            string reason,
            int emptyCount,
            int drawCount,
            int excludeCount,
            bool hasNonExcludeCandidate)
        {
            PerfTraceRecorder.Record(
                "SkeletonFusion",
                -1,
                site,
                new Dictionary<string, string>
                {
                    ["path"] = "director",
                    ["skipFill"] = skipFill ? "1" : "0",
                    ["reason"] = reason ?? string.Empty,
                    ["emptySlotCount"] = emptyCount.ToString(),
                    ["drawCount"] = drawCount.ToString(),
                    ["excludeCount"] = excludeCount.ToString(),
                    ["hasNonExcludeCandidate"] = hasNonExcludeCandidate ? "1" : "0",
                    ["chainId"] = DirectorTrace.CurrentChainId.ToString(),
                    ["choreoSeqId"] = ChoreoTraceContext.CurrentSeqId.ToString(),
                    ["refillScheduled"] = IsRefillScheduled ? "1" : "0",
                    ["refillGateArmed"] = IsRefillGateArmed ? "1" : "0",
                });
        }

        private static bool HasAnyDealStep(BoardPresentationStep[] steps)
        {
            if (steps == null)
            {
                return false;
            }

            for (var i = 0; i < steps.Length; i++)
            {
                if (steps[i].Kind == BoardPresentationStepKind.Deal
                    && steps[i].Deals != null
                    && steps[i].Deals.Length > 0)
                {
                    return true;
                }
            }

            return false;
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
