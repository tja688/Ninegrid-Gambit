using System;
using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Flow.Diagnostics;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 旁路融合（无 Intent 剧本 FusionRefill 门）播完后，向主线补挂 FusionRefill。
    /// 由 CompositionRoot 注册；BoardPresentationPlayer 在 PresentEnd 调用。
    /// </summary>
    public static class FusionRefillAftermath
    {
        private static Action<IReadOnlyList<int>> sSchedule;

        public static void Register(Action<IReadOnlyList<int>> schedule)
        {
            sSchedule = schedule;
        }

        public static void Unregister()
        {
            sSchedule = null;
        }

        /// <summary>
        /// 尚未入队 FusionRefill 时，尝试向主线追加补牌批次。
        /// 门武装（gateArmed）只表示剧本挂了分支，不阻止 Aftermath 兜底入队。
        /// </summary>
        public static bool TrySchedule(IReadOnlyList<int> excludeResultUids)
        {
            if (FusionRefillScheduler.IsRefillScheduled)
            {
                return false;
            }

            if (sSchedule == null)
            {
                return false;
            }

            sSchedule(excludeResultUids ?? Array.Empty<int>());
            return FusionRefillScheduler.IsRefillScheduled;
        }

        /// <summary>生产接线：经 Runtime.MutateMainline 入队 FusionRefill。</summary>
        public static Action<IReadOnlyList<int>> CreateProductionScheduler(
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            Func<IPresentChannel> boardPresentChannel,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected,
            FusionRefillScheduler scheduler = null)
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

            var fusionRefill = scheduler ?? new FusionRefillScheduler();
            return excludeUids =>
            {
                if (FusionRefillScheduler.IsRefillScheduled)
                {
                    return;
                }

                if (!FusionRefillPlanner.HasEmptyBoardSlot(architecture.GetModel<BoardModel>()))
                {
                    return;
                }

                var deck = architecture.GetModel<DeckModel>();
                if (deck == null || deck.DrawPileUids == null || deck.DrawPileUids.Count <= 0)
                {
                    return;
                }

                var channel = boardPresentChannel();
                if (channel == null)
                {
                    return;
                }

                var runtime = architecture.GetSystem<IPresentationRuntimeSystem>();
                if (runtime == null || !runtime.IsStarted)
                {
                    return;
                }

                if (DirectorTrace.CurrentChainId <= 0)
                {
                    DirectorTrace.BeginChain();
                }

                PerfTraceRecorder.Record(
                    "SkeletonFusion",
                    -1,
                    "AftermathRefillSchedule",
                    new Dictionary<string, string>
                    {
                        ["path"] = "director",
                        ["chainId"] = DirectorTrace.CurrentChainId.ToString(),
                        ["excludeCount"] = (excludeUids != null ? excludeUids.Count : 0).ToString(),
                    });

                runtime.MutateMainline(timeline =>
                {
                    fusionRefill.EnqueueRefillBatches(
                        timeline,
                        architecture,
                        dispatcher,
                        channel,
                        boardSlot: 0,
                        excludeUids,
                        onBoardBatchProjected);
                    FusionRefillScheduler.DisarmRefillGate();
                });
            };
        }
    }
}
