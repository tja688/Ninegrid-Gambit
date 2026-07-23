using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// #10 idle 合法性门：主线空闲时由 Flow 对 BoardModel / Phase 裁决，Cards 只做几何命中。
    /// 与 PhaseSystem 对应命令门禁对齐，不改 Core 状态。
    /// </summary>
    public static class BoardIntentLegality
    {
        public static bool TryExplainExplore(IArchitecture arch, int groundSlot, out string rejectReason)
        {
            rejectReason = null;
            if (arch == null)
            {
                rejectReason = "noArchitecture";
                return false;
            }

            var phase = arch.GetSystem<IPhaseSystem>();
            var sync = arch.GetSystem<IPresentationSyncSystem>();
            if (sync != null && sync.IsInputLocked)
            {
                rejectReason = "presentationInputLocked activeBatchId=" + sync.ActiveBatchId;
                return false;
            }

            if (!phase.CanExecute(GameCommandKind.ClickEmpty))
            {
                var pending = arch.GetModel<PendingChoiceModel>().Kind.Value;
                rejectReason = "notLegal phase=" + phase.CurrentPhase + " pendingChoice=" + pending;
                return false;
            }

            if (groundSlot < SlotId.MinBoardIndex || groundSlot > SlotId.MaxBoardIndex)
            {
                rejectReason = "slotOutOfRange";
                return false;
            }

            var slot = SlotId.Board(groundSlot);
            var board = arch.GetModel<BoardModel>();
            if (slot == board.AvatarSlot.Value || !board.IsEmpty(slot))
            {
                rejectReason = "notEmptyOrAvatar avatarSlot=" + board.AvatarSlot.Value
                    + " occupant=" + board.GetCardUid(slot);
                return false;
            }

            if (!arch.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, slot))
            {
                rejectReason = "notAdjacent avatarSlot=" + board.AvatarSlot.Value;
                return false;
            }

            return true;
        }

        public static bool TryExplainAttack(IArchitecture arch, int groundSlot, out string rejectReason)
        {
            rejectReason = null;
            if (arch == null)
            {
                rejectReason = "noArchitecture";
                return false;
            }

            var phase = arch.GetSystem<IPhaseSystem>();
            var sync = arch.GetSystem<IPresentationSyncSystem>();
            if (sync != null && sync.IsInputLocked)
            {
                rejectReason = "presentationInputLocked activeBatchId=" + sync.ActiveBatchId;
                return false;
            }

            if (!phase.CanExecute(GameCommandKind.Attack))
            {
                var pending = arch.GetModel<PendingChoiceModel>().Kind.Value;
                rejectReason = "notLegal phase=" + phase.CurrentPhase + " pendingChoice=" + pending;
                return false;
            }

            if (groundSlot < SlotId.MinBoardIndex || groundSlot > SlotId.MaxBoardIndex)
            {
                rejectReason = "slotOutOfRange";
                return false;
            }

            var slot = SlotId.Board(groundSlot);
            var board = arch.GetModel<BoardModel>();
            if (!slot.IsBoardSlot || slot == board.AvatarSlot.Value)
            {
                rejectReason = "notBoardCardSlot";
                return false;
            }

            var targetUid = board.GetCardUid(slot);
            if (targetUid == 0)
            {
                rejectReason = "emptySlot";
                return false;
            }

            var registry = arch.GetModel<CardRegistry>();
            if (!registry.TryGet(targetUid, out var target) || target.Kind != CardKind.Monster)
            {
                rejectReason = "notMonster uid=" + targetUid;
                return false;
            }

            if (!arch.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, slot))
            {
                rejectReason = "notAdjacent avatarSlot=" + board.AvatarSlot.Value;
                return false;
            }

            // 嘲讽：允许点击非嘲讽邻怪；实际目标由 AttackIntentScriptFactory /
            // ResolvePlayerAttackTargetUid 重定向，Present 播 PlayTauntRedirectAttackAsync。
            // 此处拒点会与重定向演出打架。
            return true;
        }

        public static bool TryExplainUseItem(
            IArchitecture arch,
            int itemUid,
            IReadOnlyList<int> selectedCardUids,
            string selectedOption,
            out string rejectReason)
        {
            rejectReason = null;
            if (arch == null)
            {
                rejectReason = "noArchitecture";
                return false;
            }

            var phase = arch.GetSystem<IPhaseSystem>();
            var sync = arch.GetSystem<IPresentationSyncSystem>();
            if (sync != null && sync.IsInputLocked)
            {
                rejectReason = "presentationInputLocked activeBatchId=" + sync.ActiveBatchId;
                return false;
            }

            if (!phase.CanExecute(GameCommandKind.UseItem))
            {
                var pending = arch.GetModel<PendingChoiceModel>().Kind.Value;
                rejectReason = "notLegal phase=" + phase.CurrentPhase + " pendingChoice=" + pending;
                return false;
            }

            if (itemUid <= 0)
            {
                rejectReason = "invalidItemUid";
                return false;
            }

            var registry = arch.GetModel<CardRegistry>();
            if (!registry.TryGet(itemUid, out var card))
            {
                rejectReason = "itemMissing uid=" + itemUid;
                return false;
            }

            if (card.Zone.Value != ZoneId.ItemSlots)
            {
                rejectReason = "notInItemSlots zone=" + card.Zone.Value;
                return false;
            }

            if (!IsRegisteredInItemSlots(arch.GetModel<DeckModel>(), itemUid))
            {
                rejectReason = "notRegisteredInItemSlots";
                return false;
            }

            if (card.Kind != CardKind.Item && card.Kind != CardKind.HelpCard)
            {
                rejectReason = "notUsableItem kind=" + card.Kind;
                return false;
            }

            // selectedCardUids / selectedOption 的细校验仍由 Resolve/BoardSelect 负责；此处只拦明显非法入队。
            _ = selectedCardUids;
            _ = selectedOption;
            return true;
        }

        public static bool TryExplainPickup(IArchitecture arch, int groundSlot, out string rejectReason)
        {
            rejectReason = null;
            if (arch == null)
            {
                rejectReason = "noArchitecture";
                return false;
            }

            var sync = arch.GetSystem<IPresentationSyncSystem>();
            if (sync != null && sync.IsInputLocked)
            {
                rejectReason = "presentationInputLocked activeBatchId=" + sync.ActiveBatchId;
                return false;
            }

            // 与 PhaseSystem.ApplyPickupItem 对齐：表现可信入口不查 CanExecute。
            if (groundSlot < SlotId.MinBoardIndex || groundSlot > SlotId.MaxBoardIndex)
            {
                rejectReason = "slotOutOfRange";
                return false;
            }

            var slot = SlotId.Board(groundSlot);
            var board = arch.GetModel<BoardModel>();
            if (!slot.IsBoardSlot || slot == board.AvatarSlot.Value)
            {
                rejectReason = "notBoardCardSlot";
                return false;
            }

            var targetUid = board.GetCardUid(slot);
            if (targetUid == 0)
            {
                rejectReason = "emptySlot";
                return false;
            }

            var registry = arch.GetModel<CardRegistry>();
            if (!registry.TryGet(targetUid, out var target) || target.Kind == CardKind.Monster)
            {
                rejectReason = "notPickupable uid=" + targetUid;
                return false;
            }

            if (!arch.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, slot))
            {
                rejectReason = "notAdjacent avatarSlot=" + board.AvatarSlot.Value;
                return false;
            }

            return true;
        }

        private static bool IsRegisteredInItemSlots(DeckModel deck, int itemUid)
        {
            if (deck == null)
            {
                return false;
            }

            var itemSlots = deck.ItemSlotUids;
            for (var i = 0; i < itemSlots.Count; i++)
            {
                if (itemSlots[i] == itemUid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
