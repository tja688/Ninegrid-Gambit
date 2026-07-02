using System.Collections;
using NineGrid.Presentation.Flow.Room;
using NineGrid.Presentation.Interaction;
using UnityEngine;

namespace NineGrid.Presentation.Shell
{
    /// <summary>
    /// 主流程 Shell 统一接线：FSM、Harness、屏 Presenter、Overlay 闸门。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainFlowDirector : MonoBehaviour
    {
        public static MainFlowDirector Current { get; private set; }

        [Header("Mode")]
        [SerializeField] private bool useHarness = true;

        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject gamePanels;
        [SerializeField] private GameObject rewardPanel;
        [SerializeField] private GameObject infoPanel;

        [Header("Shell")]
        [SerializeField] private MainFlowHarnessDriver harnessDriver;
        [SerializeField] private TableNineTextOverlayGate textOverlayGate;
        [SerializeField] private RunOutcomeInfoPresenter outcomePresenter;
        [SerializeField] private GamePhaseFlowShellProjection phaseProjection;

        [Header("Presenters")]
        [SerializeField] private MainMenuScreenPresenter mainMenuPresenter;
        [SerializeField] private RoomChoiceScreenPresenter roomChoicePresenter;
        [SerializeField] private RoomEventScreenPresenter roomEventPresenter;

        [Header("Selection")]
        [SerializeField] private SelectionOptionHoverPresenter generalHoverPresenter;
        [SerializeField] private RoomChoiseOptionHoverPresenter roomHoverPresenter;
        [SerializeField] private SelectionFsmOwner selectionFsmOwner;

        [Header("Flows")]
        [SerializeField] private RoomChoiseInFlow roomInFlow;
        [SerializeField] private RoomChoiseOutFlow roomOutFlow;

        private readonly MainFlowFsm flowFsm = new();
        private readonly SelectionFsm selectionFsm = new();
        private Coroutine bootCoroutine;

        public MainFlowFsm FlowFsm => flowFsm;
        public SelectionFsm SelectionFsm => selectionFsm;
        public MainFlowHarnessDriver HarnessDriver => harnessDriver;
        public bool UseHarness => useHarness;
        public TableNineTextOverlayGate TextOverlayGate => textOverlayGate;
        public RoomChoiceScreenPresenter RoomChoicePresenter => roomChoicePresenter;
        public RoomChoiseInFlow RoomInFlow => roomInFlow;
        public RoomChoiseOutFlow RoomOutFlow => roomOutFlow;

        private void Awake()
        {
            Current = this;
            flowFsm.IsHarnessMode = useHarness;
            WireComponents();
            flowFsm.ScreenChanged += OnScreenChanged;
            selectionFsm.OptionConfirmed += OnSelectionConfirmed;
            selectionFsm.OptionHovered += OnSelectionHovered;

            if (useHarness)
            {
                HideAllPanels();
            }
        }

        private void Start()
        {
            if (useHarness)
            {
                flowFsm.Enter(MainFlowScreen.Boot);
                bootCoroutine = StartCoroutine(BootToMainMenu());
            }
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }

            flowFsm.ScreenChanged -= OnScreenChanged;
            selectionFsm.OptionConfirmed -= OnSelectionConfirmed;
            selectionFsm.OptionHovered -= OnSelectionHovered;

            if (bootCoroutine != null)
            {
                StopCoroutine(bootCoroutine);
            }
        }

        public void InitializeToMainMenu()
        {
            if (bootCoroutine != null)
            {
                StopCoroutine(bootCoroutine);
                bootCoroutine = null;
            }

            flowFsm.Enter(MainFlowScreen.MainMenu);
        }

        public void JumpToScreen(MainFlowScreen screen)
        {
            flowFsm.Enter(screen);
        }

        private void WireComponents()
        {
            selectionFsm.BindPresenters(generalHoverPresenter, roomHoverPresenter);
            EnsureSelectionFsmOwner();
            WireSelectionInputRelays();

            mainMenuPresenter?.Bind(flowFsm, selectionFsm);
            roomChoicePresenter?.Bind(flowFsm, selectionFsm, null);
            roomEventPresenter?.Bind(flowFsm);
            outcomePresenter?.Bind(flowFsm);
            harnessDriver?.Bind(this, flowFsm);
            phaseProjection?.Bind(this);

            if (roomChoicePresenter != null && roomInFlow != null && roomOutFlow != null)
            {
                // Presenter 已序列化引用 Flow；Director 暴露给调试模块。
            }
        }

