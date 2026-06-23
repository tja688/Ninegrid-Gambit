using System;
using NineGrid.Core;
using NineGrid.Core.Systems;
using QFramework;
using UnityEngine;
using UnityEngine.Events;

namespace NineGrid.Presentation.FSM
{
    /// <summary>
    /// 泳道 A 流程壳：Boot →（跳过主菜单）→ RunSession；订阅 <see cref="RunModel.Phase"/> 投影屏幕态并编排子 FSM。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class InGameFlowShellFsm : MonoBehaviour, IController
    {
        [Header("Boot")]
        [SerializeField] private bool autoBootOnPlay = true;
        [SerializeField] private bool skipMainMenu = true;
        [SerializeField] private ulong bootSeed = 1UL;

        [Header("Managed Interaction FSMs")]
        [SerializeField] private BoardInteractionFsm boardInteractionFsm;
        [SerializeField] private ItemCardInteractionFsm itemCardInteractionFsm;

        [Header("Events")]
        [SerializeField] private UnityEvent<FlowShellScreen> onScreenChanged;
        [SerializeField] private UnityEvent<FlowShellAppState> onAppStateChanged;

        private FlowShellAppState appState = FlowShellAppState.Boot;
        private FlowShellScreen screen = FlowShellScreen.Idle;
        private bool nodeSessionActive;
        private bool bootCompleted;
        private bool phaseSubscribed;

        public FlowShellAppState AppState => appState;
        public FlowShellScreen Screen => screen;
        public bool NodeSessionActive => nodeSessionActive;

        /// <summary>
        /// 仅 <see cref="FlowShellScreen.NodePlaying"/> 且内核处于 <c>InteractionLoop</c> 时允许场地 Command。
        /// </summary>
        public bool CanSendBoardCommand =>
            screen == FlowShellScreen.NodePlaying
            && this.GetSystem<IPhaseSystem>().CurrentPhase == GamePhase.InteractionLoop;

        public event Action<FlowShellScreen> ScreenChanged;
        public event Action<FlowShellAppState> AppStateChanged;

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
        }

        private void Awake()
        {
            boardInteractionFsm?.SetFlowShellInteractionEnabled(false);
            itemCardInteractionFsm?.SetFlowShellInteractionEnabled(false);
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (autoBootOnPlay && !bootCompleted)
            {
                RunBoot();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || !bootCompleted)
            {
                return;
            }

            SubscribePhase();
            ApplyScreen(screen, force: true);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            UnsubscribePhase();
        }

        public void RunBoot()
        {
            if (bootCompleted)
            {
                return;
            }

            SetAppState(FlowShellAppState.Boot);

            var options = new InitialGameOptions { Seed = bootSeed };
            InitialGameFactory.Create(GetArchitecture(), options);

            nodeSessionActive = false;
            bootCompleted = true;

            if (skipMainMenu)
            {
                EnterRunSession();
            }
            else
            {
                SetAppState(FlowShellAppState.MainMenu);
            }
        }

        public void EnterRunSession()
        {
            SetAppState(FlowShellAppState.RunSession);
            SubscribePhase();
            SyncScreenFromPhase(force: true);
        }

        public void NotifyNodeSessionStarted()
        {
            nodeSessionActive = true;
            SyncScreenFromPhase(force: true);
        }

        public void NotifyNodeSessionEnded()
        {
            nodeSessionActive = false;
            SyncScreenFromPhase(force: true);
        }

        private void SubscribePhase()
        {
            if (phaseSubscribed)
            {
                return;
            }

            phaseSubscribed = true;
            this.GetModel<RunModel>().Phase.Register(OnPhaseChanged);
            OnPhaseChanged(this.GetModel<RunModel>().Phase.Value);
        }

        private void UnsubscribePhase()
        {
            if (!phaseSubscribed)
            {
                return;
            }

            phaseSubscribed = false;
            this.GetModel<RunModel>().Phase.UnRegister(OnPhaseChanged);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.NodeCompleted || phase == GamePhase.None)
            {
                nodeSessionActive = false;
            }

            SyncScreenFromPhase();
        }

        private void SyncScreenFromPhase(bool force = false)
        {
            var phase = this.GetModel<RunModel>().Phase.Value;
            var projected = GamePhaseFlowShellProjection.Project(phase, nodeSessionActive);
            ApplyScreen(projected, force);
        }

        private void ApplyScreen(FlowShellScreen nextScreen, bool force = false)
        {
            if (!force && screen == nextScreen)
            {
                return;
            }

            screen = nextScreen;
            bool boardItemEnabled = GamePhaseFlowShellProjection.EnablesBoardItemInteraction(screen);
            boardInteractionFsm?.SetFlowShellInteractionEnabled(boardItemEnabled);
            itemCardInteractionFsm?.SetFlowShellInteractionEnabled(boardItemEnabled);

            // Overlay FSM 挂点：Reward/Room 屏启用后由 SelectionOverlayFsm 接入。
            bool overlayEnabled = GamePhaseFlowShellProjection.EnablesOverlayInteraction(screen);
            if (overlayEnabled)
            {
                Debug.Log("[FlowShell] Overlay screen active (" + screen + "); SelectionOverlayFsm not wired yet.");
            }

            onScreenChanged?.Invoke(screen);
            ScreenChanged?.Invoke(screen);
        }

        private void SetAppState(FlowShellAppState nextState)
        {
            if (appState == nextState)
            {
                return;
            }

            appState = nextState;
            onAppStateChanged?.Invoke(appState);
            AppStateChanged?.Invoke(appState);
        }
    }
}
