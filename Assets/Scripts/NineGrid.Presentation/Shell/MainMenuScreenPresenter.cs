using System.Collections.Generic;
using NineGrid.Presentation.Interaction;
using UnityEngine;

namespace NineGrid.Presentation.Shell
{
    /// <summary>
    /// 主菜单屏：StartRun / QuitGame 可点，共用 General 悬停黑盒。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuScreenPresenter : MonoBehaviour
    {
        [SerializeField] private Transform startRunButton;
        [SerializeField] private Transform quitGameButton;
        [SerializeField] private SelectionFsmOwner selectionOwner;

        private MainFlowFsm flowFsm;
        private SelectionPresentation presentation;

        public void Bind(MainFlowFsm fsm, SelectionFsm selectionFsm, SelectionPresentation selectionPresentation)
        {
            flowFsm = fsm;
            presentation = selectionPresentation ?? selectionOwner?.Presentation;
            selectionOwner?.Bind(selectionFsm);
            WireGeneralOptions();
        }

        public void OnScreenEntered()
        {
            gameObject.SetActive(true);
            WireGeneralOptions();
        }

        public void OnScreenExited()
        {
            presentation?.ForceGeneralReset();
        }

        private void WireGeneralOptions()
        {
            if (presentation == null)
            {
                return;
            }

            var actors = new List<SelectionPresentation.GeneralOption>();
            AddOption(actors, startRunButton, 0);
            AddOption(actors, quitGameButton, 1);
            presentation.SetGeneralOptions(actors);
        }

        private static void AddOption(
            List<SelectionPresentation.GeneralOption> actors,
            Transform option,
            int sortingOffset)
        {
            if (option == null)
            {
                return;
            }

            actors.Add(new SelectionPresentation.GeneralOption(
                option,
                option.localPosition,
                option.localEulerAngles.z,
                10 + sortingOffset));
        }

        public void HandleOptionConfirmed(int index)
        {
            if (index == 0)
            {
                flowFsm?.RequestTransition(MainFlowTransition.StartRun);
                return;
            }

            if (index == 1)
            {
                QuitGame();
            }
        }

        public void HandleOptionHovered(int index)
        {
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