        private void EnsureSelectionFsmOwner()
        {
            if (selectionFsmOwner == null)
            {
                selectionFsmOwner = GetComponent<SelectionFsmOwner>();
            }

            if (selectionFsmOwner == null)
            {
                selectionFsmOwner = gameObject.AddComponent<SelectionFsmOwner>();
            }

            selectionFsmOwner.Bind(selectionFsm);
        }

        private void WireSelectionInputRelays()
        {
            var relays = FindObjectsByType<SelectionOptionInputRelay>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (var i = 0; i < relays.Length; i++)
            {
                relays[i].Owner = selectionFsmOwner;
            }
        }

        private IEnumerator BootToMainMenu()
        {
            yield return null;
            flowFsm.Enter(MainFlowScreen.MainMenu);
            bootCoroutine = null;
        }

        private void OnScreenChanged(MainFlowScreen previous, MainFlowScreen current)
        {
            ApplyPanelVisibility(current);
            textOverlayGate?.ApplyForScreen(current);
            InvokeScreenPresenter(current, previous);

            if (current == MainFlowScreen.MainMenu)
            {
                selectionFsm.ActivateChannel(SelectionChannel.General);
            }
            else if (current != MainFlowScreen.RewardScreen && current != MainFlowScreen.RoomChoiceScreen)
            {
                selectionFsm.Deactivate();
            }

            if (current == MainFlowScreen.RewardScreen)
            {
                selectionFsm.ActivateChannel(SelectionChannel.General);
            }
            else if (previous == MainFlowScreen.RewardScreen)
            {
                selectionFsm.Deactivate();
            }

            if (current == MainFlowScreen.NodeAdvance)
            {
                flowFsm.RequestTransition(MainFlowTransition.NodeAdvanceDone);
            }
        }

        private void ApplyPanelVisibility(MainFlowScreen screen)
        {
            SetActive(mainPanel, screen == MainFlowScreen.MainMenu);
            SetActive(gamePanels, screen == MainFlowScreen.RunSession
                || screen == MainFlowScreen.NodePlaying
                || screen == MainFlowScreen.NodeAdvance);
            SetActive(rewardPanel, screen == MainFlowScreen.RewardScreen);
            SetActive(infoPanel, screen == MainFlowScreen.Victory || screen == MainFlowScreen.Defeat);

            if (screen != MainFlowScreen.RoomChoiceScreen)
            {
                roomChoicePresenter?.OnScreenExited();
            }

            if (screen != MainFlowScreen.RoomEventScreen)
            {
                roomEventPresenter?.OnScreenExited();
            }

            if (screen != MainFlowScreen.MainMenu)
            {
                mainMenuPresenter?.OnScreenExited();
            }
        }

        private void InvokeScreenPresenter(MainFlowScreen current, MainFlowScreen previous)
        {
            switch (current)
            {
                case MainFlowScreen.MainMenu:
                    mainMenuPresenter?.OnScreenEntered();
                    break;
                case MainFlowScreen.RoomChoiceScreen:
                    roomChoicePresenter?.OnScreenEntered();
                    break;
                case MainFlowScreen.RoomEventScreen:
                    roomEventPresenter?.OnScreenEntered();
                    break;
            }
        }

        private void OnSelectionConfirmed(int index)
        {
            switch (flowFsm.CurrentScreen)
            {
                case MainFlowScreen.MainMenu:
                    mainMenuPresenter?.HandleOptionConfirmed(index);
                    selectionFsm.ForceReset();
                    break;
                case MainFlowScreen.RewardScreen:
                    flowFsm.RequestTransition(MainFlowTransition.ConfirmReward);
                    selectionFsm.ForceReset();
                    break;
                case MainFlowScreen.RoomChoiceScreen:
                    roomChoicePresenter?.HandleOptionConfirmed(index);
                    break;
            }
        }

        private void OnSelectionHovered(int index)
        {
            switch (flowFsm.CurrentScreen)
            {
                case MainFlowScreen.MainMenu:
                    mainMenuPresenter?.HandleOptionHovered(index);
                    break;
                case MainFlowScreen.RoomChoiceScreen:
                    roomChoicePresenter?.HandleOptionHovered(index);
                    break;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private void HideAllPanels()
        {
            SetActive(mainPanel, false);
            SetActive(gamePanels, false);
            SetActive(rewardPanel, false);
            SetActive(infoPanel, false);
            roomChoicePresenter?.OnScreenExited();
            roomEventPresenter?.OnScreenExited();
            mainMenuPresenter?.OnScreenExited();
            textOverlayGate?.HideAll();
        }
    }
}
