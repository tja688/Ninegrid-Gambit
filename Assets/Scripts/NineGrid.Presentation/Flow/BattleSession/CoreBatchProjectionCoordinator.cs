using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using UnityEngine;
using QFramework;

namespace NineGrid.Flow
{
    /// <summary>
    /// 核心批次投影协调：ApplyCombatHit / ResolvePostKillBoard 与六类 On*BatchProjected 的 EventLog 扫描、
    /// 投影构造与 Channel 入队；副作用经 <see cref="PresentationOutputProjector"/> 与 <see cref="BoardPresentationPlayer"/> 路由。
    /// 不拥有 Unity 场景对象。
    /// </summary>
    internal sealed class CoreBatchProjectionCoordinator
    {
        private readonly BattleSessionExecutor _session;

        public CoreBatchProjectionCoordinator(BattleSessionExecutor session)
        {
            _session = session;
        }

        public CombatHitPresentationResult ApplyCombatHitFromCore(int attackerUid, int targetUid)
        {
            var reason = BattleTraceRecorder.ConsumePendingReason("CombatHit");
            var arch = NineGridArchitecture.Current;
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var phaseBefore = phaseSystem.CurrentPhase.ToString();
            BattleTraceCardSnap attackerSnap = null;
            BattleTraceCardSnap targetSnap = null;
            try
            {
                if (BattleTraceRecorder.Enabled || FlowTraceRecorder.Enabled)
                {
                    BattleTraceRecorder.BeginSessionIfNeeded();
                    attackerSnap = BattleTraceRecorder.TryCaptureCard(attackerUid);
                    targetSnap = BattleTraceRecorder.TryCaptureCard(targetUid);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] BattleTrace pre-hit: " + ex.Message);
            }

            var result = phaseSystem.ApplyCombatHit(attackerUid, targetUid);
            var summary = new CombatHitPresentationResult { Accepted = result.Accepted };
            if (!result.Accepted)
            {
                Debug.LogWarning($"[InBattleManager] CombatHit 被拒: {result.Reason}");
                try
                {
                    if (BattleTraceRecorder.Enabled)
                    {
                        var endIndex = pipeline.EventLog.Entries.Count;
                        var events = BattleTraceRecorder.SliceEvents(startIndex, endIndex);
                        BattleTraceRecorder.RecordOp(new BattleTraceOp
                        {
                            opKind = "CombatHit",
                            reason = reason,
                            apiPath = "PhaseSystem.ApplyCombatHit",
                            phaseBefore = phaseBefore,
                            phaseAfter = phaseSystem.CurrentPhase.ToString(),
                            attacker = attackerSnap,
                            target = targetSnap,
                            eventStartIndex = startIndex,
                            eventEndIndex = endIndex,
                            events = events,
                            presentation = new BattleTracePresentation
                            {
                                accepted = false,
                                rejectReason = result.Reason ?? string.Empty,
                            },
                            verdictHints = BattleTraceRecorder.BuildVerdictHints(events, false, false),
                        });
                        RecordCombatHitFlowSummary(
                            reason,
                            attackerSnap,
                            targetSnap,
                            phaseBefore,
                            phaseSystem.CurrentPhase.ToString(),
                            accepted: false,
                            damageAmount: 0,
                            targetKilled: false,
                            avatarDefeated: false,
                            avatarHpAfter: targetSnap != null ? targetSnap.hp : -1,
                            rejectReason: result.Reason);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[InBattleManager] BattleTrace reject: " + ex.Message);
                }

                return summary;
            }

            var entries = pipeline.EventLog.Entries;
            var popups = new List<CombatDamagePopup>(4);
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.DamageDealt && e.Amount > 0 && e.TargetUid > 0)
                {
                    popups.Add(new CombatDamagePopup { TargetUid = e.TargetUid, Amount = e.Amount });
                    if (e.TargetUid == targetUid)
                    {
                        summary.DamageAmount = e.Amount;
                        summary.RemainingHp = e.RemainingHp;
                        summary.RemainingArmor = e.RemainingArmor;
                    }
                }

                if (e.Type == CoreEventType.CardKilled && e.CardUid == targetUid)
                {
                    summary.TargetKilled = true;
                }
            }

