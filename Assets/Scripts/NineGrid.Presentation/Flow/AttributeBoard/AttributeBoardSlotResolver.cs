namespace NineGrid.Flow.AttributeBoard
{
    /// <summary>
    /// 属性房三选二占格：3 候选真卡 + 离开（#137）。Avatar 落格 5。
    /// </summary>
    public static class AttributeBoardSlotResolver
    {
        public static readonly int[] CandidateSlots = { 1, 3, 7 };

        public const int LeaveSlot = 8;
        public const int AvatarSlot = 5;
        public const string LeaveContentId = "Leave";

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
