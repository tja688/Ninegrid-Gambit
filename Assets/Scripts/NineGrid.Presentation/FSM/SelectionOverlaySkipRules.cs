namespace NineGrid.Presentation.FSM
{
    /// <summary>
    /// 覆盖层「跳过」按钮出现规则：仅通关帮助卡三选与宝箱/遗物 OfferReward 显示最右跳过卡。
    /// </summary>
    public static class SelectionOverlaySkipRules
    {
        public static bool SupportsRewardSkip(string poolId)
        {
            if (string.IsNullOrEmpty(poolId))
            {
                return false;
            }

            if (poolId.StartsWith("help.") && poolId.EndsWith("choice"))
            {
                return true;
            }

            if (poolId.StartsWith("relic."))
            {
                return true;
            }

            return false;
        }

        public static string ExtractRewardPoolId(string rewardOfferedMessage)
        {
            if (string.IsNullOrEmpty(rewardOfferedMessage))
            {
                return string.Empty;
            }

            int pipe = rewardOfferedMessage.IndexOf('|');
            return pipe > 0 ? rewardOfferedMessage.Substring(0, pipe) : rewardOfferedMessage;
        }
    }
}
