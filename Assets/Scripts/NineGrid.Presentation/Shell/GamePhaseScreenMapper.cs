using NineGrid.Core;

namespace NineGrid.Presentation.Shell
{
    public readonly struct GamePhaseScreenMapping
    {
        public GamePhaseScreenMapping(
            MainFlowScreen screen,
            bool announceOutcome,
            RunOutcome outcome)
        {
            Screen = screen;
            AnnounceOutcome = announceOutcome;
            Outcome = outcome;
        }

        public MainFlowScreen Screen { get; }
        public bool AnnounceOutcome { get; }
        public RunOutcome Outcome { get; }
    }

    /// <summary>
    /// Core <see cref="GamePhase"/> → Shell <see cref="MainFlowScreen"/> 映射（可单测）。
    /// </summary>
    public static class GamePhaseScreenMapper
    {
        public static bool TryMap(GamePhase phase, out GamePhaseScreenMapping mapping)
        {
            switch (phase)
            {
                case GamePhase.None:
                    mapping = new GamePhaseScreenMapping(MainFlowScreen.MainMenu, false, RunOutcome.Victory);
                    return true;
                case GamePhase.BuildEnemyPool:
                case GamePhase.ResetNode:
                case GamePhase.DealOpeningCards:
                    mapping = new GamePhaseScreenMapping(MainFlowScreen.RunSession, false, RunOutcome.Victory);
                    return true;
                case GamePhase.InteractionLoop:
                case GamePhase.ClearCheck:
                    mapping = new GamePhaseScreenMapping(MainFlowScreen.NodePlaying, false, RunOutcome.Victory);
                    return true;
                case GamePhase.RewardItemChoice:
                    mapping = new GamePhaseScreenMapping(MainFlowScreen.RewardScreen, false, RunOutcome.Victory);
                    return true;
                case GamePhase.RoomChoice:
                    mapping = new GamePhaseScreenMapping(MainFlowScreen.RoomChoiceScreen, false, RunOutcome.Victory);
                    return true;
                case GamePhase.RoomEvent:
                    mapping = new GamePhaseScreenMapping(MainFlowScreen.RoomEventScreen, false, RunOutcome.Victory);
                    return true;
                case GamePhase.NodeCompleted:
                    mapping = new GamePhaseScreenMapping(MainFlowScreen.NodeAdvance, false, RunOutcome.Victory);
                    return true;
                case GamePhase.Victory:
                    mapping = new GamePhaseScreenMapping(MainFlowScreen.Victory, true, RunOutcome.Victory);
                    return true;
                case GamePhase.Defeat:
                    mapping = new GamePhaseScreenMapping(MainFlowScreen.Defeat, true, RunOutcome.Defeat);
                    return true;
                default:
                    mapping = default;
                    return false;
            }
        }
    }
}
