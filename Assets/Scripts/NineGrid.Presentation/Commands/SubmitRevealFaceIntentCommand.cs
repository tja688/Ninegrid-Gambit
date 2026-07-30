using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 玩家主动翻开意图：经唯一收口 <see cref="IIntentIntake"/> 进入编排。
    /// </summary>
    public sealed class SubmitRevealFaceIntentCommand : AbstractCommand<bool>
    {
        private readonly int mGroundSlot;

        public SubmitRevealFaceIntentCommand(int groundSlot)
        {
            mGroundSlot = groundSlot;
        }

        protected override bool OnExecute()
        {
            var intake = this.GetSystem<IIntentIntake>()
                ?? IntentIntakeSystem.EnsureRegistered();
            bool preview;
            var disposition = intake.Submit(
                new InputIntent(InputIntentKinds.RevealFace, mGroundSlot),
                InputOwner.ProtectedField,
                out preview);

            return disposition == IntentDisposition.Allow
                || disposition == IntentDisposition.BufferToDirector;
        }
    }
}
