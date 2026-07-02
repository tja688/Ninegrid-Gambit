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
        private bool clickEnabled;

        public void Bind(MainFlowFsm fsm)
        {
            flowFsm = fsm;
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
            flowFsm.RequestTransition(MainFlowTransition.RoomEventClicked);
        }

        public void NotifyClicked()
        {
            OnMouseDown();
        }
    }
}
