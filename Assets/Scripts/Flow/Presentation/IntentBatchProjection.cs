using System;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 导演解算批 → 盘面表演摘要的共享投影（explore / attack / useItem 切片复用）。
    /// </summary>
    public static class IntentBatchProjection
    {
        public static PostKillBoardPresentationResult Build(
            IArchitecture architecture,
            IActionPipelineSystem pipeline,
            int startIndex)
        {
            if (architecture == null)
            {
                throw new ArgumentNullException("architecture");
            }

            if (pipeline == null)
            {
                throw new ArgumentNullException("pipeline");
            }

            var phase = architecture.GetSystem<IPhaseSystem>();
            var summary = new PostKillBoardPresentationResult
            {
                Accepted = true,
                AvatarDefeated = phase.CurrentPhase == GamePhase.Defeat,
                NodeClearedOrRewardPhase =
                    phase.CurrentPhase == GamePhase.RewardItemChoice
                    || phase.CurrentPhase == GamePhase.ClearCheck
                    || phase.CurrentPhase == GamePhase.NodeCompleted
                    || architecture.GetSystem<IDeckSystem>().IsNodeCleared(),
            };

            var registry = architecture.GetModel<CardRegistry>();
            var projection = BoardPresentationStepProjector.Project(
                pipeline.EventLog.Entries,
                startIndex,
                registry);
            summary.Steps = projection.Steps ?? Array.Empty<BoardPresentationStep>();
            summary.Moves = projection.LegacyMoves ?? Array.Empty<PostKillCardMove>();
            summary.Deals = projection.LegacyDeals ?? Array.Empty<PostKillCardDeal>();
            summary.RemovedUids = projection.LegacyRemovedUids ?? Array.Empty<int>();
            summary.DamagePopups = Array.Empty<CombatDamagePopup>();
            return summary;
        }

        public static bool ContainsCardKilled(
            IActionPipelineSystem pipeline,
            int startIndex,
            int cardUid)
        {
            if (pipeline == null || cardUid <= 0)
            {
                return false;
            }

            var entries = pipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.CardKilled && entries[i].CardUid == cardUid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
