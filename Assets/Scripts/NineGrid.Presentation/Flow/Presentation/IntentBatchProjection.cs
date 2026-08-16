using System;
using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow;
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
                NodeClearedOrRewardPhase = NodeSettlementReadiness.IsPostClearPhase(
                    phase.CurrentPhase,
                    architecture.GetSystem<IDeckSystem>().IsNodeCleared()),
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
            summary.HolyDuelPunishments = ScanHolyDuelPunishments(architecture, pipeline, startIndex);
            return summary;
        }

        /// <summary>
        /// 扫描批内神圣决斗惩罚：按事件序配对 EffectTriggered（message=skill.holy_duel.activate）
        /// 与随后 source=skill.holy_duel 打向玩家卡的 DamageDealt（ActorUid=持有者）。
        /// 表现层据此在玩家攻击编排后逐个追加决斗者攻击表演。
        /// </summary>
        private static HolyDuelPunishmentEntry[] ScanHolyDuelPunishments(
            IArchitecture architecture,
            IActionPipelineSystem pipeline,
            int startIndex)
        {
            var avatarUid = architecture != null
                ? architecture.GetModel<BoardModel>().AvatarUid.Value
                : 0;
            var entries = pipeline?.EventLog?.Entries;
            if (entries == null || avatarUid <= 0)
            {
                return Array.Empty<HolyDuelPunishmentEntry>();
            }

            var results = new List<HolyDuelPunishmentEntry>(4);
            var pendingHolderUid = 0;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null)
                {
                    continue;
                }

                if (e.Type == CoreEventType.EffectTriggered
                    && string.Equals(e.Message, "skill.holy_duel.activate", StringComparison.Ordinal))
                {
                    pendingHolderUid = e.CardUid;
                }
                else if (e.Type == CoreEventType.DamageDealt
                    && e.TargetUid == avatarUid
                    && string.Equals(e.SourceDefId, "skill.holy_duel", StringComparison.Ordinal))
                {
                    var holderUid = e.ActorUid > 0 ? e.ActorUid : pendingHolderUid;
                    if (holderUid > 0 && e.Amount > 0)
                    {
                        results.Add(new HolyDuelPunishmentEntry
                        {
                            HolderUid = holderUid,
                            Amount = e.Amount,
                        });
                    }

                    pendingHolderUid = 0;
                }
            }

            return results.Count == 0
                ? Array.Empty<HolyDuelPunishmentEntry>()
                : results.ToArray();
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
