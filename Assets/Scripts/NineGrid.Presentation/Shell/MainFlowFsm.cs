using System;
using System.Collections.Generic;

namespace NineGrid.Presentation.Shell
{
    /// <summary>
    /// 主流程屏态 FSM：批外直切，不订阅 Core Phase。
    /// </summary>
    public sealed class MainFlowFsm
    {
        private static readonly Dictionary<(MainFlowScreen, MainFlowTransition), MainFlowScreen> TransitionTable =
            new()
            {
                { (MainFlowScreen.MainMenu, MainFlowTransition.StartRun), MainFlowScreen.RunSession },
                { (MainFlowScreen.RunSession, MainFlowTransition.BeginNode), MainFlowScreen.NodePlaying },
                { (MainFlowScreen.RunSession, MainFlowTransition.ShowVictory), MainFlowScreen.Victory },
                { (MainFlowScreen.RunSession, MainFlowTransition.ShowDefeat), MainFlowScreen.Defeat },
                { (MainFlowScreen.NodePlaying, MainFlowTransition.NodeComplete), MainFlowScreen.RewardScreen },
                { (MainFlowScreen.NodePlaying, MainFlowTransition.ShowVictory), MainFlowScreen.Victory },
                { (MainFlowScreen.NodePlaying, MainFlowTransition.ShowDefeat), MainFlowScreen.Defeat },
                { (MainFlowScreen.RewardScreen, MainFlowTransition.ConfirmReward), MainFlowScreen.RoomChoiceScreen },
                { (MainFlowScreen.RoomChoiceScreen, MainFlowTransition.RoomSelected), MainFlowScreen.RoomEventScreen },
                { (MainFlowScreen.RoomEventScreen, MainFlowTransition.RoomEventClicked), MainFlowScreen.NodeAdvance },
                { (MainFlowScreen.NodeAdvance, MainFlowTransition.NodeAdvanceDone), MainFlowScreen.NodePlaying },
                { (MainFlowScreen.Victory, MainFlowTransition.ReturnToMainMenu), MainFlowScreen.MainMenu },
                { (MainFlowScreen.Defeat, MainFlowTransition.ReturnToMainMenu), MainFlowScreen.MainMenu },
            };

        public event Action<MainFlowScreen, MainFlowScreen> ScreenChanged;

        public MainFlowScreen CurrentScreen { get; private set; } = MainFlowScreen.Boot;
        public bool IsHarnessMode { get; set; }

        public void Enter(MainFlowScreen screen)
        {
            if (CurrentScreen == screen)
            {
                return;
            }

            MainFlowScreen previous = CurrentScreen;
            CurrentScreen = screen;
            ScreenChanged?.Invoke(previous, screen);
        }

        public bool RequestTransition(MainFlowTransition transition)
        {
            if (!TryResolveTransition(CurrentScreen, transition, out MainFlowScreen next))
            {
                return false;
            }

            Enter(next);
            return true;
        }

        public static bool TryResolveTransition(
            MainFlowScreen current,
            MainFlowTransition transition,
            out MainFlowScreen next)
        {
            if (TransitionTable.TryGetValue((current, transition), out next))
            {
                return true;
            }

            next = current;
            return false;
        }

        public void ShowOutcome(RunOutcome outcome)
        {
            MainFlowTransition transition = outcome == RunOutcome.Victory
                ? MainFlowTransition.ShowVictory
                : MainFlowTransition.ShowDefeat;
            RequestTransition(transition);
            ShellPresentationEvents.RaiseRunOutcome(outcome);
        }
    }
}