            summary.DamagePopups = popups.Count > 0 ? popups.ToArray() : Array.Empty<CombatDamagePopup>();
            _session.BoardPlayer.PresentShuffleIntoDeckFromEventLog(startIndex);

            if (!summary.TargetKilled
                && arch.GetModel<CardRegistry>().TryGet(targetUid, out var target)
                && (target.Zone.Value == ZoneId.Graveyard || target.Zone.Value == ZoneId.Removed
                    || arch.GetSystem<IStatSystem>().GetEffectiveInt(target, StatId.Hp) <= 0))
            {
                summary.TargetKilled = target.Kind != CardKind.Avatar;
            }

            var phase = phaseSystem.CurrentPhase;
            summary.AvatarDefeated = phase == GamePhase.Defeat;
            summary.NodeClearedOrRewardPhase = NodeSettlementReadiness.IsPostClearPhase(
                phase,
                arch.GetSystem<IDeckSystem>().IsNodeCleared());

            FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out _, out var removedUids, out var steps);
            summary.Steps = steps;
            summary.Moves = moves;
            summary.Deals = deals;
            summary.RemovedUids = removedUids;

            try
            {
                if (BattleTraceRecorder.Enabled || FlowTraceRecorder.Enabled)
                {
                    var endIndex = entries.Count;
                    if (BattleTraceRecorder.Enabled)
                    {
                        var events = BattleTraceRecorder.SliceEvents(startIndex, endIndex);
                        BattleTraceRecorder.RecordOp(new BattleTraceOp
                        {
                            opKind = "CombatHit",
                            reason = reason,
                            apiPath = "PhaseSystem.ApplyCombatHit",
                            phaseBefore = phaseBefore,
                            phaseAfter = phase.ToString(),
                            attacker = attackerSnap,
                            target = targetSnap,
                            eventStartIndex = startIndex,
                            eventEndIndex = endIndex,
                            events = events,
                            presentation = new BattleTracePresentation
                            {
                                accepted = summary.Accepted,
                                damageAmount = summary.DamageAmount,
                                targetKilled = summary.TargetKilled,
                                avatarDefeated = summary.AvatarDefeated,
                                nodeClearedOrRewardPhase = summary.NodeClearedOrRewardPhase,
                            },
                            verdictHints = BattleTraceRecorder.BuildVerdictHints(
                                events, summary.TargetKilled, summary.AvatarDefeated),
                        });
                    }

                    RecordCombatHitFlowSummary(
                        reason,
                        attackerSnap,
                        targetSnap,
                        phaseBefore,
                        phase.ToString(),
                        accepted: true,
                        damageAmount: summary.DamageAmount,
                        targetKilled: summary.TargetKilled,
                        avatarDefeated: summary.AvatarDefeated,
                        avatarHpAfter: ResolveAvatarHpAfterHit(arch, targetUid, summary));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] BattleTrace post-hit: " + ex.Message);
            }

