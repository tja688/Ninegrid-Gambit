using System.Collections.Generic;
using QFramework;

namespace NineGrid.Core.Systems
{
    public interface IBoardSystem : ISystem
    {
        IReadOnlyList<SlotId> ClockwisePath { get; }
        bool AreAdjacent(SlotId left, SlotId right);
        bool IsSlotAvailable(SlotId slot);
        int RotateClockwise();
        int Swap(SlotId left, SlotId right);
        int FillEmptySlots();
    }

    public sealed class BoardSystem : AbstractSystem, IBoardSystem
    {
        public IReadOnlyList<SlotId> ClockwisePath
        {
            get { return RotateBoardClockwiseAction.ClockwisePath; }
        }

        protected override void OnInit()
        {
        }

        public bool AreAdjacent(SlotId left, SlotId right)
        {
            return left.IsAdjacentTo(right);
        }

        public bool IsSlotAvailable(SlotId slot)
        {
            var board = this.GetModel<BoardModel>();
            return slot.IsBoardSlot && slot != board.AvatarSlot.Value && board.IsEmpty(slot);
        }

        public int RotateClockwise()
        {
            return this.GetSystem<IActionPipelineSystem>().Execute(new RotateBoardClockwiseAction());
        }

        public int Swap(SlotId left, SlotId right)
        {
            return this.GetSystem<IActionPipelineSystem>().Execute(new SwapBoardSlotsAction(left, right));
        }

        public int FillEmptySlots()
        {
            return this.GetSystem<IActionPipelineSystem>().Execute(new FillEmptySlotsAction());
        }
    }
}
