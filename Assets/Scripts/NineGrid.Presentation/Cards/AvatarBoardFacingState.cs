namespace NineGrid.Cards
{
    /// <summary>
    /// 局内盘面级 Avatar 朝向真相：不随单卡 hover 进出重置。
    /// 由 <see cref="NineGrid.Presentation.Controllers.AvatarBoardFacingController"/> 每帧按指针相对 Avatar 位置写入。
    /// </summary>
    public static class AvatarBoardFacingState
    {
        public static AvatarBoardFacing Facing { get; private set; } = AvatarBoardFacing.Right;

        /// <summary>Left → 主视觉 mirrorX；Right → 正常。</summary>
        public static bool MirrorX => Facing == AvatarBoardFacing.Left;

        public static AvatarBoardFacing ResolveFromPointerX(float pointerX, float avatarCenterX)
        {
            return pointerX < avatarCenterX
                ? AvatarBoardFacing.Left
                : AvatarBoardFacing.Right;
        }

        public static bool SetFacing(AvatarBoardFacing facing)
        {
            if (Facing == facing)
            {
                return false;
            }

            Facing = facing;
            return true;
        }

        public static void Reset()
        {
            Facing = AvatarBoardFacing.Right;
        }
    }
}
