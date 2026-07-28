using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地就绪后通知表现层装配盘面 Avatar 朝向 Controller。
    /// </summary>
    public static class AvatarBoardFacingHook
    {
        public static Action<GroundFieldView> WireController;

        public static void RequestWire(GroundFieldView field)
        {
            WireController?.Invoke(field);
        }
    }
}
