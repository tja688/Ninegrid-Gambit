using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// #10 / ADR-0004 Core 棋盘合法性：只裁 BoardModel / Phase 内容，不含表演时序。
    /// 忙时互斥由 IntentIntake→MainlineBusy→Director 缓冲承担。
    /// <see cref="IPresentationSyncSystem.IsInputLocked"/> 只挡 Core 解算下一拍，不得在此把
    /// 本应 Buffer 的点击误 Reject（PhaseSystem.CanExecute 在锁输入时会清空 Attack/Explore）。
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

            if (!TryExplainPhaseAllowsBoardCommand(arch, GameCommandKind.ClickEmpty, out rejectReason))
            {
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

            if (!TryExplainPhaseAllowsBoardCommand(arch, GameCommandKind.Attack, out rejectReason))
            {
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

            if (!TryExplainPhaseAllowsBoardCommand(arch, GameCommandKind.UseItem, out rejectReason))
            {
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

            // 与 PhaseSystem.ApplyPickupItem 对齐：表现可信入口不查 CanExecute。
            // ADR-0004：表演锁步（IsInputLocked）不在此拒；忙时由 IntentIntake 缓冲。
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

        /// <summary>
        /// Phase 内容门：忽略「仅因 IsInputLocked 而 CanExecute=false」——那是时序轴，不是棋盘非法。
        /// </summary>
        private static bool TryExplainPhaseAllowsBoardCommand(
            IArchitecture arch,
            GameCommandKind command,
            out string rejectReason)
        {
            rejectReason = null;
            var phase = arch.GetSystem<IPhaseSystem>();
            if (phase.CanExecute(command))
            {
                return true;
            }

            var sync = arch.GetSystem<IPresentationSyncSystem>();
            var pending = arch.GetModel<PendingChoiceModel>().Kind.Value;
            if (sync != null
                && sync.IsInputLocked
                && pending == PendingChoiceKind.None
                && IsBoardCommandPhaseWithoutLock(phase.CurrentPhase, command))
            {
                return true;
            }

            rejectReason = "notLegal phase=" + phase.CurrentPhase + " pendingChoice=" + pending;
            return false;
        }

        private static bool IsBoardCommandPhaseWithoutLock(GamePhase phase, GameCommandKind command)
        {
            switch (phase)
            {
                case GamePhase.InteractionLoop:
                    return command == GameCommandKind.Attack
                           || command == GameCommandKind.ClickEmpty
                           || command == GameCommandKind.UseItem
                           || command == GameCommandKind.PickupItem;
                case GamePhase.RoomChoice:
                    return command == GameCommandKind.UseItem
                           || command == GameCommandKind.PickupItem;
                default:
                    return false;
            }
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
