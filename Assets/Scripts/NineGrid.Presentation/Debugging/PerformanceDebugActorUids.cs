using NineGrid.Core;

namespace NineGrid.Presentation.Debugging
{
    /// <summary>
    /// 调试场景与手搓批次对齐的 CardUid / 槽位约定。
    /// </summary>
    public static class PerformanceDebugActorUids
    {
        public const int Player = 1;
        public const int Enemy = 2;

        public static int BoardCard(int boardSlotIndex) => 100 + boardSlotIndex;

        public static SlotId PlayerSlot => SlotId.Board(5);
        public static SlotId EnemySlot => SlotId.Board(3);
    }
}
