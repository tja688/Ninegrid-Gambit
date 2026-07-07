namespace NineGrid.Cards
{
    /// <summary>
    /// 单卡自身表演所用的八向方向（局部 punch、倾斜、位移等），由外部编排层传入。
    /// </summary>
    public enum CardBoardDirection
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4,
        UpLeft = 5,
        UpRight = 6,
        DownLeft = 7,
        DownRight = 8,
    }
}
