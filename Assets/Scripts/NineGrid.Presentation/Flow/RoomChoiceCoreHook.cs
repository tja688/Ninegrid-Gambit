using System;
using NineGrid.Core;

namespace NineGrid.Flow
{
    /// <summary>
    /// 房间选择 / 进入 Core 写入：由 Presentation RoomChoiceInputController 注册。
    /// </summary>
    public static class RoomChoiceCoreHook
    {
        public static Func<int, CoreCommandResult> SelectRoom;
        public static Func<CoreCommandResult> EnterRoom;

        public static Action WireController;

        public static void RequestWire()
        {
            WireController?.Invoke();
        }
    }
}
