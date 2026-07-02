namespace NineGrid.Presentation.Interaction
{
    public enum BoardInteractionState
    {
        Idle = 0,
        Hover,
        Drag,
        ItemTargetSelect,
    }

    public enum HandInteractionState
    {
        Idle = 0,
        Hover,
        Drag,
    }

    /// <summary>
    /// 局内交互互斥模式：棋盘常规 vs 手牌道具申请。
    /// </summary>
    public enum InGameInteractionMode
    {
        Board = 0,
        HandItem,
        ItemBoardTarget,
        ItemOption,
    }
}
