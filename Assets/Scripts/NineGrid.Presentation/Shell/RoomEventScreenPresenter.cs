using NineGrid.Presentation.Bridge;
using UnityEngine;

namespace NineGrid.Presentation.Shell
{
    /// <summary>
    /// 房间事件屏：任意鼠标点击推进主流程。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomEventScreenPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject roomEventPanel;
        [SerializeField] private Collider2D fullScreenClickCollider;

        private MainFlowFsm flowFsm;
        private ShellCommandRouter commandRouter;
        private bool clickEnabled;

        public void Bind(MainFlowFsm fsm, ShellCommandRouter router = null)
        {
            flowFsm = fsm;
            commandRouter = router;
        }

        public void OnScreenEntered()
        {
            if (roomEventPanel != null)
            {
                roomEventPanel.SetActive(true);
            }

            clickEnabled = true;
        }

        public void OnScreenExited()
        {
            clickEnabled = false;
            if (roomEventPanel != null)
            {
                roomEventPanel.SetActive(false);
            }
        }

        private void OnMouseDown()
        {
            if (!clickEnabled || flowFsm == null)
            {
                return;
            }

            if (flowFsm.CurrentScreen != MainFlowScreen.RoomEventScreen)
            {
                return;
            }

            clickEnabled = false;

            if (UseProductionCommands)
            {
                commandRouter.SendEnterRoom();
                return;
            }

            flowFsm.RequestTransition(MainFlowTransition.RoomEventClicked);
        }

        private bool UseProductionCommands =>
            commandRouter != null
            && commandRouter.IsProduction
            && flowFsm != null
            && !flowFsm.IsHarnessMode;

        public void NotifyClicked()
        {
            OnMouseDown();
        }
    }
}
