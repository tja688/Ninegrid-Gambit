using System;
using NineGrid.Flow.Presentation;

namespace NineGrid.Flow
{
    /// <summary>
    /// 流程壳 Controller 接线入口（避免 Flow→Presentation 程序集环）。
    /// PublishState 镜像路径已停用：编排写相位经 SetGameFlowShellStateCommand。
    /// </summary>
    public static class GameFlowShellHook
    {
        /// <summary>已停用：保留字段以免旧调用方 NRE，调用无效果。</summary>
        public static Action<GameFlowShellState> SetState;

        public static Action WireController;

        public static void RequestWire()
        {
            WireController?.Invoke();
        }

        /// <summary>
        /// 已停用。MainGameLoop→Shell 镜像写相位路径已删除；请走 Command。
        /// </summary>
        [Obsolete("GameFlow 权威已迁入 System；请使用 SetGameFlowShellStateCommand。")]
        public static void PublishState(GameFlowShellState state)
        {
            // no-op：防止旧镜像路径偷偷写第二份相位。
            _ = state;
        }
    }
}
