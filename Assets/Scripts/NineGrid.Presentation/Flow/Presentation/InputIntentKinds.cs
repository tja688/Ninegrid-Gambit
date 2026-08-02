namespace NineGrid.Flow.Presentation
{
    /// <summary>表演导演 / IntentIntake 已支持的输入意图 kind 常量。</summary>
    public static class InputIntentKinds
    {
        public const string Explore = "explore";
        public const string Attack = "attack";
        public const string UseItem = "useItem";
        public const string Pickup = "pickup";
        /// <summary>主动翻开场上邻接背面卡（消耗一次互动）。</summary>
        public const string RevealFace = "revealFace";
        /// <summary>非战斗 Avatar 跳格走向目标空格。</summary>
        public const string BoardWalk = "boardWalk";

        /// <summary>模式切换：进入棋盘选择；忙时 Reject，不进 Director 缓冲。</summary>
        public const string BoardSelectBegin = "boardSelectBegin";

        /// <summary>
        /// 模态选择：房间/奖励。ChoiceOverlay 持有时应放行（即使主线忙），
        /// 因局内宝箱 Bounce 挂在 UseItem Present 内等待点选。
        /// </summary>
        public const string SelectRoom = "selectRoom";
        public const string EnterRoom = "enterRoom";
        public const string SelectReward = "selectReward";
        public const string SkipHelpChoice = "skipHelpChoice";
        public const string RefreshShop = "refreshShop";
        /// <summary>右键丢弃装备栏遗物；ChoiceOverlay 下满栏腾空也须放行（#98）。</summary>
        public const string DiscardRelic = "discardRelic";

        public static bool IsBoardAction(string kind)
        {
            return string.Equals(kind, Explore, System.StringComparison.Ordinal)
                || string.Equals(kind, Attack, System.StringComparison.Ordinal)
                || string.Equals(kind, UseItem, System.StringComparison.Ordinal)
                || string.Equals(kind, Pickup, System.StringComparison.Ordinal)
                || string.Equals(kind, RevealFace, System.StringComparison.Ordinal)
                || string.Equals(kind, BoardWalk, System.StringComparison.Ordinal);
        }

        public static bool IsModeOrModal(string kind)
        {
            return string.Equals(kind, BoardSelectBegin, System.StringComparison.Ordinal)
                || string.Equals(kind, SelectRoom, System.StringComparison.Ordinal)
                || string.Equals(kind, EnterRoom, System.StringComparison.Ordinal)
                || string.Equals(kind, SelectReward, System.StringComparison.Ordinal)
                || string.Equals(kind, SkipHelpChoice, System.StringComparison.Ordinal)
                || string.Equals(kind, RefreshShop, System.StringComparison.Ordinal)
                || string.Equals(kind, DiscardRelic, System.StringComparison.Ordinal);
        }
    }
}

