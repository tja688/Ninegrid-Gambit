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

        [Tooltip("房间事件面板；留空则运行时按名查找 Panels/RoomEventPanel。")]
        [SerializeField] private GameObject roomEventPanel;

        [Tooltip("信息弹层面板；留空则运行时按名查找 Panels/InfoPanel。")]
        [SerializeField] private GameObject infoPanel;

        [Tooltip("主菜单背景；留空则运行时按名查找 Panels/MainBG。")]
        [SerializeField] private GameObject mainBackground;

        [Tooltip("玩家信息 HUD 根；留空则按名查找「玩家信息」。主菜单禁用其命中，不 SetActive。")]
        [SerializeField] private GameObject playerHudRoot;

        [Tooltip("遗物栏锚点根；留空则按名查找 RelicPanelAnchors。主菜单禁用其命中，不 SetActive。")]
        [SerializeField] private GameObject relicHudRoot;

        [Tooltip("卡组锚点根；留空则按名查找 CardDeckAnchors。主菜单禁用其命中，不 SetActive。")]
        [SerializeField] private GameObject cardDeckHudRoot;

        public GameObject MainPanel => mainPanel;
        public GameObject InGamePanels => inGamePanels;
        public GameObject RewardPanel => rewardPanel;
        public GameObject RoomEventPanel => roomEventPanel;
        public GameObject InfoPanel => infoPanel;

        public void EnsureBindings()
        {
            mainPanel ??= FindByPath("Panels/MainPanel");
            inGamePanels ??= FindByPath("Panels/InGamePanels");
            rewardPanel ??= FindByPath("Panels/RewardPanel");
            roomEventPanel ??= FindByPath("Panels/RoomEventPanel");
            infoPanel ??= FindByPath("Panels/InfoPanel");
            mainBackground ??= FindByPath("Panels/MainBG");
            playerHudRoot ??= FindByName("玩家信息");
            relicHudRoot ??= FindByName(RelicManagerSingleton.DefaultAnchorsName);
            cardDeckHudRoot ??= FindByName("CardDeckAnchors");
        }

        public void ShowMainMenu()
        {
            EnsureBindings();
            SetActiveSafe(mainBackground, true);
            SetActiveSafe(mainPanel, true);
            SetActiveSafe(inGamePanels, false);
            SetHudHitsEnabled(false);
            HideAllOverlays();
        }

        /// <summary>
        /// 局内壳（对战视角）。玩家数值 HUD 由 <see cref="PlayerInfoHudPresenter"/> 持续持有，不随面板切换 SetActive；
        /// 主菜单只关 HUD 命中，避免开始界面能点到血条/遗物栏。
        /// <paramref name="inBattle"/> 保留兼容，不再用于隐藏信息栏。
        /// </summary>
        public void ShowInRunShell(bool inBattle = true)
        {
            _ = inBattle;
            EnsureBindings();
            SetActiveSafe(mainBackground, true);
            SetActiveSafe(mainPanel, false);
            SetActiveSafe(inGamePanels, true);
            SetHudHitsEnabled(true);
            HideAllOverlays();
        }

        public void ShowRewardOverlay()
        {
            EnsureBindings();
            SetActiveSafe(mainPanel, false);
            SetActiveSafe(inGamePanels, true);
            SetActiveSafe(rewardPanel, true);
            SetActiveSafe(roomEventPanel, false);
            SetActiveSafe(infoPanel, false);
        }

        public void ShowRoomEventOverlay()
        {
            EnsureBindings();
            SetActiveSafe(mainPanel, false);
            SetActiveSafe(inGamePanels, true);
            SetActiveSafe(rewardPanel, false);
            SetActiveSafe(roomEventPanel, true);
            SetActiveSafe(infoPanel, false);
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
            SetActiveSafe(roomEventPanel, false);
            SetActiveSafe(infoPanel, false);
        }

        private void SetHudHitsEnabled(bool enabled)
        {
            SetCollidersEnabled(playerHudRoot, enabled);
            SetCollidersEnabled(relicHudRoot, enabled);
            SetCollidersEnabled(cardDeckHudRoot, enabled);
        }

        private static void SetCollidersEnabled(GameObject root, bool enabled)
        {
            if (root == null)
            {
                return;
            }

            var colliders = root.GetComponentsInChildren<Collider2D>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = enabled;
                }
            }
        }

        private static GameObject FindByName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            var found = GameObject.Find(objectName);
            if (found != null)
            {
                return found;
            }

            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (var r = 0; r < roots.Length; r++)
            {
                var match = FindDeep(roots[r].transform, objectName);
                if (match != null)
                {
                    return match.gameObject;
                }
            }

            return null;
        }

        private static Transform FindDeep(Transform parent, string objectName)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent.name == objectName)
            {
                return parent;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var match = FindDeep(parent.GetChild(i), objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
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
