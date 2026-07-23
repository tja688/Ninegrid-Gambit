using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Systems;
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
                return false;
            }

            // MainlineBusy 由 IntentIntake（BoardSelect begin / UseItem）裁决，此处不提前短路。

                        string selectedOption = null;
            if (IsStatBoostCard(card.DefId))
            {
                HideHandCardForChoice(card);
                selectedOption = await PresentStatBoostChoiceAsync();
                if (string.IsNullOrEmpty(selectedOption))
                {
                    RestoreHandCardAfterChoiceCancel(card);
                    return false;
                }
            }

            HelpCardBoardSelectResolver.TryGetPlayKind(
                card.DefId,
                out var playKind,
                out var selectedCardsSpec);

            if (playKind == HelpCardPlayKind.MultiBoardSelect)
            {
                if (!HelpCardBoardSelectResolver.TryGetRequiredBoardSelectCount(card.DefId, out var boardSelectCount)
                    || !BoardCardSelectModeController.Begin(card.Uid, card.DefId, boardSelectCount))
                {
                    if (IsStatBoostCard(card.DefId))
                    {
                        RestoreHandCardAfterChoiceCancel(card);
                    }

                    return false;
                }

                if (HelpCardBoardSelectResolver.TryGetBoardSelectPrompt(card.DefId, out var prompt))
                {
                    DescriptionDisplayHook.RequestShowText(prompt, DescriptionShowRoute.BoardSelect);
                }

                return true;
            }

            int[] selectedUids = null;
            if (playKind == HelpCardPlayKind.SingleDragTarget)
            {
                if (!TryResolveSingleDragTarget(targetGroundSlot, selectedCardsSpec, out var targetUid))
                {
                    return false;
                }

                selectedUids = new[] { targetUid };
            }

            if (UseItemInputHook.TrySubmitUseItem == null)
            {
                Debug.LogWarning("[BattleSession] UseItemInputHook.TrySubmitUseItem 未装配。");
                if (IsStatBoostCard(card.DefId))
                {
                    RestoreHandCardAfterChoiceCancel(card);
                }

                return false;
            }

            if (!UseItemInputHook.TrySubmitUseItem(card.Uid, selectedUids, selectedOption))
            {
                if (IsStatBoostCard(card.DefId))
                {
                    RestoreHandCardAfterChoiceCancel(card);
                }

                return false;
            }

            return true;
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

            if (spec.RequiresMonster && targetCard.CoreKind != CardPresentationKind.Monster)
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

        private static void HideHandCardForChoice(ManagedCard card)
        {
            if (card?.Transform == null)
            {
                return;
            }

            CardDeckTween.KillMotion(card.Transform);
            card.Transform.localScale = Vector3.zero;
        }

        private static void RestoreHandCardAfterChoiceCancel(ManagedCard card)
        {
            if (card?.Transform == null)
            {
                return;
            }

            card.Transform.localScale = Vector3.one;
        }

        private static bool IsStatBoostCard(string defId)
        {
            return string.Equals(defId, StatBoostCardDefId, StringComparison.Ordinal);
        }
    }
}
