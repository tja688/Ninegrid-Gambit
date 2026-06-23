using NineGrid.Core;
using NineGrid.Core.Commands;
using QFramework;

namespace NineGrid.Presentation.FSM
{
    /// <summary>
    /// 场地交互邻接判定与 Command 类型解析（对齐 RandomAgent.AddBoardInteractionCandidates）。
    /// </summary>
    public static class BoardInteractionRules
    {
        public static bool IsValidBoardTarget(SlotId targetSlot, SlotId avatarSlot)
        {
            return targetSlot.IsBoardSlot
                && avatarSlot.IsBoardSlot
                && targetSlot != avatarSlot
                && avatarSlot.IsAdjacentTo(targetSlot);
        }

        public static SlotId FindSlotForCardUid(BoardModel board, int cardUid)
        {
            if (board == null || cardUid <= 0)
            {
                return SlotId.None;
            }

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (board.GetCardUid(slot) == cardUid)
                {
                    return slot;
                }
            }

            return SlotId.None;
        }

        public static bool TryResolveBoardCommand(
            IArchitecture architecture,
            SlotId targetSlot,
            out ICommand<CoreCommandResult> command)
        {
            command = null;
            if (architecture == null || !targetSlot.IsBoardSlot)
            {
                return false;
            }

            var board = architecture.GetModel<BoardModel>();
            var registry = architecture.GetModel<CardRegistry>();
            var avatarSlot = board.AvatarSlot.Value;

            if (!IsValidBoardTarget(targetSlot, avatarSlot))
            {
                return false;
            }

            int uid = board.GetCardUid(targetSlot);
            if (uid == 0)
            {
                command = new ClickEmptyCommand(targetSlot);
                return true;
            }

            if (!registry.TryGet(uid, out CardInstance card))
            {
                return false;
            }

            if (card.Kind == CardKind.Monster)
            {
                command = new AttackCommand(targetSlot);
            }
            else
            {
                command = new PickupItemCommand(targetSlot);
            }

            return true;
        }
    }
}
