using NineGrid.Core.Content;

namespace NineGrid.Core
{
    /// <summary>
    /// 主题怪物卡组按层难度池（策划：普通→第 1 层、中等→第 2 层、困难→第 3 层）。
    /// <see cref="MonsterDeckKind.WeakElite"/> / <see cref="MonsterDeckKind.StrongElite"/> /
    /// <see cref="MonsterDeckKind.Boss"/> 在此语义为卡组难度档，与怪物「层主」rank 无关。
    /// </summary>
    public static class MonsterDeckFloorPool
    {
        public static int DifficultyToFloor(MonsterDeckKind kind)
        {
            switch (kind)
            {
                case MonsterDeckKind.WeakElite:
                    return 1;
                case MonsterDeckKind.StrongElite:
                    return 2;
                case MonsterDeckKind.Boss:
                    return 3;
                default:
                    return 0;
            }
        }

        public static bool IsPlayableDifficulty(MonsterDeckKind kind)
        {
            return DifficultyToFloor(kind) > 0;
        }

        public static bool MatchesFloor(MonsterDeckKind kind, int floor)
        {
            var poolFloor = DifficultyToFloor(kind);
            return poolFloor > 0 && poolFloor == floor;
        }
    }
}
