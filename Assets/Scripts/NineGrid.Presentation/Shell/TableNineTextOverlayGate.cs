using UnityEngine;

namespace NineGrid.Presentation.Shell
{
    /// <summary>
    /// 控制 TableNine Text Overlay 三窗口互斥显隐。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineTextOverlayGate : MonoBehaviour
    {
        [SerializeField] private GameObject inGameInfoRoot;
        [SerializeField] private GameObject roomInfoRoot;
        [SerializeField] private GameObject noticeRoot;

        public void ApplyForScreen(MainFlowScreen screen)
        {
            bool showInGame = screen == MainFlowScreen.RunSession
                || screen == MainFlowScreen.NodePlaying
                || screen == MainFlowScreen.NodeAdvance;
            bool showNotice = screen == MainFlowScreen.Victory || screen == MainFlowScreen.Defeat;

            SetActive(inGameInfoRoot, showInGame);
            SetActive(roomInfoRoot, false);
            SetActive(noticeRoot, showNotice);
        }

        public void ShowRoomInfo(bool show)
        {
            SetActive(roomInfoRoot, show);
        }

        public void HideAll()
        {
            SetActive(inGameInfoRoot, false);
            SetActive(roomInfoRoot, false);
            SetActive(noticeRoot, false);
        }

        private static void SetActive(GameObject root, bool active)
        {
            if (root != null)
            {
                root.SetActive(active);
            }
        }
    }
}
