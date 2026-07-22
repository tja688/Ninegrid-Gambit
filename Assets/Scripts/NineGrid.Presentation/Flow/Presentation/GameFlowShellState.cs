namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 流程壳相位权威枚举（由 <c>IGameFlowShellSystem</c> 持有）。
    /// </summary>
    public enum GameFlowShellState
    {
        MainMenu = 0,
        BattleStub = 1,
        RewardChoice = 2,
        RoomChoice = 3,
        RoomEvent = 4,
        VictoryNotice = 5,
        DefeatNotice = 6,
    }
}
