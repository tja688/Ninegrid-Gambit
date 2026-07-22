namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 流程壳相位（与 MainGameLoop LoopState 对齐，供 QF System/Event 使用）。
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