            return summary;
        }

        private static int ResolveAvatarHpAfterHit(
            IArchitecture arch,
            int targetUid,
            CombatHitPresentationResult summary)
        {
            try
            {
                var board = arch.GetModel<BoardModel>();
                var avatarUid = board.AvatarUid.Value;
                // 始终读打后有效 HP；勿信 summary.RemainingHp（0 伤时常为默认 0）。
                if (arch.GetModel<CardRegistry>().TryGet(avatarUid, out var avatar) && avatar != null)
                {
                    return arch.GetSystem<IStatSystem>().GetEffectiveInt(avatar, StatId.Hp);
                }

                if (targetUid == avatarUid && summary.DamageAmount > 0)
                {
                    return summary.RemainingHp;
                }
            }
            catch
            {
                // ignore
            }

            return -1;
        }

        private static void RecordCombatHitFlowSummary(
            string reason,
            BattleTraceCardSnap attackerSnap,
            BattleTraceCardSnap targetSnap,
            string phaseBefore,
            string phaseAfter,
            bool accepted,
            int damageAmount,
            bool targetKilled,
            bool avatarDefeated,
            int avatarHpAfter,
            string rejectReason = null)
        {
            try
            {
                if (!FlowTraceRecorder.Enabled)
                {
                    return;
                }

                FlowTraceRecorder.BeginSessionIfNeeded();
                FlowTraceRecorder.Record(
                    FlowTraceCategory.CombatSummary,
                    FlowTraceNames.CombatHitSummary,
                    new Dictionary<string, string>
                    {
                        { "reason", reason ?? string.Empty },
                        { "rejectReason", rejectReason ?? string.Empty },
                        { "attackerDefId", attackerSnap != null ? attackerSnap.defId : string.Empty },
                        { "targetDefId", targetSnap != null ? targetSnap.defId : string.Empty },
                        { "attackerUid", attackerSnap != null ? attackerSnap.uid.ToString() : "0" },
                        { "targetUid", targetSnap != null ? targetSnap.uid.ToString() : "0" },
                        { "damage", damageAmount.ToString() },
                        { "targetKilled", targetKilled ? "true" : "false" },
                        { "avatarDefeated", avatarDefeated ? "true" : "false" },
                        { "avatarHpAfter", avatarHpAfter.ToString() },
                    },
                    phaseBefore: phaseBefore,
                    phaseAfter: phaseAfter,
                    accepted: accepted,
                    refBattleOpIndex: BattleTraceRecorder.LastOpIndex);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] FlowTrace CombatHitSummary: " + ex.Message);
            }
        }

        public PostKillBoardPresentationResult ResolvePostKillBoardFromCore()
        {
            var arch = NineGridArchitecture.Current;
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var phaseBefore = phaseSystem.CurrentPhase.ToString();
            var result = phaseSystem.ResolvePostKillBoard();
            var phase = phaseSystem.CurrentPhase;
            var summary = new PostKillBoardPresentationResult
            {
                Accepted = result.Accepted,
                AvatarDefeated = phase == GamePhase.Defeat,
                NodeClearedOrRewardPhase = NodeSettlementReadiness.IsPostClearPhase(
                    phase,
                    arch.GetSystem<IDeckSystem>().IsNodeCleared()),
            };

            if (!result.Accepted)
            {
                Debug.LogWarning($"[InBattleManager] ResolvePostKillBoard 被拒: {result.Reason}");
                summary.Moves = Array.Empty<PostKillCardMove>();
                summary.Deals = Array.Empty<PostKillCardDeal>();
                summary.RemovedUids = Array.Empty<int>();
                summary.DamagePopups = Array.Empty<CombatDamagePopup>();
                try
                {
                    if (BattleTraceRecorder.Enabled)
                    {
                        var endIndex = pipeline.EventLog.Entries.Count;
                        var events = BattleTraceRecorder.SliceEvents(startIndex, endIndex);
                        BattleTraceRecorder.RecordOp(new BattleTraceOp
                        {
                            opKind = "PostKillBoard",
                            reason = "PostKillBoard",
                            apiPath = "PhaseSystem.ResolvePostKillBoard",
                            phaseBefore = phaseBefore,
                            phaseAfter = phase.ToString(),
                            eventStartIndex = startIndex,
                            eventEndIndex = endIndex,
                            events = events,
                            presentation = new BattleTracePresentation { accepted = false },
                            verdictHints = BattleTraceRecorder.BuildVerdictHints(events, false, false),
                        });
                        RecordPostKillFlowSummary(
                            arch,
                            phaseBefore,
                            phase.ToString(),
                            accepted: false,
                            moveCount: 0,
                            dealCount: 0);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[InBattleManager] BattleTrace PostKill reject: " + ex.Message);
                }

                return summary;
            }

            FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out _, out var removedUids, out var steps);
            summary.Steps = steps;
            summary.Moves = moves;
            summary.Deals = deals;
            summary.RemovedUids = removedUids;
            summary.DamagePopups = PresentationOutputProjector.CollectDamagePopups(pipeline.EventLog.Entries, startIndex);
            _session.BoardPlayer.PresentShuffleIntoDeckFromEventLog(startIndex);

            try
            {
                if (BattleTraceRecorder.Enabled)
                {
                    var endIndex = pipeline.EventLog.Entries.Count;
                    var events = BattleTraceRecorder.SliceEvents(startIndex, endIndex);
                    BattleTraceRecorder.RecordOp(new BattleTraceOp
                    {
                        opKind = "PostKillBoard",
                        reason = "PostKillBoard",
                        apiPath = "PhaseSystem.ResolvePostKillBoard",
                        phaseBefore = phaseBefore,
                        phaseAfter = phase.ToString(),
                        eventStartIndex = startIndex,
                        eventEndIndex = endIndex,
                        events = events,
                        presentation = new BattleTracePresentation
                        {
                            accepted = summary.Accepted,
                            avatarDefeated = summary.AvatarDefeated,
                            nodeClearedOrRewardPhase = summary.NodeClearedOrRewardPhase,
                        },
                        verdictHints = BattleTraceRecorder.BuildVerdictHints(
                            events, false, summary.AvatarDefeated),
                    });
                    RecordPostKillFlowSummary(
                        arch,
                        phaseBefore,
                        phase.ToString(),
                        accepted: true,
                        moveCount: moves != null ? moves.Length : 0,
                        dealCount: deals != null ? deals.Length : 0);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] BattleTrace PostKill: " + ex.Message);
            }

            return summary;
        }

        private static void RecordPostKillFlowSummary(
            IArchitecture arch,
            string phaseBefore,
            string phaseAfter,
            bool accepted,
            int moveCount,
            int dealCount)
        {
            try
            {
                if (!FlowTraceRecorder.Enabled)
                {
                    return;
                }

                var deckEmpty = true;
                var enemyDrawEmpty = true;
                var drawPileCount = 0;
                try
                {
                    var deck = arch.GetModel<DeckModel>();
                    drawPileCount = deck.DrawPileUids.Count;
                    deckEmpty = drawPileCount == 0;
                    enemyDrawEmpty = !arch.GetSystem<IDeckSystem>().HasEnemyInDrawPile();
                }
                catch
                {
                    // ignore
                }

                FlowTraceRecorder.BeginSessionIfNeeded();
                FlowTraceRecorder.Record(
                    FlowTraceCategory.Deck,
                    FlowTraceNames.PostKillBoard,
                    new Dictionary<string, string>
                    {
                        { "moveCount", moveCount.ToString() },
                        { "dealCount", dealCount.ToString() },
                        { "drawPileCount", drawPileCount.ToString() },
                        { "deckEmpty", deckEmpty ? "true" : "false" },
                        { "enemyDrawEmpty", enemyDrawEmpty ? "true" : "false" },
                    },
                    phaseBefore: phaseBefore,
                    phaseAfter: phaseAfter,
                    accepted: accepted,
                    refBattleOpIndex: BattleTraceRecorder.LastOpIndex);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] FlowTrace PostKillBoard: " + ex.Message);
            }
        }

        public static void FillBoardDeltaFromEventLog(
            IActionPipelineSystem pipeline,
            int startIndex,
            out PostKillCardMove[] moves,
            out PostKillCardDeal[] deals,
            out int pickedUid)
        {
            FillBoardDeltaFromEventLog(pipeline, startIndex, out moves, out deals, out pickedUid, out _, out _);
        }

        public static void FillBoardDeltaFromEventLog(
            IActionPipelineSystem pipeline,
            int startIndex,
            out PostKillCardMove[] moves,
            out PostKillCardDeal[] deals,
            out int pickedUid,
            out int[] removedUids)
        {
            FillBoardDeltaFromEventLog(
                pipeline,
                startIndex,
                out moves,
                out deals,
                out pickedUid,
                out removedUids,
                out _);
        }

        public static void FillBoardDeltaFromEventLog(
            IActionPipelineSystem pipeline,
            int startIndex,
            out PostKillCardMove[] moves,
            out PostKillCardDeal[] deals,
            out int pickedUid,
            out int[] removedUids,
            out BoardPresentationStep[] steps)
        {
            pickedUid = 0;
            moves = Array.Empty<PostKillCardMove>();
            deals = Array.Empty<PostKillCardDeal>();
            removedUids = Array.Empty<int>();
            steps = Array.Empty<BoardPresentationStep>();
            if (pipeline?.EventLog?.Entries == null)
            {
                return;
            }

            var registry = NineGridArchitecture.Current.GetModel<CardRegistry>();
            var projection = BoardPresentationStepProjector.Project(
                pipeline.EventLog.Entries,
                startIndex,
                registry);
            pickedUid = projection.PickedUid;
            moves = projection.LegacyMoves ?? Array.Empty<PostKillCardMove>();
            deals = projection.LegacyDeals ?? Array.Empty<PostKillCardDeal>();
            removedUids = projection.LegacyRemovedUids ?? Array.Empty<int>();
            steps = projection.Steps ?? Array.Empty<BoardPresentationStep>();
        }

        public void OnExploreBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            var pipeline = NineGridArchitecture.Current.GetSystem<IActionPipelineSystem>();
            result.DamagePopups = PresentationOutputProjector.CollectDamagePopups(pipeline.EventLog.Entries, startIndex);

            _session.ExplorePresentChannel?.Enqueue(result);
            _session.BoardPlayer.PresentShuffleIntoDeckFromEventLog(startIndex);
        }

        public void OnAttackHitBatchProjected(
            int startIndex,
            int boardSlot,
            int resolvedCombatUid,
            PostKillBoardPresentationResult result)
        {
            var arch = NineGridArchitecture.Current;
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            result.DamagePopups = PresentationOutputProjector.CollectDamagePopups(pipeline.EventLog.Entries, startIndex);

            RecordIntentCombatOpDiagnostics(
                arch,
                pipeline,
                startIndex,
                "IntentCombatHit",
                arch.GetModel<BoardModel>().AvatarUid.Value,
                resolvedCombatUid,
                result);

            _session.AttackHitPresentChannel?.Enqueue(boardSlot, resolvedCombatUid, result);
            _session.BoardPlayer.PresentShuffleIntoDeckFromEventLog(startIndex);
        }

        /// <summary>
        /// Intent 交战批（玩家命中 / 反击 / 先手 / 齐射伤害）不经 ApplyCombatHitFromCore；此处补
        /// BattleTrace / FlowTrace，否则 battlelog 只有 StartNode，反击缺席、献身等 OnRemove
        /// 效果均无法从日志核对（2026-08-12 禁反击跨局泄漏即因反击批无 op 而难以定位）。
        /// </summary>
        private static void RecordIntentCombatOpDiagnostics(
            IArchitecture arch,
            IActionPipelineSystem pipeline,
            int startIndex,
            string reason,
            int attackerUid,
            int targetUid,
            PostKillBoardPresentationResult result)
        {
            try
            {
                if (pipeline?.EventLog?.Entries == null)
                {
                    return;
                }

                var endIndex = pipeline.EventLog.Entries.Count;
                var targetKilled = IntentBatchProjection.ContainsCardKilled(
                    pipeline, startIndex, targetUid);
                var damageAmount = 0;
                for (var i = startIndex; i < endIndex; i++)
                {
                    var e = pipeline.EventLog.Entries[i];
                    if (e.Type == CoreEventType.DamageDealt
                        && e.TargetUid == targetUid
                        && e.Amount > damageAmount)
                    {
                        damageAmount = e.Amount;
                    }
                }

                if (BattleTraceRecorder.Enabled)
                {
                    BattleTraceRecorder.BeginSessionIfNeeded();
                    var events = BattleTraceRecorder.SliceEvents(startIndex, endIndex);
                    var attackerSnap = BattleTraceRecorder.TryCaptureCard(attackerUid);
                    var targetSnap = BattleTraceRecorder.TryCaptureCard(targetUid);
                    BattleTraceRecorder.RecordOp(new BattleTraceOp
                    {
                        opKind = "CombatHit",
                        reason = reason,
                        apiPath = "CombatHitCommand",
                        phaseBefore = string.Empty,
                        phaseAfter = arch.GetSystem<IPhaseSystem>().CurrentPhase.ToString(),
                        attacker = attackerSnap,
                        target = targetSnap,
                        eventStartIndex = startIndex,
                        eventEndIndex = endIndex,
                        events = events,
                        presentation = new BattleTracePresentation
                        {
                            accepted = result.Accepted,
                            damageAmount = damageAmount,
                            targetKilled = targetKilled,
                            avatarDefeated = result.AvatarDefeated,
                            nodeClearedOrRewardPhase = result.NodeClearedOrRewardPhase,
                        },
                        verdictHints = BattleTraceRecorder.BuildVerdictHints(
                            events, targetKilled, result.AvatarDefeated),
                    });
                }

                if (!FlowTraceRecorder.Enabled)
                {
                    return;
                }

                FlowTraceRecorder.BeginSessionIfNeeded();
                RecordEffectAndStatFlowEvents(pipeline, startIndex, endIndex);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] Intent CombatHit diagnostics: " + ex.Message);
            }
        }

        private static void RecordEffectAndStatFlowEvents(
            IActionPipelineSystem pipeline,
            int startIndex,
            int endIndex)
        {
            // EffectTriggered 已改由 RhythmFaceFlowTraceBinder 全链路扫描记录（#206），
            // 此处只补交战链的 BaseStatModified，避免双写。
            var entries = pipeline.EventLog.Entries;
            for (var i = startIndex; i < endIndex; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.BaseStatModified)
                {
                    FlowTraceRecorder.Record(
                        FlowTraceCategory.CombatSummary,
                        FlowTraceNames.BaseStatModified,
                        new Dictionary<string, string>
                        {
                            { "stat", ((StatId)e.Amount).ToString() },
                            { "delta", e.Delta.ToString() },
                            { "resultValue", e.ResultValue.ToString() },
                            { "cardUid", e.CardUid.ToString() },
                            { "targetUid", e.TargetUid.ToString() },
                            { "reason", e.Message ?? string.Empty },
                            { "sourceDefId", e.SourceDefId ?? string.Empty },
                        },
                        accepted: true,
                        refBattleOpIndex: BattleTraceRecorder.LastOpIndex);
                }
            }
        }

        public void OnAttackBoardBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            var pipeline = NineGridArchitecture.Current.GetSystem<IActionPipelineSystem>();
            result.DamagePopups = PresentationOutputProjector.CollectDamagePopups(pipeline.EventLog.Entries, startIndex);

            _session.AttackBoardPresentChannel?.Enqueue(result);
            _session.BoardPlayer.PresentShuffleIntoDeckFromEventLog(startIndex);

            if (result.NodeClearedOrRewardPhase)
            {
                ScheduleNodeSettlementAfterBoardPresent();
            }
        }

        public void OnAttackCounterBatchProjected(
            int startIndex,
            int attackerBoardSlot,
            int attackerUid,
            PostKillBoardPresentationResult result)
        {
            var arch = NineGridArchitecture.Current;
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            result.DamagePopups = PresentationOutputProjector.CollectDamagePopups(pipeline.EventLog.Entries, startIndex);

            // 怪→玩家批（反击 / 先手 / 齐射伤害）同样落 battlelog op；
            // reason 由派发方经 CombatHitTraceContext 标注，缺省按反击记。
            RecordIntentCombatOpDiagnostics(
                arch,
                pipeline,
                startIndex,
                BattleTraceRecorder.ConsumePendingReason("IntentCounterHit"),
                attackerUid,
                arch.GetModel<BoardModel>().AvatarUid.Value,
                result);

            _session.AttackCounterPresentChannel?.Enqueue(attackerBoardSlot, attackerUid, result);
            _session.BoardPlayer.PresentShuffleIntoDeckFromEventLog(startIndex);
        }

        public void OnUseItemBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            var pipeline = NineGridArchitecture.Current.GetSystem<IActionPipelineSystem>();
            result.DamagePopups = PresentationOutputProjector.CollectDamagePopups(pipeline.EventLog.Entries, startIndex);
            _session.PendingUseItemPresent = BuildUseItemPresentationFromBatch(startIndex, result);

            _session.UseItemPresentChannel?.Enqueue(result);
            _session.BoardPlayer.PresentShuffleIntoDeckFromEventLog(startIndex);
        }

        public void OnUseItemBoardBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            var pipeline = NineGridArchitecture.Current.GetSystem<IActionPipelineSystem>();
            result.DamagePopups = PresentationOutputProjector.CollectDamagePopups(pipeline.EventLog.Entries, startIndex);

            _session.UseItemBoardPresentChannel?.Enqueue(result);
            _session.BoardPlayer.PresentShuffleIntoDeckFromEventLog(startIndex);

            if (result.NodeClearedOrRewardPhase)
            {
                ScheduleNodeSettlementAfterBoardPresent();
            }
        }

        public void OnUseItemResolvedWithoutKill()
        {
            // 非击杀副作用（宝箱 Bounce 等）已在用牌 Present 通道处理。
        }

        private UseItemPresentationResult BuildUseItemPresentationFromBatch(
            int startIndex,
            PostKillBoardPresentationResult boardResult)
        {
            var arch = NineGridArchitecture.Current;
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var entries = pipeline.EventLog.Entries;
            var killedUids = new List<int>(2);
            var primaryUid = 0;
            var damageAmount = 0;

            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.CardKilled && e.CardUid > 0 && !killedUids.Contains(e.CardUid))
                {
                    killedUids.Add(e.CardUid);
                }

                if (e.Type == CoreEventType.DamageDealt && e.Amount > 0 && e.TargetUid > 0)
                {
                    if (primaryUid == 0)
                    {
                        primaryUid = e.TargetUid;
                    }

                    if (e.TargetUid == primaryUid)
                    {
                        damageAmount = e.Amount;
                    }
                }
            }

            if (primaryUid == 0 && killedUids.Count > 0)
            {
                primaryUid = killedUids[0];
            }

            var phase = arch.GetSystem<IPhaseSystem>().CurrentPhase;
            return new UseItemPresentationResult
            {
                Accepted = true,
                TargetKilled = killedUids.Count > 0,
                KilledTargetUids = killedUids.Count > 0 ? killedUids.ToArray() : Array.Empty<int>(),
                DamagePopups = boardResult.DamagePopups ?? Array.Empty<CombatDamagePopup>(),
                DamageAmount = damageAmount,
                PrimaryTargetUid = primaryUid,
                AvatarDefeated = phase == GamePhase.Defeat || boardResult.AvatarDefeated,
                RewardChoicePending =
                    arch.GetModel<PendingChoiceModel>().Kind.Value == PendingChoiceKind.Reward,
                NodeClearedOrRewardPhase = boardResult.NodeClearedOrRewardPhase,
                PostKillBoard = boardResult,
            };
        }

        private void ScheduleNodeSettlementAfterBoardPresent()
        {
            // 旋转批投影已进清场相位：等当前 Present drain 完成后再结算（下一帧检查导演空闲）。
            AwaitDirectorIdleThenSettleAsync().Forget();
        }

        private async UniTaskVoid AwaitDirectorIdleThenSettleAsync()
        {
            var token = _session.EnsurePresentationToken();
            try
            {
                while (BattleSessionExecutor.IsPresentationMainlineBusy())
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                _session.TryEnterNodeSettlement();
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
