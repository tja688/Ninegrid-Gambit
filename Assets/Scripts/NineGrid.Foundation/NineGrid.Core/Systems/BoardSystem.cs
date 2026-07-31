using System.Collections.Generic;
using NineGrid.Core.Stats;
using QFramework;

namespace NineGrid.Core.Systems
{
    public interface IBoardSystem : ISystem
    {
        IReadOnlyList<SlotId> ClockwisePath { get; }
        bool AreAdjacent(SlotId left, SlotId right);
        bool AreAdjacent(CardInstance left, CardInstance right);
        bool AreAdjacent(CardInstance left, SlotId rightSlot, int rightUid);
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
            if (!left.IsBoardSlot || !right.IsBoardSlot)
            {
                return false;
            }

            if (left.IsAdjacentTo(right))
            {
                return true;
            }

            var board = this.GetModel<BoardModel>();
            return AreVirtuallyAdjacent(board.GetCardUid(left), board.GetCardUid(right));
        }

        public bool AreAdjacent(CardInstance left, CardInstance right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return AreAdjacent(left, right.Slot.Value, right.Uid);
        }

        public bool AreAdjacent(CardInstance left, SlotId rightSlot, int rightUid)
        {
            if (left == null || !left.Slot.Value.IsBoardSlot || !rightSlot.IsBoardSlot)
            {
                return false;
            }

            if (left.Slot.Value.IsAdjacentTo(rightSlot))
            {
                return true;
            }

            return AreVirtuallyAdjacent(left.Uid, rightUid);
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

        private bool AreVirtuallyAdjacent(int leftUid, int rightUid)
        {
            if (HasVirtualAdjacency(leftUid, rightUid) || HasVirtualAdjacency(rightUid, leftUid))
            {
                return true;
            }

            return HasGlobalMonsterAdjacencyBetween(leftUid, rightUid);
        }

        private bool HasVirtualAdjacency(int ownerUid, int targetUid)
        {
            if (ownerUid == 0 || targetUid == 0 || ownerUid == targetUid)
            {
                return false;
            }

            var registry = this.GetModel<CardRegistry>();
            CardInstance owner;
            CardInstance target;
            if (!registry.TryGet(ownerUid, out owner)
                || !registry.TryGet(targetUid, out target)
                || owner.Kind != CardKind.Monster
                || target.Kind != CardKind.Monster)
            {
                return false;
            }

            var statSystem = this.GetSystem<IStatSystem>();
            var context = statSystem.CreateContext(owner).WithTarget(targetUid);
            return statSystem.EvaluateRule(RuleId.VirtualAdjacency, 0f, context) > 0f;
        }

        private bool HasGlobalMonsterAdjacencyBetween(int leftUid, int rightUid)
        {
            if (leftUid == 0 || rightUid == 0 || leftUid == rightUid)
            {
                return false;
            }

            var registry = this.GetModel<CardRegistry>();
            CardInstance left;
            CardInstance right;
            if (!registry.TryGet(leftUid, out left)
                || !registry.TryGet(rightUid, out right)
                || left.Kind != CardKind.Monster
                || right.Kind != CardKind.Monster)
            {
                return false;
            }

            return MonsterBoardRules.HasGlobalMonsterAdjacency(
                this.GetSystem<IStatSystem>(),
                this.GetModel<BoardModel>(),
                registry);
        }
    }
}
