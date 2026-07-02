using NineGrid.Core;

namespace NineGrid.Presentation.Orchestration
{
    /// <summary>
    /// Flow 绑定在事件未携带 uid 时的棋盘演员回退约定（与测试夹具对齐）。
    /// </summary>
    public static class PresentationFallbackActorUids
    {
        public const int Player = 1;
        public const int Enemy = 2;

        public static int BoardCard(int boardSlotIndex) => 100 + boardSlotIndex;

        public static SlotId PlayerSlot => SlotId.Board(5);
        public static SlotId EnemySlot => SlotId.Board(6);
    }
}
