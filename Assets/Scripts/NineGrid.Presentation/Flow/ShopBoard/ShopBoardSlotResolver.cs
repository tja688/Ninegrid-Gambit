namespace NineGrid.Flow.ShopBoard
{
    /// <summary>
    /// 商店七占格：最多 6 货架（含道具牌格升级）+ 刷新 + 离开（#92 / #109 / #211）。Avatar 落格 5。
    /// </summary>
    public static class ShopBoardSlotResolver
    {
        public static readonly int[] ShelfSlots = { 1, 3, 7, 9, 4 };
        public static readonly int[] ShelfFallbackPool = { 1, 3, 4, 6, 7, 9 };
        public const int RefreshSlot = 2;
        public const int LeaveSlot = 8;
        public const int AvatarSlot = 5;

        public const string RefreshContentId = "RefreshShop";
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
