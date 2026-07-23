namespace NineGrid.Flow.Presentation
{
    /// <summary>表演导演 / IntentIntake 已支持的输入意图 kind 常量。</summary>
    public static class InputIntentKinds
    {
        public const string Explore = "explore";
        public const string Attack = "attack";
        public const string UseItem = "useItem";
        public const string Pickup = "pickup";

        /// <summary>模式切换：进入棋盘选择；忙时 Reject，不进 Director 缓冲。</summary>
        public const string BoardSelectBegin = "boardSelectBegin";

        /// <summary>模态选择：房间/奖励；忙时 Reject，不进 Director 缓冲。</summary>
        public const string SelectRoom = "selectRoom";
        public const string EnterRoom = "enterRoom";
        public const string SelectReward = "selectReward";
        public const string SkipHelpChoice = "skipHelpChoice";

        public static bool IsBoardAction(string kind)
        {
            return string.Equals(kind, Explore, System.StringComparison.Ordinal)
                || string.Equals(kind, Attack, System.StringComparison.Ordinal)
                || string.Equals(kind, UseItem, System.StringComparison.Ordinal)
                || string.Equals(kind, Pickup, System.StringComparison.Ordinal);
        }

        public static bool IsModeOrModal(string kind)
        {
            return string.Equals(kind, BoardSelectBegin, System.StringComparison.Ordinal)
                || string.Equals(kind, SelectRoom, System.StringComparison.Ordinal)
                || string.Equals(kind, EnterRoom, System.StringComparison.Ordinal)
                || string.Equals(kind, SelectReward, System.StringComparison.Ordinal)
                || string.Equals(kind, SkipHelpChoice, System.StringComparison.Ordinal);
        }
    }
}

