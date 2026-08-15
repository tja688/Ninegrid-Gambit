using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using UnityEngine;

namespace NineGrid.Flow
{
    internal sealed partial class BattleSessionExecutor
    {
        public async UniTask<bool> ValidateHandDragApplyAsync(ManagedCard card, int? targetGroundSlot)
        {
            if (card == null)
            {
                return false;
            }

            if (PresentationInputGates.ChoiceOverlayActive)
            {
                LogHandDragApplyReject(card, "choice-overlay");
                return false;
            }

            // ADR-0032：非战斗相位（RoomChoice / RewardItemChoice）仅 usableOutsideBattle=true 的卡可拖到棋盘使用；
            // 其余（含 RoomEvent）ApplyZone 一律回手，回收区不受此限。
            var phase = NineGridArchitecture.Current?.GetSystem<IPhaseSystem>();
            if (phase == null)
            {
                LogHandDragApplyReject(card, "no-phase");
                return false;
            }

            var inBattle = phase.CurrentPhase == GamePhase.InteractionLoop;
            if (!inBattle && !ItemUseEligibility.IsUsableInCurrentPhase(NineGridArchitecture.Current, card.DefId))
            {
                LogHandDragApplyReject(card, "not-usable-outside-battle");
                return false;
            }

            // MainlineBusy 由 IntentIntake（BoardSelect begin / UseItem）裁决，此处不提前短路。
            // 旧属性三选一（Attack/Armor/Hp Bounce）已退役（#90）；永久属性改由属性房发卡。

            HelpCardBoardSelectResolver.TryGetPlayKind(
                card.DefId,
                out var playKind,
                out var selectedCardsSpec);

            // ADR-0032：非战斗只放行免目标类（None）；需选目标/多选的战斗卡维持战斗限定。
            if (!inBattle && playKind != HelpCardPlayKind.None)
            {
                LogHandDragApplyReject(card, "non-battle-playKind=" + playKind);
                return false;
            }

            if (playKind == HelpCardPlayKind.MultiBoardSelect)
            {
                if (!HelpCardBoardSelectResolver.TryGetRequiredBoardSelectCount(card.DefId, out var boardSelectCount)
                    || !BoardCardSelectModeController.Begin(card.Uid, card.DefId, boardSelectCount))
                {
                    LogHandDragApplyReject(card, "board-select-begin-failed");
                    return false;
                }

                return true;
            }

            int[] selectedUids = null;
            if (playKind == HelpCardPlayKind.SingleDragTarget)
            {
                if (!TryResolveSingleDragTarget(targetGroundSlot, selectedCardsSpec, out var targetUid))
                {
                    LogHandDragApplyReject(card, "single-drag-target-missing");
                    return false;
                }

                selectedUids = new[] { targetUid };
            }

            if (UseItemInputHook.TrySubmitUseItem == null)
            {
                LogHandDragApplyReject(card, "use-item-hook-unwired");
                Debug.LogWarning("[BattleSession] UseItemInputHook.TrySubmitUseItem 未装配。");
                return false;
            }

            if (!UseItemInputHook.TrySubmitUseItem(card.Uid, selectedUids, null))
            {
                // 权威门禁镜像已判「遗物栏满拒开宝箱」时，在卡视图消失前提示 + 拒绝音；
                // 返回 false 让拖放路径直接回手（FinishDragWithReturnAsync）。
                string legalityReason;
                if (!BoardIntentLegality.TryExplainUseItem(
                        NineGridArchitecture.Current,
                        card.Uid,
                        selectedUids,
                        null,
                        out legalityReason)
                    && RejectedUseItemRecovery.IsChestRelicFullRejection(
                        NineGridArchitecture.Current,
                        card.DefId))
                {
                    RejectedUseItemRecovery.SurfaceRejection(
                        legalityReason,
                        card.DefId,
                        chestRelicFull: true);
                }

                LogHandDragApplyReject(card, "use-item-submit-false");
                return false;
            }

            return true;
        }

        private static void LogHandDragApplyReject(ManagedCard card, string detail)
        {
            var defId = card != null ? card.DefId : "?";
            var uid = card != null ? card.Uid : 0;
            BoardIntentGateDiagnostics.LogConsole(
                "HandDragApply",
                "reject uid=" + uid + " defId=" + defId + " detail=" + detail,
                NineGridArchitecture.Current,
                GameCommandKind.UseItem);
        }

