using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 玩家跳格意图：经唯一收口 <see cref="IIntentIntake"/> 进入编排。
    /// </summary>
    public sealed class SubmitBoardWalkIntentCommand : AbstractCommand<bool>
    {
        private readonly int mGroundSlot;

        public SubmitBoardWalkIntentCommand(int groundSlot)
        {
            mGroundSlot = groundSlot;
        }

        protected override bool OnExecute()
        {
            var intake = this.GetSystem<IIntentIntake>()
                ?? IntentIntakeSystem.EnsureRegistered();
            bool preview;
            var disposition = intake.Submit(
                new InputIntent(InputIntentKinds.BoardWalk, mGroundSlot),
                InputOwner.ProtectedField,
                out preview);

            var accepted = disposition == IntentDisposition.Allow
                           || disposition == IntentDisposition.BufferToDirector;
            if (!accepted)
            {
                Debug.LogWarning(
                    "[BoardWalk] IntentIntake disposition=" + disposition
                    + " slot=" + mGroundSlot
                    + " owner=" + PresentationInputGates.CurrentOwner
                    + " choiceOverlay=" + PresentationInputGates.ChoiceOverlayActive);
            }
            else
            {
                Debug.Log(
                    "[BoardWalk] accepted disposition=" + disposition
                    + " slot=" + mGroundSlot
                    + " owner=" + PresentationInputGates.CurrentOwner);
            }

            return accepted;
        }
    }
}
