using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 玩家探索意图：经唯一收口 <see cref="IIntentIntake"/> 进入编排。
    /// </summary>
    public sealed class SubmitExploreIntentCommand : AbstractCommand<bool>
    {
        private readonly int mGroundSlot;

        public SubmitExploreIntentCommand(int groundSlot)
        {
            mGroundSlot = groundSlot;
        }

        protected override bool OnExecute()
        {
            var intake = this.GetSystem<IIntentIntake>()
                ?? IntentIntakeSystem.EnsureRegistered();
            bool preview;
            var disposition = intake.Submit(
                new InputIntent(InputIntentKinds.Explore, mGroundSlot),
                InputOwner.ProtectedField,
                out preview);

            if (disposition == IntentDisposition.Reject)
            {
                this.SendEvent(new ExploreIntentRejectedEvent
                {
                    GroundSlot = mGroundSlot,
                    Reason = "intentIntakeReject"
                });
                return false;
            }

            return disposition == IntentDisposition.Allow
                || disposition == IntentDisposition.BufferToDirector;
        }
    }
}
