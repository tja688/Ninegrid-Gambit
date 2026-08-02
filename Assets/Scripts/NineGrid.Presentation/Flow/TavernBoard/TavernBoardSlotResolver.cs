namespace NineGrid.Flow.TavernBoard
{
    /// <summary>
    /// 卡店占格：3 服务选项 + 刷新 + 离开；二级选择占剩余空格（#93）。Avatar 落格 5。
    /// </summary>
    public static class TavernBoardSlotResolver
    {
        public static readonly int[] ServiceSlots = { 1, 3, 7 };
        public static readonly int[] CandidateSlots = { 1, 3, 4, 6, 7, 9 };
        public const int RefreshSlot = 2;
        public const int LeaveSlot = 8;
        public const int AvatarSlot = 5;

        public const string RefreshContentId = "RefreshShop";
        public const string LeaveContentId = "Leave";

        public static int ServiceSlotAt(int serviceIndex)
        {
            if (serviceIndex < 0 || serviceIndex >= ServiceSlots.Length)
            {
                return 0;
            }

            return ServiceSlots[serviceIndex];
        }

        public static int CandidateSlotAt(int candidateIndex)
        {
            if (candidateIndex < 0 || candidateIndex >= CandidateSlots.Length)
            {
                return 0;
            }

            return CandidateSlots[candidateIndex];
        }
    }
}
