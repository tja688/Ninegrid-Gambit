using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 面板显隐路由：互斥切换主菜单 / 局内壳 / 流程叠层面板。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiPanelRouter : MonoBehaviour
    {
        [Tooltip("主菜单面板；留空则运行时按名查找 Panels/MainPanel。")]
        [SerializeField] private GameObject mainPanel;

        [Tooltip("局内 HUD 壳；留空则运行时按名查找 Panels/InGamePanels。")]
        [SerializeField] private GameObject inGamePanels;

        [Tooltip("帮助卡奖励面板；留空则运行时按名查找 Panels/RewardPanel。")]
        [SerializeField] private GameObject rewardPanel;

        [Tooltip("房间二选一面板；留空则运行时按名查找 Panels/RoomChoisePanel。")]
        [SerializeField] private GameObject roomChoicePanel;

        [Tooltip("房间事件面板；留空则运行时按名查找 Panels/RoomEventPanel。")]
        [SerializeField] private GameObject roomEventPanel;

        [Tooltip("信息弹层面板；留空则运行时按名查找 Panels/InfoPanel。")]
        [SerializeField] private GameObject infoPanel;

        [Tooltip("主菜单背景；留空则运行时按名查找 Panels/MainBG。")]
        [SerializeField] private GameObject mainBackground;

        [Tooltip("局内对战信息文字根；留空则运行时按名查找 TableNine Text Overlay UI/InGameInfo Text。局内对战与选择叠层时显示，主菜单隐藏。")]
        [SerializeField] private GameObject inGameInfoText;

        public GameObject MainPanel => mainPanel;
        public GameObject InGamePanels => inGamePanels;
        public GameObject RewardPanel => rewardPanel;
        public GameObject RoomChoicePanel => roomChoicePanel;
        public GameObject RoomEventPanel => roomEventPanel;
        public GameObject InfoPanel => infoPanel;
        public GameObject InGameInfoText => inGameInfoText;

        public void EnsureBindings()
        {
            mainPanel ??= FindByPath("Panels/MainPanel");
            inGamePanels ??= FindByPath("Panels/InGamePanels");
            rewardPanel ??= FindByPath("Panels/RewardPanel");
            roomChoicePanel ??= FindByPath("Panels/RoomChoisePanel");
            roomEventPanel ??= FindByPath("Panels/RoomEventPanel");
            infoPanel ??= FindByPath("Panels/InfoPanel");
            mainBackground ??= FindByPath("Panels/MainBG");
            inGameInfoText ??= FindByPath("TableNine Text Overlay UI/InGameInfo Text");
        }

        public void ShowMainMenu()
        {
            EnsureBindings();
            SetActiveSafe(mainBackground, true);
            SetActiveSafe(mainPanel, true);
            SetActiveSafe(inGamePanels, false);
            SetInGameInfoTextVisible(false);
            HideAllOverlays();
        }

        /// <summary>
        /// 局内壳（对战视角）。<paramref name="inBattle"/> 为 true 时才显示 InGameInfo Text。
        /// </summary>
        public void ShowInRunShell(bool inBattle = true)
        {
            EnsureBindings();
            SetActiveSafe(mainBackground, true);
            SetActiveSafe(mainPanel, false);
            SetActiveSafe(inGamePanels, true);
            SetInGameInfoTextVisible(inBattle);
            HideAllOverlays();
        }

        public void ShowRewardOverlay()
        {
            EnsureBindings();
            SetActiveSafe(mainPanel, false);
            SetActiveSafe(inGamePanels, true);
            // 奖励选择需悬停写 Card Info Text，保持信息栏可见。
            SetInGameInfoTextVisible(true);
            SetActiveSafe(rewardPanel, true);
            SetActiveSafe(roomChoicePanel, false);
            SetActiveSafe(roomEventPanel, false);
            SetActiveSafe(infoPanel, false);
        }

        public void ShowRoomChoiceOverlay()
        {
            EnsureBindings();
            SetActiveSafe(mainPanel, false);
            SetActiveSafe(inGamePanels, true);
            // 房间选择需悬停写 Card Info Text，保持信息栏可见。
            SetInGameInfoTextVisible(true);
            SetActiveSafe(rewardPanel, false);
            SetActiveSafe(roomChoicePanel, true);
            SetActiveSafe(roomEventPanel, false);
            SetActiveSafe(infoPanel, false);
        }

        public void ShowRoomEventOverlay()
        {
            EnsureBindings();
            SetActiveSafe(mainPanel, false);
            SetActiveSafe(inGamePanels, true);
            // 主流程叠层与玩家信息面板互不影响，保持 InGameInfoText 可见。
            SetInGameInfoTextVisible(true);
            SetActiveSafe(rewardPanel, false);
            SetActiveSafe(roomChoicePanel, false);
            SetActiveSafe(roomEventPanel, true);
            SetActiveSafe(infoPanel, false);
        }

        public void SetInGameInfoTextVisible(bool visible)
        {
            EnsureBindings();
            SetActiveSafe(inGameInfoText, visible);
        }

        public void ShowInfoOverlay(bool visible)
        {
            EnsureBindings();
            SetActiveSafe(infoPanel, visible);
        }

        public void HideAllOverlays()
        {
            EnsureBindings();
            SetActiveSafe(rewardPanel, false);
            SetActiveSafe(roomChoicePanel, false);
            SetActiveSafe(roomEventPanel, false);
            SetActiveSafe(infoPanel, false);
        }

        private static GameObject FindByPath(string path)
        {
            var found = GameObject.Find(path);
            if (found != null)
            {
                return found;
            }

            // 含 inactive：按层级手拆
            var parts = path.Split('/');
            Transform current = null;
            for (var i = 0; i < parts.Length; i++)
            {
                if (current == null)
                {
                    var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                    for (var r = 0; r < roots.Length; r++)
                    {
                        if (roots[r].name == parts[i])
                        {
                            current = roots[r].transform;
                            break;
                        }
                    }
                }
                else
                {
                    current = current.Find(parts[i]);
                }

                if (current == null)
                {
                    return null;
                }
            }

            return current != null ? current.gameObject : null;
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }
    }
}
