using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 临时 UI 测试引导：阻断正常游戏流程，直接进局内面板，
    /// 提供键盘快捷键按需触发各个 UI 状态。
    /// 仅 UITestSence 使用，正式场景不要挂载。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class UITestBootstrap : MonoBehaviour
    {
        [Header("快捷面板引用（留空则从同 GameObject 自动查找）")]
        [SerializeField] private UiPanelRouter panelRouter;

        [Header("灵动形态选择测试（留空则从同 GameObject 自动查找 IUITestKeyConsumer）")]
        [SerializeField] private MonoBehaviour livingFormChoiceTest;

        private GameFlowController _loopManager;
        private IUITestKeyConsumer _livingFormChoiceConsumer;
        private bool _inGameVisible = true;

        private void Awake()
        {
            _loopManager = GetComponent<GameFlowController>();
            if (_loopManager != null)
            {
                _loopManager.enabled = false;
                Debug.Log("[UITestBootstrap] 已阻断 GameFlowController 自动流程。");
            }

            if (panelRouter == null)
            {
                panelRouter = GetComponent<UiPanelRouter>();
            }

            _livingFormChoiceConsumer = livingFormChoiceTest as IUITestKeyConsumer
                ?? GetComponent<IUITestKeyConsumer>();

            Debug.Log("[UITestBootstrap] UI 测试模式就绪。数字 1~2/4~6/0 切面板；小键盘 1 切形态选择（3→6→关）。");
        }

        private void Start()
        {
            panelRouter?.ShowInRunShell(inBattle: true);
        }

        private void Update()
        {
            if (panelRouter == null) return;

            if (KeyboardUtility.GetKeyDown(KeyCode.Alpha1)) ToggleInGameMain();
            if (KeyboardUtility.GetKeyDown(KeyCode.Alpha2)) ToggleRewardOverlay();
            if (KeyboardUtility.GetKeyDown(KeyCode.Alpha4)) ToggleRoomEventOverlay();
            if (KeyboardUtility.GetKeyDown(KeyCode.Alpha5)) ToggleInfoOverlay();
            if (KeyboardUtility.GetKeyDown(KeyCode.Alpha6)) ToggleMainBackground();
            if (KeyboardUtility.GetKeyDown(KeyCode.Alpha0)) DumpPanelStates();
            if (KeyboardUtility.GetKeyDown(KeyCode.Keypad1)) _livingFormChoiceConsumer?.HandleKeypad1();
        }

        [ContextMenu("1. 切换 主菜单/局内")]
        public void ToggleInGameMain()
        {
            if (_inGameVisible)
            {
                panelRouter.ShowMainMenu();
                Debug.Log("[UITestBootstrap] → 主菜单面板");
            }
            else
            {
                panelRouter.ShowInRunShell(inBattle: true);
                Debug.Log("[UITestBootstrap] → 局内面板");
            }

            _inGameVisible = !_inGameVisible;
        }

        [ContextMenu("2. 切换 奖励面板叠层")]
        public void ToggleRewardOverlay()
        {
            if (panelRouter.RewardPanel != null && panelRouter.RewardPanel.activeSelf)
            {
                panelRouter.ShowInRunShell(inBattle: true);
                Debug.Log("[UITestBootstrap] 关闭奖励面板");
            }
            else
            {
                panelRouter.ShowRewardOverlay();
                Debug.Log("[UITestBootstrap] → 奖励面板叠层");
            }

            _inGameVisible = true;
        }

        [ContextMenu("4. 切换 房间事件叠层")]
        public void ToggleRoomEventOverlay()
        {
            if (panelRouter.RoomEventPanel != null && panelRouter.RoomEventPanel.activeSelf)
            {
                panelRouter.ShowInRunShell(inBattle: true);
                Debug.Log("[UITestBootstrap] 关闭房间事件面板");
            }
            else
            {
                panelRouter.ShowRoomEventOverlay();
                Debug.Log("[UITestBootstrap] → 房间事件叠层");
            }

            _inGameVisible = true;
        }

        [ContextMenu("5. 切换 Info 弹层")]
        public void ToggleInfoOverlay()
        {
            var infoPanel = panelRouter.InfoPanel;
            if (infoPanel == null) return;

            var active = infoPanel.activeSelf;
            panelRouter.ShowInfoOverlay(!active);
            Debug.Log($"[UITestBootstrap] Info 弹层 → {(active ? "关闭" : "打开")}");
        }

        [ContextMenu("6. 切换 MainBG")]
        public void ToggleMainBackground()
        {
            // 直接通过 ShowInRunShell 恢复即可；这里只做简单 toggle
            var go = GameObject.Find("Panels/MainBG");
            if (go != null)
            {
                go.SetActive(!go.activeSelf);
                Debug.Log($"[UITestBootstrap] MainBG → {(go.activeSelf ? "显示" : "隐藏")}");
            }
        }

        [ContextMenu("0. 输出面板状态")]
        public void DumpPanelStates()
        {
            panelRouter.EnsureBindings();
            Debug.Log("[UITestBootstrap] === 面板状态 ===");
            LogPanelState("MainPanel", panelRouter.MainPanel);
            LogPanelState("InGamePanels", panelRouter.InGamePanels);
            LogPanelState("RewardPanel", panelRouter.RewardPanel);
            LogPanelState("RoomEventPanel", panelRouter.RoomEventPanel);
            LogPanelState("InfoPanel", panelRouter.InfoPanel);
            LogPanelState("MainBG", GameObject.Find("Panels/MainBG"));
        }

        private static void LogPanelState(string label, GameObject go)
        {
            var state = go == null ? "null" : go.activeSelf ? "显示" : "隐藏";
            Debug.Log($"  {label}: {state}");
        }
    }
}
