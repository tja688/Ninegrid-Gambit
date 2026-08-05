using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 流程壳 Controller：Bind View、订阅 BattleSession Event → Signal、相位写入走 Command。
    /// </summary>
    public sealed class GameFlowShellController : PresentationController
    {
        private IUnRegister mSettlementUnRegister;
        private IUnRegister mBattleEndedUnRegister;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            GameFlowShellHook.WireController = Wire;
            // PublishState 镜像路径已停用；编排写相位直接走 Command。
            GameFlowShellHook.SetState = null;
        }

        protected override void OnBind()
        {
            var shell = GameFlowShellSystem.EnsureRegistered();
            TryBindSceneView(shell);
            SubscribeBattleSessionEvents();
        }

        protected override void OnUnbind()
        {
            UnsubscribeBattleSessionEvents();
        }

        public void HandleSetState(GameFlowShellState next)
        {
            this.SendCommand(new SetGameFlowShellStateCommand(next));
        }

        public void HandleBeginRun(GameFlowRunOptions options = null)
        {
            this.SendCommand(new BeginGameFlowRunCommand(options));
        }

        public void HandleReturnToMainMenu()
        {
            this.SendCommand(new ReturnToMainMenuCommand());
        }

        public void HandleSignal(GameFlowSignal signal)
        {
            this.SendCommand(new SignalGameFlowCommand(signal));
        }

        private void SubscribeBattleSessionEvents()
        {
            UnsubscribeBattleSessionEvents();
            var architecture = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (architecture == null)
            {
                return;
            }

            mSettlementUnRegister = architecture.RegisterEvent<BattleSessionSettlementReadyEvent>(_ =>
                this.SendCommand(SignalGameFlowCommand.SettlementReady()));
            mBattleEndedUnRegister = architecture.RegisterEvent<BattleSessionEndedEvent>(e =>
                this.SendCommand(SignalGameFlowCommand.BattleEnded(e.Victory)));
        }

        private void UnsubscribeBattleSessionEvents()
        {
            mSettlementUnRegister?.UnRegister();
            mSettlementUnRegister = null;
            mBattleEndedUnRegister?.UnRegister();
            mBattleEndedUnRegister = null;
        }

        private static void TryBindSceneView(GameFlowShellSystem shell)
        {
            if (shell.IsBound)
            {
                return;
            }

            var view = Object.FindFirstObjectByType<GameFlowController>();
            if (view != null)
            {
                shell.Bind(view);
            }
        }

        private static void Wire()
        {
            var existing = Object.FindFirstObjectByType<GameFlowShellController>();
            if (existing == null)
            {
                var host = new GameObject(nameof(GameFlowShellController));
                existing = host.AddComponent<GameFlowShellController>();
            }

            GameFlowShellSystem.EnsureRegistered();
            TryBindSceneView(GameFlowShellSystem.EnsureRegistered());
            existing.SubscribeBattleSessionEvents();
        }
    }
}
