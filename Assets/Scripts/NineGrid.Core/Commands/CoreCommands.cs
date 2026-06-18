using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Core.Commands
{
    public sealed class StartNodeCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly NodeDeckOptions mOptions;

        public StartNodeCommand(NodeDeckOptions options)
        {
            mOptions = options;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().StartNode(mOptions);
        }
    }

    public sealed class AttackCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly SlotId mTargetSlot;

        public AttackCommand(SlotId targetSlot)
        {
            mTargetSlot = targetSlot;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().Attack(mTargetSlot);
        }
    }

    public sealed class PickupItemCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly SlotId mTargetSlot;

        public PickupItemCommand(SlotId targetSlot)
        {
            mTargetSlot = targetSlot;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().PickupItem(mTargetSlot);
        }
    }

    public sealed class ClickEmptyCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly SlotId mTargetSlot;

        public ClickEmptyCommand(SlotId targetSlot)
        {
            mTargetSlot = targetSlot;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().ClickEmpty(mTargetSlot);
        }
    }

    public sealed class UseItemCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly int mItemUid;

        public UseItemCommand(int itemUid)
        {
            mItemUid = itemUid;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().UseItem(mItemUid);
        }
    }
}
