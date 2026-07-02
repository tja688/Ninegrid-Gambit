using NineGrid.Core;
using NineGrid.Presentation.Bridge;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Shell
{
    /// <summary>
    /// 订阅 <see cref="RunModel.Phase"/>，生产环境驱动 <see cref="MainFlowFsm"/>（取代 Harness 直切）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamePhaseFlowShellProjection : MonoBehaviour
    {
        [SerializeField] private bool enabledProjection = true;

        private MainFlowDirector director;
        private MainFlowFsm flowFsm;
        private ShellCommandRouter commandRouter;
        private IArchitecture architecture;
        private IUnRegister mPhaseUnregister;
        private bool runSessionActive;

        public bool EnabledProjection
        {
            get => enabledProjection;
            set => enabledProjection = value;
        }

        public bool RunSessionActive => runSessionActive;

        public void Bind(MainFlowDirector flowDirector)
        {
            director = flowDirector;
            flowFsm = flowDirector?.FlowFsm;
        }

        public void WireProduction(ShellCommandRouter router, IArchitecture arch)
        {
            commandRouter = router;
            architecture = arch;
            TrySubscribePhase();
        }

        public void NotifyRunStarted()
        {
            runSessionActive = true;
            ApplyPhase(architecture?.GetModel<RunModel>()?.Phase.Value ?? GamePhase.None, force: true);
        }

        public void NotifyReturnedToMainMenu()
        {
            runSessionActive = false;
            flowFsm?.Enter(MainFlowScreen.MainMenu);
        }

        private void OnEnable()
        {
            TrySubscribePhase();
        }

        private void OnDisable()
        {
            UnsubscribePhase();
        }

        private void TrySubscribePhase()
        {
            if (!enabledProjection || architecture == null || mPhaseUnregister != null)
            {
                return;
            }

            var run = architecture.GetModel<RunModel>();
            if (run?.Phase == null)
            {
                return;
            }

            mPhaseUnregister = run.Phase.RegisterWithInitValue(OnPhaseChanged);
        }

        private void UnsubscribePhase()
        {
            mPhaseUnregister?.UnRegister();
            mPhaseUnregister = null;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            ApplyPhase(phase, force: false);
        }

        private void ApplyPhase(GamePhase phase, bool force)
        {
            if (!enabledProjection || flowFsm == null)
            {
                return;
            }

            if (director != null && director.IsTestFlowActive)
            {
                return;
            }

            if (!runSessionActive && phase != GamePhase.None && phase != GamePhase.Victory && phase != GamePhase.Defeat)
            {
                return;
            }

            if (!GamePhaseScreenMapper.TryMap(phase, out GamePhaseScreenMapping mapping))
            {
                return;
            }

            if (!force && flowFsm.CurrentScreen == mapping.Screen)
            {
                return;
            }

            if (mapping.AnnounceOutcome)
            {
                flowFsm.ShowOutcome(mapping.Outcome);
                return;
            }

            flowFsm.Enter(mapping.Screen);
        }
    }
}
