using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    public sealed class SetOpeningPresentationGateCommand : AbstractCommand
    {
        private readonly bool mActive;

        public SetOpeningPresentationGateCommand(bool active)
        {
            mActive = active;
        }

        protected override void OnExecute()
        {
            EnsureInput().SetOpeningPresentationActive(mActive);
        }

        private IPresentationInputStateSystem EnsureInput()
        {
            var input = this.GetSystem<IPresentationInputStateSystem>();
            if (input != null)
            {
                return input;
            }

            return PresentationInputStateSystem.EnsureRegistered();
        }
    }

    public sealed class SetChoiceOverlayGateCommand : AbstractCommand
    {
        private readonly bool mActive;

        public SetChoiceOverlayGateCommand(bool active)
        {
            mActive = active;
        }

        protected override void OnExecute()
        {
            EnsureInput().SetChoiceOverlayActive(mActive);
        }

        private IPresentationInputStateSystem EnsureInput()
        {
            var input = this.GetSystem<IPresentationInputStateSystem>();
            if (input != null)
            {
                return input;
            }

            return PresentationInputStateSystem.EnsureRegistered();
        }
    }

    public sealed class SetBoardSelectModeGateCommand : AbstractCommand
    {
        private readonly bool mActive;

        public SetBoardSelectModeGateCommand(bool active)
        {
            mActive = active;
        }

        protected override void OnExecute()
        {
            EnsureInput().SetBoardSelectModeActive(mActive);
        }

        private IPresentationInputStateSystem EnsureInput()
        {
            var input = this.GetSystem<IPresentationInputStateSystem>();
            if (input != null)
            {
                return input;
            }

            return PresentationInputStateSystem.EnsureRegistered();
        }
    }

    public sealed class ResetPresentationInputGatesCommand : AbstractCommand
    {
        private readonly string mReason;

        public ResetPresentationInputGatesCommand(string reason = null)
        {
            mReason = reason;
        }

        protected override void OnExecute()
        {
            EnsureInput().ResetGates(mReason);
        }

        private IPresentationInputStateSystem EnsureInput()
        {
            var input = this.GetSystem<IPresentationInputStateSystem>();
            if (input != null)
            {
                return input;
            }

            return PresentationInputStateSystem.EnsureRegistered();
        }
    }
}
