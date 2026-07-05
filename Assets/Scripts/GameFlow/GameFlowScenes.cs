namespace NineGrid.GameFlow
{
    /// <summary>
    /// 主流程场景名约定。序章与主要战斗均在 MainScene。
    /// </summary>
    public static class GameFlowScenes
    {
        public const string Main = "MainScene";
        public const string MainPanel = "MainPanelScene";
        public const string Island = "IslandScene";
        public const string Route = "RouteScene";
        public const string Transitional = "TransitionalScene";

        public static string GetSceneName(GameFlowState state)
        {
            switch (state)
            {
                case GameFlowState.MainMenu:
                    return MainPanel;

                case GameFlowState.Prologue:
                case GameFlowState.Battle0:
                case GameFlowState.Battle1:
                case GameFlowState.Battle2:
                case GameFlowState.Battle3Elite:
                case GameFlowState.Battle4:
                case GameFlowState.Battle5:
                case GameFlowState.Battle6:
                case GameFlowState.BossBattle:
                    return Main;

                case GameFlowState.Island1:
                case GameFlowState.Island2:
                case GameFlowState.Island3:
                case GameFlowState.IslandEliteReward:
                case GameFlowState.Island4:
                case GameFlowState.Island5:
                case GameFlowState.Island6:
                    return Island;

                case GameFlowState.Route1:
                case GameFlowState.Route2:
                case GameFlowState.Route3:
                case GameFlowState.Route4:
                case GameFlowState.Route5:
                case GameFlowState.Route6:
                    return Route;

                case GameFlowState.Event1:
                case GameFlowState.Event2:
                case GameFlowState.Event3:
                case GameFlowState.Event4:
                case GameFlowState.Event5:
                case GameFlowState.Event6:
                    return Route;

                case GameFlowState.VictorySettlement:
                    return Transitional;

                default:
                    return Main;
            }
        }

        public static bool IsBattleState(GameFlowState state)
        {
            switch (state)
            {
                case GameFlowState.Prologue:
                case GameFlowState.Battle0:
                case GameFlowState.Battle1:
                case GameFlowState.Battle2:
                case GameFlowState.Battle3Elite:
                case GameFlowState.Battle4:
                case GameFlowState.Battle5:
                case GameFlowState.Battle6:
                case GameFlowState.BossBattle:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsIslandState(GameFlowState state)
        {
            switch (state)
            {
                case GameFlowState.Island1:
                case GameFlowState.Island2:
                case GameFlowState.Island3:
                case GameFlowState.IslandEliteReward:
                case GameFlowState.Island4:
                case GameFlowState.Island5:
                case GameFlowState.Island6:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsRouteState(GameFlowState state)
        {
            switch (state)
            {
                case GameFlowState.Route1:
                case GameFlowState.Route2:
                case GameFlowState.Route3:
                case GameFlowState.Route4:
                case GameFlowState.Route5:
                case GameFlowState.Route6:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsEventState(GameFlowState state)
        {
            switch (state)
            {
                case GameFlowState.Event1:
                case GameFlowState.Event2:
                case GameFlowState.Event3:
                case GameFlowState.Event4:
                case GameFlowState.Event5:
                case GameFlowState.Event6:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>TransitionalScene：路线选择、事件、胜利结算等过场场景。</summary>
        public static bool IsTransitionalState(GameFlowState state)
        {
            switch (state)
            {
                case GameFlowState.Route1:
                case GameFlowState.Route2:
                case GameFlowState.Route3:
                case GameFlowState.Route4:
                case GameFlowState.Route5:
                case GameFlowState.Route6:
                case GameFlowState.Event1:
                case GameFlowState.Event2:
                case GameFlowState.Event3:
                case GameFlowState.Event4:
                case GameFlowState.Event5:
                case GameFlowState.Event6:
                case GameFlowState.VictorySettlement:
                    return true;
                default:
                    return false;
            }
        }
    }
}
