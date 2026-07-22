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
    /// 流程壳 Controller：注册 <see cref="GameFlowShellHook"/>，相位写入走 Command。
    /// </summary>
    public sealed class GameFlowShellController : PresentationController
    {
        private System.Action<GameFlowShellState> mSetStateHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            GameFlowShellHook.WireController = Wire;
            GameFlowShellHook.SetState = null;
        }

        protected override void OnBind()
        {
            EnsureShellRegistered();
            InstallSetStateHandler();
        }

        protected override void OnUnbind()
        {
            ClearSetStateHandler();
        }

        public void HandleSetState(GameFlowShellState next)
        {
            this.SendCommand(new SetGameFlowShellStateCommand(next));
        }

        private void InstallSetStateHandler()
        {
            mSetStateHandler = HandleSetState;
            GameFlowShellHook.SetState = mSetStateHandler;
        }

        private void ClearSetStateHandler()
        {
            if (mSetStateHandler != null && GameFlowShellHook.SetState == mSetStateHandler)
            {
                GameFlowShellHook.SetState = null;
            }

            mSetStateHandler = null;
        }

        private void EnsureShellRegistered()
        {
            var architecture = NineGridArchitecture.Interface;
            if (architecture.GetSystem<IGameFlowShellSystem>() != null)
            {
                return;
            }

            architecture.RegisterSystem(new GameFlowShellSystem());
        }

        private static void Wire()
        {
            var existing = Object.FindObjectOfType<GameFlowShellController>();
            if (existing == null)
            {
                var host = new GameObject(nameof(GameFlowShellController));
                existing = host.AddComponent<GameFlowShellController>();
            }

            existing.InstallSetStateHandler();
            existing.EnsureShellRegistered();
        }
    }
}
