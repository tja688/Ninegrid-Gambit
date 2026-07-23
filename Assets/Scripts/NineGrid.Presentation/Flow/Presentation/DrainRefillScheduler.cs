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
    /// drain 退场补牌锁步：非击杀移除 Present 之后按需追加 ResolveDrainRefill → Present。
    /// V4：由静态 Lockstep 收为实例调度器，Resolve 亦可经 Presentation Command 统一入口。
    /// </summary>
    public sealed class DrainRefillScheduler
    {
        public void AppendAfterPresentIfNeeded(
            BattleTimeline timeline,
            Func<bool> needsDrainRefill,
            Action<BattleTimeline> enqueueDrainRefill)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException("timeline");
            }

            if (needsDrainRefill == null)
            {
                throw new ArgumentNullException("needsDrainRefill");
            }

            if (enqueueDrainRefill == null)
            {
                throw new ArgumentNullException("enqueueDrainRefill");
            }

            timeline.Enqueue(new AttackPostHitBranchStep(timeline, needsDrainRefill, enqueueDrainRefill));
        }

        public void EnqueueRefillBatches(
            BattleTimeline timeline,
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel boardPresentChannel,
            int boardSlot,
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
            var refillGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(
                    architecture,
                    dispatcher,
                    boardSlot,
                    onBoardBatchProjected));

            timeline.Enqueue(new ResolveBatchStep(refillGate));
            timeline.Enqueue(new PresentStep(refillGate, boardPresentChannel));
        }

        public bool ShouldRefill(IArchitecture architecture)
        {
            if (architecture == null)
            {
                return false;
            }

            var phase = architecture.GetSystem<IPhaseSystem>();
            if (phase.CurrentPhase != GamePhase.InteractionLoop)
            {
                return false;
            }

            var deck = architecture.GetModel<DeckModel>();
            if (deck == null || deck.DrawPileUids == null || deck.DrawPileUids.Count <= 0)
            {
                return false;
            }

            return FusionRefillPlanner.HasEmptyBoardSlot(architecture.GetModel<BoardModel>());
        }

        public CoreCommandDispatchResult ResolveAndProject(
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            int boardSlot,
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

            if (!ShouldRefill(architecture))
            {
                return dispatcher.Send(new ResolveDrainRefillCommand(skipFill: true));
            }

            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            PerfTraceRecorder.Record(
                "DrainRefill",
                -1,
                "RefillBatchBegin",
                new Dictionary<string, string>
                {
                    ["path"] = "director",
                    ["chainId"] = DirectorTrace.CurrentChainId.ToString(),
                    ["choreoSeqId"] = ChoreoTraceContext.CurrentSeqId.ToString(),
                });

            var dispatch = dispatcher.Send(new ResolveDrainRefillCommand());
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

            PerfTraceRecorder.Record(
                "DrainRefill",
                -1,
                "RefillBatchEnd",
                new Dictionary<string, string>
                {
                    ["path"] = "director",
                    ["chainId"] = DirectorTrace.CurrentChainId.ToString(),
                    ["choreoSeqId"] = ChoreoTraceContext.CurrentSeqId.ToString(),
                });

            return dispatch;
        }
    }
}
