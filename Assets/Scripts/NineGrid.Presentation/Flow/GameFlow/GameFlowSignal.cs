namespace NineGrid.Flow
{
    public enum GameFlowSignalKind
    {
        SettlementReady = 0,
        BattleEnded = 1,
    }

    /// <summary>
    /// 流程编排信号：结算就绪 / 整局胜负结束。
    /// </summary>
    public readonly struct GameFlowSignal
    {
        public GameFlowSignalKind Kind { get; }

        public bool Victory { get; }

        public GameFlowSignal(GameFlowSignalKind kind, bool victory = false)
        {
            Kind = kind;
            Victory = victory;
        }

        public static GameFlowSignal SettlementReady()
        {
            return new GameFlowSignal(GameFlowSignalKind.SettlementReady);
        }

        public static GameFlowSignal BattleEnded(bool victory)
        {
            return new GameFlowSignal(GameFlowSignalKind.BattleEnded, victory);
        }
    }
}
