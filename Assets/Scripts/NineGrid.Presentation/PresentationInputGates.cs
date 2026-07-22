using NineGrid.Core;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation
{
    /// <summary>
    /// Cards / Flow 宿主读门禁与写 external-hold 的薄访问；避免散落 Architecture 判空。
    /// </summary>
    public static class PresentationInputGates
    {
        public static IPresentationInputStateSystem OrNull()
        {
            return NineGridArchitecture.Interface?.GetSystem<IPresentationInputStateSystem>();
        }

        public static IPresentationInputStateSystem Ensure()
        {
            return PresentationInputStateSystem.EnsureRegistered();
        }

        public static bool OpeningPresentationActive =>
            OrNull()?.OpeningPresentationActive.Value ?? false;

        public static bool ChoiceOverlayActive =>
            OrNull()?.ChoiceOverlayActive.Value ?? false;

        public static bool BoardSelectModeActive =>
            OrNull()?.BoardSelectModeActive.Value
            ?? NineGrid.Cards.BoardCardSelectModeController.IsActive;

        public static bool MainlineBusy => OrNull()?.MainlineBusy ?? false;

        public static bool HasExternalHold => OrNull()?.HasExternalHold ?? false;

        public static void SetOpening(bool active)
        {
            var arch = NineGridArchitecture.Interface;
            if (arch == null)
            {
                Ensure()?.SetOpeningPresentationActive(active);
                return;
            }

            arch.SendCommand(new SetOpeningPresentationGateCommand(active));
        }

        public static void SetChoiceOverlay(bool active)
        {
            var arch = NineGridArchitecture.Interface;
            if (arch == null)
            {
                Ensure()?.SetChoiceOverlayActive(active);
                return;
            }

            arch.SendCommand(new SetChoiceOverlayGateCommand(active));
        }

        public static void SetBoardSelect(bool active)
        {
            var arch = NineGridArchitecture.Interface;
            if (arch == null)
            {
                Ensure()?.SetBoardSelectModeActive(active);
                return;
            }

            arch.SendCommand(new SetBoardSelectModeGateCommand(active));
        }

        public static void Reset(string reason = null)
        {
            var arch = NineGridArchitecture.Interface;
            if (arch == null)
            {
                Ensure()?.ResetGates(reason);
                return;
            }

            arch.SendCommand(new ResetPresentationInputGatesCommand(reason));
        }

        public static bool TryBeginExternalHold(string reason = null)
        {
            var arch = NineGridArchitecture.Interface;
            if (arch == null)
            {
                return false;
            }

            return arch.SendCommand(new BeginDirectorExternalHoldCommand(reason));
        }

        public static void EndExternalHold(string reason = null)
        {
            var arch = NineGridArchitecture.Interface;
            if (arch == null)
            {
                return;
            }

            arch.SendCommand(new EndDirectorExternalHoldCommand(reason));
        }

        public static void ForceEndExternalHold(string reason = null)
        {
            var arch = NineGridArchitecture.Interface;
            if (arch == null)
            {
                return;
            }

            arch.SendCommand(new ForceEndDirectorExternalHoldCommand(reason));
        }
    }
}
