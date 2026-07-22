using System;
using NineGrid.Flow.Presentation;

namespace NineGrid.Flow
{
    /// <summary>
    /// 流程壳相位写入入口：由 Presentation GameFlowShellController 注册，MainGameLoop 调用。
    /// 避免 Flow→Presentation 程序集环。
    /// </summary>
    public static class GameFlowShellHook
    {
        public static Action<GameFlowShellState> SetState;

        public static Action WireController;

        public static void RequestWire()
        {
            WireController?.Invoke();
        }

        public static void PublishState(GameFlowShellState state)
        {
            SetState?.Invoke(state);
        }
    }
}
