using NineGrid.Cards;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>写入：几何登记 Place。</summary>
    public sealed class PlaceGroundOccupancyCommand : AbstractCommand<bool>
    {
        private readonly int mSlot;
        private readonly ManagedCard mCard;
        private readonly bool mSkipBusyGuard;

        public PlaceGroundOccupancyCommand(int slot, ManagedCard card, bool skipBusyGuard = false)
        {
            mSlot = slot;
            mCard = card;
            mSkipBusyGuard = skipBusyGuard;
        }

        protected override bool OnExecute()
        {
            var geometry = this.GetSystem<IGroundFieldGeometrySystem>();
            return geometry != null
                   && geometry.IsBound
                   && geometry.RequestPlaceCard(mSlot, mCard, mSkipBusyGuard);
        }
    }

    /// <summary>写入：仅清占格（不销毁视图）。</summary>
    public sealed class VacateGroundOccupancyCommand : AbstractCommand<bool>
    {
        private readonly int mSlot;
        private readonly bool mSkipBusyGuard;

        public VacateGroundOccupancyCommand(int slot, bool skipBusyGuard = false)
        {
            mSlot = slot;
            mSkipBusyGuard = skipBusyGuard;
        }

        protected override bool OnExecute()
        {
            var geometry = this.GetSystem<IGroundFieldGeometrySystem>();
            return geometry != null
                   && geometry.IsBound
                   && geometry.ClearSlotOccupancy(mSlot, mSkipBusyGuard);
        }
    }

    /// <summary>写入：占格迁移（可选贴锚）。</summary>
    public sealed class RelocateGroundOccupancyCommand : AbstractCommand<bool>
    {
        private readonly int mUid;
        private readonly int mToSlot;
        private readonly bool mSnapToAnchor;
        private readonly bool mSkipBusyGuard;

        public RelocateGroundOccupancyCommand(
            int uid,
            int toSlot,
            bool snapToAnchor,
            bool skipBusyGuard = false)
        {
            mUid = uid;
            mToSlot = toSlot;
            mSnapToAnchor = snapToAnchor;
            mSkipBusyGuard = skipBusyGuard;
        }

        protected override bool OnExecute()
        {
            var geometry = this.GetSystem<IGroundFieldGeometrySystem>();
            return geometry != null
                   && geometry.IsBound
                   && geometry.RequestRelocateOccupancy(mUid, mToSlot, mSnapToAnchor, mSkipBusyGuard);
        }
    }

    /// <summary>写入：清场。</summary>
    public sealed class ClearGroundOccupancyCommand : AbstractCommand
    {
        private readonly bool mForce;

        public ClearGroundOccupancyCommand(bool force = false)
        {
            mForce = force;
        }

        protected override void OnExecute()
        {
            var geometry = this.GetSystem<IGroundFieldGeometrySystem>();
            geometry?.ClearField(mForce);
        }
    }
}
