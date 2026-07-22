namespace NineGrid.Flow
{
    /// <summary>
    /// 局内战斗结束（胜/负）：由 BattleSession 发出，MainGameLoop / GameFlow 订阅推进流程。
    /// 不再由会话宿主直接持有或调用 MainGameLoop。
    /// </summary>
    public struct BattleSessionEndedEvent
    {
        public bool Victory;

        public BattleSessionEndedEvent(bool victory)
        {
            Victory = victory;
        }
    }
}
