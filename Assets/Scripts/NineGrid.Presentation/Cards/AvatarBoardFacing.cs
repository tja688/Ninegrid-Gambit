namespace NineGrid.Cards
{
    /// <summary>
    /// 盘面级 Avatar 水平朝向（相对指针与玩家位置）。
    /// </summary>
    public enum AvatarBoardFacing
    {
        /// <summary>朝右（默认立绘方向，不镜像）。</summary>
        Right = 0,

        /// <summary>朝左（主视觉水平镜像）。</summary>
        Left = 1,
    }
}
