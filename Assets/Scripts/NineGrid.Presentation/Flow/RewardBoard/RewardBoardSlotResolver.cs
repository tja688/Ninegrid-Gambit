namespace NineGrid.Flow.RewardBoard
{
    /// <summary>
    /// 特殊奖励房占格：最多 5 张真卡 + 离开（#94）。Avatar 落格 5。
    /// </summary>
    public static class RewardBoardSlotResolver
    {
        /// <summary>宝箱奖励 4 卡用前 4；道具奖励 5 卡全用。</summary>
        public static readonly int[] ShelfSlots = { 1, 2, 3, 7, 9 };

        public const int LeaveSlot = 8;
        public const int AvatarSlot = 5;
        public const string LeaveContentId = "Leave";

        public static int ShelfSlotAt(int shelfIndex)
        {
            if (shelfIndex < 0 || shelfIndex >= ShelfSlots.Length)
            {
                return 0;
            }

            return ShelfSlots[shelfIndex];
        }
    }
}
