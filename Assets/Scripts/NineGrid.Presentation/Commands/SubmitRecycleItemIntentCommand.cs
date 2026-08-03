using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 玩家回收道具卡格意图：经唯一收口 <see cref="IIntentIntake"/> 进入编排。
    /// </summary>
    public sealed class SubmitRecycleItemIntentCommand : AbstractCommand<bool>
    {
        private readonly int mItemUid;

        public SubmitRecycleItemIntentCommand(int itemUid)
        {
            mItemUid = itemUid;
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
                new InputIntent(InputIntentKinds.RecycleItem, mItemUid),
                InputOwner.ProtectedField,
                out preview);

            if (disposition == IntentDisposition.Reject)
            {
                return false;
            }

            return disposition == IntentDisposition.Allow
                || disposition == IntentDisposition.BufferToDirector;
        }
    }
}
