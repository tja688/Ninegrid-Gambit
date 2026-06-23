using NineGrid.Core;

namespace NineGrid.Presentation.FSM
{
    /// <summary>
    /// Core <see cref="GamePhase"/> → <see cref="FlowShellScreen"/> 弱同步投影表（泳道 A 唯一 GamePhase 引用点）。
    /// </summary>
    public static class GamePhaseFlowShellProjection
    {
        public static FlowShellScreen Project(GamePhase phase, bool nodeSessionActive)
        {
            switch (phase)
            {
                case GamePhase.None:
                case GamePhase.NodeCompleted:
                    return FlowShellScreen.Idle;
                case GamePhase.BuildEnemyPool:
                    return nodeSessionActive ? FlowShellScreen.NodePlaying : FlowShellScreen.Idle;
                case GamePhase.ResetNode:
                case GamePhase.DealOpeningCards:
                case GamePhase.InteractionLoop:
                case GamePhase.ClearCheck:
                    return FlowShellScreen.NodePlaying;
                case GamePhase.RewardItemChoice:
                    return FlowShellScreen.RewardScreen;
                case GamePhase.RoomChoice:
                    return FlowShellScreen.RoomChoiceScreen;
                case GamePhase.RoomEvent:
                    return FlowShellScreen.RoomEventScreen;
                case GamePhase.Victory:
                case GamePhase.Defeat:
                    return FlowShellScreen.RunTerminal;
                default:
                    return FlowShellScreen.Idle;
            }
        }

        public static bool EnablesBoardItemInteraction(FlowShellScreen screen)
        {
            return screen == FlowShellScreen.NodePlaying;
        }

        public static bool EnablesOverlayInteraction(FlowShellScreen screen)
        {
            return screen == FlowShellScreen.RewardScreen
                || screen == FlowShellScreen.RoomChoiceScreen
                || screen == FlowShellScreen.RoomEventScreen;
        }
    }
}
