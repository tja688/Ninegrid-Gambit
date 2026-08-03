namespace NineGrid.Flow
{
    /// <summary>
    /// Router 表面优先级（ADR-0023）：场地面 / 手牌带 / 覆层显式且互不相同。
    /// 数值越大越优先；覆层最低，重叠时不靠排序抢几何（权限交 IntentIntake）。
    /// Field=28：避开 ContentIcon(25) 与过渡期场地卡(30)；#102 去卡 collider 后再收口。
    /// </summary>
    public static class PointerHitSurfacePriorities
    {
        public const int Field = 28;

        public const int Hand = 20;

        public const int Overlay = 10;
    }
}