        private bool TryResolveSingleDragTarget(
            int? targetGroundSlot,
            HelpCardSelectedCardsSpec spec,
            out int targetUid)
        {
            targetUid = 0;
            if (!targetGroundSlot.HasValue || Field == null)
            {
                return false;
            }

            if (!Field.TryGetCardAt(targetGroundSlot.Value, out var targetCard)
                || targetCard == null)
            {
                return false;
            }

            if (targetCard.CoreKind == CardPresentationKind.Avatar)
            {
                return false;
            }

            if (spec.RequiresTrueMonster && targetCard.CoreKind != CardPresentationKind.Monster)
            {
                return false;
            }

            if (spec.RequiresCombatTarget
                && targetCard.CoreKind != CardPresentationKind.Monster
                && targetCard.CoreKind != CardPresentationKind.Trap)
            {
                return false;
            }

            targetUid = targetCard.Uid;
            return targetUid > 0;
        }

        public void AbortBoardSelectIfActive(string reason)
        {
            if (!BoardCardSelectModeController.IsActive)
            {
                // RequestAbort 旧路径可能留下门禁布尔粘连：IsActive=false 但 gate=true。
                if (PresentationInputGates.BoardSelectModeActive)
                {
                    BoardCardSelectModeController.End();
                }

                return;
            }

            BoardCardSelectModeController.RequestAbort(reason);
        }

        public async UniTask OnBoardSelectionCompletedAsync(int itemUid, int[] selectedUids)
        {
            var defId = BoardCardSelectModeController.ItemDefId;
            var requiredCount = BoardCardSelectModeController.RequiredCount;
            if (itemUid <= 0 || selectedUids == null || selectedUids.Length == 0)
            {
                await RestoreBoardSelectItemToHandAsync(itemUid, defId, "invalid-selection");
                return;
            }

            // #10：BoardSelect 合法性读 BoardModel，不再前置 Sync 自愈占格镜像。
            if (!TryValidateBoardSelectTargets(selectedUids, requiredCount, out var validateReason))
            {
                Debug.LogWarning(
                    $"[BattleSession] BoardSelect 目标校验失败 itemUid={itemUid} reason={validateReason} uids={string.Join(",", selectedUids)}");
                await RestoreBoardSelectItemToHandAsync(itemUid, defId, $"invalid-targets:{validateReason}");
                return;
            }

            if (!await TryAwaitDirectorIdleForBoardSelectAsync())
            {
                await RestoreBoardSelectItemToHandAsync(itemUid, defId, "director-busy-timeout");
                return;
            }

            if (UseItemInputHook.TrySubmitUseItem == null)
            {
                Debug.LogWarning("[BattleSession] UseItemInputHook.TrySubmitUseItem 未装配。");
                await RestoreBoardSelectItemToHandAsync(itemUid, defId, "use-intent-unwired");
                return;
            }

            if (!UseItemInputHook.TrySubmitUseItem(itemUid, selectedUids, null))
            {
                BoardIntentGateDiagnostics.LogConsole(
                    "BoardSelectUseItem",
                    "use-intent-rejected itemUid=" + itemUid,
                    NineGridArchitecture.Current,
                    GameCommandKind.UseItem);
                await RestoreBoardSelectItemToHandAsync(itemUid, defId, "use-intent-rejected");
                return;
            }

            RegistryTraceSink.NotifyUserInteraction?.Invoke("BoardSelectUseItemAccepted");
            await VanishParkedBoardSelectItemIfPresentAsync(itemUid);
            await UniTask.CompletedTask;
        }

        public UniTask OnBoardSelectionAbortedAsync(int itemUid, string defId, string reason)
        {
            return RestoreBoardSelectItemToHandAsync(itemUid, defId, reason);
        }

        private static async UniTask<bool> TryAwaitDirectorIdleForBoardSelectAsync()
        {
            const int stepMs = 50;
            var elapsed = 0;
            while (elapsed < BoardSelectLockWaitMs)
            {
                if (!PresentationInputGates.ChoiceOverlayActive
                    && !PresentationInputGates.MainlineBusy)
                {
                    return true;
                }

                await UniTask.Delay(stepMs);
                elapsed += stepMs;
            }

            return false;
        }

        private static bool TryValidateBoardSelectTargets(int[] selectedUids, int requiredCount, out string failReason)
        {
            failReason = null;
            if (selectedUids == null || selectedUids.Length == 0)
            {
                failReason = "empty";
                return false;
            }

            if (requiredCount > 0 && selectedUids.Length != requiredCount)
            {
                failReason = "count-mismatch";
                return false;
            }

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                failReason = "no-arch";
                return false;
            }

            var registry = arch.GetModel<CardRegistry>();
            var board = arch.GetModel<BoardModel>();
            for (var i = 0; i < selectedUids.Length; i++)
            {
                var uid = selectedUids[i];
                if (uid <= 0 || !registry.TryGet(uid, out var card) || card == null)
                {
                    failReason = $"missing-uid:{uid}";
                    return false;
                }

                if (card.Kind == CardKind.Avatar)
                {
                    failReason = $"avatar-uid:{uid}";
                    return false;
                }

                if (card.Zone.Value != ZoneId.Board)
                {
                    failReason = $"zone-{card.Zone.Value}-uid:{uid}";
                    return false;
                }

                var slot = card.Slot.Value;
                if (!slot.IsBoardSlot || board.GetCardUid(slot) != uid)
                {
                    failReason = $"slot-mismatch-uid:{uid}";
                    return false;
                }
            }

