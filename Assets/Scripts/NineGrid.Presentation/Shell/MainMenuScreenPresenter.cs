using System.Collections.Generic;
using NineGrid.Presentation.Interaction;
using UnityEngine;

namespace NineGrid.Presentation.Shell
{
    /// <summary>
    /// 主菜单屏：StartRun / QuitGame 可点，共用 General 悬停。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuScreenPresenter : MonoBehaviour
    {
        [SerializeField] private Transform startRunButton;
        [SerializeField] private Transform quitGameButton;
        [SerializeField] private SelectionOptionHoverPresenter hoverPresenter;
        [SerializeField] private SelectionFsmOwner selectionOwner;

        private MainFlowFsm flowFsm;

        public void Bind(MainFlowFsm fsm, SelectionFsm selectionFsm)
        {
            flowFsm = fsm;
            selectionOwner?.Bind(selectionFsm);
            WireHoverOptions();
        }

        public void OnScreenEntered()
        {
            gameObject.SetActive(true);
            WireHoverOptions();
        }

        public void OnScreenExited()
        {
            hoverPresenter?.ForceReset();
        }

        private void WireHoverOptions()
        {
            if (hoverPresenter == null)
            {
                return;
            }

            var actors = new List<SelectionOptionHoverPresenter.OptionActor>();
            AddOption(actors, startRunButton, 0);
            AddOption(actors, quitGameButton, 1);
            hoverPresenter.SetOptions(actors);
        }

        private static void AddOption(
            List<SelectionOptionHoverPresenter.OptionActor> actors,
            Transform option,
            int sortingOffset)
        {
            if (option == null)
            {
                return;
            }

            actors.Add(new SelectionOptionHoverPresenter.OptionActor(
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
            hoverPresenter?.PlayHover(index);
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
