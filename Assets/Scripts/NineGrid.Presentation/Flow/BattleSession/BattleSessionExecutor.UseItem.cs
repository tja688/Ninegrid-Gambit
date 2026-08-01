using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow
{
    internal sealed partial class BattleSessionExecutor
    {
        public async UniTask PlayDirectorUseItemPresentAsync(
            PostKillBoardPresentationResult boardResult,
            CancellationToken token)
        {
            // #8：无盘面 delta 的洗回（传送卡）也必须在用牌 Present 主线内 Flush，禁止 Forget 旁路。
            await FlushPendingShuffleIntoPresentationAsync(token);

            var useResult = _pendingUseItemPresent;
            _pendingUseItemPresent = default;
            if (!useResult.Accepted)
            {
                useResult = new UseItemPresentationResult
                {
                    Accepted = boardResult.Accepted,
                    PostKillBoard = boardResult,
                    DamagePopups = boardResult.DamagePopups ?? Array.Empty<CombatDamagePopup>(),
                    KilledTargetUids = boardResult.RemovedUids ?? Array.Empty<int>(),
                    TargetKilled = boardResult.RemovedUids != null && boardResult.RemovedUids.Length > 0,
                    AvatarDefeated = boardResult.AvatarDefeated,
                    NodeClearedOrRewardPhase = boardResult.NodeClearedOrRewardPhase,
                    RewardChoicePending =
                        NineGridArchitecture.Current.GetModel<PendingChoiceModel>().Kind.Value
                        == PendingChoiceKind.Reward,
                };
            }
            else if (!useResult.PostKillBoard.Accepted && boardResult.Accepted)
            {
                useResult.PostKillBoard = boardResult;
            }

            await PlayUseItemPresentCoreAsync(useResult, token);
        }

        private async UniTask PlayUseItemPresentCoreAsync(
            UseItemPresentationResult useResult,
            CancellationToken ct)
        {
            // UseItem Present：只刷已提交投影的视觉；飘字/血甲在 Vacate 前认领，TriggerEffect 留给运动后。
            CoreCardPresentationMapper.RefreshVisualsPreservingCommittedStatsOnAllSpawned();
            PresentationOutputProjector.UpdateAvatarDebugText();

            // ADR-0018：Vacate 前只冲刷非 TriggerEffect 的 Impact（保飘字世界坐标）；
            // TriggerEffect 留在 pending，由后续 Drain 运动落地 Impact（无 delta 则由 PresentStep FlushBeats）消费。
            BattleBeatFlush.FlushImpactExcept(PresentationInstructionKind.TriggerEffect);

            // 击杀必须先 Vacate 尸体，再 Drain/Sync；否则 Register 会静默挤占格留下钉住幽灵。
            if (useResult.TargetKilled)
            {
                BeginUseItemLethalVictims(useResult, ct);
            }

            if (useResult.PostKillBoard.Accepted
                && ((useResult.PostKillBoard.Steps != null && useResult.PostKillBoard.Steps.Length > 0)
                    || (useResult.PostKillBoard.Moves != null && useResult.PostKillBoard.Moves.Length > 0)
                    || (useResult.PostKillBoard.Deals != null && useResult.PostKillBoard.Deals.Length > 0)
                    || (useResult.PostKillBoard.RemovedUids != null && useResult.PostKillBoard.RemovedUids.Length > 0)))
            {
                await DrainPostKillBoardAsync(useResult.PostKillBoard, ct);
            }
            // #10：无盘面 delta 时不再 soft Sync；几何由导演 Present 维护。

            if (useResult.AvatarDefeated)
            {
                EnsureBattleEndedIfAvatarDefeated(
                    new PostKillBoardPresentationResult
                    {
                        Accepted = true,
                        AvatarDefeated = true,
                        DamagePopups = useResult.DamagePopups,
                        Steps = useResult.PostKillBoard.Steps,
                        Moves = useResult.PostKillBoard.Moves,
                        Deals = useResult.PostKillBoard.Deals,
                        RemovedUids = useResult.PostKillBoard.RemovedUids,
                        NodeClearedOrRewardPhase = useResult.NodeClearedOrRewardPhase,
                    },
                    ct);
                return;
            }

            if (useResult.RewardChoicePending)
            {
                // 局内宝箱等：仍在 InteractionLoop，当场 Bounce；通关奖励由主循环接 OnNodeSettlementReady。
                var phase = NineGridArchitecture.Current.GetSystem<IPhaseSystem>().CurrentPhase;
                if (phase == GamePhase.RewardItemChoice)
                {
                    TryEnterNodeSettlement();
                }
                else
                {
                    await PresentRewardChoiceFromCoreAsync(hoverOnNotice: false);
                }

                return;
            }

            // 击杀清场由旋转批投影 ScheduleNodeSettlement；此处仅处理用牌批内已进入结算相位的非击杀路径。
            if (useResult.NodeClearedOrRewardPhase && !useResult.TargetKilled)
            {
                TryEnterNodeSettlement();
            }
        }

        /// <summary>
        /// UseItem 击杀：对齐 FieldBattle 卸尸（MarkFieldDead → Vacate → 异步 Release）。
        /// </summary>
        private void BeginUseItemLethalVictims(UseItemPresentationResult useResult, CancellationToken cancellationToken)
        {
                        var battle = ResolveBattlePresentation();
            if (battle == null || Cards == null)
            {
                Debug.LogWarning("[BattleSession] UseItem 击杀卸尸缺少 FieldBattle/CardManager。");
                return;
            }

            var killed = useResult.KilledTargetUids;
            if (killed == null || killed.Length == 0)
            {
                return;
            }

            for (var i = 0; i < killed.Length; i++)
            {
                var uid = killed[i];
                if (uid <= 0 || !Cards.TryGet(uid, out var victim) || victim == null)
                {
                    continue;
                }

                battle.TryBeginLethalVictimPresentation(victim, cancellationToken);
            }
        }

    }
}
