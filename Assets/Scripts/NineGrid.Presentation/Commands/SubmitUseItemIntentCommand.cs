using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 玩家用牌意图：经唯一收口 <see cref="IIntentIntake"/> 进入编排。
    /// </summary>
    public sealed class SubmitUseItemIntentCommand : AbstractCommand<bool>
    {
        private readonly int mItemUid;
        private readonly int[] mSelectedCardUids;
        private readonly string mSelectedOption;

        public SubmitUseItemIntentCommand(
            int itemUid,
            int[] selectedCardUids,
            string selectedOption)
        {
            mItemUid = itemUid;
            mSelectedCardUids = selectedCardUids;
            mSelectedOption = selectedOption;
        }

        protected override bool OnExecute()
        {
            if (mItemUid <= 0)
            {
                return false;
            }

            var intake = this.GetSystem<IIntentIntake>()
                ?? IntentIntakeSystem.EnsureRegistered();
            bool preview;
            var disposition = intake.Submit(
                new InputIntent(
                    InputIntentKinds.UseItem,
                    mItemUid,
                    mSelectedCardUids,
                    mSelectedOption),
                InputOwner.ProtectedField,
                out preview);

            if (disposition == IntentDisposition.RouteToBoardSelect)
            {
                return false;
            }

            if (disposition == IntentDisposition.Reject)
            {
                this.SendEvent(new UseItemIntentRejectedEvent
                {
                    ItemUid = mItemUid,
                    Reason = "intentIntakeReject"
                });
                return false;
            }

            return disposition == IntentDisposition.Allow
                || disposition == IntentDisposition.BufferToDirector;
        }
    }
}
