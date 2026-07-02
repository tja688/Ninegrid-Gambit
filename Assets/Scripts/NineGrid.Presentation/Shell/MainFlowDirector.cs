using System.Collections;
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
        [SerializeField] private RewardScreenPresenter rewardPresenter;
        [SerializeField] private RoomChoiceScreenPresenter roomChoicePresenter;
        [SerializeField] private RoomEventScreenPresenter roomEventPresenter;

        [Header("Selection")]
        [SerializeField] private SelectionFsmOwner selectionFsmOwner;

        private readonly MainFlowFsm flowFsm = new();
        private readonly SelectionFsm selectionFsm = new();
        private Coroutine bootCoroutine;

        public MainFlowFsm FlowFsm => flowFsm;
        public SelectionFsm SelectionFsm => selectionFsm;
        public SelectionPresentation SelectionPresentation => selectionFsmOwner != null
            ? selectionFsmOwner.Presentation
            : null;
        public MainFlowHarnessDriver HarnessDriver => harnessDriver;
        public bool IsTestFlowActive => harnessDriver != null && harnessDriver.IsActive;
        public TableNineTextOverlayGate TextOverlayGate => textOverlayGate;
        public RewardScreenPresenter RewardPresenter => rewardPresenter;
        public RoomChoiceScreenPresenter RoomChoicePresenter => roomChoicePresenter;

        private void Awake()
        {
            Current = this;
            flowFsm.IsHarnessMode = false;
            WireComponents();
            flowFsm.ScreenChanged += OnScreenChanged;
            selectionFsm.OptionConfirmed += OnSelectionConfirmed;
            selectionFsm.OptionHovered += OnSelectionHovered;
        }

        private void Start()
        {
            if (flowFsm.CurrentScreen == MainFlowScreen.Boot)
            {
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
            EnsureSelectionFsmOwner();
            WireSelectionInputRelays();

            var presentation = SelectionPresentation;
            selectionFsm.BindPresentation(presentation);

            mainMenuPresenter?.Bind(flowFsm, selectionFsm, presentation);
            rewardPresenter?.Bind(flowFsm, selectionFsm, presentation);
            roomChoicePresenter?.Bind(flowFsm, selectionFsm, presentation, null);
            roomEventPresenter?.Bind(flowFsm);
            outcomePresenter?.Bind(flowFsm);
            harnessDriver?.Bind(this, flowFsm);
            phaseProjection?.Bind(this);
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

            if (GetComponent<SelectionPresentation>() == null)
            {
                gameObject.AddComponent<SelectionPresentation>();
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

            if (current == MainFlowScreen.MainMenu || current == MainFlowScreen.RewardScreen)
            {
                selectionFsm.ActivateChannel(SelectionChannel.General);
            }
            else if (current != MainFlowScreen.RoomChoiceScreen)
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

            if (screen != MainFlowScreen.RewardScreen)
            {
                rewardPresenter?.OnScreenExited();
            }

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
                case MainFlowScreen.RewardScreen:
                    rewardPresenter?.OnScreenEntered();
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
                    rewardPresenter?.HandleOptionConfirmed(index);
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
    }
}
