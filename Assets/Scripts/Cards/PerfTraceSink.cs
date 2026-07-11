using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// Cards → Flow PerfTrace 旁路。由 Flow <c>PerfTraceRecorder</c> 注册。
    /// Cards 不可引用 Flow，故用静态 Action 解耦。
    /// </summary>
    public static class PerfTraceSink
    {
        /// <summary>kind, uid, site, payloadPairs (flat key/value alternating, may be null)</summary>
        public static Action<string, int, string, string[]> Record;

        /// <summary>请求 Flow 侧拍 BoardSnap（phase, full）</summary>
        public static Action<string, bool> RequestBoardSnap;

        /// <summary>beatKind, nodeIndex</summary>
        public static Action<string, int> OpenBeat;

        public static Action CloseBeat;

        /// <summary>attackerUid, targetUid</summary>
        public static Action<int, int> SetCombatants;

        public static void ClearHandlers()
        {
            Record = null;
            RequestBoardSnap = null;
            OpenBeat = null;
            CloseBeat = null;
            SetCombatants = null;
        }
    }
}
