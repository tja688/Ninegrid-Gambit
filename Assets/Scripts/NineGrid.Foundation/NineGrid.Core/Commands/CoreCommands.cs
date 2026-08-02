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
    /// 击杀后盘面结算：交互计数 + 补牌 + 旋转 + 清场判定（整拍兼容）。
    /// </summary>
    public sealed class ResolvePostKillBoardCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().ResolvePostKillBoard();
        }
    }

    /// <summary>
    /// 九宫格互动分拍：全局 interactionCount +1（触发 OnInteract）。
    /// </summary>
    public sealed class AdvanceInteractionCountCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().AdvanceInteractionCount();
        }
    }

    /// <summary>
    /// 击杀后分拍：仅补牌。不含交互计数。
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

    /// <summary>
    /// 敌方行动阶段报名：倒计时 −1 并冻结行动名单。
    /// </summary>
    public sealed class RegisterEnemyActionPhaseCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().RegisterEnemyActionPhase();
        }
    }

    /// <summary>
    /// 敌方行动阶段逐条：结算名单下一条（单向打击或窗口作废）。
    /// </summary>
    public sealed class ResolveNextEnemyActionCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().ResolveNextEnemyAction();
        }
    }

    /// <summary>
    /// 敌方行动阶段收尾：补牌 + 通关检查，不旋转。
    /// </summary>
    public sealed class ResolveEnemyActionFinaleCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().ResolveEnemyActionFinale();
        }
    }

    /// <summary>
    /// 融合伴随补牌分拍：仅 FillEmptySlots（不含交互计数）。skipFill 用于打开空批 ack。
    /// </summary>
    public sealed class ResolveFusionRefillCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly bool mSkipFill;

        public ResolveFusionRefillCommand(bool skipFill = false)
        {
            mSkipFill = skipFill;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().ResolveFusionRefill(mSkipFill);
        }
    }

    /// <summary>
    /// drain 退场补牌分拍：仅 FillEmptySlots（不含交互计数）。skipFill 用于打开空批 ack。
    /// </summary>
    public sealed class ResolveDrainRefillCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly bool mSkipFill;

        public ResolveDrainRefillCommand(bool skipFill = false)
        {
            mSkipFill = skipFill;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().ResolveDrainRefill(mSkipFill);
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

    public sealed class MoveAvatarCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly SlotId mTargetSlot;

        public MoveAvatarCommand(SlotId targetSlot)
        {
            mTargetSlot = targetSlot;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().MoveAvatar(mTargetSlot);
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

    /// <summary>
    /// 表现层可信用牌：无相位门禁；击杀后补牌/旋转由导演分拍。
    /// </summary>
    public sealed class ApplyUseItemCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly int mItemUid;
        private readonly IReadOnlyList<int> mSelectedCardUids;
        private readonly string mSelectedOption;

        public ApplyUseItemCommand(int itemUid)
            : this(itemUid, null, null)
        {
        }

        public ApplyUseItemCommand(int itemUid, IReadOnlyList<int> selectedCardUids)
            : this(itemUid, selectedCardUids, null)
        {
        }

        public ApplyUseItemCommand(int itemUid, IReadOnlyList<int> selectedCardUids, string selectedOption)
        {
            mItemUid = itemUid;
            mSelectedCardUids = selectedCardUids == null ? new int[0] : new List<int>(selectedCardUids).ToArray();
            mSelectedOption = selectedOption ?? string.Empty;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().ApplyUseItem(mItemUid, mSelectedCardUids, mSelectedOption);
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

    /// <summary>主动翻开场上邻接背面卡。</summary>
    public sealed class RevealFaceCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly SlotId mTargetSlot;

        public RevealFaceCommand(SlotId targetSlot)
        {
            mTargetSlot = targetSlot;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.GetSystem<IPhaseSystem>().RevealFace(mTargetSlot);
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