            return true;
        }

        private static async UniTask<bool> WaitForHandReadyForRestoreAsync(CardHandManagerSingleton hand)
        {
            if (hand == null)
            {
                return false;
            }

            const int stepMs = 50;
            var elapsed = 0;
            while (elapsed < BoardSelectLockWaitMs)
            {
                if (!hand.IsDragging
                    && !PresentationInputGates.BoardSelectModeActive
                    && (hand.CanAcceptCard || !hand.IsBusy))
                {
                    return true;
                }

                await UniTask.Delay(stepMs);
                elapsed += stepMs;
            }

            return !hand.IsDragging
                   && !PresentationInputGates.BoardSelectModeActive
                   && (hand.CanAcceptCard || !hand.IsBusy);
        }

        private async UniTask RestoreBoardSelectItemToHandAsync(int itemUid, string defId, string reason)
        {
            if (itemUid <= 0)
            {
                return;
            }

            Debug.LogWarning(
                $"[BattleSession] BoardSelectAbort itemUid={itemUid} defId={defId ?? "?"} reason={reason ?? "unknown"}");
            BoardIntentGateDiagnostics.LogConsole(
                "BoardSelectAbort",
                "itemUid=" + itemUid + " defId=" + (defId ?? "?") + " reason=" + (reason ?? "unknown"),
                NineGridArchitecture.Current,
                GameCommandKind.UseItem);
            RegistryTraceSink.NotifyUserInteraction?.Invoke($"BoardSelectAbort:{reason}");

            BoardCardSelectModeController.End();

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            var registry = arch.GetModel<CardRegistry>();
            if (!registry.TryGet(itemUid, out var coreCard)
                || coreCard.Zone.Value != ZoneId.ItemSlots)
            {
                Debug.LogWarning(
                    $"[BattleSession] BoardSelectAbort skip restore: item not in ItemSlots uid={itemUid}");
                return;
            }

            var deck = arch.GetModel<DeckModel>();
            var inSlots = false;
            for (var i = 0; i < deck.ItemSlotUids.Count; i++)
            {
                if (deck.ItemSlotUids[i] == itemUid)
                {
                    inSlots = true;
                    break;
                }
            }

            if (!inSlots)
            {
                return;
            }

            defId = string.IsNullOrEmpty(defId) ? coreCard.DefId : defId;
                        if (Cards == null)
            {
                return;
            }

            if (!Cards.TryGet(itemUid, out var card) || card == null)
            {
                card = Cards.SpawnView(
                    itemUid,
                    defId,
                    initialMode: CardDisplayMode.HandCardMode,
                    kind: CoreCardPresentationMapper.ResolvePresentationKind(itemUid, defId));
                if (card != null)
                {
                    CoreCardPresentationMapper.ApplyToManagedCard(card);
                    CardFaceGenerationBootstrap.ApplyFaceHistoryForUid(arch, itemUid);
                }
            }
            else
            {
                CardHandManagerSingleton.DisarmBoardSelectParkedHitProxy(card);
                Cards.SetDisplayMode(card, CardDisplayMode.HandCardMode);
                CardOpacityUtility.ResetAlpha(card);
            }

            if (card == null)
            {
                return;
            }

            var hand = Hand;
            if (hand == null)
            {
                return;
            }

            if (!await WaitForHandReadyForRestoreAsync(hand))
            {
                Debug.LogWarning(
                    $"[BattleSession] BoardSelectAbort restore hand busy timeout itemUid={itemUid}");
                return;
            }

            var restored = await hand.PullFromGroundAsync(card);
            if (!restored)
            {
                Debug.LogWarning(
                    $"[BattleSession] BoardSelectAbort PullFromGround failed itemUid={itemUid}");
            }
        }

        private async UniTask VanishParkedBoardSelectItemIfPresentAsync(int itemUid)
        {
            if (itemUid <= 0)
            {
                return;
            }

                        if (Cards == null || !Cards.TryGet(itemUid, out var card) || card == null)
            {
                return;
            }

            var hand = Hand;
            if (hand == null)
            {
                CardHandManagerSingleton.DisarmBoardSelectParkedHitProxy(card);
                (Cards ?? CardEntityLifecycleHook.CardsOrNull()).Release(card, "BoardSelect.VanishNoHand");
                return;
            }

            await hand.VanishParkedBoardSelectItemAsync(card);
        }
    }
}
