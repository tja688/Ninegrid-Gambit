using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 诊断 Recorder 接线入口：由 Presentation DiagnosticOutputController 注册，
    /// Flow 只调用 Attach/Detach，不持有静态 Sink 注册权威。
    /// </summary>
    public static class DiagnosticOutputHook
    {
        public static Action AttachRecorders;
        public static Action DetachRecorders;

        public static void RequestAttach()
        {
            AttachRecorders?.Invoke();
        }

        public static void RequestDetach()
        {
            DetachRecorders?.Invoke();
        }
    }
}
