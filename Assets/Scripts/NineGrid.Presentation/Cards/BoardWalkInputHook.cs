using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 非战斗跳格：Cards → Presentation 窄接线（非业务 Sink）。
    /// </summary>
    public static class BoardWalkInputHook
    {
        public static Action<GroundFieldView> WireController;

        /// <summary>空槽跳格提交；返回是否接纳（含忙时缓冲）。</summary>
        public static Func<int, bool> TrySubmitBoardWalk;

        /// <summary>跳格门禁是否开启（沙盒 / 未来房间巡场）。</summary>
        public static Func<bool> IsEnabled;

        public static void RequestWire(GroundFieldView field)
        {
            WireController?.Invoke(field);
        }
    }
}
