using NineGrid.Presentation.Shell;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Slices
{
    /// <summary>
    /// PerformanceTest 专用：保留真实 Bootstrap / Gateway，断开 Shell 主菜单→局内自动链路；
    /// 由 <see cref="ScenarioDriverBase"/> 切片 Driver 发令，面板由 Inspector 手动切换。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VerticalSliceSceneController : MonoBehaviour
    {
        [Title("Shell 断开")]
        [SerializeField, LabelText("启动时断开生产主流程")]
        private bool disconnectShellFlowOnStart = true;

        [SerializeField, LabelText("初始视图")]
        private MainFlowScreen initialView = MainFlowScreen.NodePlaying;

        [SerializeField, LabelText("禁用主菜单选择输入")]
        private bool deactivateMenuSelection = true;

        [Title("引用（可留空，自动解析）")]
        [SerializeField] private MainFlowDirector flowDirector;
        [SerializeField] private GamePhaseFlowShellProjection phaseProjection;

        [Title("面板根节点（手动开关用，可留空自动解析）")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject gamePanels;
        [SerializeField] private GameObject rewardPanel;
        [SerializeField] private GameObject infoPanel;
        [SerializeField] private GameObject roomChoicePanel;
        [SerializeField] private GameObject roomEventPanel;

        private bool sliceModeApplied;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            var director = ResolveDirector();
            if (director?.FlowFsm != null)
            {
                director.FlowFsm.ScreenChanged += OnShellScreenChanged;
            }
        }

        private void OnDisable()
        {
            var director = ResolveDirector();
            if (director?.FlowFsm != null)
            {
                director.FlowFsm.ScreenChanged -= OnShellScreenChanged;
            }
        }

        private void Start()
        {
            if (!disconnectShellFlowOnStart)
            {
                return;
            }

            ApplySliceMode();
        }

        private void OnShellScreenChanged(MainFlowScreen previous, MainFlowScreen current)
        {
            if (!disconnectShellFlowOnStart || sliceModeApplied)
            {
                return;
            }

            if (current == MainFlowScreen.MainMenu)
            {
                ApplySliceMode();
            }
        }

        [Button("应用切片模式（断开主流程）"), ShowIf("@UnityEngine.Application.isPlaying")]
        public void ApplySliceMode()
        {
            ResolveReferences();

            var projection = ResolvePhaseProjection();
            if (projection != null)
            {
                projection.EnabledProjection = false;
            }

            var director = ResolveDirector();
            if (director == null)
            {
                Debug.LogWarning("[VerticalSliceScene] MainFlowDirector not found.");
                return;
            }

            director.JumpToScreen(initialView);

            if (deactivateMenuSelection)
            {
                director.SelectionFsm?.Deactivate();
            }

            sliceModeApplied = true;
            Debug.Log("[VerticalSliceScene] Shell flow disconnected; use slice drivers + panel toggles.");
        }

        [Title("快捷预设")]
        [Button("局内"), ShowIf("@UnityEngine.Application.isPlaying")]
        public void PresetInGame()
        {
            SetManualPanels(main: false, game: true, reward: false, info: false, roomChoice: false, roomEvent: false);
        }

        [Button("主菜单"), ShowIf("@UnityEngine.Application.isPlaying")]
        public void PresetMainMenu()
        {
            SetManualPanels(main: true, game: false, reward: false, info: false, roomChoice: false, roomEvent: false);
        }

        [Button("奖励"), ShowIf("@UnityEngine.Application.isPlaying")]
        public void PresetReward()
        {
            SetManualPanels(main: false, game: false, reward: true, info: false, roomChoice: false, roomEvent: false);
        }

        [Button("房间选择"), ShowIf("@UnityEngine.Application.isPlaying")]
        public void PresetRoomChoice()
        {
            SetManualPanels(main: false, game: false, reward: false, info: false, roomChoice: true, roomEvent: false);
        }

        [Button("房间事件"), ShowIf("@UnityEngine.Application.isPlaying")]
        public void PresetRoomEvent()
        {
            SetManualPanels(main: false, game: false, reward: false, info: false, roomChoice: false, roomEvent: true);
        }

        [Button("结局信息"), ShowIf("@UnityEngine.Application.isPlaying")]
        public void PresetOutcome()
        {
            SetManualPanels(main: false, game: false, reward: false, info: true, roomChoice: false, roomEvent: false);
        }

        [Title("单面板开关")]
        [ShowInInspector, LabelText("主菜单 MainPanel")]
        [ShowIf("@UnityEngine.Application.isPlaying")]
        private bool ToggleMainPanel
        {
            get => mainPanel != null && mainPanel.activeSelf;
            set => SetActive(mainPanel, value);
        }

        [ShowInInspector, LabelText("局内 GamePanels")]
        [ShowIf("@UnityEngine.Application.isPlaying")]
        private bool ToggleGamePanels
        {
            get => gamePanels != null && gamePanels.activeSelf;
            set => SetActive(gamePanels, value);
        }

        [ShowInInspector, LabelText("奖励 RewardPanel")]
        [ShowIf("@UnityEngine.Application.isPlaying")]
        private bool ToggleRewardPanel
        {
            get => rewardPanel != null && rewardPanel.activeSelf;
            set => SetActive(rewardPanel, value);
        }

        [ShowInInspector, LabelText("结局 InfoPanel")]
        [ShowIf("@UnityEngine.Application.isPlaying")]
        private bool ToggleInfoPanel
        {
            get => infoPanel != null && infoPanel.activeSelf;
            set => SetActive(infoPanel, value);
        }

        [ShowInInspector, LabelText("房间选择 RoomChoice")]
        [ShowIf("@UnityEngine.Application.isPlaying")]
        private bool ToggleRoomChoicePanel
        {
            get => roomChoicePanel != null && roomChoicePanel.activeSelf;
            set => SetActive(roomChoicePanel, value);
        }

        [ShowInInspector, LabelText("房间事件 RoomEvent")]
        [ShowIf("@UnityEngine.Application.isPlaying")]
        private bool ToggleRoomEventPanel
        {
            get => roomEventPanel != null && roomEventPanel.activeSelf;
            set => SetActive(roomEventPanel, value);
        }

        private void SetManualPanels(
            bool main,
            bool game,
            bool reward,
            bool info,
            bool roomChoice,
            bool roomEvent)
        {
            ResolveReferences();
            SetActive(mainPanel, main);
            SetActive(gamePanels, game);
            SetActive(rewardPanel, reward);
            SetActive(infoPanel, info);
            SetActive(roomChoicePanel, roomChoice);
            SetActive(roomEventPanel, roomEvent);
        }

        private void ResolveReferences()
        {
            if (flowDirector == null)
            {
                flowDirector = MainFlowDirector.Current;
            }

            if (phaseProjection == null && flowDirector != null)
            {
                phaseProjection = flowDirector.PhaseProjection;
            }

            var panelsRoot = GameObject.Find("Panels")?.transform;
            if (panelsRoot == null)
            {
                return;
            }

            mainPanel ??= FindChild(panelsRoot, "MainPanel");
            gamePanels ??= FindChild(panelsRoot, "GamePanels");
            rewardPanel ??= FindChild(panelsRoot, "RewardPanel");
            infoPanel ??= FindChild(panelsRoot, "InfoPanel");
            roomChoicePanel ??= FindChild(panelsRoot, "RoomChoisePanel");
            roomEventPanel ??= FindChild(panelsRoot, "RoomEventPanel");
        }

        private static GameObject FindChild(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            return child != null ? child.gameObject : null;
        }

        private MainFlowDirector ResolveDirector()
        {
            if (flowDirector == null)
            {
                flowDirector = MainFlowDirector.Current;
            }

            return flowDirector;
        }

        private GamePhaseFlowShellProjection ResolvePhaseProjection()
        {
            if (phaseProjection == null)
            {
                phaseProjection = ResolveDirector()?.PhaseProjection;
            }

            return phaseProjection;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
