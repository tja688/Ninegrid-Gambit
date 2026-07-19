using System.Collections.Generic;
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

    /// <summary>
    /// 表现层可信命中：一段伤害，无交互门禁。
    /// </summary>
    public sealed class CombatHitCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly int mAttackerUid;
        private readonly int mTargetUid;

        public CombatHitCommand(int attackerUid, int targetUid)
        {
            mAttackerUid = attackerUid;
            mTargetUid = targetUid;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().ApplyCombatHit(mAttackerUid, mTargetUid);
        }
    }

    /// <summary>
    /// 击杀后盘面结算：补牌 + 旋转 + 清场判定（整拍兼容）。
    /// </summary>
    public sealed class ResolvePostKillBoardCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().ResolvePostKillBoard();
        }
    }

    /// <summary>
    /// 击杀后分拍：交互计数 + 补牌。
    /// </summary>
    public sealed class ResolvePostKillFillCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().ResolvePostKillFill();
        }
    }

    /// <summary>
    /// 击杀后分拍：旋转 + 清场判定。
    /// </summary>
    public sealed class ResolvePostKillRotateCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().ResolvePostKillRotate();
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
        private readonly IReadOnlyList<int> mSelectedCardUids;
        private readonly string mSelectedOption;

        public UseItemCommand(int itemUid)
            : this(itemUid, null, null)
        {
        }

        public UseItemCommand(int itemUid, IReadOnlyList<int> selectedCardUids)
            : this(itemUid, selectedCardUids, null)
        {
        }

        public UseItemCommand(int itemUid, IReadOnlyList<int> selectedCardUids, string selectedOption)
        {
            mItemUid = itemUid;
            mSelectedCardUids = selectedCardUids == null ? new int[0] : new List<int>(selectedCardUids).ToArray();
            mSelectedOption = selectedOption ?? string.Empty;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().UseItem(mItemUid, mSelectedCardUids, mSelectedOption);
        }
    }

    public sealed class SelectRewardCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly int mOptionIndex;

        public SelectRewardCommand(int optionIndex)
        {
            mOptionIndex = optionIndex;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().SelectReward(mOptionIndex);
        }
    }

    public sealed class SkipHelpChoiceCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().SkipHelpChoice();
        }
    }

    public sealed class SelectRoomCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly int mOptionIndex;

        public SelectRoomCommand(int optionIndex)
        {
            mOptionIndex = optionIndex;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().SelectRoom(mOptionIndex);
        }
    }

    public sealed class EnterRoomCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().EnterRoom();
        }
    }

    public sealed class PresentationFinishedCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly int mBatchId;

        public PresentationFinishedCommand(int batchId)
        {
            mBatchId = batchId;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPresentationSyncSystem>().FinishBatch(mBatchId);
        }
    }
}
