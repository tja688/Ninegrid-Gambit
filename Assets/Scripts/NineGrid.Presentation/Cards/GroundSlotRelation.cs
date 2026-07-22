using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 九宫格格位之间的结构关系，供效果反馈路由查询。
    /// </summary>
    [Flags]
    public enum GroundSlotRelation
    {
        None = 0,
        Self = 1 << 0,
        Orthogonal = 1 << 1,
        Diagonal = 1 << 2,
        SameRow = 1 << 3,
        SameColumn = 1 << 4,
        Corner = 1 << 5,
        OuterRing = 1 << 6,
    }
}
