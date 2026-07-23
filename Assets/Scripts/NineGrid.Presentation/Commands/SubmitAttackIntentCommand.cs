using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 玩家攻击意图：经唯一收口 <see cref="IIntentIntake"/> 进入编排。
    /// </summary>
    public sealed class SubmitAttackIntentCommand : AbstractCommand<bool>
    {
        private readonly int mGroundSlot;

        public SubmitAttackIntentCommand(int groundSlot)
        {
            mGroundSlot = groundSlot;
        }

        protected override bool OnExecute()
        {
            var intake = this.GetSystem<IIntentIntake>()
                ?? IntentIntakeSystem.EnsureRegistered();
            bool preview;
            var disposition = intake.Submit(
                new InputIntent(InputIntentKinds.Attack, mGroundSlot),
                InputOwner.ProtectedField,
                out preview);

            if (disposition == IntentDisposition.Reject)
            {
                this.SendEvent(new AttackIntentRejectedEvent
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
